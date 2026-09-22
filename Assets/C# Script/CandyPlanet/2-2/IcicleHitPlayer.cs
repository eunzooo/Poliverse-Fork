using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class IcicleHitPlayer : MonoBehaviour
{
    public PlayerMoveByClick playerMove;

    private void OnTriggerEnter2D(Collider2D coll)
    {
        if (!coll.CompareTag("Icicle")) return;

        // 부딪힌 고드름이 속한 미니게임 인스턴스를 그 자리에서 찾음
        MiniGame2_2 minigame_2_2 = coll.GetComponentInParent<MiniGame2_2>();
        if (minigame_2_2 == null) return;

        Debug.Log("충돌 감지 성공!");
        minigame_2_2.missCount++;
        minigame_2_2.CheckGameResult();

        var move = GetComponent<PlayerMoveByClick>();
        if (move != null) move.ForceMove();
    }


    // 제한시간 추가
}