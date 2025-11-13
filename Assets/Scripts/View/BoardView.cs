using System.Collections.Generic;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    public GameObject blockPrefab;  // BlockSprite.prefab を割り当てる
    public float cellSize = 1.0f;   // 1マスの大きさ（Unity上の1 = 1m）

    private DigChainCore core;
    private List<GameObject> blocks = new List<GameObject>();

    // BoardController からコアを受け取る
    public void SetCore(DigChainCore core)
    {
        this.core = core;
        Redraw();
    }

    // 盤面を全描画し直す
    public void Redraw()
    {
        foreach (var b in blocks)
        {
            Destroy(b);
        }
        blocks.Clear();

        if (core == null)
        {
            Debug.LogWarning("BoardView: core が null です");
            return;
        }

        int created = 0;

        for (int y = 0; y < core.H; y++)
        {
            for (int x = 0; x < core.W; x++)
            {
                int colorId = core.grid[y, x];
                if (colorId == 0) continue;

                var obj = Instantiate(blockPrefab, transform);
                obj.transform.localPosition = new Vector3(
                    x * cellSize,
                    -y * cellSize,
                    0
                );

                var sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = GetColorById(colorId);
                }
                else
                {
                    Debug.LogError("BlockPrefab に SpriteRenderer が付いていません");
                }

                blocks.Add(obj);
                created++;
            }
        }

        Debug.Log("BoardView: Redraw で " + created + " 個のブロックを生成しました");
    }

    // 簡易カラー設定：IDごとに色を決める
    private Color GetColorById(int id)
    {
        switch (id)
        {
            case 1: return Color.red;
            case 2: return Color.green;
            case 3: return Color.blue;
            case 4: return Color.yellow;
            default: return Color.white;
        }
    }
}
