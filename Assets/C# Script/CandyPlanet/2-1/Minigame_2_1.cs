using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Minigame_2_1 : MiniGameBase
{
    // ���� ���� �������̵�
    public override float perfectWindowOverride => 0.15f;
    public override float goodWindowOverride => 0.5f;
    public override float hitWindowOverride => 1f;

    protected override float TimerDuration => 5f;
    protected override string MinigameExplain => "�����!";

    private bool ended;
    private int missCount = 0;
    private int totalCount = 2;

    private DropCake dropCake;
    private PlayerSrChange srChange;

    //���ھ�
    private int score;
    [SerializeField] private int missAmount;
    [SerializeField] private int goodAmount;
    [SerializeField] private int perfectAmount;

    [SerializeField] private float duration;

    public override void StartGame()
    {
        base.StartGame();
        dropCake = GetComponent<DropCake>();
        srChange = GetComponentInChildren<PlayerSrChange>(true);
        ended = false;
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
        action = action.Trim();
        if (action == "Input")
        {
            dropCake.MoveDownAndBack(duration);
        }
    }
    public override void OnPlayerInput(string action = null)
    {
        // �Է� ��� ���¸� ����
        if (IsInputLocked) return;
        base.OnPlayerInput(action);
    }

    public override void OnJudgement(JudgementResult judgement)
    {
        if (IsInputLocked || ended) return;

        base.OnJudgement(judgement);

        if (judgement == JudgementResult.Miss)
        {
            srChange.ChangeSpriteTemporarily();
            missCount++;
        }
    }
    public void CheckGameResult()
    {
        if (IsInputLocked || ended) return;
        ended = true;
        
    }

}