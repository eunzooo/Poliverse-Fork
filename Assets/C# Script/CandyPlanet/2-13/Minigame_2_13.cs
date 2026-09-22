using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2-13 젤리 블록 철거 미니게임
///
/// 리듬 흐름 (2박 1세트):
///  1박) CSV type = "Show"  -> 탄환 소환 + 낙하 (RigidBody 미사용, Transform 보간)
///  1박) CSV type = "Input" -> 플레이어가 화면을 터치해야 하는 타이밍.
///                             정확한 판정(Perfect/Good/Miss)은 RhythmManager가 수행하고,
///                             그 결과가 OnJudgement()로 전달된다.
///
/// 판정 결과에 따라:
///  - Perfect / Good -> 투석기 발사 모션 + 탄환이 현재 타겟 블록으로 날아감 (명중 처리)
///  - Miss           -> 투석기 발사 모션 없이, 탄환이 그 자리에서 아래로 떨어지다가 사라짐
///
/// Enemy가 있는 고정 젤리 블록들을 targetBlocks 리스트로 순서대로 등록해두고,
/// 그중 currentTargetIndex번째 블록 위치를 발사 목표로 사용한다.
/// 한 블록당 명중 2회(Perfect/Good)로 Enemy 스프라이트가 Stage1 -> Stage2 -> Stage3로 바뀌고,
/// Stage3에 도달하면 다음 블록으로 넘어간다. 모든 블록을 처리하면 Success() 호출.
///
/// 점수 집계는 MiniGameBase의 UseRhythmJudgementScore(기본 true) 경로를 그대로 사용하므로
/// 이 스크립트에서 별도로 점수를 계산하지 않는다.
/// </summary>
[Serializable]
public class JellyEnemyTarget
{
    [Tooltip("이 Enemy가 있는 젤리 블록의 위치 (탄환 발사 목표 지점)")]
    public Transform point;

    [Tooltip("이 블록 위 Enemy의 SpriteRenderer (스프라이트 교체 대상)")]
    public SpriteRenderer enemyRenderer;
}

public class Minigame_2_13 : MiniGameBase
{
    // 판정 윈도우 오버라이드
    // hitWindow를 goodWindow에 가깝게 좁혀서, 판정 하나가 다음 사이클 이벤트까지
    // 잘못 소모시키는 여지를 최소화한다. (Show/Input 간격, 즉 1박 길이의 절반보다
    // 반드시 작게 유지할 것 — 겹치면 다시 같은 문제가 재발함)
    public override float perfectWindowOverride => 0.1f;
    public override float goodWindowOverride => 0.25f;
    public override float hitWindowOverride => 0.35f;
    protected override string MinigameExplain => "젤리빌딩 부수기!";

    [Header("Jelly Block Demolition - Positions")]
    [Tooltip("탄환이 처음 소환되는 위치 (좌측 상단)")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("탄환이 낙하해서 도착하는 위치 (투석기 위치)")]
    [SerializeField] private Transform catapultPoint;

    [Header("Jelly Block Demolition - Enemy Targets (고정 블록)")]
    [Tooltip("Enemy가 있는 젤리 블록들. 고정된 순서대로 등록하며, 하나가 클리어되면 다음 블록이 타겟이 된다.")]
    [SerializeField] private List<JellyEnemyTarget> targetBlocks = new List<JellyEnemyTarget>();

    [Header("Enemy Sprite Stages (공용)")]
    [Tooltip("피격 전 기본 Enemy 스프라이트. 비워두면 씬에 원래 설정된 스프라이트를 그대로 사용.")]
    [SerializeField] private Sprite enemySpriteStage1;

    [Tooltip("1회 명중(Perfect/Good) 후 표시할 스프라이트")]
    [SerializeField] private Sprite enemySpriteStage2;

    [Tooltip("2회 명중 후 표시할 스프라이트. 이 상태가 되면 다음 타겟 블록으로 넘어간다.")]
    [SerializeField] private Sprite enemySpriteStage3;

    [Header("Jelly Block Demolition - Prefab & Timing")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("소환 후 투석기 위치까지 낙하하는 데 걸리는 시간(초). 1박 길이에 맞춰 조정.")]
    [SerializeField] private float fallDuration = 0.5f;

    [Tooltip("투석기에서 목표 지점까지 날아가는 데 걸리는 시간(초). 1박 길이에 맞춰 조정.")]
    [SerializeField] private float flyDuration = 0.3f;

    [Header("Catapult Procedural Motion (애니메이션 클립 없이 코드로 구현)")]
    [Tooltip(
        "회전시킬 투석기 팔 Transform.\n" +
        "★ 중요: 이 Transform의 위치(피벗)는 받침대와 팔이 연결되는 지점에 있어야 함.\n" +
        "팔 스프라이트는 그 지점에서 오프셋되어 배치 (또는 스프라이트 자체 Pivot을 연결 지점으로 설정).\n" +
        "그래야 회전 시 연결 지점을 축으로 자연스럽게 휘두름.")]
    [SerializeField] private Transform catapultArm;

    [Tooltip(
        "씬에 배치해둔 arm의 '평상시 기울어진 자세' 그대로를 Rest로 사용할지 여부.\n" +
        "true면 게임 시작 시 arm의 현재 회전값을 자동으로 Rest 각도로 저장한다.\n" +
        "false면 아래 catapultRestAngleOverride 값을 Rest로 사용한다.")]
    [SerializeField] private bool useCurrentArmRotationAsRest = true;

    [Tooltip("useCurrentArmRotationAsRest가 false일 때 사용할 Rest 각도(Z, degrees)")]
    [SerializeField] private float catapultRestAngleOverride = 0f;

    [Tooltip("Rest 각도에서 받침대 쪽으로 얼마나 더 당겨질지 (상대 오프셋, degrees). " +
             "예: -20이면 Rest보다 20도 더 눕는(당겨지는) 방향.")]
    [SerializeField] private float catapultPullBackOffset = -20f;

    [Tooltip("Rest 각도에서 얼마나 앞으로 튕겨나갈지 (상대 오프셋, degrees). " +
             "예: 70이면 Rest보다 70도 앞으로 휘두르는 방향. 부호/크기는 실제 아트 방향에 맞춰 조정.")]
    [SerializeField] private float catapultThrowOffset = 70f;

    [Tooltip("당김 동작에 걸리는 시간(초). 0이면 당김 동작 없이 바로 던짐.")]
    [SerializeField] private float catapultPullBackDuration = 0.08f;

    [Tooltip("PullBack(or Rest) -> Throw 각도로 휘두르는 데 걸리는 시간(초)")]
    [SerializeField] private float catapultSwingDuration = 0.12f;

    [Tooltip("던진 후 Rest 각도로 복귀하는 데 걸리는 시간(초)")]
    [SerializeField] private float catapultReturnDuration = 0.2f;

    [Header("Miss Motion")]
    [Tooltip("Miss 시 탄환이 투석기 아래로 떨어지는 상대 거리 (현재 위치 기준 아래 방향 오프셋)")]
    [SerializeField] private float missDropDistance = 1.5f;

    [Tooltip("Miss 시 낙하 모션 후 탄환이 사라지기까지 걸리는 시간(초)")]
    [SerializeField] private float missDropDuration = 0.3f;

    private Coroutine catapultMotionRoutine;
    private float catapultRestAngle; // 실제 사용되는 Rest 각도 (자동 캡처 또는 Override)

    private GameObject currentProjectile;
    private Coroutine projectileMotionRoutine; // 탄환의 낙하/발사 이동을 제어하는 코루틴 (겹치지 않도록 관리)
    private bool isProjectileReadyForInput = false; // Show 이후 ~ Input 판정 전까지 true

    private int currentTargetIndex = 0;
    private int currentHitStage = 0; // 0 = 기본, 1 = 1회 명중(Stage2), 2 = 2회 명중(Stage3, 다음 타겟으로)

    protected override float TimerDuration => 15f;
    protected override string MinigameTitle => "젤리 블록 철거";

    protected override void Awake()
    {
        base.Awake();
        CaptureCatapultRestAngle();
    }

    private void CaptureCatapultRestAngle()
    {
        if (useCurrentArmRotationAsRest && catapultArm != null)
        {
            catapultRestAngle = catapultArm.localEulerAngles.z;
        }
        else
        {
            catapultRestAngle = catapultRestAngleOverride;
        }
    }

    public override void StartGame()
    {
        base.StartGame();

        isProjectileReadyForInput = false;

        InitializeTargets();
        StopProjectileMotion();

        if (currentProjectile != null)
        {
            Destroy(currentProjectile);
            currentProjectile = null;
        }
    }

    public override void ResetGame()
    {
        base.ResetGame();

        isProjectileReadyForInput = false;

        InitializeTargets();
        StopProjectileMotion();

        if (currentProjectile != null)
        {
            Destroy(currentProjectile);
            currentProjectile = null;
        }
    }

    private void InitializeTargets()
    {
        currentTargetIndex = 0;
        currentHitStage = 0;

        if (targetBlocks == null)
            return;

        // 모든 Enemy를 기본(피격 전) 스프라이트로 되돌린다.
        for (int i = 0; i < targetBlocks.Count; i++)
        {
            SetEnemySprite(targetBlocks[i], enemySpriteStage1);
        }
    }

    private void StopProjectileMotion()
    {
        if (projectileMotionRoutine != null)
        {
            StopCoroutine(projectileMotionRoutine);
            projectileMotionRoutine = null;
        }
    }

    private void Update()
    {
        if (IsInputLocked || IsSuccess)
            return;

        // 판정할 탄환이 없는 구간(직전 판정 완료 ~ 다음 Show 스폰 전)의 클릭은
        // 아예 RhythmManager로 전달하지 않는다.
        // 이걸 막지 않으면, hitWindow가 넓을 때 이 클릭이 "다음 사이클"의 Input 이벤트를
        // 미리 소모시켜버려서 진짜 다음 타이밍에 클릭해도 판정이 씹히는 버그가 생긴다.
        if (currentProjectile == null)
            return;

        if (WasTouchedThisFrame())
        {
            // 판정 자체는 RhythmManager가 수행한다.
            // action 이름은 CSV의 type 값과 대소문자 무관하게 매칭된다.
            OnPlayerInput("Input");
        }
    }

    private bool WasTouchedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

#if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            return true;
        }
#endif

        return false;
    }

    // RhythmManager -> MiniGameBase -> 여기 : 차트 타이밍 신호 (Show / Input 등)
    public override void OnRhythmEvent(string action)
    {
        base.OnRhythmEvent(action);

        if (string.Equals(action, "Show", StringComparison.OrdinalIgnoreCase))
        {
            SpawnAndDropProjectile();
        }
        else if (string.Equals(action, "Input", StringComparison.OrdinalIgnoreCase))
        {
            isProjectileReadyForInput = true;
        }
    }

    // RhythmManager -> MiniGameBase -> 여기 : Perfect/Good/Miss 판정 결과
    public override void OnJudgement(JudgementResult judgement)
    {
        // 점수 집계(총 노드/Perfect/Good/Miss)는 베이스에서 그대로 처리한다.
        base.OnJudgement(judgement);

        isProjectileReadyForInput = false;

        if (currentProjectile == null)
            return; // Show 없이 들어온 입력 등 예외 상황 방어

        switch (judgement)
        {
            case JudgementResult.Perfect:
            case JudgementResult.Good:
                LaunchProjectile();
                break;

            case JudgementResult.Miss:
                DropProjectileOnMiss();
                break;
        }
    }

    private void SpawnAndDropProjectile()
    {
        StopProjectileMotion();

        if (currentProjectile != null)
        {
            Destroy(currentProjectile);
            currentProjectile = null;
        }

        if (projectilePrefab == null || spawnPoint == null || catapultPoint == null)
        {
            Debug.LogWarning("[Minigame_2_13] projectilePrefab/spawnPoint/catapultPoint가 설정되지 않았습니다.");
            return;
        }

        currentProjectile = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity, transform);

        projectileMotionRoutine = StartCoroutine(MoveRoutine(
            currentProjectile,
            spawnPoint.position,
            catapultPoint.position,
            fallDuration,
            onComplete: () =>
            {
                projectileMotionRoutine = null;
            }));
    }

    private void LaunchProjectile()
    {
        if (currentProjectile == null)
            return;

        GameObject projectile = currentProjectile;
        currentProjectile = null; // 다음 Show 소환과 겹치지 않도록 즉시 참조 해제

        // 아직 낙하 중이었다면(예: Input 타이밍보다 일찍 클릭) 낙하 코루틴을 반드시 멈춘다.
        // 그렇지 않으면 낙하 코루틴과 아래 발사 코루틴이 같은 오브젝트의 position을 동시에
        // 건드리면서 위치가 튀는 버그가 생긴다.
        StopProjectileMotion();

        PlayCatapultThrowMotion();

        Vector3 startPos = projectile.transform.position;
        Vector3 targetPos = GetCurrentTargetPosition();

        projectileMotionRoutine = StartCoroutine(MoveRoutine(
            projectile,
            startPos,
            targetPos,
            flyDuration,
            onComplete: () =>
            {
                projectileMotionRoutine = null;

                HandleEnemyHit();

                // 날아가는 모션이 끝난 뒤 탄환 삭제
                if (projectile != null)
                {
                    Destroy(projectile);
                }
            }));
    }

    /// <summary>
    /// Miss 판정 시: 투석기 발사 모션 없이, 탄환이 그 자리에서 아래로 떨어지다가 사라진다.
    /// </summary>
    private void DropProjectileOnMiss()
    {
        if (currentProjectile == null)
            return;

        GameObject projectile = currentProjectile;
        currentProjectile = null; // 다음 Show 소환과 겹치지 않도록 즉시 참조 해제

        StopProjectileMotion();

        Vector3 startPos = projectile.transform.position;
        Vector3 dropPos = startPos + (Vector3.down * missDropDistance);

        projectileMotionRoutine = StartCoroutine(MoveRoutine(
            projectile,
            startPos,
            dropPos,
            missDropDuration,
            onComplete: () =>
            {
                projectileMotionRoutine = null;

                if (projectile != null)
                {
                    Destroy(projectile);
                }
            }));
    }

    /// <summary>
    /// 애니메이션 클립 없이 코드로 구현하는 투석기 던지기 모션.
    /// (선택) PullBack 각도 -> Throw 각도로 빠르게 휘두른 뒤 -> Rest 각도로 복귀.
    /// catapultArm이 지정되지 않았다면 아무 동작도 하지 않는다.
    /// </summary>
    private void PlayCatapultThrowMotion()
    {
        if (catapultArm == null)
            return;

        if (catapultMotionRoutine != null)
            StopCoroutine(catapultMotionRoutine);

        catapultMotionRoutine = StartCoroutine(CatapultThrowMotionRoutine());
    }

    private IEnumerator CatapultThrowMotionRoutine()
    {
        float pullBackAngle = catapultRestAngle + catapultPullBackOffset;
        float throwAngle = catapultRestAngle + catapultThrowOffset;

        // 1) (선택) 던지기 직전 받침대 쪽으로 당기는 준비 동작
        if (catapultPullBackDuration > 0f &&
            !Mathf.Approximately(catapultPullBackOffset, 0f))
        {
            yield return RotateArmRoutine(
                catapultRestAngle,
                pullBackAngle,
                catapultPullBackDuration);
        }
        else
        {
            SetArmAngle(catapultRestAngle);
        }

        // 2) 던지는 순간: PullBack(or Rest) -> Throw 각도로 빠르게 휘두름
        float swingFrom = (catapultPullBackDuration > 0f &&
                            !Mathf.Approximately(catapultPullBackOffset, 0f))
            ? pullBackAngle
            : catapultRestAngle;

        yield return RotateArmRoutine(
            swingFrom,
            throwAngle,
            catapultSwingDuration);

        // 3) 던진 뒤 다시 Rest 각도로 복귀
        yield return RotateArmRoutine(
            throwAngle,
            catapultRestAngle,
            catapultReturnDuration);

        catapultMotionRoutine = null;
    }

    private void SetArmAngle(float angle)
    {
        if (catapultArm == null)
            return;

        Vector3 euler = catapultArm.localEulerAngles;
        euler.z = angle;
        catapultArm.localEulerAngles = euler;
    }

    private IEnumerator RotateArmRoutine(float fromAngle, float toAngle, float duration)
    {
        if (catapultArm == null)
            yield break;

        duration = Mathf.Max(0.0001f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (catapultArm == null)
                yield break;

            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);

            // 필요하면 여기 ratio에 easing(ease-out 등)을 적용해 손맛을 더할 수 있음
            float angle = Mathf.LerpAngle(fromAngle, toAngle, ratio);
            SetArmAngle(angle);

            yield return null;
        }

        SetArmAngle(toAngle);
    }

    private Vector3 GetCurrentTargetPosition()
    {
        if (targetBlocks != null &&
            currentTargetIndex < targetBlocks.Count &&
            targetBlocks[currentTargetIndex] != null &&
            targetBlocks[currentTargetIndex].point != null)
        {
            return targetBlocks[currentTargetIndex].point.position;
        }

        return transform.position; // 타겟이 없을 때의 안전한 기본값
    }

    /// <summary>
    /// 탄환이 현재 타겟 블록에 명중했을 때 호출됨.
    /// 1회 명중 -> Enemy Stage2, 2회 명중 -> Enemy Stage3 + 다음 타겟으로 이동.
    /// </summary>
    private void HandleEnemyHit()
    {
        if (targetBlocks == null || currentTargetIndex >= targetBlocks.Count)
            return;

        JellyEnemyTarget target = targetBlocks[currentTargetIndex];
        currentHitStage++;

        if (currentHitStage == 1)
        {
            SetEnemySprite(target, enemySpriteStage2);
        }
        else if (currentHitStage >= 2)
        {
            SetEnemySprite(target, enemySpriteStage3);
            MoveToNextTarget();
        }
    }

    private void MoveToNextTarget()
    {
        currentTargetIndex++;
        currentHitStage = 0;

        if (targetBlocks == null || currentTargetIndex >= targetBlocks.Count)
        {
            // 모든 Enemy 블록 처리 완료 -> 미니게임 성공
            Success();
        }

        // TODO: 다음 타겟으로 넘어갈 때 카메라 이동/안내 연출이 필요하면 여기서 처리
    }

    private void SetEnemySprite(JellyEnemyTarget target, Sprite sprite)
    {
        if (target == null || target.enemyRenderer == null || sprite == null)
            return;

        target.enemyRenderer.sprite = sprite;
    }

    // RigidBody를 사용하지 않는 좌표 보간 이동 (낙하 / 발사 공용)
    private IEnumerator MoveRoutine(
        GameObject target,
        Vector3 from,
        Vector3 to,
        float duration,
        Action onComplete)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.0001f, duration);

        while (elapsed < duration)
        {
            if (target == null)
                yield break;

            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);

            // 필요하다면 여기서 ratio에 easing / 포물선 곡선을 적용해 궤적을 다듬을 수 있음
            target.transform.position = Vector3.Lerp(from, to, ratio);

            yield return null;
        }

        if (target != null)
            target.transform.position = to;

        onComplete?.Invoke();
    }
}