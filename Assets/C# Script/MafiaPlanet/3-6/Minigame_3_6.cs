using UnityEngine;

public class Minigame_3_6 : MiniGameBase
{
    protected override float TimerDuration => 10f;
    protected override string MinigameExplain => "빨간불 타이밍에 녹음하세요!";

    // 이 미니게임은 RhythmManager의 입력 타이밍 판정을 쓰지 않는다.
    // CSV의 Input 노드 순간에 버튼이 눌려 있는지만 직접 검사한다.
    protected override bool UseRhythmJudgementScore => false;

    [Header("3-6 References")]
    [SerializeField] private LightController_3_6 lightController;

    public override float perfectWindowOverride => 0.1f;
    public override float goodWindowOverride => 0.25f;
    public override float hitWindowOverride => 0.35f;

    private bool isRecording;
    private bool ended;

    public override void StartGame()
    {
        base.StartGame();

        isRecording = false;
        ended = false;

        if (lightController != null)
            lightController.TurnOffAll();
    }

    // 버튼 PointerDown에서 호출
    public void StartRecording()
    {
        if (ended) return;
        isRecording = true;
        Debug.Log("[3-6] Recording Start");
    }

    // 버튼 PointerUp에서 호출
    public void StopRecording()
    {
        isRecording = false;
        Debug.Log("[3-6] Recording Stop");
    }

    public override void OnRhythmEvent(string action)
    {
        if (ended) return;
        if (string.IsNullOrEmpty(action)) return;

        action = action.Trim();

        switch (action)
        {
            case "ShowGreen":
                lightController?.ShowGreen();
                break;

            case "ShowYellow":
                lightController?.ShowYellow();
                break;

            case "ShowRed":
                lightController?.ShowRed();
                break;

            case "RecordCheck":
                JudgeRecordingNode();
                break;

            case "Off":
                lightController?.TurnOffAll();
                break;

            case "End":
                ended = true;
                isRecording = false;
                lightController?.TurnOffAll();
                break;
        }
    }

    private void JudgeRecordingNode()
    {
        if (isRecording)
        {
            ReportManualSuccess();
           // Debug.Log("[3-6] Manual Success");
        }
        else
        {
            ReportManualFail();
           // Debug.Log("[3-6] Manual Fail");
        }
    }

    private void OnDisable()
    {
        isRecording = false;
    }
}