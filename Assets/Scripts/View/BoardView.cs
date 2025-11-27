using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 盤面の見た目（ブロックの生成・位置・落下アニメ）を担当するクラス
public class BoardView : MonoBehaviour
{
    [SerializeField]
    private GameObject blockPrefab;    // 1マス分のブロック見た目

    [SerializeField]
    public float cellSize = 1f;        // 1マスの大きさ（ワールド単位）

    [SerializeField]
    private float fallTime = 0.15f;    // 1回の落下にかける時間

    [SerializeField]
    private float crushDelay = 0.05f;  // 消去してから落下を始めるまでの待ち時間

    [SerializeField]
    private float chainPause = 0.05f;  // 連鎖ステップ間の間

    private DigChainCore core;         // ロジック本体

    // PowerManager は GameManager から取る
    private PowerManager PM => GameManager.Instance?.Power;



    // 現在の見た目のブロックを管理するテーブル
    // blocks[y, x] が、そのマスを表現している GameObject（なければ null）
    private GameObject[,] blocks;


    // === 外部からコアをセットする ===
    public void SetCore(DigChainCore c)
    {
        core = c;
        blocks = new GameObject[core.H, core.W];

        // 初回に盤面を全部生成
        RebuildAllBlocks();
    }

    // === デバッグ用／強制再描画 ===
    public void Redraw()
    {
        RebuildAllBlocks();
    }

    // ブロックの値ごとに色を決める
    private Color GetColorForValue(int v)
    {
        // 好きなように変えてOK。例として 1〜4 を固定色にする。
        switch (v)
        {
            case 1: return Color.red;
            case 2: return Color.blue;
            case 3: return Color.green;
            case 4: return Color.yellow;
            default: return Color.white;
        }
    }


    // 盤面全体を作り直す（今の core.grid をそのまま描画）
    private void RebuildAllBlocks()
    {
        if (core == null) return;

        // 既存のブロックを全消し
        if (blocks != null)
        {
            for (int y = 0; y < core.H; y++)
            {
                for (int x = 0; x < core.W; x++)
                {
                    if (blocks[y, x] != null)
                    {
                        Destroy(blocks[y, x]);
                        blocks[y, x] = null;
                    }
                }
            }
        }

        // 新しく作り直す
        for (int y = 0; y < core.H; y++)
        {
            for (int x = 0; x < core.W; x++)
            {
                int v = core.grid[y, x];
                if (v == 0) continue;

                GameObject go = Instantiate(blockPrefab, transform);
                go.transform.localPosition = CellToLocalPos(y, x);

                // ★ ここで色を決める
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = GetColorForValue(v);
                }

                blocks[y, x] = go;
            }
        }

    }

    // マス(y,x) → Boardローカル座標への変換
    private Vector3 CellToLocalPos(int y, int x)
    {
        // 左上が (0,0) で、右方向に +x、下方向に +y になるように
        return new Vector3(x * cellSize, -y * cellSize, 0f);
    }

    private bool InBounds(int y, int x)
    {
        return (core != null &&
                0 <= y && y < core.H &&
                0 <= x && x < core.W);
    }

    // === 掘削＋連鎖の結果（DigChainResult）を受け取り、アニメーション再生を開始する ===
    public void PlayDigChainAnimation(DigChainResult result, int oldPower, int newPower)
    {
        if (PM != null && result.totalCrushed > 0)
        {
            Debug.Log($"BoardView: BeginGain old={oldPower}, new={newPower}, crushed={result.totalCrushed}");
            PM.BeginGain(oldPower, newPower, result.totalCrushed);
        }
        else
        {
            Debug.Log($"BoardView: BeginGain 未実行 PM={PM != null}, crushed={result.totalCrushed}");
        }

        StartCoroutine(CoPlayDigChainAnimation(result));
    }


    // 掘削＋連鎖の各ステップに従って、
    // 1) 削除されたブロックを破壊し、
    // 2) 落ちたブロックを from→to にスムーズに動かす
    private IEnumerator CoPlayDigChainAnimation(DigChainResult result)
    {
        if (result.steps == null || result.steps.Count == 0)
            yield break;

        // result.steps[0] : 掘削ステップ
        // result.steps[1]〜 : 連鎖ステップ
        for (int stepIndex = 0; stepIndex < result.steps.Count; stepIndex++)
        {
            ChainStep step = result.steps[stepIndex];

            // --- 1. このステップで消えたブロックを消す ---
            if (step.crushedBlocks != null)
            {
                foreach (Pos p in step.crushedBlocks)
                {
                    if (!InBounds(p.y, p.x)) continue;
                    if (blocks[p.y, p.x] != null)
                    {
                        Destroy(blocks[p.y, p.x]);
                        blocks[p.y, p.x] = null;
                    }

                    // ★PowerManagerに「1ブロック消えた」ことを通知
                    if (PM != null)
                    {
                        PM.OnBlockCrushed();
                    }

                }
            }

            // 少し間をおく（演出）
            if (crushDelay > 0f)
                yield return new WaitForSeconds(crushDelay);

            // --- 2. 落ちるブロックをアニメーションさせる ---
            if (step.fallInfos != null && step.fallInfos.Count > 0)
            {
                // どの GameObject がどこからどこへ動くかをまとめる
                var animList = new List<(GameObject go, Vector3 fromPos, Vector3 toPos)>();

                foreach (FallInfo fi in step.fallInfos)
                {
                    if (!InBounds(fi.fromY, fi.fromX)) continue;
                    if (!InBounds(fi.toY, fi.toX)) continue;

                    GameObject go = blocks[fi.fromY, fi.fromX];
                    if (go == null) continue; // 何かの理由で存在しなければスキップ

                    Vector3 fromPos = CellToLocalPos(fi.fromY, fi.fromX);
                    Vector3 toPos = CellToLocalPos(fi.toY, fi.toX);

                    animList.Add((go, fromPos, toPos));

                    // 論理上の位置テーブルだけ先に更新しておく
                    blocks[fi.toY, fi.toX] = go;
                    blocks[fi.fromY, fi.fromX] = null;
                }

                // 実際の位置は fallTime かけて補間
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / fallTime;
                    float tt = Mathf.Clamp01(t);

                    foreach (var a in animList)
                    {
                        a.go.transform.localPosition = Vector3.Lerp(a.fromPos, a.toPos, tt);
                    }

                    yield return null;
                }

                // 最終位置にピタッとスナップ
                foreach (var a in animList)
                {
                    a.go.transform.localPosition = a.toPos;
                }
            }

            // 連鎖ステップ間のポーズ
            if (chainPause > 0f)
                yield return new WaitForSeconds(chainPause);
        }
    }
}
