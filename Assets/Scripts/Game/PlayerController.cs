using UnityEngine;

// プレイヤーの横移動 + 足元掘りを担当
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 5f;          // 左右移動の速さ

    [SerializeField]
    private BoardController board;         // 掘削ロジックを持っている BoardController

    private void Update()
    {
        HandleMove();
        HandleDigInput();
    }

    // 左右移動
    private void HandleMove()
    {
        float h = Input.GetAxisRaw("Horizontal"); // -1,0,1
        Vector3 dir = new Vector3(h, 0f, 0f);
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    // 掘るボタン入力
    private void HandleDigInput()
    {
        // Space か J を押した瞬間
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J))
        {
            DigAtFeet();
        }
    }

    /// <summary>
    /// プレイヤーの「足元」に一番近いマスを掘る
    /// 足元 = 現在のY座標から 1マス分（cellSize）下の位置として扱う。
    /// </summary>
    private void DigAtFeet()
    {
        if (board == null || board.core == null || board.view == null)
        {
            Debug.LogWarning("PlayerController: board / core / view が設定されていません");
            return;
        }

        float cell = board.view.cellSize;

        // ① プレイヤーのワールド座標
        Vector3 worldPos = transform.position;

        // ② Board を基準にしたローカル座標に変換
        Vector3 localPos = board.view.transform.InverseTransformPoint(worldPos);

        // ③ 足元のローカルY座標（1マス分だけ下 = 今のY - cell）
        float feetLocalY = localPos.y - cell;

        // ④ ローカル座標 → マス座標（Xは近い列、Yは足元の少し下の行）
        int gridX = Mathf.RoundToInt(localPos.x / cell);
        int gridY = Mathf.RoundToInt(-feetLocalY / cell);

        // ⑤ 範囲チェック
        if (gridX < 0 || gridX >= board.core.W || gridY < 0 || gridY >= board.core.H)
        {
            Debug.Log($"Player Dig: 盤面外 grid=({gridX},{gridY}), localFeetY={feetLocalY}");
            return;
        }

        Debug.Log($"Player Dig: ターゲットマス=({gridX},{gridY}), 値={board.core.grid[gridY, gridX]}");

        // ⑥ 掘削＋連鎖
        int chain = board.core.DigAndChain(gridY, gridX);

        // ⑦ 盤面更新
        board.view.Redraw();

        Debug.Log($"Player Dig: 連鎖回数 = {chain}, Power = {board.core.power}");
    }
}
