using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class RhythmManager :
    MonoBehaviour,
    MiniGameBase.IRhythmManager
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    public enum ChartLoadMode
    {
        TextAsset,
        Addressables
    }

    [Header("Chart Source")]
    public ChartLoadMode loadMode =
        ChartLoadMode.Addressables;

    [Header("TextAsset (Optional)")]
    public TextAsset chartFile;

    [Header("Addressables")]
    [Tooltip(
        "전체 차트를 하나로 쓸 거면 key를 고정. " +
        "미니게임마다 다르면 ConfigureForMinigame에서 교체."
    )]
    public string addressablesKey;

    [Header("Judgement Settings (seconds)")]
    public float perfectWindow = 0.1f;
    public float goodWindow = 0.3f;
    public float hitWindow = 1f;

    public class RhythmEvent
    {
        public string action;
        public string type;
        public double time;
        public bool consumed;
    }

    [Header("Runtime")]
    [SerializeField]
    private List<RhythmEvent> events =
        new List<RhythmEvent>();

    private int eventIndex;
    private double dspStartTime;

    private MiniGameBase currentMinigame;
    private string currentMinigameId;
    private bool isRunning;
    public double SongTimePublic => SongTime;

    // 연습 씬처럼 이 RhythmManager가
    // 음악까지 직접 재생할 때만 true가 된다.
    private bool playAudioWithTimeline;

    private AsyncOperationHandle<TextAsset>?
        loadedHandle;

    private double songTime =>
        AudioSettings.dspTime - dspStartTime;

    public double DspStartTime => dspStartTime;
    public bool IsRunning => isRunning;
    public double SongTime => songTime;

    public event Action<double> OnSongStarted;
    public event Action OnSongStopped;

    public event Action<string> OnEventTriggered;

    public event Action<MiniGameBase.JudgementResult>
        OnPlayerJudged;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        UnbindCurrentMinigame();

        if (loadedHandle.HasValue)
        {
            Addressables.Release(
                loadedHandle.Value
            );

            loadedHandle = null;
        }
    }

    /// <summary>
    /// 연습 모드처럼 RhythmManager가
    /// 음악까지 직접 재생할 때 사용한다.
    ///
    /// 본게임에서는 호출하지 않으면
    /// 기존 타임라인 동작이 유지된다.
    /// </summary>
    public void SetTimelineMusic(
        AudioClip clip,
        bool loop = false)
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError(
                "[RhythmManager] AudioSource가 없습니다."
            );

            return;
        }

        StopSongInternal();

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = loop;

        playAudioWithTimeline = clip != null;
    }

    public int GetTotalNodeCount()
    {
        int count = 0;

        for (int i = 0; i < events.Count; i++)
        {
            if (IsJudgeType(events[i].type))
                count++;
        }

        return count;
    }

    private static bool IsCueType(string type)
    {
        return string.Equals(
                   type,
                   "Show",
                   StringComparison.OrdinalIgnoreCase
               )
               ||
               string.Equals(
                   type,
                   "Move",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static bool IsJudgeType(string type)
    {
        return string.Equals(
                   type,
                   "Input",
                   StringComparison.OrdinalIgnoreCase
               )
               ||
               string.Equals(
                   type,
                   "Tap",
                   StringComparison.OrdinalIgnoreCase
               )
               ||
               string.Equals(
                   type,
                   "Hold",
                   StringComparison.OrdinalIgnoreCase
               )
               ||
               string.Equals(
                   type,
                   "Swipe",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    public async Task ConfigureForMinigameAsync(
        MiniGameBase minigame,
        string minigameId,
        TextAsset csv)
    {
        if (audioSource == null)
        {
            throw new NullReferenceException(
                "[RhythmManager] audioSource is NULL"
            );
        }

        UnbindCurrentMinigame();
        StopSongInternal();

        currentMinigame = minigame;
        currentMinigameId = minigameId;

        if (csv == null)
        {
            throw new NullReferenceException(
                "[RhythmManager] csv(TextAsset) is NULL"
            );
        }

        loadMode = ChartLoadMode.TextAsset;
        chartFile = csv;

        await LoadChartAsync(
            currentMinigameId
        );

        if (currentMinigame != null)
        {
            currentMinigame.BindRhythmManager(
                this
            );
        }

        ApplyWindowsFromMinigame();
    }

    public void RefreshWindowsFromCurrentMinigame()
    {
        ApplyWindowsFromMinigame();
    }

    private static bool IsShowType(string type)
    {
        return string.Equals(
            type,
            "Show",
            StringComparison.OrdinalIgnoreCase
        );
    }

    public void ClearCurrent()
    {
        UnbindCurrentMinigame();
        StopSongInternal();

        currentMinigameId = null;

        events.Clear();
        eventIndex = 0;
    }

    public async Task LoadChartAsync(
        string minigameId)
    {
        events.Clear();

        TextAsset csv = null;

        if (loadMode == ChartLoadMode.TextAsset)
        {
            csv = chartFile;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(
                    addressablesKey))
            {
                throw new NullReferenceException(
                    "[RhythmManager] " +
                    "addressablesKey is EMPTY"
                );
            }

            if (loadedHandle.HasValue)
            {
                Addressables.Release(
                    loadedHandle.Value
                );

                loadedHandle = null;
            }

            var handle =
                Addressables.LoadAssetAsync<TextAsset>(
                    addressablesKey
                );

            loadedHandle = handle;

            await handle.Task;

            if (handle.Status !=
                AsyncOperationStatus.Succeeded)
            {
                throw new Exception(
                    "[RhythmManager] " +
                    "Failed to load Addressables CSV: " +
                    addressablesKey
                );
            }

            csv = handle.Result;
        }

        if (csv == null)
        {
            throw new NullReferenceException(
                "[RhythmManager] CSV asset is NULL"
            );
        }

        ParseCsv(
            csv.text,
            minigameId
        );

        Debug.Log(
            $"[RhythmManager] Loaded " +
            $"{events.Count} notes for " +
            $"{minigameId} " +
            $"(key={addressablesKey})"
        );
    }

    private void ParseCsv(
        string text,
        string minigameId)
    {
        string raw =
            text.Replace("\uFEFF", "");

        string[] lines =
            raw.Split('\n');

        List<double?> times = null;
        List<string> types = null;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            char delimiter = line.Contains("\t") ? '\t' : ',';   
            string[] parts = line.Split(delimiter);

            if (parts.Length == 0)
                continue;

            string first = parts[0].Trim();

            if (first.Equals(
                    "minigame",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (first == $"{minigameId}_time")
            {
                times = new List<double?>();

                for (int i = 1;
                     i < parts.Length;
                     i++)
                {
                    string value =
                        parts[i].Trim();

                    if (string.IsNullOrWhiteSpace(
                            value)
                        || value == "-")
                    {
                        times.Add(null);
                        continue;
                    }

                    if (double.TryParse(
                            value,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double time))
                    {
                        times.Add(time);
                    }
                    else
                    {
                        times.Add(null);
                    }
                }
            }

            if (first == $"{minigameId}_type")
            {
                types = new List<string>();

                for (int i = 1;
                     i < parts.Length;
                     i++)
                {
                    string value =
                        parts[i].Trim();

                    if (string.IsNullOrWhiteSpace(
                            value)
                        || value == "-")
                    {
                        types.Add(null);
                        continue;
                    }

                    types.Add(value);
                }
            }
        }

        if (times == null)
        {
            throw new Exception(
                "[RhythmManager] No time row found for " +
                minigameId
            );
        }

        if (types == null)
        {
            types = new List<string>(
                new string[times.Count]
            );
        }

        int count =
            Math.Min(times.Count, types.Count);

        for (int i = 0; i < count; i++)
        {
            if (!times[i].HasValue)
                continue;

            string type = types[i];

            if (string.IsNullOrWhiteSpace(type))
                type = "Tap";

            events.Add(
                new RhythmEvent
                {
                    time = times[i].Value,
                    type = type,
                    action = type,
                    consumed = false
                }
            );
        }

        events.Sort(
            (a, b) =>
                a.time.CompareTo(b.time)
        );

        eventIndex = 0;
    }

    public void StartSong(double? syncDspStartTime = null)
    {
        double startTime = syncDspStartTime ?? AudioSettings.dspTime;

        // 연습 모드에서만 설정되는 음악이다.
        if (playAudioWithTimeline &&
            audioSource != null &&
            audioSource.clip != null)
        {
            audioSource.Stop();
            audioSource.time = 0f;

            // 너무 즉시 예약하면 프레임 상황에 따라
            // 시작 시점을 놓칠 수 있으므로 조금 뒤에 예약한다.
            startTime =
                AudioSettings.dspTime + 0.05;

            audioSource.PlayScheduled(
                startTime
            );
        }

        dspStartTime = startTime;

        eventIndex = 0;

        for (int i = 0;
             i < events.Count;
             i++)
        {
            events[i].consumed = false;
        }

        isRunning = true;

        OnSongStarted?.Invoke(
            dspStartTime
        );
    }

    private void StopSongInternal()
    {
        isRunning = false;

        if (playAudioWithTimeline &&
            audioSource != null)
        {
            audioSource.Stop();
        }

        OnSongStopped?.Invoke();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        RunEventTimeline();
        CheckMisses();
    }

    private void RunEventTimeline()
    {
        while (eventIndex < events.Count &&
               events[eventIndex].time <= SongTime)
        {
            RhythmEvent rhythmEvent =
                events[eventIndex];

            OnEventTriggered?.Invoke(
                rhythmEvent.action
            );

            eventIndex++;
        }
    }

    private void ApplyWindowsFromMinigame()
    {
        if (currentMinigame == null)
            return;

        perfectWindow =
            currentMinigame.perfectWindowOverride;

        goodWindow =
            currentMinigame.goodWindowOverride;

        hitWindow =
            currentMinigame.hitWindowOverride;

        Debug.Log(
            $"[RhythmManager] Override windows from " +
            $"{currentMinigame.name} " +
            $"(Perfect={perfectWindow}, " +
            $"Good={goodWindow}, " +
            $"Hit={hitWindow})"
        );
    }

    public void ReceivePlayerInput(
        string action = null)
    {
        if (!isRunning)
            return;

        double now = SongTime;

        RhythmEvent nearest = null;
        double bestDelta = double.MaxValue;

        for (int i = 0;
             i < events.Count;
             i++)
        {
            RhythmEvent rhythmEvent =
                events[i];

            if (rhythmEvent.consumed)
                continue;

            if (!IsJudgeType(rhythmEvent.type))
                continue;

            if (action != null &&
                !string.Equals(
                    rhythmEvent.action,
                    action,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double delta =
                Math.Abs(
                    rhythmEvent.time - now
                );

            if (delta > hitWindow)
                continue;

            if (delta < bestDelta)
            {
                bestDelta = delta;
                nearest = rhythmEvent;
            }
        }

        // 현재 hitWindow 안에 판정 가능한 노드가 없다면
        // 이번 입력은 어떤 노드에도 귀속시키지 않고 무시한다.
        // Miss는 실제 노드가 판정되었거나
        // 노드의 판정 시간이 만료되었을 때만 발생한다.
        if (nearest == null)
            return;

        MiniGameBase.JudgementResult judgement;

        if (bestDelta <= perfectWindow)
        {
            judgement =
                MiniGameBase.JudgementResult.Perfect;
        }
        else if (bestDelta <= goodWindow)
        {
            judgement =
                MiniGameBase.JudgementResult.Good;
        }
        else
        {
            judgement =
                MiniGameBase.JudgementResult.Miss;
        }

        // 선택된 노드는 결과와 관계없이 한 번만 처리한다.
        nearest.consumed = true;

        OnPlayerJudged?.Invoke(judgement);
    }

    private void CheckMisses()
    {
        double now = SongTime;

        for (int i = 0;
             i < events.Count;
             i++)
        {
            RhythmEvent rhythmEvent =
                events[i];

            if (rhythmEvent.consumed)
                continue;

            if (!IsJudgeType(rhythmEvent.type))
                continue;

            // events는 시간순으로 정렬되어 있으므로,
            // 아직 만료되지 않은 첫 판정 노드를 만나면
            // 그 뒤의 노드들도 아직 만료되지 않았다.
            if (now <=
                rhythmEvent.time + hitWindow)
            {
                break;
            }

            // 입력 없이 hitWindow가 지난 노드는
            // 정확히 한 번 자동 Miss 처리한다.
            rhythmEvent.consumed = true;
            events[i] = rhythmEvent;

            OnPlayerJudged?.Invoke(
                MiniGameBase.JudgementResult.Miss
            );

            Debug.Log(
                $"CheckMiss : " +
                $"{rhythmEvent.type} @ " +
                $"{rhythmEvent.time:F3}"
            );
        }
    }

    private void UnbindCurrentMinigame()
    {
        if (currentMinigame != null)
        {
            currentMinigame.BindRhythmManager(
                null
            );
        }

        currentMinigame = null;
    }

    public bool HasDispatchedAllEvents
    {
        get
        {
            return isRunning &&
                   eventIndex >= events.Count;
        }
    }
}