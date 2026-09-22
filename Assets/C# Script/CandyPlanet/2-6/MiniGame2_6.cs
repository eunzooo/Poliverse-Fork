using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniGame2_6 : MiniGameBase
{
    protected override float TimerDuration => 5f;
    protected override string MinigameExplain => "좌우로 피해라!";

    //public override float perfectWindowOverride => 0.1f;
    //public override float goodWindowOverride => 0.3f;
    //public override float hitWindowOverride => 0.5f;

    protected override bool UseRhythmJudgementScore => false;
    protected override int ManualTotalNodeCount => -1;

    private bool ended;

    public EnemySpawner2_6 spawner;

    private int hitCount = 0;

    public override void StartGame()
    {
        base.StartGame();

        ended = false;
    }

    public override void OnRhythmEvent(string action)
    {
        if (ended) return;
        if (string.IsNullOrEmpty(action)) return;

        Debug.Log($"{gameObject.name} 리듬메세지: {action}");

        action = action.Trim();

        switch (action)
        {
            case "Show":
                spawner.SpawnObstacle();
                break;
        }
    }


    public void OnPlayerHit()
    {
        if (ended) return;

        ReportManualFail();

        Debug.Log("장애물 충돌!");
    }

    public void OnObstaclePassed()
    {
        if (ended) return;

        ReportManualSuccess();
    }
}
