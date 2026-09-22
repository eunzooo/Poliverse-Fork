using System;
using System.Collections;
using UnityEngine;

public class Icicle : MonoBehaviour
{
    public static event Action OnMoveAllowed;
    public static event Action OnMoveBlocked;
    public static event Action<Icicle> OnIcicleDestroyed;

    private int beatCount = 0; // 박자 카운트
    private bool isFalling = false;

    [SerializeField] private float roundTripTime = 0.5f;

    [Header("Sprite")]
    [SerializeField] private Sprite fallingSprite;

    [Header("Warning Move Distance")]
    [SerializeField] private float warningMoveDistance = 0.5f;

    private SpriteRenderer sr;
    private Rigidbody2D rb;

    public event Action OnIcicleFalling;

    private RhythmManager rhythmTest;
    private double spawnSongTime;
    private bool hasCapturedStartTime = false; // 실제 곡 시작 이후 기준 시간을 잡았는지 여부

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true; //떨어지기 전까지 고정
        rhythmTest = FindObjectOfType<RhythmManager>();
    }

    public static void RaiseMoveAllowed()
    {
        OnMoveAllowed?.Invoke();
    }

    public static void RaiseMoveBlocked()
    {
        OnMoveBlocked?.Invoke();
    }

    // 외부에서 박자(beat) 정보를 받아 낙하 시점 결정
    public void StartIcicle(float delay)
    {
        OnMoveBlocked?.Invoke();
        beatCount = 0;
        isFalling = false;
        hasCapturedStartTime = false; // 스폰 시점에는 아직 기준 시간을 확정하지 않음
    }

    void Update()
    {
        if (isFalling || rhythmTest == null || !rhythmTest.IsRunning) return;

        // 곡이 실제로 Running 상태가 된 이후 첫 프레임에서 기준 시간(spawnSongTime) 캡처
        if (!hasCapturedStartTime)
        {
            spawnSongTime = rhythmTest.SongTimePublic;
            hasCapturedStartTime = true;
            return; // 이번 프레임은 기준만 잡고, 카운트는 다음 프레임부터 진행
        }

        double elapsed = rhythmTest.SongTimePublic - spawnSongTime;
        int currentBeat = Mathf.FloorToInt((float)(elapsed / roundTripTime));

        if (currentBeat > beatCount)
        {
            beatCount = currentBeat;
            Debug.Log($"고드름 박자: {beatCount}");
            if(beatCount == 2)
            {
                RaiseMoveAllowed();
            }

            // beat 1~3: 꿈틀거림, beat 4: 낙하
            if (beatCount >= 4)
            {
                StartCoroutine(DropRoutine());
            }
        }
    }

    private IEnumerator DropRoutine()
    {
        isFalling = true;
        sr.sprite = fallingSprite;
        rb.isKinematic = false;
        OnIcicleFalling?.Invoke();
        yield break;
    }
}