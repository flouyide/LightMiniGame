using LightMiniGame.Relic;
using LightMiniGame.Shop;
using UnityEngine;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 通用遗物：内部福利。
    ///
    /// 获得后，为之后所有普通商店登记统一商品折扣。
    /// 折扣覆盖卡牌、遗物与删牌服务；事件触发的特价商店不使用本效果。
    ///
    /// 可配置参数（选中“内部福利”RelicData 资产 -> Inspector）：
    ///   Effect Params [0] = 所有普通商店的价格比例，默认 0.5（5 折）。
    ///   取值会夹取至 0..1；多个折扣同时生效时，保留价格比例更低的一档，
    ///   不会将两张“内部福利”叠成 2.5 折。
    ///
    /// 该效果是局外即时效果，因此只在 OnGain 中登记一次，不监听战斗生命周期。
    /// </summary>
    public class InternalBenefitEffect : RelicEffectBase
    {
        public const float DefaultDiscountRatio = 0.5f;

        public override void OnGain(RelicEffectContext ctx)
        {
            float discountRatio = Mathf.Clamp01(GetEffectParam(ctx.relic, 0, DefaultDiscountRatio));
            var shop = ShopManager.EnsureInstance();
            if (shop == null)
            {
                Debug.LogWarning("[InternalBenefit] 找不到 ShopManager，无法登记普通商店持续折扣");
                return;
            }

            shop.RegisterRegularShopDiscount(discountRatio);
            Debug.Log($"[InternalBenefit] 已为之后所有普通商店登记 {discountRatio:P0} 折扣");
        }
    }
}
