using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";  // 実際のゲームシーン名に合わせる

    // 「スタート」ボタン用
    public void OnClickStart()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // 「終了」ボタン用（任意）
    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
