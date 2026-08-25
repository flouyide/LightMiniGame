using LightMiniGame.Relic;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：董事长像。
    ///
    /// 拾起（获得）时立即发放指定数量的金币，属于局外即时一次性效果，
    /// 只在 OnGain 中执行一次，不监听战斗生命周期，也不需要 OnLost 清理。
    ///
    /// 可配置参数（选中“董事长像”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 拾起时获得的金币数量，默认 300。
    /// </summary>
    public class ChairmanPortraitEffect : RelicEffectBase
    {
        public const int DefaultGoldGain = 300;

        public override void OnGain(RelicEffectContext ctx)
        {
            ChapterManager chapter = ctx?.chapter;
            if (chapter == null)
            {
                Debug.LogWarning("[ChairmanPortrait] 找不到 ChapterManager，无法发放金币");
                return;
            }

            int gold = Mathf.Max(0, Mathf.RoundToInt(GetEffectParam(ctx.relic, 0, DefaultGoldGain)));
            chapter.AddGold(gold);
            Debug.Log($"[ChairmanPortrait] 拾起获得 +{gold} 金币");
        }
    }
}
