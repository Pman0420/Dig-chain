using UnityEngine;

public class BoardController : MonoBehaviour
{
    public int height = 6;
    public int width = 6;

    public BoardView view;

    public DigChainCore core { get; private set; }

    void Start()
    {
        core = new DigChainCore(height, width);

        int[,] init =
        {
            {1,2,3,4,1,2},
            {1,1,1,4,2,3},
            {2,3,3,1,2,4},
            {1,2,2,4,1,1},
            {4,3,2,2,4,2},
            {4,3,3,4,1,1}
        };

        for (int y = 0; y < core.H; y++)
        {
            for (int x = 0; x < core.W; x++)
            {
                core.grid[y, x] = init[y, x];
            }
        }

        if (view != null)
        {
            view.SetCore(core);   // ここで初期盤面を描画
        }

        Debug.Log("初期盤面を表示しました。マウスクリックで掘る処理をこれから追加します。");
    }

    // ここに後でクリック処理を足す
    void Update()
    {
        // 左クリックした瞬間だけ処理する
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("クリック検知");

            if (core == null || view == null)
            {
                Debug.LogWarning("core または view が null");
                return;
            }

            Vector3 mouseScreenPos = Input.mousePosition;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("Camera.main が見つかりません");
                return;
            }

            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(
                new Vector3(mouseScreenPos.x, mouseScreenPos.y, -cam.transform.position.z)
            );

            Vector3 localPos = view.transform.InverseTransformPoint(mouseWorldPos);

            float cell = view.cellSize;
            int gridX = Mathf.FloorToInt(localPos.x / cell + 0.0001f);
            int gridY = Mathf.FloorToInt(-localPos.y / cell + 0.0001f);

            Debug.Log($"mouseScreen={mouseScreenPos}, world={mouseWorldPos}, local={localPos}, grid=({gridX},{gridY})");

            if (gridX < 0 || gridX >= core.W || gridY < 0 || gridY >= core.H)
            {
                Debug.Log("盤面外クリック: (" + gridX + "," + gridY + ")");
                return;
            }

            Debug.Log($"マスを掘る: ({gridX},{gridY}), そのマスの値={core.grid[gridY, gridX]}");

            // ★テスト1: 直接そのマスを空にしてみる
            //   これで見た目が変わるかどうかをまず確認
            //core.grid[gridY, gridX] = 0;
            //view.Redraw();
            //return;

            // ★本来の処理
            int chain = core.DigAndChain(gridY, gridX);
            view.Redraw();

            Debug.Log("連鎖回数 = " + chain + " / Power = " + core.power);
        }
    }
}


