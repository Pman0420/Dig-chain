using UnityEngine;

// 連鎖に応じてキャラリアクションを出す
public class ChainReactionCharacter : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Cheer Thresholds")]
    [SerializeField] private int cheerChain = 40;      // 20以上で喜ぶ
    [SerializeField] private int bigCheerChain = 80;   // 40以上で大喜び
    [SerializeField] private int superCheerChain = 100; // 60以上で超大喜び

    private static readonly int TrgCheer = Animator.StringToHash("Cheer");
    private static readonly int TrgBigCheer = Animator.StringToHash("BigCheer");
    private static readonly int TrgSuperCheer = Animator.StringToHash("SuperCheer");
    private static readonly int TrgSad = Animator.StringToHash("Sad"); // 任意：ミス時など
    private static readonly int TrgOver = Animator.StringToHash("GameOver"); // 任意：ミス時など
    private static readonly int TrgIdle = Animator.StringToHash("Idle");

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    // BoardController から呼ぶ入口
    public void OnChainResolved(int chainCount, bool isMistake)
    {
        Debug.Log($"[ChainReaction] CALLED chain={chainCount}, mistake={isMistake}");

        if (animator == null)
        {
            Debug.LogError("[ChainReaction] Animator is NULL");
            return;
        }

        if (isMistake)
        {
            // 任意：色ミス等で落ち込む
            animator.SetTrigger(TrgSad);
            return;
        }

        if (chainCount >= superCheerChain)
        {
            animator.SetTrigger(TrgSuperCheer);
        }
        else if (chainCount >= bigCheerChain)
        {
            animator.SetTrigger(TrgBigCheer);
        }
        else if (chainCount >= cheerChain)
        {
            animator.SetTrigger(TrgCheer);
        }
        else if (chainCount >= 0)
        {
            animator.SetTrigger(TrgIdle);
        }
        else if (chainCount < 0)
        {
            animator.SetTrigger(TrgOver);
        }
        // 1連鎖以下は反応なし（好みで Idle/Smile などを追加してもOK）
    }
}
