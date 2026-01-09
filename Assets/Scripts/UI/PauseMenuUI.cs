using UnityEngine;
using UnityEngine.SceneManagement;

/// Escキーで開く一時停止メニュー
public class PauseMenuUI : MonoBehaviour
{
    [Header("UI ルート")]
    [SerializeField] private GameObject root;   // パネル全体（メニューの親）

    [Header("タイトルシーン名")]
    [SerializeField] private string titleSceneName = "Title"; // 実際のタイトルシーン名に変更

    private bool isOpen = false;

    private void Awake()
    {
        // 最初は非表示
        if (root != null)
        {
            root.SetActive(false);
        }
        Time.timeScale = 1f;   // 念のため通常速度に戻しておく
    }

    private void Update()
    {
        // Esc キーでメニューの開閉をトグル
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ゲームオーバー中はメニューを開かない（好みで外してもよい）
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
            {
                return;
            }

            if (isOpen)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }
    }

    public void Show()
    {
        isOpen = true;

        if (root != null)
        {
            root.SetActive(true);
        }

        // 一時停止
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        isOpen = false;

        if (root != null)
        {
            root.SetActive(false);
        }

        // 再開
        Time.timeScale = 1f;
    }

    // 「再開」ボタン（欲しければ）
    public void OnClickResume()
    {
        Hide();
    }

    // 「リスタート」ボタンから呼ぶ
    public void OnClickRestart()
    {
        // シーンを再読み込みしてゲームを最初から
        Time.timeScale = 1f;   // 念のため戻す
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    // 「タイトルへ」ボタンから呼ぶ
    public void OnClickReturnToTitle()
    {
        Debug.Log("[PauseMenuUI] Main Menu ボタン押された");  // デバッグ用
        Time.timeScale = 1f;
        SceneManager.LoadScene("TitleScene");  // ← 実際のタイトルシーン名に合わせて
    }
}
