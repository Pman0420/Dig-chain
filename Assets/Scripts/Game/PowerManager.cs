using UnityEngine;

// ゲーム全体で共有する「現在のPower」を管理するクラス
public class PowerManager : MonoBehaviour
{
    // ゲームロジック上の「本当のパワー」
    public int LogicalPower { get; private set; }

    // UI用のなめらかなゲージ値
    public float CurrentPower { get; private set; }

    private float targetPower;    // この掘削が全部終わったときの最終値
    private float gainPerBlock;   // ブロック1個あたり何ポイント増えるか
    private int blocksRemaining;  // この掘削でまだ反映していないブロック数

    public int TotalScore { get; private set; }

    // 掘削開始時に呼ぶ
    public void BeginGain(int oldPower, int newPower, int totalBlocks)
    {
        // 論理上の最終値は newPower に確定
        LogicalPower = newPower;

        // ゲージ表示は old → new へ徐々に伸ばす
        CurrentPower = oldPower;
        targetPower = newPower;
        Debug.Log($"PowerManager.BeginGain: old={oldPower}, new={newPower}, blocks={totalBlocks}");

        if (totalBlocks <= 0)

            if (totalBlocks <= 0)
        {
            gainPerBlock = 0f;
            blocksRemaining = 0;
            // 念のため揃えておく
            CurrentPower = targetPower;
            return;
        }

        float gain = newPower - oldPower;
        if (gain > 0)
        {
            TotalScore += (int)gain;
        }
        gainPerBlock = gain / totalBlocks;
        blocksRemaining = totalBlocks;
    }

    // ブロックが1個消えたときに呼ぶ
    public void OnBlockCrushed()
    {
        Debug.Log($"PowerManager.OnBlockCrushed before: Current={CurrentPower}, remaining={blocksRemaining}");

        if (blocksRemaining <= 0)
        {
            // 念のため最終値に合わせておく
            CurrentPower = targetPower;
            return;
        }

        CurrentPower += gainPerBlock;
        blocksRemaining--;

        if (blocksRemaining == 0)
        {
            CurrentPower = targetPower;
        }
        Debug.Log($"PowerManager.OnBlockCrushed after: Current={CurrentPower}, remaining={blocksRemaining}");
    }

    // ゲーム全体をリセットしたいとき用
    public void ResetAll()
    {
        LogicalPower = 0;
        CurrentPower = 0;
        targetPower = 0;
        gainPerBlock = 0;
        blocksRemaining = 0;
        TotalScore = 0;

    }
    public void ResetPower()
    {
        LogicalPower = 0;
        CurrentPower = 0;
        targetPower = 0;
        gainPerBlock = 0;
        blocksRemaining = 0;

    }
}
