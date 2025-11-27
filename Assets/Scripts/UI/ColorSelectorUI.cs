using UnityEngine;
using UnityEngine.UI;

// 「今掘れる色」「次」「次の次」を UI に表示するスクリプト
public class ColorSelectorUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private BoardController board;   // GameRoot に付いている BoardController

    [Header("色表示用 Image")]
    [SerializeField] private Image currentColorImage;
    [SerializeField] private Image nextColor1Image;
    [SerializeField] private Image nextColor2Image;

    [Header("ブロックID -> 表示色の対応表")]
    // index = ブロックID (0 は空なので未使用)
    [SerializeField] private Color[] blockColors;

    // パワーUI
    [SerializeField] private PowerUIController powerUIController;   // PowerUIController を直接参照

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        // 参照チェック
        if (board == null || board.core == null || board.core.colorSelector == null)
        {
            // デバッグ用ログ（邪魔なら消してOK）
            // Debug.Log("[ColorSelectorUI] board/core/colorSelector が設定されていません");
            return;
        }

        var cs = board.core.colorSelector;

        // ここで現在の色をログに出す（ちゃんと動いているか確認用）
        Debug.Log($"[ColorSelectorUI] now={cs.currentColor}, next1={cs.nextColor1}, next2={cs.nextColor2}");

        // 現在色
        if (currentColorImage != null)
        {
            currentColorImage.color = GetColorById(cs.currentColor);
        }

        // 次
        if (nextColor1Image != null)
        {
            nextColor1Image.color = GetColorById(cs.nextColor1);
        }

        // 次の次
        if (nextColor2Image != null)
        {
            nextColor2Image.color = GetColorById(cs.nextColor2);
        }
    }

    // ブロックID → Color に変換
    private Color GetColorById(int id)
    {
        // 配列が無い or 範囲外 or 0以下 → 白で表示
        if (blockColors == null || blockColors.Length == 0) return Color.white;
        if (id <= 0 || id >= blockColors.Length) return Color.white;

        return blockColors[id];
    }
    // 掘れない色の処理
    public void HandleInvalidDig()
    {
        // パワーをリセット
        if (powerUIController != null)
        {
            powerUIController.ResetPowerSlider();  // ゲージを0に
        }

        // 色を進める
        if (board != null && board.core != null && board.core.colorSelector != null)
        {
            board.core.colorSelector.ShiftColors();  // 次の色に進める
        }

        Debug.Log("掘れない色なので、次の色に進みます。");
    }
}
