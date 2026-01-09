using System;
using System.Collections.Generic;

// =======================
// 色管理クラス
// =======================
public class ColorSelector
{
    // 今掘れる色
    public int currentColor;

    // 次に掘れる色
    public int nextColor1;
    public int nextColor2;

    // 利用可能な色一覧（盤面から抽出）
    private List<int> availableColors = new List<int>();

    // System.Random で毎回同じになりにくくする
    private static System.Random rng = new System.Random();

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
                if (c != 0)
                {
                    set.Add(c);
                }
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

    // ランダム取得
    private int RandomPick()
    {
        if (availableColors.Count == 0) return 1;
        int idx = rng.Next(availableColors.Count);
        return availableColors[idx];
    }

    public int GetRandomColor()
    {
        return RandomPick();  // 既存の private メソッドを呼ぶだけ
    }

    // 掘ったときにカラーをスライド
    public void ShiftColors()
    {
        currentColor = nextColor1;
        nextColor1 = nextColor2;
        nextColor2 = RandomPick();
    }
}

// =======================
// グリッド座標 / 落下情報 / 連鎖結果
// =======================

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

public class ChainStep
{
    public List<Pos> crushedBlocks; // このステップで消えたブロック
    public List<FallInfo> fallInfos;     // このステップで落下したブロック
}

public class DigChainResult
{
    public List<ChainStep> steps;        // 0: 掘削 or 置き, 1以降: 連鎖
    public int chainCount;   // 連鎖数
    public int totalCrushed; // 合計で消えたブロック数
}

// =======================
// 盤面ロジック本体
// =======================

public class DigChainCore
{
    // 盤の高さ・幅
    public int H { get; private set; }
    public int W { get; private set; }

    // 0 = 空, 1..n = 色（ブロックID）
    public int[,] grid;

    // 蓄積Power
    public int power;

    // 最後に発生した連鎖数
    public int lastChainNum;

    // 色選択
    public ColorSelector colorSelector { get; private set; }

    public DigChainCore(int h, int w)
    {
        H = h;
        W = w;
        grid = new int[H, W];
        power = 0;
        lastChainNum = 0;

        colorSelector = new ColorSelector();
    }

    public bool InBounds(int y, int x)
    {
        return (0 <= y && y < H && 0 <= x && x < W);
    }

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

    // ---------- 掘削（内部用） ----------
    private int DigClusterInternal(int sy, int sx, List<Pos> removed)
    {
        if (!InBounds(sy, sx)) return 0;
        int color = grid[sy, sx];
        if (color == 0) return 0;

        int[] dy = { -1, 1, 0, 0 };
        int[] dx = { 0, 0, -1, 1 };

        var visited = new bool[H, W];
        var que = new Queue<Pos>();

        visited[sy, sx] = true;
        que.Enqueue(new Pos(sy, sx));

        while (que.Count > 0)
        {
            Pos p = que.Dequeue();
            removed.Add(p);

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

        foreach (var p in removed)
        {
            grid[p.y, p.x] = 0;
        }

        return removed.Count;
    }

    // ---------- 重力 ----------
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
                    writeRow--;
                }
            }
            while (writeRow >= 0)
            {
                grid[writeRow, x] = 0;
                writeRow--;
            }
        }

        return fallen;
    }

    // ---------- 連鎖：落下ブロック起点で3個以上の塊を壊す ----------
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
                    crushedOut.Add(p);
                }
                totalCrushed += comp.Count;
            }
        }

        return totalCrushed;
    }

    // ---------- 盤面だけ更新する純ロジック（掘り） ----------
    public DigChainResult DigAndChainLogicOnly(int y, int x)
    {
        var result = new DigChainResult
        {
            steps = new List<ChainStep>(),
            chainCount = 0,
            totalCrushed = 0
        };

        // 掘削
        var digRemoved = new List<Pos>();
        int removedDig = DigClusterInternal(y, x, digRemoved);
        if (removedDig == 0)
        {
            lastChainNum = 0;
            return result;
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

        // 掘削ステップを steps[0] に
        var firstStep = new ChainStep
        {
            crushedBlocks = new List<Pos>(digRemoved),
            fallInfos = new List<FallInfo>(firstFalls)
        };
        result.steps.Add(firstStep);

        // 連鎖
        int chain = 0;
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

            List<FallInfo> newFallInfos = ApplyGravityAndGetFallInfos();

#if UNITY_EDITOR
            Print($"連鎖 {chain} 回目 重力適用後");
#endif

            var step = new ChainStep
            {
                crushedBlocks = new List<Pos>(crushedThisStep),
                fallInfos = new List<FallInfo>(newFallInfos)
            };
            result.steps.Add(step);

            if (newFallInfos.Count == 0) break;

            fallenStarts.Clear();
            foreach (var fi in newFallInfos)
            {
                fallenStarts.Add(new Pos(fi.toY, fi.toX));
            }
        }

        lastChainNum = chain;
        result.chainCount = chain;
        return result;
    }

    // ---------- 既存API：掘り＋Power更新 ----------
    public DigChainResult DigAndChainWithSteps(int y, int x)
    {
        DigChainResult result = DigAndChainLogicOnly(y, x);

        if (result.chainCount > 0 && result.totalCrushed > 0)
        {
            double mult = Math.Pow(1.5, result.chainCount);
            int gained = (int)Math.Round(result.totalCrushed * mult);
            power += gained;
        }

        return result;
    }

    // ====================================================
    // ★ 新規：ブロックを置いて「重力だけ」かける処理
    // ====================================================
    public DigChainResult PlaceBlockAndFall(int y, int x, int color)
    {
        var result = new DigChainResult
        {
            steps = new List<ChainStep>(),
            chainCount = 0,
            totalCrushed = 0
        };

        // 範囲外なら何もしない
        if (!InBounds(y, x)) return result;

        // 既にブロックがあるマスには置けない
        if (grid[y, x] != 0) return result;

        // ① ブロックを置く
        grid[y, x] = color;

#if UNITY_EDITOR
        Print("ブロック設置後");
#endif

        // ② 重力をかける（列ごとの下詰め簡易版）
        List<FallInfo> falls = ApplyGravityAndGetFallInfos();

#if UNITY_EDITOR
        Print("ブロック設置後 重力適用後");
#endif

        // ③ 「置き＋落下」用の1ステップを作る（消去はなし）
        var step0 = new ChainStep
        {
            crushedBlocks = new List<Pos>(),     // 置いただけなので空
            fallInfos = new List<FallInfo>(falls)  // 今回の落下情報
        };

        result.steps.Add(step0);

        // Power や lastChainNum は変更しない（壊していないため）
        return result;
    }
    /// <summary>
    /// 盤面を1行ぶん「せり上げる」。
    /// 上の行に押し出される動きを FallInfo として返す。
    /// 一番下の行にはランダムな色を敷き詰める。
    /// ※ 連鎖やPower計算はここでは行わない。
    /// </summary>
    public List<FallInfo> RaiseOneLine()
    {
        var moved = new List<FallInfo>();

        // ※先に上方向へコピー（y=H-1 から y=1 へ）
        for (int y = 1; y < H; y++)
        {
            int toY = y - 1;
            for (int x = 0; x < W; x++)
            {
                int c = grid[y, x];
                grid[toY, x] = c;

                // 元にブロックがあったなら、移動情報を記録
                if (c != 0)
                {
                    moved.Add(new FallInfo(y, x, toY, x));
                }
            }
        }

        // 一番下の行をランダムなブロックで埋める
        for (int x = 0; x < W; x++)
        {
            int c = 0;

            // ColorSelector が使えるなら、その候補から色を選ぶ
            if (colorSelector != null)
            {
                // 候補がまだ空の場合は更新しておく
                if (true)
                {
                    colorSelector.UpdateAvailableColors(grid);
                }
                c = colorSelector.GetRandomColor();
            }
            else
            {
                // 念のためのフォールバック（1〜4）
                c = 1 + (x % 4);
            }

            grid[H - 1, x] = c;
        }

        // 盤面が変わったので色候補も更新しておく
        if (colorSelector != null)
        {
            colorSelector.UpdateAvailableColors(grid);
        }

        return moved;
    }
}
