using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class MinigameUIManager : MonoBehaviour
{
    // UI, 배치 외부참조
    [SerializeField] private TMP_Text guideText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private GameObject blockInputPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject lifeManager;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private GameObject standingArea;
    [SerializeField] private GameStartEnd gameStartEnd;

    [Header("Planet CSVs (index: 0=1번행성, 1=2번행성, 2=3번행성, 3=4번행성)")]
    [SerializeField] private TextAsset[] planetCsvs;

    [Header("Standing Sprites")]
    [SerializeField] private Sprite playerStandingSprite;
    [SerializeField] private Sprite[] enemyStandingSprites;
    [SerializeField] private Sprite[] enemyVictorySprites;

    [Header("GameOver Fade")]
    [SerializeField] private Image gameOverPanelImage; // 패널의 Image
    [SerializeField] private float gameOverFadeDuration = 1.0f;
    [SerializeField] private float sceneStartFadeDuration = 1.0f;
    [SerializeField] private GameObject[] FadeAwayObject;

    private bool isGameOver = false;

    // ===== NEW MODE: Timeline =====
    [Header("NEW MODE - Timeline CSV")]
    [SerializeField] private TextAsset stageTimelineCsv;
    [SerializeField] private float preEndGap = 0.5f;

    // CSV에서 현재 행성(stage) 행을 읽어 채운다.
    // 마지막 값은 미니게임 시작 시간이 아니라 전체 스테이지 종료 시간이다.
    private readonly List<float> startTimes = new List<float>();

    [Header("NEW MODE - Black Panel")]
    [SerializeField] private Image blackPanelImage;
    [SerializeField] private float blackFadeDuration = 0.08f;

    [Header("NEW MODE - Single BGM")]
    [SerializeField] private AudioClip[] stageBGMs;
    [SerializeField] private bool loopBGM = true;

    [Header("NEW MODE - Final Event")]
    [SerializeField] private GameObject finalObject; // 마지막에 보여줄 오브젝트
    [SerializeField] private float finalBgmTargetVolume = 0.5f;
    [SerializeField] private float finalBgmFadeTime = 5f;

    [Header("NEW MODE - Timeline Offset")]
    [SerializeField] private bool useStartTimeOffset = false;
    [SerializeField] private float startTimeOffset = 0f;

    [Header("Score Debug")]
    [SerializeField] private bool printTotalScoreDebug = true;

    private int plannedMinigameCount;

    private bool fullRunFinalized;

    private readonly HashSet<int>
        collectedMinigameInstanceIds =
            new HashSet<int>();

    private GameSaveManager saveManager;

    public PlanetRunResult FinalRunResult
    {
        get;
        private set;
    }

    private int totalNodeSum;
    private int totalPerfectSum;
    private int totalGoodSum;
    private int totalMissSum;
    private int endedMinigameCount;

    private Coroutine timelineCoroutine;
    private bool isSwitching = false;

    // 라운드 흐름 내부 변수
    private int selectedPlanet;
    private float loadingTime = 2f;
    private float timerDuration;
    private float timerElapsed;
    private bool isTimerActive;
    private bool isEndingMinigame = false;
    private int currentStage = 1;
    private int bossStageIndex = 0;
    private string bossMinigamePath = "";
    private Coroutine failCoroutine;
    private RhythmManager rhythmManager;

    // 성공, 실패 관련 연출
    private SpriteRenderer playerRenderer;
    private SpriteRenderer enemyRenderer;
    private Sprite originalEnemySprite;
    private Sprite originalPlayerSprite;
    private SuccessFailPanel successFailPanel;

    // 기타 미니게임 관리
    private MiniGameBase currentMinigame;
    private LifeNumber lifeNumber;
    private Queue<string> minigameQueue = new Queue<string>();
    private MiniGameBase preparedMinigame;
    private string preparedMinigamePath;
    private bool preparedReady = false;

    private double bgmStartDspTime;
    private float pendingStartT;
    private bool bgmScheduled = false;
    private bool bgmActuallyStarted = false;
    private double gameplayStartDspTime;
    private const double BgmStartDelay = 0.1f;
    //private int life = 4;

    // 사운드 및 폴리싱
    //[SerializeField] private AudioClip successBGM;
    //[SerializeField] private AudioClip failureBGM;
    public AudioSource audioSource;

    private void Awake()
    {
        selectedPlanet = GetSelectedPlanetFromSession();

        saveManager =
            FindObjectOfType<GameSaveManager>();
    }

    private int GetSelectedPlanetFromSession()
    {
        if (GameRoot.Instance == null)
        {
            Debug.LogError(
                "[MinigameUIManager] GameRoot.Instance가 없습니다. " +
                "BootStrapScene부터 실행했는지 확인하세요."
            );

            return 1;
        }

        GameSessionManager session =
            GameRoot.Instance.Session;

        if (session == null)
        {
            Debug.LogError(
                "[MinigameUIManager] GameSessionManager가 없습니다."
            );

            return 1;
        }

        if (!session.IsInitialized)
        {
            Debug.LogError(
                "[MinigameUIManager] GameSessionManager가 " +
                "초기화되지 않았습니다."
            );

            return 1;
        }

        int planetId =
            session.Data.selectedPlanetId;

        if (planetId < 1 || planetId > 4)
        {
            Debug.LogError(
                $"[MinigameUIManager] 잘못된 selectedPlanetId: " +
                $"{planetId}"
            );

            return 1;
        }

        return planetId;
    }

    void Start()
    {
        resultPanel.SetActive(false);
        timerSlider.gameObject.SetActive(false);
        guideText.gameObject.SetActive(false);
        blockInputPanel.SetActive(false);
        timerSlider.value = 0f;

        successFailPanel = resultPanel.GetComponent<SuccessFailPanel>();
        lifeNumber = lifeManager.GetComponent<LifeNumber>();
        playerRenderer = standingArea.transform.GetChild(0).GetComponent<SpriteRenderer>();
        enemyRenderer = standingArea.transform.GetChild(1).GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        playerRenderer.sprite = playerStandingSprite;
        enemyRenderer.sprite = enemyStandingSprites[selectedPlanet - 1];
        rhythmManager = GetComponent<RhythmManager>();
        //UpdateStageText();
        //PlayBounceAnimation(playerRenderer.transform);
        //PlayBounceAnimation(enemyRenderer.transform);
        StartCoroutine(HideFadeAwayObjectsAfterDelay(10f));

        InitBlackPanel();

        switch (selectedPlanet)
        {
            case 1: SetMinigameQueue("PolicePlanet", 10); break;
            case 2: SetMinigameQueue("CandyPlanet", 15); break;
            case 3: SetMinigameQueue("MafiaPlanet", 15); break;
            case 4: SetMinigameQueue("MusicPlanet", 15); break;
            default: return;
        }
        plannedMinigameCount = minigameQueue.Count;

        ResetFullRunScore();

        if (!LoadTimelineFromCsv())
            return;

        ValidateTimeline();

        PlayStageBGM();
        timelineCoroutine = StartCoroutine(TimelineRoutine());

        if (gameOverPanelImage != null)
        {
            var c = gameOverPanelImage.color;
            c.a = 1f;
            gameOverPanelImage.color = c;

            gameOverPanelImage.gameObject.SetActive(true);
        }
        StartCoroutine(SceneStartFadeRoutine());
        //StartCoroutine(LoadNextMinigameRoutine());
    }

    private IEnumerator SceneStartFadeRoutine()
    {
        if (gameOverPanelImage == null)
            yield break;

        gameOverPanelImage.gameObject.SetActive(true);

        var c = gameOverPanelImage.color;
        c.a = 1f;
        gameOverPanelImage.color = c;

        yield return gameOverPanelImage
            .DOFade(0f, sceneStartFadeDuration)
            .SetEase(Ease.Linear)
            .WaitForCompletion();

        gameOverPanelImage.gameObject.SetActive(false);
    }

    void Update()
    {
        if (isTimerActive)
        {
            timerElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(1f - (timerElapsed / timerDuration));
            timerSlider.value = t;

            if (timerElapsed >= timerDuration)
            {
                isTimerActive = false;

                if (failCoroutine == null)
                    failCoroutine = StartCoroutine(TimerEndFailDelay());
            }
        }
    }

    private void PlayStageBGM()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("[MinigameUIManager] AudioSource is NULL");
            return;
        }
        int idx = selectedPlanet - 1;
        AudioClip clip = (stageBGMs != null && idx >= 0 && idx < stageBGMs.Length)
            ? stageBGMs[idx]
            : null;

        if (clip == null)
        {
            Debug.LogWarning("[MinigameUIManager] stageBGM is NULL");
            return;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = loopBGM;

        bgmStartDspTime = AudioSettings.dspTime + BgmStartDelay;
        audioSource.PlayScheduled(bgmStartDspTime);
        bgmScheduled = true;
    }

    private double GetBGMElapsedTime()
    {
        if (!bgmScheduled) return 0.0;

        double now = AudioSettings.dspTime;
        double elapsed = now - bgmStartDspTime;

        if (elapsed < 0.0) return 0.0;
        return elapsed;
    }

    private void InitBlackPanel()
    {
        if (blackPanelImage == null) return;

        blackPanelImage.gameObject.SetActive(true);
        var c = blackPanelImage.color;
        c.a = 0f;
        blackPanelImage.color = c;
        blackPanelImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeBlack(bool show)
    {
        if (blackPanelImage == null) yield break;

        blackPanelImage.gameObject.SetActive(true);
        float targetA = show ? 1f : 0f;

        yield return blackPanelImage
            .DOFade(targetA, blackFadeDuration)
            .SetEase(Ease.Linear)
            .WaitForCompletion();

        if (!show) blackPanelImage.gameObject.SetActive(false);
    }
    private bool LoadTimelineFromCsv()
    {
        startTimes.Clear();

        if (stageTimelineCsv == null)
        {
            Debug.LogError(
                "[Timeline] stageTimelineCsv가 할당되지 않았습니다."
            );
            return false;
        }

        string targetStage =
            $"{selectedPlanet}stage";

        string[] lines =
            stageTimelineCsv.text.Split(
                new[] { "\r\n", "\n", "\r" },
                System.StringSplitOptions.RemoveEmptyEntries
            );

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            char delimiter =
                line.Contains("\t") ? '\t' : ',';

            string[] cells =
                line.Split(delimiter);

            if (cells.Length < 2)
                continue;

            string rowName =
                cells[0]
                    .Trim()
                    .Trim('\uFEFF', '"')
                    .Replace(" ", "")
                    .ToLowerInvariant();

            if (rowName != targetStage)
                continue;

            for (int i = 1; i < cells.Length; i++)
            {
                string valueText =
                    cells[i]
                        .Trim()
                        .Trim('"');

                if (string.IsNullOrWhiteSpace(valueText))
                    continue;

                if (float.TryParse(
                    valueText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value))
                {
                    startTimes.Add(value);
                }
                else
                {
                    Debug.LogWarning(
                        $"[Timeline] 숫자로 읽을 수 없는 값 무시: " +
                        $"{valueText}"
                    );
                }
            }

            break;
        }

        int expectedCount =
            plannedMinigameCount + 1;

        if (startTimes.Count != expectedCount)
        {
            Debug.LogError(
                $"[Timeline] {targetStage}에서 읽은 시간 개수: " +
                $"{startTimes.Count}, 필요 개수: {expectedCount} " +
                $"(미니게임 {plannedMinigameCount}개 + 종료시간 1개)"
            );

            return false;
        }

        Debug.Log(
            $"[Timeline] {targetStage} 로드 완료: " +
            $"{plannedMinigameCount}개 미니게임, " +
            $"종료 {startTimes[plannedMinigameCount]:0.###}초"
        );

        return true;
    }

    private float GetTimelineStartTime(int index)
    {
        if (startTimes == null || index < 0 || index >= startTimes.Count)
            return 0f;

        float t = startTimes[index];

        if (useStartTimeOffset)
            t += startTimeOffset;

        return Mathf.Max(0f, t);
    }

    private void ValidateTimeline()
    {
        // startTimes가 오름차순이 아니면 경고
        for (int i = 1; i < startTimes.Count; i++)
        {
            if (startTimes[i] < startTimes[i - 1])
            {
                Debug.LogWarning($"[Timeline] startTimes not sorted: {startTimes[i - 1]} -> {startTimes[i]}");
                break;
            }
        }

        int gameCount = minigameQueue.Count;

        // CSV 형식은 "미니게임 시작시간 N개 + 스테이지 종료시간 1개"
        if (startTimes.Count != gameCount + 1)
        {
            Debug.LogError(
                $"[Timeline] startTimes({startTimes.Count}) != " +
                $"minigameQueue({gameCount}) + 종료시간(1)."
            );
        }
    }

    private IEnumerator TimelineRoutine()
    {
        int gameCount = minigameQueue.Count;

        // BGM이 실제로 시작될 때까지 대기
        while (GetBGMElapsedTime() <= 0.0)
            yield return null;

        bgmActuallyStarted = true;
        gameplayStartDspTime = bgmStartDspTime;

        // 미니게임 시작시간 + 마지막 종료시간까지 있어야 진행
        if (startTimes.Count != gameCount + 1)
        {
            Debug.LogError(
                $"[Timeline] startTimes({startTimes.Count}) != " +
                $"gameCount({gameCount}) + 1."
            );
            yield break;
        }

        // 미니게임 순차 실행
        for (int i = 0; i < gameCount; i++)
        {
            float startT = GetTimelineStartTime(i);
            pendingStartT = startT;
            float preEndT = Mathf.Max(0f, startT - preEndGap);

            // 시작 직전 검은 패널 띄우고 이전 미니게임 제거
            yield return WaitUntilBGMTime(preEndT);
            yield return EndCurrentMinigame_ShowBlack();

            // 다음 미니게임 준비
            string nextPath = minigameQueue.Dequeue();
            yield return PrepareNextMinigame(nextPath);

            // 정확한 시작 시간까지 대기
            yield return WaitUntilBGMTime(startT);

            // 준비된 미니게임 시작
            yield return StartPreparedMinigame();
        }

        // 마지막 엔딩 이벤트 시간
        // startTimes가 gameCount + 1개면 마지막 값을 엔딩 타이밍으로 사용
        if (startTimes.Count > gameCount)
        {
            float finalT = GetTimelineStartTime(gameCount);
            float finalPreEndT = Mathf.Max(0f, finalT - preEndGap);

            yield return WaitUntilBGMTime(finalPreEndT);
            yield return EndCurrentMinigame_ShowBlack();

            yield return WaitUntilBGMTime(finalT);

            yield return FadeBlack(false);

            CompleteFullRun();

            FadeBGM(finalBgmTargetVolume, finalBgmFadeTime);
        }
        else
        {
            // 엔딩 타이밍이 따로 없으면 마지막 미니게임 끝난 뒤 바로 종료 처리
            yield return EndCurrentMinigame_ShowBlack();

            yield return FadeBlack(false);

            CompleteFullRun();

            FadeBGM(finalBgmTargetVolume, finalBgmFadeTime);
        }
    }

    private void FadeBGM(float target, float duration)
    {
        if (audioSource == null) return;

        audioSource.DOFade(target, duration);
    }

    private IEnumerator PrepareNextMinigame(string minigamePath)
    {
        preparedReady = false;
        preparedMinigame = null;
        preparedMinigamePath = minigamePath;

        var prefab = Resources.Load<GameObject>(minigamePath);
        if (prefab == null)
        {
            Debug.LogError($"[Timeline] Missing prefab: {minigamePath}");
            yield break;
        }

        GameObject obj = Instantiate(prefab);
        preparedMinigame = obj.GetComponent<MiniGameBase>();

        if (preparedMinigame == null)
        {
            Debug.LogError($"[Timeline] MiniGameBase not found: {minigamePath}");
            Destroy(obj);
            yield break;
        }

        // 아직 시작 전이므로 입력 막아두기
        blockInputPanel.SetActive(true);

        ShowGuide(preparedMinigame.GetMinigameExplain, 1f);

        if (rhythmManager != null)
        {
            string minigameId = ExtractMinigameIdFromPath(minigamePath);

            TextAsset csv = null;
            int idx = selectedPlanet - 1;
            if (planetCsvs != null && idx >= 0 && idx < planetCsvs.Length)
                csv = planetCsvs[idx];

            if (csv == null)
                Debug.LogWarning($"[MinigameUIManager] planetCsvs[{idx}] is NULL. (selectedPlanet={selectedPlanet})");

            yield return ConfigureRhythmRoutine(preparedMinigame, minigameId, csv);
            RefreshRhythmWindows();
        }

        preparedReady = true;
    }

    private IEnumerator StartPreparedMinigame()
    {
        if (!preparedReady || preparedMinigame == null)
        {
            Debug.LogError("[Timeline] Prepared minigame is not ready.");
            yield break;
        }

        currentMinigame = preparedMinigame;
        preparedMinigame = null;
        preparedReady = false;

        yield return FadeBlack(false);
        blockInputPanel.SetActive(false);

        timerDuration = currentMinigame.GetTimerDuration;
        StartTimer(timerDuration);

        // 먼저 미니게임 초기화
        currentMinigame.StartGame();

        // 초기화가 한 프레임 반영되게 함
        yield return null;

        // 그 다음 노드 실행
        if (rhythmManager != null)
            rhythmManager.StartSong(bgmStartDspTime + pendingStartT);
    }

    private IEnumerator WaitUntilBGMTime(float t)
    {
        while (GetBGMElapsedTime() < t)
            yield return null;
    }

    private IEnumerator EndCurrentMinigame_ShowBlack()
    {
        if (isSwitching) yield break;
        isSwitching = true;

        // 입력 막기 + 타이머 정리
        blockInputPanel.SetActive(true);
        isTimerActive = false;

        if (failCoroutine != null)
        {
            StopCoroutine(failCoroutine);
            failCoroutine = null;
        }

        // 공백: 검은 패널 ON
        yield return FadeBlack(true);

        // 미니게임 제거
        if (currentMinigame != null)
        {
            CollectMinigameScore(currentMinigame);

            DOTween.Kill(currentMinigame.transform);

            if (rhythmManager != null)
                rhythmManager.ClearCurrent();

            if (mainCamera != null)
                mainCamera.transform.position = new Vector3(0f, 0f, -10f);

            Destroy(currentMinigame.gameObject);
            currentMinigame = null;
        }

        // UI 정리
        if (resultPanel != null) resultPanel.SetActive(false);
        if (timerSlider != null) timerSlider.gameObject.SetActive(false);

        isSwitching = false;
    }

    // 점수 책정 디버그
    private void CollectMinigameScore(
    MiniGameBase minigame)
    {
        if (minigame == null)
            return;

        int instanceId =
            minigame.GetInstanceID();

        if (!collectedMinigameInstanceIds.Add(
                instanceId))
        {
            Debug.LogWarning(
                $"[Score] 이미 수집한 미니게임 점수 무시: " +
                $"{minigame.gameObject.name}"
            );

            return;
        }

        MiniGameBase.ScoreResult result =
            minigame.FinalizeScoreSession();

        totalNodeSum += result.totalNode;
        totalPerfectSum += result.perfect;
        totalGoodSum += result.good;
        totalMissSum += result.miss;

        endedMinigameCount++;

        if (printTotalScoreDebug)
        {
            Debug.Log(
                $"[Planet Score After Minigame " +
                $"{endedMinigameCount}]\n" +
                $"- Total Nodes: {totalNodeSum}\n" +
                $"- Perfect: {totalPerfectSum}\n" +
                $"- Good: {totalGoodSum}\n" +
                $"- Miss: {totalMissSum}"
            );
        }
    }
    private void CompleteFullRun()
    {
        if (fullRunFinalized)
            return;

        fullRunFinalized = true;

        bool isCleared =
            plannedMinigameCount > 0 &&
            endedMinigameCount >=
            plannedMinigameCount;

        FinalRunResult =
            PlanetScoreCalculator.Calculate(
                selectedPlanet,
                endedMinigameCount,
                totalNodeSum,
                totalPerfectSum,
                totalGoodSum,
                totalMissSum,
                isCleared
            );

        Debug.Log(
            $"[Planet Full Run Result]\n" +
            $"- Planet: {FinalRunResult.planetId}\n" +
            $"- Minigames: " +
            $"{FinalRunResult.completedMinigameCount}/" +
            $"{plannedMinigameCount}\n" +
            $"- Nodes: {FinalRunResult.totalNode}\n" +
            $"- Perfect: {FinalRunResult.perfect}\n" +
            $"- Good: {FinalRunResult.good}\n" +
            $"- Miss: {FinalRunResult.miss}\n" +
            $"- Score: {FinalRunResult.score}\n" +
            $"- Evaluation: " +
            $"{FinalRunResult.evaluation}\n" +
            $"- Cleared: {FinalRunResult.isCleared}"
        );

        if (saveManager == null)
        {
            saveManager =
                FindObjectOfType<GameSaveManager>();
        }

        if (saveManager != null)
        {
            saveManager.RecordFullRunResult(
                FinalRunResult
            );
        }
        else
        {
            Debug.LogError(
                "[MinigameUIManager] " +
                "GameSaveManager를 찾을 수 없습니다."
            );
        }

        if (finalObject != null)
            finalObject.SetActive(true);

        if (gameStartEnd != null)
        {
            gameStartEnd.ShowFinalPanel(
                FinalRunResult
            );
        }
    }
    private IEnumerator TimerEndFailDelay()
    {
        yield return new WaitForSeconds(1f);

        if (isGameOver || isEndingMinigame || currentMinigame == null)
        {
            failCoroutine = null;
            yield break;
        }

        //currentMinigame.Fail();
        failCoroutine = null;
    }

    // 미니게임 세팅
    private void SetMinigameQueue(string planetName, int minigameCount)
    {
        // 기존 큐 초기화(원하면 유지)
        minigameQueue.Clear();

        // 1 ~ minigameCount 까지 순서대로, 단 6과 7만 교체
        for (int i = 1; i <= minigameCount; i++)
        {
            int idx = i;

            if (i == 6) idx = 7;
            else if (i == 7) idx = 6;

            string path = $"MinigamePrefab/{planetName}/{selectedPlanet}_{idx}minigame_remake";

            if (Resources.Load<GameObject>(path) != null)
            {
                minigameQueue.Enqueue(path);
            }
            else
            {
                Debug.LogWarning($"[SetMinigameQueue] Missing prefab: {path}");
            }
        }
    }

    // 디음 미니게임 코루틴
    private IEnumerator LoadNextMinigameRoutine()
    {
        if (minigameQueue.Count == 0)
        {
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("LobbyScene");
            yield break;
        }

        yield return new WaitForSeconds(loadingTime);

        string minigamePath = minigameQueue.Dequeue();
        GameObject minigameObj = Instantiate(Resources.Load<GameObject>(minigamePath));
        currentMinigame = minigameObj.GetComponent<MiniGameBase>();

        ShowGuide(currentMinigame.GetMinigameExplain, 1f);
        yield return new WaitForSeconds(0.5f);

        if (rhythmManager != null)
        {
            string minigameId = ExtractMinigameIdFromPath(minigamePath);

            TextAsset csv = null;
            int idx = selectedPlanet - 1;
            if (planetCsvs != null && idx >= 0 && idx < planetCsvs.Length)
                csv = planetCsvs[idx];

            if (csv == null)
                Debug.LogWarning($"[MinigameUIManager] planetCsvs[{idx}] is NULL. 리듬 차트 로드 실패 가능");

            yield return ConfigureRhythmRoutine(currentMinigame, minigameId, csv);
            RefreshRhythmWindows();

            if (csv == null)
                Debug.LogWarning($"[MinigameUIManager] planetCsvs[{idx}] is NULL. 리듬 차트 로드 실패 가능");
        }

        StartMinigame();
    }

    private IEnumerator ConfigureRhythmRoutine(MiniGameBase targetMinigame, string minigameId, TextAsset csv)
    {
        var task = rhythmManager.ConfigureForMinigameAsync(targetMinigame, minigameId, csv);
        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
            Debug.LogError($"[MinigameUIManager] {minigameId} 차트 로드 실패: {task.Exception?.GetBaseException().Message}");
    }

    private void RefreshRhythmWindows()
    {
        if (rhythmManager == null) return;
        rhythmManager.RefreshWindowsFromCurrentMinigame();
    }

    private IEnumerator HideFadeAwayObjectsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (FadeAwayObject == null) yield break;

        foreach (var obj in FadeAwayObject)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }

    // 미니게임 상태 및 시간 초기화
    private void StartMinigame()
    {
        //CancelInvoke(nameof(PlayWinEffect));
        //CancelInvoke(nameof(PlayLoseEffect));

        //currentMinigame.OnSuccess += OnMinigameSuccess;
        //currentMinigame.OnFail += OnMinigameFail;
        //currentMinigame.BindRhythmManager(FindObjectOfType<RhythmManager>());

        timerDuration = currentMinigame.GetTimerDuration;
        StartTimer(timerDuration);

        currentMinigame.StartGame();
    }

    private string ExtractMinigameIdFromPath(string minigamePath)
    {
        // 예: "MinigamePrefab/MusicPlanet/4_12minigame_remake"
        // -> "4-12"
        var file = minigamePath.Substring(minigamePath.LastIndexOf('/') + 1); // "4_12minigame_remake"
        int us = file.IndexOf('_');
        if (us < 0) return $"{selectedPlanet}-1";

        string a = file.Substring(0, us); // "4"
        string rest = file.Substring(us + 1); // "12minigame_remake"

        int k = 0;
        while (k < rest.Length && char.IsDigit(rest[k])) k++;
        string b = (k > 0) ? rest.Substring(0, k) : "1";

        return $"{a}-{b}";
    }
    private void ResetFullRunScore()
    {
        totalNodeSum = 0;
        totalPerfectSum = 0;
        totalGoodSum = 0;
        totalMissSum = 0;

        endedMinigameCount = 0;
        fullRunFinalized = false;

        FinalRunResult = null;

        collectedMinigameInstanceIds.Clear();
    }

    // 가이드 텍스트
    public void ShowGuide(string text, float duration)
    {
        StartCoroutine(ShowGuideCoroutine(text, duration));
    }

    private IEnumerator ShowGuideCoroutine(string text, float duration)
    {
        guideText.text = text;
        //guideText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        guideText.gameObject.SetActive(false);
    }

    // 제한시간 관리
    public void StartTimer(float duration)
    {
        timerDuration = duration;
        timerElapsed = 0f;
        isTimerActive = true;
        timerSlider.value = 1f;
    }
    /*
    // 미니게임 성공/실패
    private void OnMinigameSuccess()
    {
        isTimerActive = false;

        if (failCoroutine != null)
        {
            StopCoroutine(failCoroutine);
            failCoroutine = null;
        }

        resultPanel.SetActive(true);
        successFailPanel.SuccessPanel();
        Invoke("PlayWinEffect",1f);

        currentStage++;
        if (currentStage >= bossStageIndex) currentStage = bossStageIndex;
        StartCoroutine(DelayAndEndMinigame());
    }

    private void OnMinigameFail()
    {
        if (isGameOver) return;

        isTimerActive = false;
        life--;
        lifeNumber.LoseLife();

        resultPanel.SetActive(true);
        successFailPanel.FailurePanel();
        Invoke("PlayLoseEffect", 1f);

        if (currentStage == bossStageIndex)
        {
            if (life <= 0)
            {
                StartCoroutine(GameOverRoutine());
                return;
            }

            StartCoroutine(RetryCurrentMinigame());
        }
        else
        {
            currentStage++;

            StartCoroutine(DelayAndEndMinigame());
        }
    }
    
    // 승리 실패 연출
    private void PlayWinEffect()
    {
        if (originalPlayerSprite == null)
            originalPlayerSprite = playerRenderer.sprite;

        StartCoroutine(PlayStandingEffect(playerRenderer, originalPlayerSprite, originalPlayerSprite, -5f, 1.5f));
        StartCoroutine(PlayDefeatedEffect(enemyRenderer, -10f, 1.0f));

        audioSource.clip = successBGM;
    }
    private void PlayLoseEffect()
    {
        if (originalEnemySprite == null)
            originalEnemySprite = enemyRenderer.sprite;

        StartCoroutine(PlayStandingEffect(enemyRenderer, enemyVictorySprites[selectedPlanet - 1], originalEnemySprite, 5f, -1.5f));
        StartCoroutine(PlayDefeatedEffect(playerRenderer, 10f, -1.0f));

        audioSource.clip = failureBGM;
    }
    */
    private IEnumerator GameOverRoutine()
    {
        if (isGameOver) yield break;
        isGameOver = true;

        // 타이머/입력/코루틴 정리
        isTimerActive = false;

        if (failCoroutine != null)
        {
            StopCoroutine(failCoroutine);
            failCoroutine = null;
        }

        blockInputPanel.SetActive(true); // 입력 막기

        // 리듬 정리(있으면)
        if (rhythmManager != null)
            rhythmManager.ClearCurrent();

        if (gameOverPanelImage != null)
        {
            gameOverPanelImage.gameObject.SetActive(true);

            // 알파 0으로 시작
            var c = gameOverPanelImage.color;
            c.a = 0f;
            gameOverPanelImage.color = c;

            yield return gameOverPanelImage.DOFade(1f, gameOverFadeDuration)
                                           .SetEase(Ease.Linear)
                                           .WaitForCompletion();
        }

        CameraScrollController.selectedPlanetIndex = 0;
        SceneManager.LoadScene("LobbyScene");
    }

    private IEnumerator DelayAndEndMinigame()
    {
        if (isEndingMinigame) yield break;
        isEndingMinigame = true;

        //UpdateStageText();
        blockInputPanel.SetActive(true);

        yield return new WaitForSeconds(1f);

        if (currentMinigame != null)
        {
            CollectMinigameScore(currentMinigame);

            DOTween.Kill(currentMinigame.transform);

            if (rhythmManager != null)
                rhythmManager.ClearCurrent();

            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            Destroy(currentMinigame.gameObject);
        }

        Debug.Log("게임 끝난 판정");
        blockInputPanel.SetActive(false);
        timerSlider.gameObject.SetActive(false);
        isEndingMinigame = false;
        StartCoroutine(WaitAndLoadNext());
        audioSource.Play();
    }

    private IEnumerator WaitAndLoadNext()
    {
        yield return new WaitForSeconds(2f);
        resultPanel.SetActive(false);
        StartCoroutine(LoadNextMinigameRoutine());
    }

    /*
    // UI 추가 관리
    private void UpdateStageText()
    {
        stageText.text = $"{currentStage}";
    }

    // idle 모션
    private void PlayBounceAnimation(Transform target)
    {
        Sequence seq = DOTween.Sequence();

        float scaleX = 1.01f; 
        float scaleY = 0.99f; 
        float moveY = -0.01f; 
        float duration = 0.15f; 

        Vector3 originalScale = target.localScale;
        Vector3 squashedScale = new Vector3(originalScale.x * scaleX, originalScale.y * scaleY, originalScale.z);
        Vector3 stretchedScale = new Vector3(originalScale.x * 0.95f, originalScale.y * 1.05f, originalScale.z);
        Vector3 originalPosition = target.localPosition;

        seq.Append(target.DOScale(squashedScale, duration).SetEase(Ease.OutQuad));
        seq.Join(target.DOLocalMoveY(originalPosition.y + moveY, duration).SetEase(Ease.OutQuad));

        seq.Append(target.DOScale(stretchedScale, duration).SetEase(Ease.OutQuad));
        seq.Join(target.DOLocalMoveY(originalPosition.y - moveY, duration).SetEase(Ease.OutQuad));

        seq.Append(target.DOScale(originalScale, duration).SetEase(Ease.OutQuad));
        seq.Join(target.DOLocalMoveY(originalPosition.y, duration).SetEase(Ease.OutQuad));

        seq.AppendInterval(0.15f);

        seq.SetLoops(-1); // 무한 반복
    }

    // 승리 모션
    private IEnumerator PlayStandingEffect(SpriteRenderer targetRenderer, Sprite victorySprite, Sprite originalSprite, float rotationAmount, float moveXAmount)
    {
        Vector3 originalPos = targetRenderer.transform.localPosition;
        Vector3 originalRotation = targetRenderer.transform.localEulerAngles;

        // 기존 점프
        targetRenderer.transform.DOLocalMoveY(originalPos.y + 0.15f, 0.15f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.15f);
        targetRenderer.transform.DOLocalMoveY(originalPos.y, 0.15f).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(0.15f);

        if (victorySprite != null)
        {
            targetRenderer.sprite = victorySprite;

            Sequence seq = DOTween.Sequence();
            Vector3 targetPos = new Vector3(originalPos.x + moveXAmount, originalPos.y + 0.2f, originalPos.z);

            seq.Append(targetRenderer.transform.DOLocalMove(targetPos, 0.1f).SetEase(Ease.OutQuad));
            seq.Join(targetRenderer.transform.DOLocalRotate(new Vector3(0f, 0f, rotationAmount), 0.1f).SetEase(Ease.OutQuad));

            yield return seq.WaitForCompletion();

            yield return new WaitForSeconds(2f);

            Sequence resetSeq = DOTween.Sequence();
            resetSeq.Append(targetRenderer.transform.DOLocalMove(originalPos, 0.1f).SetEase(Ease.InQuad));
            resetSeq.Join(targetRenderer.transform.DOLocalRotate(originalRotation, 0.1f).SetEase(Ease.InQuad));

            yield return resetSeq.WaitForCompletion();

            targetRenderer.sprite = originalSprite;
        }
    }

    // 패배 모션    
    private IEnumerator PlayDefeatedEffect(SpriteRenderer targetRenderer, float rotationAmount, float moveXAmount)
    {
        Vector3 originalPos = targetRenderer.transform.localPosition;
        Vector3 originalRotation = targetRenderer.transform.localEulerAngles;

        targetRenderer.transform.DOLocalMoveY(originalPos.y + 0.15f, 0.15f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.15f);
        targetRenderer.transform.DOLocalMoveY(originalPos.y, 0.15f).SetEase(Ease.InQuad);
        yield return new WaitForSeconds(0.15f);

        Sequence seq = DOTween.Sequence();
        Vector3 targetPos = new Vector3(originalPos.x + moveXAmount, originalPos.y + 0.2f, originalPos.z);

        seq.Append(targetRenderer.transform.DOLocalMove(targetPos, 0.1f).SetEase(Ease.OutQuad));
        seq.Join(targetRenderer.transform.DOLocalRotate(new Vector3(0f, 0f, rotationAmount), 0.1f).SetEase(Ease.OutQuad));

        yield return seq.WaitForCompletion();

        yield return new WaitForSeconds(2f);

        Sequence resetSeq = DOTween.Sequence();
        resetSeq.Append(targetRenderer.transform.DOLocalMove(originalPos, 0.1f).SetEase(Ease.InQuad));
        resetSeq.Join(targetRenderer.transform.DOLocalRotate(originalRotation, 0.1f).SetEase(Ease.InQuad));

        yield return resetSeq.WaitForCompletion();
    }
    */
    public void DebugSkipToNextMinigame()
    {
        if (isSwitching) return;
        if (isGameOver) return;

        StartCoroutine(DebugSkipToNextMinigameRoutine());
    }

    private IEnumerator DebugSkipToNextMinigameRoutine()
    {
        if (timelineCoroutine != null)
        {
            StopCoroutine(timelineCoroutine);
            timelineCoroutine = null;
        }

        yield return EndCurrentMinigame_ShowBlack();

        if (minigameQueue.Count <= 0)
        {
            yield return FadeBlack(false);

            CompleteFullRun();

            FadeBGM(finalBgmTargetVolume, finalBgmFadeTime);
            yield break;
        }

        string nextPath = minigameQueue.Dequeue();

        yield return PrepareNextMinigame(nextPath);
        yield return StartPreparedMinigame();
    }
}