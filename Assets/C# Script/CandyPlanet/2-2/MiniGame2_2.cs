using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniGame2_2 : MiniGameBase
{
    // 판정 범위 오버라이드
    public override float perfectWindowOverride => 0.15f;
    public override float goodWindowOverride => 0.5f;
    public override float hitWindowOverride => 1f;
    protected override float TimerDuration => 5f;
    protected override string MinigameExplain => "피해라!";

    private bool ended;
    public int missCount = 0;
    [SerializeField] SpawnIcicle spawnIcicle;
  
    public override void StartGame()
    {
        base.StartGame();
        ended = false;
        missCount = 0;
        // 추가 초기화
        // 예: instructionText.text = MinigameExplain;
    }

    public void Succeed()
    {
        ended = true;
        Success();
    }
    public void Failure()
    {
        ended = true;
        Fail();
    }

    public override void OnRhythmEvent(string action)
    {
        if (ended) return;
        Debug.Log($"{gameObject.name} 리듬메세지: {action}");
        action = action.Trim();
        if (action == "PatternStart")
        {
            spawnIcicle.SpawnNext();
        }
    }
    public override void OnPlayerInput(string action = null)
    {
        // 입력 잠금 상태면 무시
        if (IsInputLocked) return;
        base.OnPlayerInput(action);
    }

    public override void OnJudgement(JudgementResult judgement)
    {
        if (ended) return;
        base.OnJudgement(judgement);

        if (judgement == JudgementResult.Miss)
        {
            missCount++;
            CheckGameResult();
        }
    }


    // 외부에서 호출 가능하도록 실패/성공 로직 캡슐화
    public void ForceMiss() => OnJudgement(JudgementResult.Miss);

    public void CheckGameResult()
    {
        if (IsInputLocked || ended) return;
        ended = true;
        // 모두 Miss 3번 이상 실패
        if (missCount >= 3)
        {
            Debug.Log("실패");
           // Failure();
        }
    }
}
