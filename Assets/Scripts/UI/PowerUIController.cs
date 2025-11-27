using UnityEngine;
using UnityEngine.UI;

public class PowerUIController : MonoBehaviour
{
    [SerializeField] private Slider powerSlider;
    [SerializeField] private float lerpSpeed = 10f;

    // GameManager 経由で PowerManager を取得
    private PowerManager PM => GameManager.Instance?.Power;

    private void Start()
    {
        if (powerSlider != null)
        {
            powerSlider.minValue = 0;
            powerSlider.value = 0;
        }
    }

    private void Update()
    {
        if (powerSlider == null)
        {
            Debug.LogError("PowerUI: powerSlider が Inspector で未設定です");
            return;
        }

        if (PM == null)
        {
            Debug.LogError("PowerUI: PowerManager(PM) が null です。GameManager の設定を確認してください。");
            return;
        }
        // ここまで来ていれば PM は生きている
        Debug.Log($"PowerUI: Logical={PM.LogicalPower}, Current={PM.CurrentPower}");

        float target = PM.CurrentPower;

        // 必要なら最大値を広げる
        if (target > powerSlider.maxValue)
        {
            powerSlider.maxValue = target;
        }

        // ゲージはなめらかに追従
        powerSlider.value = Mathf.Lerp(
            powerSlider.value,
            target,
            Time.deltaTime * lerpSpeed
        );
    }

    // 見た目だけ0にする（ロジックとは無関係）
    public void ResetPowerSlider()
    {
        if (powerSlider != null)
        {
            powerSlider.value = 0;
        }
    }
}
