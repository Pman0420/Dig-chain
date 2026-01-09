using UnityEngine;

public class GameStartUI : MonoBehaviour
{
    [SerializeField] private GameObject root;   // Startパネル(Press Any Key 的なやつ)

    private bool started = false;

    private void Awake()
    {
        SetStarted(false);
        // メニュー中はタイムスケールを0にしているならここで 0f に
        Time.timeScale = 0f;
    }

    private void SetStarted(bool value)
    {
        started = value;

        if (root != null)
        {
            root.SetActive(!value);   // started==true なら非表示
        }
    }

    private void Update()
    {
        if (started) return;

        if (Input.anyKeyDown)
        {
            SetStarted(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GameStart();
            }
        }
    }
}
