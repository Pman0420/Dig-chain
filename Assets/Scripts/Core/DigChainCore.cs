// DigChainCore.cs
// 目的: マス単位の掘削・落下・連鎖・Power計算のみを担当する「純ロジック」クラス。
// UnityEngine に依存しないのでテストしやすく、View/アクションと分離できる。

using System;
using System.Collections.Generic;

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

    // 最後に発生した連鎖数
    public int lastChainNum;

    public DigChainCore(int h, int w)
    {
        H = h;
        W = w;
        grid = new int[H, W];
        power = 0;
        lastChainNum = 0;
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

    // 掘削:
    // ・(sy, sx) と同色で4近傍連結なブロックを全て破壊する
    // ・個数に関係なく同色塊は必ず破壊
    // 戻り値: 破壊したブロック数
    public int DigCluster(int sy, int sx)
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

        return comp.Count;
    }

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

    // 落下したブロック群を起点に、同色で3つ以上の塊のみ破壊する。
    // 戻り値: 破壊した総ブロック数
    public int CrushFromFallen(List<Pos> fallenStarts)
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
            }
            // 3未満の塊は壊さない
        }

        return totalCrushed;
    }

    // 掘削後の連鎖処理:
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

    // 「掘削→重力→連鎖（連鎖中は再び落下）」を一気に処理する。
    // Unity側からは普通これだけ呼べばOK。
    public int DigAndChain(int y, int x)
    {
        int removed = DigCluster(y, x);
        if (removed == 0)
        {
            lastChainNum = 0;
            return 0;
        }

        // 掘削後の重力（最初の落下）
        List<FallInfo> fallInfos = ApplyGravityAndGetFallInfos();

        // この落下ブロックから始まる連鎖
        int chain = ResolveChainWithInitialFall(fallInfos);
        return chain;
    }
}
