using TMPro;
using UnityEngine;

public class ScoreUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;

    private PowerManager PM => GameManager.Instance?.Power;

    private void Update()
    {
        if (scoreText == null || PM == null) return;

        scoreText.text = $"SCORE : {PM.TotalScore}";
    }
}
