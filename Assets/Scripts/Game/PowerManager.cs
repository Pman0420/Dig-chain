using UnityEngine;

// ゲーム全体で共有する「現在のPower」を管理するクラス
public class PowerManager : MonoBehaviour
{
    //[SerializeField]
    //private int maxPower = 100;   // 将来 UI の最大値などに使える

    // 現在のPower（他のスクリプトはここを見る）
    public float CurrentPower { get; private set; }

    private float targetPower;    // この掘削が全部終わったときの理論値（= core.power）
    private float gainPerBlock;   // ブロック1個あたり何ポイント増えるか
    private int blocksRemaining;  // この掘削でまだ反映していないブロック数

    // 掘削開始時に呼ぶ
    public void BeginGain(int oldPower, int newPower, int totalBlocks)
    {
        CurrentPower = oldPower;
        targetPower = newPower;

        if (totalBlocks <= 0)
        {
            gainPerBlock = 0f;
            blocksRemaining = 0;
            return;
        }

        float gain = newPower - oldPower;
        gainPerBlock = gain / totalBlocks;
        blocksRemaining = totalBlocks;
    }

    // ブロックが1個消えたときに呼ぶ
    public void OnBlockCrushed()
    {
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
    }
}
