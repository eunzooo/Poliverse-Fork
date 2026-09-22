using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FoodManager : MonoBehaviour
{
    public List<GameObject> foodPrefabs;
    public Transform spawnPoint;

    [Header("도넛 굴러오기 연출")]
    [Tooltip("spawnPoint 기준 왼쪽으로 얼마나 떨어진 곳에서 시작할지")]
    public float startOffsetX = 8f;
    [Tooltip("굴러오는 데 걸리는 시간(초)")]
    public float rollDuration = 1f;

    private int currentIndex = 0;
    public static FoodManager Instance;
    private Minigame_2_15 minigame_2_15;
    public GameObject stage_2_15;

    // 현재 화면에 있는(판정 중인) 음식의 조각 트래커. Minigame_2_15에서 판정 성공 시 참조함
    public FoodPiecesTracker CurrentTracker { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SpawnNextFood();
        minigame_2_15 = stage_2_15.GetComponent<Minigame_2_15>();

    }

    public void SpawnNextFood()
    {
        if (currentIndex >= foodPrefabs.Count)
        {
            Debug.Log("모든 음식 끝!");
            minigame_2_15.Succeed();
            return;
        }

        GameObject prefab = foodPrefabs[currentIndex];

        // 화면 왼쪽 바깥에서 시작해서 spawnPoint까지 굴러옴
        Vector3 startPos = spawnPoint.position + Vector3.left * startOffsetX;

        Debug.Log($"Spawning food {currentIndex}, prefab = {prefab} at {startPos}");

        GameObject foodRoot = Instantiate(prefab, startPos, Quaternion.identity, transform);
        currentIndex++;

        // 조각(비주얼)을 먼저 조립해야 굴러오는 동안 화면에 실제로 보임
        AssembleFood(foodRoot);

        StartCoroutine(RollToSpawnPoint(foodRoot));
    }

    private IEnumerator RollToSpawnPoint(GameObject foodRoot)
    {
        Vector3 start = foodRoot.transform.position;
        Vector3 end = spawnPoint.position;

        // 회전은 FoodRotate 스크립트가 이미 담당하므로, 여기서는 이동(위치)만 처리
        float elapsed = 0f;
        while (elapsed < rollDuration)
        {
            if (foodRoot == null) yield break; // 도중에 파괴된 경우 안전장치

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rollDuration);

            foodRoot.transform.position = Vector3.Lerp(start, end, t);

            yield return null;
        }

        if (foodRoot == null) yield break;
        foodRoot.transform.position = end;

        // 중앙 도착 완료 -> 여기서부터 "판정용 한 바퀴" 시작
        FoodRotate rotate = foodRoot.GetComponent<FoodRotate>();
        if (rotate != null)
        {
            rotate.OnOneRevolutionComplete += () => HandleFoodCycleEnd(foodRoot);
            rotate.BeginJudgementRotation();
        }
        else
        {
            // FoodRotate가 없으면 회전 연출 없이 바로 다음 음식으로 넘어감
            Debug.LogWarning("FoodRotate 스크립트가 Prefab에 없음! 즉시 다음 음식으로 넘어갑니다.");
            HandleFoodCycleEnd(foodRoot);
        }
    }

    // 판정용 한 바퀴가 끝났을 때 호출됨: 남은 조각이 있어도 무조건 다음 음식으로 넘어감
    private void HandleFoodCycleEnd(GameObject foodRoot)
    {
        if (CurrentTracker != null && CurrentTracker.gameObject == foodRoot)
        {
            CurrentTracker = null;
        }

        if (foodRoot != null)
        {
            Destroy(foodRoot);
        }

        SpawnNextFood();
    }

    private void AssembleFood(GameObject foodRoot)
    {
        // FoodAssembler가 붙어 있으면 조각 생성
        FoodAssembler assembler = foodRoot.GetComponent<FoodAssembler>();
        if (assembler != null)
        {
            assembler.AssembleSlices(foodRoot.transform);
            CurrentTracker = foodRoot.GetComponent<FoodPiecesTracker>();
        }
        else
        {
            Debug.LogWarning("FoodAssembler 스크립트가 Prefab에 없음!");
        }
    }
}