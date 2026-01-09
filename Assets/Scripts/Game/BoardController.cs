using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    [Header("Character Reaction")]
    [SerializeField] private ChainReactionCharacter chainCharacter;

    [Header("盤面サイズ（論理）")]
    public int height = 40;
    public int width = 10;

    [Header("見た目")]
    public BoardView view;

    [Header("SE")]
    [SerializeField] private AudioSource seSource;   // SE再生用（AudioSource）
    [SerializeField] private AudioClip placeSE;      // 置いたときのSE


    // 盤面ロジック
    public DigChainCore core { get; private set; }

    // せり上げ間隔（秒）
    [SerializeField]
    private float riseInterval = 5f;

    private float riseTimer = 0f;

    // 盤面アニメーション中フラグ
    private bool isBoardBusy = false;
    public bool IsBoardBusy => isBoardBusy;
    //  アニメ終了後のクールダウン秒数（Inspectorで調整可能）
    [SerializeField] private float riseCooldownAfterAnimation = 0.3f;
    //  実際にカウントダウンするクールダウンタイマー
    private float riseCooldownTimer = 0f;

    // Busy を切り替える共通関数（ログ付き）
    private void SetBoardBusy(bool busy, string reason)
    {
        isBoardBusy = busy;
        Debug.Log($"[BoardBusy] => {busy} ({reason})");
    }

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
        // ★ まず core を作る（Inspector の値で高さ・幅は決まっている）
        //    height=40, width=10 などを Inspector で設定しておくこと
        core = new DigChainCore(height, width);

        for (int y = 0; y < core.H; y++)
        {
            for (int x = 0; x < core.W; x++)
            {
                core.grid[y, x] = 0;
            }
        }


        // 3) 盤面の「下の方だけ」ランダムで埋める
        //    → ここを変えることで「広がってる」のが視覚的にわかりやすくなる
        int filledRows = Mathf.Min(3, core.H);  // 下から3行ぶんランダム（盤面が小さい場合は調整）
        int startY = core.H - filledRows;    // 下から filledRows 行ぶん

        for (int y = startY; y < core.H; y++)
        {
            for (int x = 0; x < core.W; x++)
            {
                // 1〜4 の色をランダムで入れる
                core.grid[y, x] = Random.Range(1, 5); // 1,2,3,4 のどれか
            }
        }

        // ★ 色候補の更新と初期色決定
        core.colorSelector.UpdateAvailableColors(core.grid);
        core.colorSelector.InitColors();
        Debug.Log($"現在の色 = {core.colorSelector.currentColor}, 次 = {core.colorSelector.nextColor1}, 次の次 = {core.colorSelector.nextColor2}");
   if (view != null)
        {
            view.SetCore(core);
            view.Redraw();
        }

        Debug.Log($"初期盤面を下端にセットしました。盤サイズ H={core.H}, W={core.W}");
    }

        // ★ 盤面描画
     
    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            return;
        }

        // ★ B: 盤面アニメーション中はせり上がりタイマーを止める
        if (isBoardBusy)
        {
            Debug.Log("[BoardBusy] Update: busy のため riseTimer 加算をスキップ");
            return;
        }

        // ★ A: アニメ終了直後のクールダウン中もせり上がりを止める
        if (riseCooldownTimer > 0f)
        {
            riseCooldownTimer -= Time.deltaTime;
            // 念のため 0 未満にならないようにクランプ
            if (riseCooldownTimer < 0f) riseCooldownTimer = 0f;

            Debug.Log($"[RiseCooldown] クールダウン中: 残り {riseCooldownTimer:F2} 秒");
            return;
        }

        // ★ 普通時：せり上がりタイマーを進める
        riseTimer += Time.deltaTime;

        if (riseTimer >= riseInterval)
        {
            Debug.Log("[Rise] タイマー到達 → DoRise 呼び出し");
            DoRise();
            riseTimer = 0f;
        }
    }

    /// <summary>
    /// 一番上の行にブロックがあるかどうかをチェック → あればゲームオーバー
    /// </summary>
    private bool CheckGameOverByTopRow()
    {
        // 盤面の 0 行目（最上段）を見て、1〜4 のブロックがあればアウト
        for (int x = 0; x < core.W; x++)
        {
            if (core.grid[0, x] != 0)
            {
                return true;
            }
        }
        return false;
    }
    private void DoRise()
    {
        if (core == null || view == null) return;

        // ① 1 行せり上げ（core 内部のロジック更新）
        List<FallInfo> moved = core.RaiseOneLine();

        // ② この時点の盤面で、トップ行にブロックが乗っているか判定
        bool willGameOver = CheckGameOverByTopRow();

        if (moved != null && moved.Count > 0)
        {
            // ★ せり上げアニメが発生する場合だけ Busy にする
            SetBoardBusy(true, "RiseAnimation start");

            // ★ アニメ完了時に GameOver 判定結果を反映させてから BoardBusy を解除
            view.PlayRiseAnimation(moved, () =>
            {
                if (willGameOver)
                {
                    GameManager.Instance?.GameOver();
                }

                OnBoardAnimationFinished();
            });
        }
        else
        {
            Debug.Log("[Rise] moved が空だったためアニメ無し");

            // ★ アニメがない場合も、トップ行が埋まっていれば即ゲームオーバー
            if (willGameOver)
            {
                GameManager.Instance?.GameOver();
            }
        }
    }



    // ★ BoardView から呼んでもらう「アニメ終了」コールバック
    public void OnBoardAnimationFinished()
    {
        Debug.Log("[BoardBusy] OnBoardAnimationFinished() 呼び出し → false に戻す");
        SetBoardBusy(false, "Animation finished");

        // ★アニメ直後のクールダウン開始
        riseCooldownTimer = riseCooldownAfterAnimation;
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
                GameManager.Instance.Power.ResetPower();
                // ← 論理＆ゲージを両方ゼロに
            }
            // 色を 1つ進める（now ← next1, next1 ← next2, next2 ← ランダム）
            core.colorSelector.ShiftColors();
            if (chainCharacter != null)
            {
                chainCharacter.OnChainResolved(0, true);
            }
            // UIは毎フレーム colorSelector / power を見ているので、ここで値だけ変えればOK
            return EmptyResult();
        }

        // ここまで来たら「正しい色で掘った」→ 掘削＋連鎖実行
        int oldPower = core.power;

        DigChainResult res = core.DigAndChainWithSteps(gridY, gridX);

        if (res.totalCrushed == 0)
        {
            Debug.Log("BoardController.DigAt: 掘ったが消えるブロックはありませんでした。");
            // Busyにしない
            // （必要なら chainCharacter.OnChainResolved(0,false) を呼ぶのもあり）
            return res;
        }


        int newPower = core.power;

        // 盤面に存在する色リストを更新し、その中から次の色を決める
        core.colorSelector.UpdateAvailableColors(core.grid);
        core.colorSelector.ShiftColors();

        // 見た目更新（アニメーション）
        if (view != null)
        {
            SetBoardBusy(true, "DigChainAnimation start");

            view.PlayDigChainAnimation(res, oldPower, newPower, () =>
            {
                

                OnBoardAnimationFinished();
            });

        }
        else
        {
            Debug.LogWarning("BoardController: view が未設定です。");
        }

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
        if (seSource != null && placeSE != null)
        {
            seSource.PlayOneShot(placeSE);
        }
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
        view.PlayDigChainAnimation(res, oldPower, core.power, OnBoardAnimationFinished);  // power は増えない想定

        Debug.Log("BoardController.PlaceAt: 落下あり配置 → アニメ再生");
        return res;
    }
    




}

