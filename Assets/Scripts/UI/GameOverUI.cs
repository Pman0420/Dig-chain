using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject root;             // パネル全体（背景含む）
    [SerializeField] private TextMeshProUGUI messageText; // "GAME OVER" 表示
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI bestScoreText; // best
    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "TitleScene"; // タイトルシーン名に合わせる

    private void Awake()
    {
        Debug.Log("[GameOverUI] Awake");
        if (root != null) root.SetActive(false);
    }

    public void Show()
    {
        if (root != null) root.SetActive(true);

        if (messageText != null)
        {
            messageText.text = "GAME OVER";
        }
        var pm = GameManager.Instance != null ? GameManager.Instance.Power : null;
        int score = (pm != null) ? pm.TotalScore : 0;

        if (finalScoreText != null)
        {
            finalScoreText.text = $"FINAL SCORE : {score}";
        }

        int best = (GameManager.Instance != null) ? GameManager.Instance.GetBestScore() : 0;

        if (bestScoreText != null)
        {
            bestScoreText.text = $"BEST SCORE : {best}";
        }

        // ゲームオーバー時は止めたい場合（任意）
        Time.timeScale = 0f;

    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        Time.timeScale = 1f;
    }

    // Retry ボタンから呼ぶ（今のシーンをリロード）
    public void OnClickRetry()
    {
        Debug.Log("[GameOverUI] Retry clicked");
        Time.timeScale = 1f;

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    // Title ボタンから呼ぶ（タイトルへ）
    public void OnClickReturnToTitle()
    {
        Debug.Log("[GameOverUI] ReturnToTitle clicked");
        Time.timeScale = 1f;

        SceneManager.LoadScene(titleSceneName);
    }
}
