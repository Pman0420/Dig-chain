// DigChainCore.cs
// 目的: マス単位の掘削・落下・連鎖・Power計算のみを担当する「純ロジック」クラス。

using System;
using System.Collections.Generic;
using UnityEngine;

// グリッド上の座標
public struct Pos
{
    public int y;
    public int x;

    public Pos(int y, int x)
    {
        this.y = y;
        this.x = x;
    }
}

// 「どこからどこにブロックが落ちたか」を表す情報
public struct FallInfo
{
    public int fromY;
    public int fromX;
    public int toY;
    public int toX;

    public FallInfo(int fromY, int fromX, int toY, int toX)
    {
        this.fromY = fromY;
        this.fromX = fromX;
        this.toY = toY;
        this.toX = toX;
    }
}

// ▲ 追加: アニメーション用に「1ステップ分の情報」をまとめる型
public struct ChainStep
{
    // このステップで消えたブロック
    public List<Pos> crushedBlocks;
    // このステップのあとに発生した落下（from→to）
    public List<FallInfo> fallInfos;
}

// ▲ 追加: 掘削～連鎖全体の結果（アニメーション用）
public struct DigChainResult
{
    public int chainCount;           // 連鎖回数
    public int totalCrushed;         // 消したブロック総数（掘削 + 連鎖）
    public List<ChainStep> steps;    // 各ステップ（最初の掘削 + 各連鎖）の情報
}

public class DigChainCore
{
    // 盤の高さ・幅
    public int H { get; private set; }
    public int W { get; private set; }

    // 0 = 空, 1..n = 色（ブロックID）
    // grid[y, x]
    public int[,] grid;

    // 蓄積Power
    public int power;

    //色選択
    public ColorSelector colorSelector;

    // 最後に発生した連鎖数
    public int lastChainNum;

    public DigChainCore(int h, int w)
    {
        H = h;
        W = w;
        grid = new int[H, W];
        power = 0;
        lastChainNum = 0;
        colorSelector = new ColorSelector();
    }

    // 盤面内かどうか
    public bool InBounds(int y, int x)
    {
        return (0 <= y && y < H && 0 <= x && x < W);
    }

    // デバッグ用: コンソールに盤面を出す（UnityEditor のみ使う想定）
#if UNITY_EDITOR
    public void Print(string title)
    {
        UnityEngine.Debug.Log("=== " + title + " ===");
        string s = "";
        for (int y = 0; y < H; ++y)
        {
            for (int x = 0; x < W; ++x)
            {
                s += grid[y, x].ToString();
            }
            s += "\n";
        }
        UnityEngine.Debug.Log(s);
    }
#endif

    // =========================
    // 掘削まわり
    // =========================

    // 内部実装: 掘削し、消したブロック座標を out にためる（null可）
    private int DigClusterInternal(int sy, int sx, List<Pos> removedOut)
    {
        if (!InBounds(sy, sx)) return 0;
        int color = grid[sy, sx];
        if (color == 0) return 0;

        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        var visited = new bool[H, W];
        var que = new Queue<Pos>();
        var comp = new List<Pos>();

        visited[sy, sx] = true;
        que.Enqueue(new Pos(sy, sx));

        while (que.Count > 0)
        {
            Pos p = que.Dequeue();
            comp.Add(p);

            for (int k = 0; k < 4; ++k)
            {
                int ny = p.y + dy[k];
                int nx = p.x + dx[k];
                if (!InBounds(ny, nx)) continue;
                if (visited[ny, nx]) continue;
                if (grid[ny, nx] != color) continue;
                visited[ny, nx] = true;
                que.Enqueue(new Pos(ny, nx));
            }
        }

        foreach (var p in comp)
        {
            grid[p.y, p.x] = 0;
        }

        if (removedOut != null)
        {
            removedOut.AddRange(comp);
        }

        return comp.Count;
    }

    // 公開API: 元のシンプル版（座標は返さない）
    public int DigCluster(int sy, int sx)
    {
        return DigClusterInternal(sy, sx, null);
    }

    // =========================
    // 重力
    // =========================

    // 重力 + 「落下したブロックの情報」を返す
    // 今回は列ごとの全体下詰め簡易版。
    public List<FallInfo> ApplyGravityAndGetFallInfos()
    {
        var fallen = new List<FallInfo>();

        for (int x = 0; x < W; ++x)
        {
            int writeRow = H - 1;
            for (int y = H - 1; y >= 0; --y)
            {
                if (grid[y, x] != 0)
                {
                    if (writeRow != y)
                    {
                        int color = grid[y, x];
                        grid[writeRow, x] = color;
                        grid[y, x] = 0;
                        fallen.Add(new FallInfo(y, x, writeRow, x));
                    }
                    // writeRow == y の場合、落下していないので FallInfo は追加しない
                    writeRow--;
                }
            }
            // 上側は空で埋める
            while (writeRow >= 0)
            {
                grid[writeRow, x] = 0;
                writeRow--;
            }
        }

        return fallen;
    }

    // =========================
    // 連鎖（クラッシュ）まわり
    // =========================

    // 内部実装: 落下したブロック群を起点に、同色で3つ以上の塊のみ破壊する。
    // crushedOut が non-null のとき、消した座標をすべて追加する。
    // 戻り値: 破壊した総ブロック数
    private int CrushFromFallenInternal(List<Pos> fallenStarts, List<Pos> crushedOut)
    {
        if (fallenStarts == null || fallenStarts.Count == 0) return 0;

        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        var visited = new bool[H, W];
        int totalCrushed = 0;

        foreach (var f in fallenStarts)
        {
            if (!InBounds(f.y, f.x)) continue;
            if (visited[f.y, f.x]) continue;

            int color = grid[f.y, f.x];
            if (color == 0) continue;

            var que = new Queue<Pos>();
            var comp = new List<Pos>();

            visited[f.y, f.x] = true;
            que.Enqueue(f);

            while (que.Count > 0)
            {
                Pos p = que.Dequeue();
                comp.Add(p);

                for (int k = 0; k < 4; ++k)
                {
                    int ny = p.y + dy[k];
                    int nx = p.x + dx[k];
                    if (!InBounds(ny, nx)) continue;
                    if (visited[ny, nx]) continue;
                    if (grid[ny, nx] != color) continue;
                    visited[ny, nx] = true;
                    que.Enqueue(new Pos(ny, nx));
                }
            }

            if (comp.Count >= 3)
            {
                foreach (var p in comp)
                {
                    grid[p.y, p.x] = 0;
                }
                totalCrushed += comp.Count;

                if (crushedOut != null)
                {
                    crushedOut.AddRange(comp);
                }
            }
            // 3未満の塊は壊さない
        }

        return totalCrushed;
    }

    // 互換用: もともとのシンプル版 API（座標は返さない）
    public int CrushFromFallen(List<Pos> fallenStarts)
    {
        return CrushFromFallenInternal(fallenStarts, null);
    }

    // 掘削後の連鎖処理: （従来のロジック版）
    // ・引数 initialFallInfos = 掘削→重力 直後の落下情報
    // ・毎ステップ:
    //   - 落下ブロック群を起点に3個以上塊を破壊
    //   - 重力をかけて新たな落下情報を得る
    //   - それを次のステップの起点とする
    public int ResolveChainWithInitialFall(List<FallInfo> initialFallInfos)
    {
        int chain = 0;
        int totalCrushed = 0;

        // FallInfo( fromY, fromX, toY, toX ) → 連鎖起点は「落下後」の位置
        List<Pos> fallenStarts = new List<Pos>();
        foreach (var f in initialFallInfos)
        {
            fallenStarts.Add(new Pos(f.toY, f.toX));
        }

        while (true)
        {
            int crushed = CrushFromFallen(fallenStarts);
            if (crushed == 0) break;

            totalCrushed += crushed;
            chain++;

#if UNITY_EDITOR
            Print($"連鎖 {chain} 回目 消去後");
#endif

            // 壊れた結果、新たに落下するブロックが出る
            List<FallInfo> newFallInfos = ApplyGravityAndGetFallInfos();

#if UNITY_EDITOR
            Print($"連鎖 {chain} 回目 重力適用後");
#endif

            if (newFallInfos.Count == 0) break;

            fallenStarts.Clear();
            foreach (var fi in newFallInfos)
            {
                fallenStarts.Add(new Pos(fi.toY, fi.toX));
            }
        }

        if (chain > 0 && totalCrushed > 0)
        {
            double mult = Math.Pow(1.5, chain);
            int gained = (int)Math.Round(totalCrushed * mult);
            power += gained;
        }

        lastChainNum = chain;
        return chain;
    }

    // =========================
    // 新・アニメーション対応版: 掘削～連鎖の「全ステップ情報」を返す
    // =========================

    public DigChainResult DigAndChainWithSteps(int y, int x)
    {
        var result = new DigChainResult
        {
            steps = new List<ChainStep>(),
            chainCount = 0,
            totalCrushed = 0
        };

        // ★ まず範囲＆色チェック（追加部分）

        // 盤面外ガード（InBounds がある前提。無ければこの2行は省いてもよい）
        if (!InBounds(y, x))
        {
            lastChainNum = 0;
            return result;
        }

        int target = grid[y, x];

        // 空マスなら何も起きない
        if (target == 0)
        {
            lastChainNum = 0;
            return result;
        }

        // ColorSelector が有効なら「今掘れる色」と一致しているか確認
        if (colorSelector != null && target != colorSelector.currentColor)
        {
            // 掘れない色だったので何もせず終了
            // （アニメ・盤面変化なし）
            // Debug.Log したければここで出してもよい
            // UnityEngine.Debug.Log($"掘れない色: grid={target}, allowed={colorSelector.currentColor}");
            power = 0;
            colorSelector.ShiftColors();
            Debug.Log("掘れない色: 次の色に進む");
            lastChainNum = 0;
            return result;
        }

        // --- 1. 掘削 ---
        var digRemoved = new List<Pos>();
        int removedDig = DigClusterInternal(y, x, digRemoved);
        if (removedDig == 0)
        {
            lastChainNum = 0;
            return result; // 何も起こらない
        }

        // ★ 掘削に成功したので色キューを1つ進める
        if (colorSelector != null)
        {
            colorSelector.ShiftColors();
        }

        result.totalCrushed += removedDig;

#if UNITY_EDITOR
        Print("掘削後");
#endif

        // 掘削直後の重力
        List<FallInfo> firstFalls = ApplyGravityAndGetFallInfos();

#if UNITY_EDITOR
        Print("掘削後 重力適用後");
#endif

        // 掘削ステップを steps[0] として登録
        var firstStep = new ChainStep
        {
            crushedBlocks = new List<Pos>(digRemoved),
            fallInfos = new List<FallInfo>(firstFalls)
        };
        result.steps.Add(firstStep);

        // --- 2. 連鎖ステップ ---
        int chain = 0;

        // 初期の落下位置から開始
        List<Pos> fallenStarts = new List<Pos>();
        foreach (var f in firstFalls)
        {
            fallenStarts.Add(new Pos(f.toY, f.toX));
        }

        while (true)
        {
            var crushedThisStep = new List<Pos>();
            int crushed = CrushFromFallenInternal(fallenStarts, crushedThisStep);
            if (crushed == 0) break;

            result.totalCrushed += crushed;
            chain++;

#if UNITY_EDITOR
            Print($"連鎖 {chain} 回目 消去後");
#endif

            // 壊れた結果の重力
            List<FallInfo> newFallInfos = ApplyGravityAndGetFallInfos();

#if UNITY_EDITOR
            Print($"連鎖 {chain} 回目 重力適用後");
#endif

            // この連鎖ステップを追加
            var step = new ChainStep
            {
                crushedBlocks = new List<Pos>(crushedThisStep),
                fallInfos = new List<FallInfo>(newFallInfos)
            };
            result.steps.Add(step);

            if (newFallInfos.Count == 0) break;

            // 次ステップ用の起点を更新
            fallenStarts.Clear();
            foreach (var fi in newFallInfos)
            {
                fallenStarts.Add(new Pos(fi.toY, fi.toX));
            }
        }

        // Power計算（従来と同じ式）
        if (chain > 0 && result.totalCrushed > 0)
        {
            double mult = Math.Pow(1.5, chain);
            int gained = (int)Math.Round(result.totalCrushed * mult);
            power += gained;
        }

        lastChainNum = chain;
        result.chainCount = chain;

        return result;
    }

    // =========================
    // 互換用・従来API: 「掘削→重力→連鎖」を一気に処理する。
    // Unity側からは普通これだけ呼べばOKだった版（今も使える）。
    // =========================
    public int DigAndChain(int y, int x)
    {
        // 色が一致しない場合掘れない
        if (grid[y, x] != colorSelector.currentColor)
        {
            return 0;
        }

        // 色一致 → 掘削
        int removed = DigCluster(y, x);
        if (removed == 0)
        {
            return 0;
        }

        // 色をスライド
        colorSelector.ShiftColors();

        // 掘削後の重力と連鎖処理
        List<FallInfo> fallInfos = ApplyGravityAndGetFallInfos();
        int chain = ResolveChainWithInitialFall(fallInfos);

        return chain;
    }
}

public class ColorSelector
{
    private System.Random rnd = new System.Random();

    public int currentColor;
    public int nextColor1;
    public int nextColor2;

    private List<int> availableColors = new List<int>();

    public void UpdateAvailableColors(int[,] grid)
    {
        availableColors.Clear();
        HashSet<int> set = new HashSet<int>();

        int h = grid.GetLength(0);
        int w = grid.GetLength(1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int c = grid[y, x];
                if (c != 0) set.Add(c);
            }
        }

        availableColors.AddRange(set);
    }

    public void InitColors()
    {
        currentColor = RandomPick();
        nextColor1 = RandomPick();
        nextColor2 = RandomPick();
    }

    private int RandomPick()
    {
        if (availableColors.Count == 0) return 1;
        return availableColors[UnityEngine.Random.Range(0, availableColors.Count)];
    }

    // ★ 掘った時に色をずらす（ShiftColors）
    public void ShiftColors()
    {
        currentColor = nextColor1;
        nextColor1 = nextColor2;
        nextColor2 = RandomPick();
    }
}