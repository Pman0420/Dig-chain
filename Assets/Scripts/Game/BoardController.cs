using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    [Header("盤面サイズ（論理）")]
    public int height = 6;
    public int width = 6;

    [Header("見た目")]
    public BoardView view;

    // 盤面ロジック
    public DigChainCore core { get; private set; }

    // ★ UI 用の読み取り専用プロパティを追加 ★
    public int CurrentPower
    {
        get { return core != null ? core.power : 0; }
    }

    public ColorSelector ColorSelector
    {
        get { return core != null ? core.colorSelector : null; }
    }

    private void Awake()
    {
        core = new DigChainCore(height, width);
    }


    private void Start()
    {
        // 固定の初期盤面（あとでステージデータに差し替え可）
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

        // 色候補の更新と初期色決定
        core.colorSelector.UpdateAvailableColors(core.grid);
        core.colorSelector.InitColors();
        Debug.Log($"現在の色 = {core.colorSelector.currentColor}, 次 = {core.colorSelector.nextColor1}, 次の次 = {core.colorSelector.nextColor2}");

        if (view != null)
        {
            view.SetCore(core);
            view.Redraw();
        }

        Debug.Log("初期盤面を表示しました。");
    }

    /// <summary>
    /// (gridY, gridX) のマスを掘る窓口。
    /// Core に処理を投げ、View にアニメーションを依頼する。
    /// </summary>
    public DigChainResult DigAt(int gridY, int gridX)
    {
        // 空の結果（何も起きなかったとき用）
        DigChainResult EmptyResult()
        {
            return new DigChainResult
            {
                steps = new List<ChainStep>(),
                chainCount = 0,
                totalCrushed = 0
            };
        }

        if (core == null)
        {
            Debug.LogWarning("BoardController.DigAt: core が null");
            return EmptyResult();
        }

        if (!core.InBounds(gridY, gridX))
        {
            Debug.Log($"BoardController.DigAt: 盤面外 ({gridY},{gridX})");
            return EmptyResult();
        }

        int cellColor = core.grid[gridY, gridX];

        // 0 = 空マス → 何も起こさない（仕様：空振り扱いにはしない）
        if (cellColor == 0)
        {
            Debug.Log("BoardController.DigAt: 空マスなので何も起こりません。");
            return EmptyResult();
        }

        // ★ 色制限チェック：currentColor と違う色を掘ろうとしたら「空振り」
        int current = core.colorSelector.currentColor;
        // 色ミスマッチ：空振りペナルティ（パワー全消費＋色を1つ進める）
        if (cellColor != current)
        {
            Debug.Log($"BoardController.DigAt: 色ミスマッチ! target={cellColor}, current={current} → パワーリセット＆色ローテ");

            // パワーをリセット
            core.power = 0;

            if (view != null && GameManager.Instance.Power != null)
            {
                GameManager.Instance.Power.ResetAll();
                // ← 論理＆ゲージを両方ゼロに
            }
            // 色を 1つ進める（now ← next1, next1 ← next2, next2 ← ランダム）
            core.colorSelector.ShiftColors();
            // UIは毎フレーム colorSelector / power を見ているので、ここで値だけ変えればOK
            return EmptyResult();
        }

        // ここまで来たら「正しい色で掘った」→ 掘削＋連鎖実行
        int oldPower = core.power;

        DigChainResult res = core.DigAndChainWithSteps(gridY, gridX);

        if (res.totalCrushed == 0)
        {
            Debug.Log("BoardController.DigAt: 掘ったが消えるブロックはありませんでした。");
            return res;
        }

        int newPower = core.power;

        // 盤面に存在する色リストを更新し、その中から次の色を決める
        core.colorSelector.UpdateAvailableColors(core.grid);
        core.colorSelector.ShiftColors();

        // 見た目更新（アニメーション）
        if (view != null)
        {
            view.PlayDigChainAnimation(res, oldPower, newPower);
        }
        else
        {
            Debug.LogWarning("BoardController: view が未設定です。");
        }

        Debug.Log($"BoardController.DigAt: total={res.totalCrushed}, chain={res.chainCount}, power={core.power}");
        return res;
    }
    // 掘りと同じように、(gridY, gridX) に currentColor を置く
    public DigChainResult PlaceAt(int gridY, int gridX)
    {
        var empty = new DigChainResult
        {
            steps = new List<ChainStep>(),
            chainCount = 0,
            totalCrushed = 0
        };

        if (core == null || view == null || core.colorSelector == null)
        {
            Debug.LogWarning("BoardController.PlaceAt: core / view / colorSelector が設定されていません");
            return empty;
        }

        if (gridY < 0 || gridY >= core.H || gridX < 0 || gridX >= core.W)
        {
            Debug.Log($"BoardController.PlaceAt: 範囲外 ({gridY},{gridX})");
            return empty;
        }

        // すでに埋まっているマスには置かない
        if (core.grid[gridY, gridX] != 0)
        {
            Debug.Log("BoardController.PlaceAt: すでにブロックがあるので置けない");
            return empty;
        }

        int color = core.colorSelector.currentColor;
        int oldPower = core.power;   // 置きでは増えないが形式上保存

        DigChainResult res = core.PlaceBlockAndFall(gridY, gridX, color);

        // PlaceBlockAndFall の結果を確認
        bool hasStep0 = (res.steps != null && res.steps.Count > 0);
        bool hasFall = hasStep0 &&
                        res.steps[0].fallInfos != null &&
                        res.steps[0].fallInfos.Count > 0;

        // ★ 1) 一切落下が発生しなかった（＝その場に置かれた）場合
        if (!hasFall)
        {
            // grid 上はすでに更新されている前提
            view.EnsureBlockVisualAt(gridY, gridX);

            // 色ローテーションだけ回す
            core.colorSelector.ShiftColors();

            Debug.Log("BoardController.PlaceAt: 落下なし配置 → その場で表示のみ");
            return res;
        }

        // ★ 2) 落下が発生した場合はアニメーションに任せる
        core.colorSelector.ShiftColors();
        view.PlayDigChainAnimation(res, oldPower, core.power);  // power は増えない想定

        Debug.Log("BoardController.PlaceAt: 落下あり配置 → アニメ再生");
        return res;
    }


}

