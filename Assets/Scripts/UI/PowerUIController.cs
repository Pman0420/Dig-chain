using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class PowerUIController : MonoBehaviour
{
    [Header("Character Reaction")]
    [SerializeField] private ChainReactionCharacter chainCharacter;

    [SerializeField] private Slider powerSlider;
    [SerializeField] private float lerpSpeed = 10f;

    // 数値表示用のテキスト（UIのText）
    [SerializeField] private TMP_Text powerText;

    // GameManager 経由で PowerManager を取得
    private PowerManager PM => GameManager.Instance?.Power;

    private void Start()
    {
        if (powerSlider != null)
        {
            powerSlider.minValue = 0;
            powerSlider.value = 0;
        }

        // 初期表示
        if (powerText != null)
        {
            powerText.text = "Power: 0";
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

        // ★ 数値テキスト更新（必要なら LogicalPower 側に変えてもよい）
        if (powerText != null)
        {
            int shown = Mathf.RoundToInt(target);      // 小数いらないなら丸める
            powerText.text = $"Power: {shown}";
        }

        int chainForReaction = (int)target;
        Debug.Log($"[ChainReaction] reactionChain={chainForReaction}");

        if (chainCharacter != null)
        {
            chainCharacter.OnChainResolved(chainForReaction, false);
        }


    }

    // 見た目だけ0にする（ロジックとは無関係）
    public void ResetPowerSlider()
    {
        if (powerSlider != null)
        {
            powerSlider.value = 0;
        }

        if (powerText != null)
        {
            powerText.text = "Power: 0";
        }
    }
}
