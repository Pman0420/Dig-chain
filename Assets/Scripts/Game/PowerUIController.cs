using UnityEngine;
using UnityEngine.UI;

// PowerManager の CurrentPower をそのままスライダーに表示する
public class PowerUIController : MonoBehaviour
{
    [SerializeField]
    private Slider powerSlider;

    [SerializeField]
    private PowerManager powerManager;

    [SerializeField]
    private float lerpSpeed = 10f;  // ゲージの追従速度（見た目用）

    private void Start()
    {
        if (powerSlider != null)
        {
            powerSlider.minValue = 0;
           //powerSlider.maxValue = 100; // 必要なら後で動的に更新
            powerSlider.value = 0;
        }
    }

    private void Update()
    {
        if (powerSlider == null || powerManager == null) return;

        float target = powerManager.CurrentPower;

        // 必要なら最大値を広げる
        if (target > powerSlider.maxValue)
        {
            powerSlider.maxValue = target;
        }

        // ゲージはなめらかに追従（見た目用）
        powerSlider.value = Mathf.Lerp(powerSlider.value, target, Time.deltaTime * lerpSpeed);
    }
}
