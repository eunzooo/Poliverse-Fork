using UnityEngine;

public class LightController_3_6 : MonoBehaviour
{
    [System.Serializable]
    private class LightVisual
    {
        public GameObject root;                 // 항상 활성 상태로 두는 오브젝트 (SetActive 대상 아님)
        public SpriteRenderer spriteRenderer;    // 스프라이트를 바꿀 SpriteRenderer
        public Sprite onSprite;                  // 예: 3_6_GreenOn
        public Sprite offSprite;                 // 예: 3_6_GreenOff
        public GameObject childToActivate;       // 켜질 때 true, 꺼질 때 false

        public void TurnOn()
        {
            if (spriteRenderer != null && onSprite != null) spriteRenderer.sprite = onSprite;
            if (childToActivate != null) childToActivate.SetActive(true);
        }

        public void TurnOff()
        {
            if (spriteRenderer != null && offSprite != null) spriteRenderer.sprite = offSprite;
            if (childToActivate != null) childToActivate.SetActive(false);
        }
    }

    [Header("Lights")]
    [SerializeField] private LightVisual greenLight;
    [SerializeField] private LightVisual yellowLight;
    [SerializeField] private LightVisual redLight;

    public void ShowGreen()
    {
        TurnOffAll();
        greenLight?.TurnOn();
    }

    public void ShowYellow()
    {
        TurnOffAll();
        yellowLight?.TurnOn();
    }

    public void ShowRed()
    {
        TurnOffAll();
        redLight?.TurnOn();
    }

    public void TurnOffAll()
    {
        greenLight?.TurnOff();
        yellowLight?.TurnOff();
        redLight?.TurnOff();
    }
}