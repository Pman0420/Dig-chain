using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsGameOver { get; private set; } = false;
    public bool IsGameStarted { get; private set; } = false;
    [SerializeField] private PowerManager powerManager;
    public PowerManager Power => powerManager;
    [SerializeField] private GameOverUI gameOverUI;

    [SerializeField] private AudioSource bgmSource;  // BGM用の AudioSource
    [SerializeField] private AudioSource overbgmSource;  // GAME OVER BGM用の AudioSource
    //best score save
    private const string BEST_SCORE_KEY = "BEST_SCORE";
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public int GetBestScore()
    {
        return PlayerPrefs.GetInt(BEST_SCORE_KEY, 0);
    }

    public bool TryUpdateBestScore(int score)
    {
        int best = GetBestScore();
        if (score <= best) return false;

        PlayerPrefs.SetInt(BEST_SCORE_KEY, score);
        PlayerPrefs.Save(); // 念のため即保存
        return true;
    }

    public void GameStart()
    {
        if (IsGameStarted) return;

        IsGameStarted = true;
        Debug.Log("[GameManager] GAME START!");
        bgmSource.UnPause();
        overbgmSource.Pause();
        // 念のためタイムスケールも戻しておく
        Time.timeScale = 1f;
    }

    public void GameOver()
    {
        if (IsGameOver) return;

        IsGameOver = true;
        int finalScore = (Power != null) ? Power.TotalScore : 0;
        bool updated = TryUpdateBestScore(finalScore);
        Debug.Log($"[GameManager] FinalScore={finalScore}, BestScore={GetBestScore()}, Updated={updated}");
        bgmSource.Pause();
        overbgmSource.UnPause();
        if (gameOverUI != null)
        {
            gameOverUI.Show();
        }
    }
}

