using UnityEngine;

// Rigidbody2D を使った横移動 + ジャンプ + 足元掘り
public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("ジャンプ・接地判定")]
    [SerializeField] private float jumpSpeed = 8f;
    [SerializeField] private Transform groundCheck;      // 足元のチェック用（掘り・Gizmo 用にも使用）
    [SerializeField] private float groundCheckRadius = 0.1f;  // groundCheckの半径（接地判定範囲）※フォールバック用
    [SerializeField] private LayerMask groundLayer;      // 地面レイヤー

    [Header("掘削設定")]
    [SerializeField] private BoardController board;

    [Header("キャラクターのスプライト設定")]
    [SerializeField] private Sprite idleSprite;    // キャラクターの待機状態のスプライト
    [SerializeField] private Sprite jumpSprite;    // ジャンプ状態のスプライト
    [SerializeField] private Sprite moveSprite;    // 移動状態のスプライト

    private SpriteRenderer spriteRenderer;  // SpriteRenderer

    private Rigidbody2D rb;
    private BoxCollider2D col;      // ★ 追加：接地判定用に BoxCollider2D を使用
    private bool isGrounded = false;
    private bool isTouchingWall = false;  // 壁に接触しているかどうか

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("PlayerController: Rigidbody2D がありません");
        }

        col = GetComponent<BoxCollider2D>();
        if (col == null)
        {
            Debug.LogWarning("PlayerController: BoxCollider2D がありません。接地判定は groundCheck の OverlapCircle にフォールバックします。");
        }
        spriteRenderer = GetComponent<SpriteRenderer>();  // SpriteRendererを取得
        if (spriteRenderer == null)
        {
            Debug.LogError("PlayerController: SpriteRenderer がありません");
        }
    }

    private void Update()
    {
        // ゲームオーバー時は操作無効
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            // rb.velocity = Vector2.zero; // 完全停止したいなら有効化
            return;
        }

        // ★ 毎フレーム最初に接地・壁判定を更新
        CheckGrounded();
        CheckWall();

        HandleMove();
        HandleJump();
        HandleDigInput();
        HandlePlaceInput();

        // デバッグ表示
        //Debug.Log($"isGrounded: {isGrounded}, isTouchingWall: {isTouchingWall}, velY={rb.velocity.y}");
    }

    private void FixedUpdate()
    {
        // 物理ステップごとに再確認しても良いが、
        // ここでの再計算は任意。Update で毎フレームやっているので必須ではない。
        // CheckGrounded();
        // CheckWall();
    }

    // 左右移動：Rigidbody2D の速度を書き換える
    private void HandleMove()
    {
        if (rb == null) return;

        float h = Input.GetAxisRaw("Horizontal"); // -1, 0, 1

        // 壁に接触している場合でも、地面にいるときは横移動できる
        if (isTouchingWall && !isGrounded)
        {
            // 壁に接触しているときは横方向の移動を止める（空中でのみ有効）
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
        else
        {
            // 通常の横移動
            rb.velocity = new Vector2(h * moveSpeed, rb.velocity.y);
        }
        if (h != 0)
        {
            // 移動中のスプライトに切り替え
            spriteRenderer.sprite = moveSprite;
        }
        else
        {
            // 待機中のスプライトに切り替え
            spriteRenderer.sprite = idleSprite;
        }
    }

    // ジャンプ入力
    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump"))
        {
            Debug.Log($"[Player] Jump input detected. isGrounded={isGrounded}, velY(before)={rb.velocity.y}");
        }

        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            if (rb == null) return;

            Vector2 v = rb.velocity;
            v.y = jumpSpeed;
            rb.velocity = v;
            // ジャンプ中のスプライトに切り替え
            spriteRenderer.sprite = jumpSprite;
            Debug.Log($"[Player] Jump applied. velY(after)={rb.velocity.y}");
        }
        else if (!isGrounded && Input.GetButtonDown("Jump"))
        {
            // 接地していないのにジャンプしようとした場合のログ
            Debug.Log("[Player] Jump cancelled because not grounded.");
        }
    }

    // 足元にコライダがあるかで接地判定（BoxCast ベース）
    private void CheckGrounded()
    {
        bool newGrounded = false;

        if (col != null)
        {
            // ★ プレイヤーの BoxCollider2D から少しだけ下に BoxCast して接地判定
            float extraHeight = 0.05f;
            RaycastHit2D hit = Physics2D.BoxCast(
                col.bounds.center,
                col.bounds.size,
                0f,
                Vector2.down,
                extraHeight,
                groundLayer
            );

            newGrounded = (hit.collider != null);
        }
        else if (groundCheck != null)
        {
            // BoxCollider2D がない場合は従来通り groundCheck の OverlapCircle を使用
            newGrounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }
        else
        {
            // 判定元が無い場合は常に空中扱い
            newGrounded = false;
        }

        if (newGrounded != isGrounded)
        {
            Debug.Log($"[Ground] {isGrounded} -> {newGrounded}");
        }

        isGrounded = newGrounded;
    }

    // 壁との接触をチェック
    private void CheckWall()
    {
        float h = Input.GetAxisRaw("Horizontal");

        if (h != 0f)
        {
            // 壁方向の少し先を判定する
            Vector3 origin;

            if (col != null)
            {
                // コライダ中心から少し横にずらした位置
                origin = col.bounds.center + new Vector3(h * (col.bounds.extents.x + 0.05f), 0f, 0f);
            }
            else if (groundCheck != null)
            {
                // フォールバック：groundCheck 基準で少し横
                origin = groundCheck.position + new Vector3(h * 0.5f, 0f, 0f);
            }
            else
            {
                origin = transform.position + new Vector3(h * 0.5f, 0f, 0f);
            }

            isTouchingWall = Physics2D.OverlapCircle(origin, groundCheckRadius, groundLayer);
        }
        else
        {
            isTouchingWall = false;
        }
    }

    // 掘るボタン入力（地上にいるときだけ掘れる）
    // 掘るボタン入力（地上にいるときだけ掘れる）
    private void HandleDigInput()
    {
        // Jキーで掘る
        if (Input.GetButtonDown("Dig"))
        {
            Debug.Log($"[Player] DIG input detected. isGrounded={isGrounded}");

            if (!isGrounded)
            {
                Debug.Log("[Player] Dig cancelled because not grounded.");
                return; // 空中では掘れない
            }

            DigAtFeet();
        }
    }

    // プレイヤーの足元の1マス下を掘る
    private void DigAtFeet()
    {
        if (board == null || board.view == null)
        {
            Debug.LogWarning("PlayerController: board / view が設定されていません");
            return;
        }

        Vector3 feetWorldPos = (groundCheck != null)
            ? groundCheck.position
            : transform.position;

        float cell = board.view.cellSize;

        // 足元から少し下にずらした位置をターゲット
        Vector3 targetWorldPos = feetWorldPos + Vector3.down * (0.6f * cell);

        int gridY, gridX;
        // ★ ここで Nearest 版を使う
        if (!board.view.WorldToCellNearest(targetWorldPos, out gridY, out gridX))
        {
            Debug.Log($"Player Dig: 盤面外 grid=({gridX},{gridY})");
            return;
        }

        Debug.Log($"Player Dig: 掘りターゲット=({gridX},{gridY})");

        // 本体処理は BoardController に任せる
        DigChainResult res = board.DigAt(gridY, gridX);
        Debug.Log($"Player Dig: 連鎖回数 = {res.chainCount}");
    }

    // ブロックを足元1マス下に「置く」処理
    private void HandlePlaceInput()
    {
        // Input Manager で "Place" を設定してある前提
        if (!Input.GetButtonDown("Place")) return;

        if (board == null || board.view == null)
        {
            Debug.LogWarning("PlayerController: board / view が設定されていません");
            return;
        }

        Vector3 feetWorldPos = (groundCheck != null)
            ? groundCheck.position
            : transform.position;

        float cell = board.view.cellSize;
        Vector3 targetWorldPos = feetWorldPos + Vector3.down * (0.6f * cell);

        int gridY, gridX;
        if (!board.view.WorldToCellNearest(targetWorldPos, out gridY, out gridX))
        {
            Debug.Log($"Player Place: 盤面外 grid=({gridX},{gridY})");
            return;
        }

        Debug.Log($"Player Place: 設置ターゲット=({gridX},{gridY})");

        // ★ 実際のロジックは BoardController に任せる
        board.PlaceAt(gridY, gridX);
    }

    // Sceneビューで接地判定の円が見えるように
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);  // 接地判定範囲を視覚化（目安）
    }
}
