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

    [SerializeField]
    private float riseTime = 0.15f; // せり上げ1マスぶんの演出時間

    [SerializeField] private AudioClip blockDestroySound;  // 破壊音
    private AudioSource audioSource;

    [Header("破壊アニメ")]
    [SerializeField] private float breakDuration = 0.12f;   // 破壊演出の長さ
    [SerializeField] private float breakPopScale = 1.25f;   // 一瞬拡大する倍率
    [SerializeField] private int breakSortingOrderOffset = 10; // 破壊演出を手前に描く

    // ★ 追加：値(v)に応じたSprite配列（value=1 -> index0, value=2 -> index1 ...）
    [Header("Block Sprites (value=1..n)")]
    [SerializeField]
    private Sprite[] blockSprites;

    private DigChainCore core;         // ロジック本体

    // PowerManager は GameManager から取る
    private PowerManager PM => GameManager.Instance?.Power;

    // 現在の見た目のブロックを管理するテーブル
    // blocks[y, x] が、そのマスを表現している GameObject（なければ null）
    private GameObject[,] blocks;


    private void Start()
    {
        // AudioSourceの初期化
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();  // もしAudioSourceがアタッチされていなければ追加
        }
    }

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

    // ブロックの値ごとに色を決める（互換のため残す）
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

    // ★ 追加：ブロックの値ごとにSpriteを返す（未設定なら null）
    private Sprite GetSpriteForValue(int v)
    {
        if (v <= 0) return null;
        if (blockSprites == null) return null;

        int idx = v - 1;
        if (idx < 0 || idx >= blockSprites.Length) return null;

        return blockSprites[idx];
    }

    // ★ 追加：SpriteRenderer に「Sprite優先 / 無ければ色」の適用をまとめる
    private void ApplyVisual(SpriteRenderer sr, int v)
    {
        if (sr == null) return;

        Sprite sp = GetSpriteForValue(v);
        if (sp != null)
        {
            sr.sprite = sp;
            sr.color = Color.white; // Sprite表示を色で汚さない
        }
        else
        {
            // フォールバック（Sprite未設定でも動く）
            sr.color = GetColorForValue(v);
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

                // ★ ここでSprite（無ければ色）を適用
                var sr = go.GetComponent<SpriteRenderer>();
                ApplyVisual(sr, v);

                blocks[y, x] = go;
            }
        }
    }

    // マス (y,x) → Board ローカル座標（中心）
    public Vector3 CellToLocalPos(int y, int x)
    {
        // 中心を (x+0.5, y+0.5) にする
        return new Vector3(
            (x + 0.5f) * cellSize,
            -(y + 0.5f) * cellSize,
            0f
        );
    }

    // Board ローカル座標 → マス (y,x)
    public bool LocalToCell(Vector3 localPos, out int y, out int x)
    {
        x = Mathf.FloorToInt(localPos.x / cellSize);
        y = Mathf.FloorToInt(-localPos.y / cellSize);

        if (core == null)
        {
            return false;
        }

        if (y < 0 || y >= core.H || x < 0 || x >= core.W)
        {
            return false;
        }
        return true;
    }

    // ワールド座標 → マス (y,x)
    public bool WorldToCell(Vector3 worldPos, out int y, out int x)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        return LocalToCell(local, out y, out x);
    }

    public bool LocalToCellNearest(Vector3 localPos, out int y, out int x)
    {
        // center: localX = (x+0.5)*cell
        // → x = round(localX/cell - 0.5)
        float gx = localPos.x / cellSize - 0.5f;
        float gy = -localPos.y / cellSize - 0.5f;

        x = Mathf.RoundToInt(gx);
        y = Mathf.RoundToInt(gy);

        if (core == null) return false;
        if (y < 0 || y >= core.H || x < 0 || x >= core.W) return false;
        return true;
    }

    // ……既存の SetCore, RebuildAllBlocks, CoPlayDigChainAnimation など……
    public bool WorldToCellNearest(Vector3 worldPos, out int y, out int x)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        return LocalToCellNearest(local, out y, out x);
    }

    private bool InBounds(int y, int x)
    {
        return (core != null &&
                0 <= y && y < core.H &&
                0 <= x && x < core.W);
    }

    // === 掘削＋連鎖の結果（DigChainResult）を受け取り、アニメーション再生を開始する ===
    public void PlayDigChainAnimation(
        DigChainResult result,
        int oldPower,
        int newPower,
        System.Action onComplete)
    {
        var pm = GameManager.Instance?.Power;
        if (pm != null && result.totalCrushed > 0)
        {
            pm.BeginGain(oldPower, newPower, result.totalCrushed);
        }

        StartCoroutine(CoPlayDigChainAnimation(result, onComplete));
    }

    // 掘削＋連鎖の各ステップに従って、
    // 1) 削除されたブロックを破壊し、
    // 2) 落ちたブロックを from→to にスムーズに動かす
    private IEnumerator CoPlayDigChainAnimation(DigChainResult result, System.Action onComplete)
    {
        if (result.steps == null || result.steps.Count == 0)
        {
            Debug.Log("[Anim] DigChain: steps が空 → 即完了");
            onComplete?.Invoke();
            yield break;
        }

        Debug.Log("[Anim] DigChain: 開始");

        // result.steps[0] : 掘削ステップ
        // result.steps[1]〜 : 連鎖ステップ
        for (int stepIndex = 0; stepIndex < result.steps.Count; stepIndex++)
        {
            ChainStep step = result.steps[stepIndex];
            //破壊サウンド
            PlayBlockDestroySound();
            // --- 1. このステップで消えたブロックを消す ---
            if (step.crushedBlocks != null)
            {
                foreach (Pos p in step.crushedBlocks)
                {
                    if (!InBounds(p.y, p.x)) continue;
                    if (blocks[p.y, p.x] != null)
                    {
                        GameObject go = blocks[p.y, p.x];
                        if (go != null)
                        {
                            // 破壊演出（見た目だけ）を先に再生
                            PlayBreakEffect(go);

                            
                            // 本体は消す（連鎖ロジックと干渉させない）
                            Destroy(go);
                            blocks[p.y, p.x] = null;
                        }
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
                var animList = new List<(GameObject go, Vector3 fromPos, Vector3 toPos)>();

                foreach (FallInfo fi in step.fallInfos)
                {
                    if (!InBounds(fi.fromY, fi.fromX)) continue;
                    if (!InBounds(fi.toY, fi.toX)) continue;

                    GameObject go = blocks[fi.fromY, fi.fromX];

                    // ★ 新しく置かれたブロックなど、「見た目がまだ無い」場合はここで作る
                    if (go == null)
                    {
                        // to マスにある値を参照
                        int v = core.grid[fi.toY, fi.toX];
                        if (v == 0)
                        {
                            continue;
                        }

                        go = Instantiate(blockPrefab, transform);
                        go.transform.localPosition = CellToLocalPos(fi.fromY, fi.fromX);

                        var sr2 = go.GetComponent<SpriteRenderer>();
                        ApplyVisual(sr2, v);

                        // from の位置に一旦登録（これが落ち始めの場所）
                        blocks[fi.fromY, fi.fromX] = go;
                    }

                    Vector3 fromPos = CellToLocalPos(fi.fromY, fi.fromX);
                    Vector3 toPos = CellToLocalPos(fi.toY, fi.toX);

                    animList.Add((go, fromPos, toPos));

                    // 論理位置テーブルだけ先に更新
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

                // 最終位置にピタッ
                foreach (var a in animList)
                {
                    a.go.transform.localPosition = a.toPos;
                }
            }

            // 連鎖ステップ間のポーズ
            if (chainPause > 0f)
                yield return new WaitForSeconds(chainPause);
        }

        Debug.Log("[Anim] DigChain: 完了 → onComplete 呼び出し");
        onComplete?.Invoke();
    }

    public void EnsureBlockVisualAt(int y, int x)
    {
        if (core == null) return;
        if (!InBounds(y, x)) return;

        int v = core.grid[y, x];
        if (v == 0) return;               // 空マスなら何もしない
        if (blocks[y, x] != null) return; // すでに見た目があるなら何もしない

        GameObject go = Instantiate(blockPrefab, transform);
        go.transform.localPosition = CellToLocalPos(y, x);

        var sr = go.GetComponent<SpriteRenderer>();
        ApplyVisual(sr, v);

        blocks[y, x] = go;
    }

    public void PlayRiseAnimation(List<FallInfo> moved, System.Action onComplete)
    {
        StartCoroutine(CoRiseAnimation(moved, onComplete));
    }

    private IEnumerator CoRiseAnimation(List<FallInfo> moved, System.Action onComplete)
    {
        if (moved == null || moved.Count == 0)
        {
            Debug.Log("[Anim] Rise: moved が空 → 即完了");
            onComplete?.Invoke();
            yield break;
        }

        Debug.Log("[Anim] Rise: 開始");

        // どのブロックがどこからどこへ移動するか
        var animList = new List<(GameObject go, Vector3 fromPos, Vector3 toPos)>();

        // まずは blocks テーブルを更新しつつ、移動情報を集める
        foreach (var fi in moved)
        {
            if (!InBounds(fi.fromY, fi.fromX)) continue;
            if (!InBounds(fi.toY, fi.toX)) continue;

            GameObject go = blocks[fi.fromY, fi.fromX];
            if (go == null) continue;

            Vector3 fromPos = CellToLocalPos(fi.fromY, fi.fromX);
            Vector3 toPos = CellToLocalPos(fi.toY, fi.toX);

            animList.Add((go, fromPos, toPos));

            // 論理上のテーブルを更新
            blocks[fi.toY, fi.toX] = go;
            blocks[fi.fromY, fi.fromX] = null;
        }

        // 実際の座標アニメーション
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / riseTime;
            float tt = Mathf.Clamp01(t);

            foreach (var a in animList)
            {
                if (a.go == null) continue;
                a.go.transform.localPosition = Vector3.Lerp(a.fromPos, a.toPos, tt);
            }

            yield return null;
        }

        // 最終位置に合わせる
        foreach (var a in animList)
        {
            if (a.go == null) continue;
            a.go.transform.localPosition = a.toPos;
        }

        // 一番下の行に新しいブロックを生成
        int bottomY = core.H - 1;
        for (int x = 0; x < core.W; x++)
        {
            // 盤面にブロックがあり、まだ GameObject が無ければ生成
            if (core.grid[bottomY, x] != 0 && blocks[bottomY, x] == null)
            {
                int v = core.grid[bottomY, x];

                GameObject go = Instantiate(blockPrefab, transform);
                go.transform.localPosition = CellToLocalPos(bottomY, x);

                var sr = go.GetComponent<SpriteRenderer>();
                ApplyVisual(sr, v);

                blocks[bottomY, x] = go;
            }
        }

        Debug.Log("[Anim] Rise: 完了 → onComplete 呼び出し");
        onComplete?.Invoke();
    }

    // 破壊演出：その場でスプライトだけ複製して、拡大＋フェードして消す
    private void PlayBreakEffect(GameObject originalBlockGO)
    {
        if (originalBlockGO == null) return;

        var srcSR = originalBlockGO.GetComponent<SpriteRenderer>();
        if (srcSR == null) return;

        // 破壊演出用オブジェクト（SpriteRendererだけ）
        GameObject fx = new GameObject("BreakFX");
        fx.transform.SetParent(transform, false);
        fx.transform.localPosition = originalBlockGO.transform.localPosition;
        fx.transform.localRotation = originalBlockGO.transform.localRotation;
        fx.transform.localScale = originalBlockGO.transform.localScale;

        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite = srcSR.sprite;
        sr.color = srcSR.color;
        sr.material = srcSR.sharedMaterial;

        // Sorting を元より手前に
        sr.sortingLayerID = srcSR.sortingLayerID;
        sr.sortingOrder = srcSR.sortingOrder + breakSortingOrderOffset;

        StartCoroutine(CoBreakFX(fx, sr));
    }

    private IEnumerator CoBreakFX(GameObject fx, SpriteRenderer sr)
    {
        if (fx == null || sr == null) yield break;

        float t = 0f;
        Vector3 baseScale = fx.transform.localScale;
        Color baseColor = sr.color;

        // 0→1 で「一瞬ポップして消える」
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, breakDuration);
            float tt = Mathf.Clamp01(t);

            // ポップ：前半で拡大、後半で縮小
            float pop;
            if (tt < 0.5f)
            {
                pop = Mathf.Lerp(1f, breakPopScale, tt / 0.5f);
            }
            else
            {
                pop = Mathf.Lerp(breakPopScale, 0.85f, (tt - 0.5f) / 0.5f);
            }
            fx.transform.localScale = baseScale * pop;

            // フェードアウト
            Color c = baseColor;
            c.a = Mathf.Lerp(1f, 0f, tt);
            sr.color = c;

            yield return null;
        }

        Destroy(fx);
    }

    //break Sound
    private void PlayBlockDestroySound()
    {
        if (blockDestroySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(blockDestroySound);  // 破壊音を再生
        }
    }
}
