using UnityEngine;

// Rigidbody2D を使った横移動 + ジャンプ + 足元掘り
public class PlayerController : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("ジャンプ・接地判定")]
    [SerializeField] private float jumpSpeed = 8f;
    [SerializeField] private Transform groundCheck;      // 足元のチェック用
    [SerializeField] private float groundCheckRadius = 0.1f;  // groundCheckの半径（接地判定範囲）
    [SerializeField] private LayerMask groundLayer;      // 地面レイヤー

    [Header("掘削設定")]
    [SerializeField] private BoardController board;

    private Rigidbody2D rb;
    private bool isGrounded = false;
    private bool isTouchingWall = false;  // 壁に接触しているかどうか

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("PlayerController: Rigidbody2D がありません");
        }
    }

    private void Update()
    {
        HandleMove();
        HandleJump();
        HandleDigInput();
        Debug.Log($"isGrounded: {isGrounded}, isTouchingWall: {isTouchingWall}");
    }

    private void FixedUpdate()
    {
        CheckGrounded();  // 接地判定
        CheckWall();      // 壁判定
    }

    // 左右移動：Rigidbody2D の速度を書き換える
    private void HandleMove()
    {
        float h = Input.GetAxisRaw("Horizontal"); // -1, 0, 1
        if (rb == null) return;

        // 壁に接触している場合でも、地面にいるときは横移動できる
        if (isTouchingWall && !isGrounded)
        {
            // 壁に接触しているときは横方向の移動を止める（空中でのみ有効）
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
        else
        {
            // 地面にいるときは、通常通り横方向に移動
            rb.velocity = new Vector2(h * moveSpeed, rb.velocity.y);  // 横方向速度はそのまま、縦方向の速度は物理に任せる
        }
    }

    // ジャンプ入力
    private void HandleJump()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpSpeed);  // ジャンプ速度を設定
        }
    }

    // 足元にコライダがあるかで接地判定
    private void CheckGrounded()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        // GroundCheck の位置の小さな円と groundLayer で Overlap をチェック
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    // 壁との接触をチェック
    private void CheckWall()
    {
        // 壁方向に移動しているときのみチェック
        float h = Input.GetAxisRaw("Horizontal");
        if (h != 0)
        {
            // 壁との接触判定
            isTouchingWall = Physics2D.OverlapCircle(groundCheck.position + new Vector3(h * 0.5f, 0f, 0f), groundCheckRadius, groundLayer);
        }
        else
        {
            isTouchingWall = false;
        }
    }

    // 掘るボタン入力（地上にいるときだけ掘れる）
    private void HandleDigInput()
    {
        if (!isGrounded) return;  // 空中では掘れない

        if (Input.GetButtonDown("Dig"))
        {
            DigAtFeet();
        }
    }

    // プレイヤーの足元の1マス下を掘る
    // プレイヤーの足元の1マス下を掘る
  // プレイヤーの足元の1マス下を掘る
private void DigAtFeet()
{
    if (board == null || board.core == null || board.view == null)
    {
        Debug.LogWarning("PlayerController: board / core / view が設定されていません");
        return;
    }

    float cell = board.view.cellSize;

    // プレイヤーの位置（中心）を Board ローカル座標へ
    Vector3 worldPos = transform.position;
    Vector3 localPos = board.view.transform.InverseTransformPoint(worldPos);

    // 足元1マス下
    float feetLocalY = localPos.y - cell;

    int gridX = Mathf.RoundToInt(localPos.x / cell);
    int gridY = Mathf.RoundToInt(-feetLocalY / cell);

    if (gridX < 0 || gridX >= board.core.W || gridY < 0 || gridY >= board.core.H)
    {
        Debug.Log($"Player Dig: 盤面外 grid=({gridX},{gridY})");
        return;
    }

    Debug.Log($"Player Dig: ターゲットマス=({gridX},{gridY}), 値={board.core.grid[gridY, gridX]}");

    // ★ 本体処理は BoardController に任せる
    DigChainResult res = board.DigAt(gridY, gridX);

    Debug.Log($"Player Dig: 連鎖回数 = {res.chainCount}, Power = {board.core.power}");
}



    // Sceneビューで接地判定の円が見えるように
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);  // 接地判定範囲を視覚化
    }
}
