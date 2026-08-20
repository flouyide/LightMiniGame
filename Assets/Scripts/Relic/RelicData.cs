using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 遗物数据（ScriptableObject）。仿《杀戮尖塔》遗物：全局持有，拥有价格 value。
    /// 在编辑器通过菜单 CardGame/Relic Data 创建。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Relic Data", fileName = "NewRelic")]
    public class RelicData : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("稳定唯一ID（如 iron_ring），持久化/查询用")]
        public string relicId;

        [Tooltip("显示名（如 铁戒指）")]
        public string relicName;

        [TextArea(2, 4)]
        public string description;

        public Sprite icon;

        [Header("商店")]
        [Tooltip("购买价格（仿 CardData.value 的商店价值字段）")]
        public int value = 100;

        [Header("品级（商店按品级概率刷新）")]
        public CardGrade grade = CardGrade.Bronze;

        [Header("排序")]
        [Tooltip("在总遗物库中的排序权重，越大越靠前（可选）")]
        public int sortOrder;

        [Header("效果脚本")]
        [Tooltip("实现该遗物效果的效果类全名（含命名空间，如 LightMiniGame.RelicEffects.IronRingEffect）。运行时通过反射实例化")]
        public string effectScriptName;

        [Tooltip("效果参数（含义由 Effect Script 决定，见效果类注释；未配置时效果类用各自默认值）。\n" +
                 "例：FirstHitVulnerableEffect 参数[0]=首次命中额外伤害比例（0.25 即 +25%）；\n" +
                 "DeathSanityLossEffect 参数[0]=死亡扣除的玩家理智")]
        public List<float> effectParams = new List<float>();

        [Tooltip("效果字符串参数（与 effectParams 对应的字符串版，含义由 Effect Script 决定）。\n" +
                 "例：DamageTransferEffect 字符串参数[0]=伤害转移目标的 enemyName（EnemyConfig 里的敌人名）")]
        public List<string> effectStringParams = new List<string>();

        [Tooltip("效果 Object 参数（与 effectParams 对应的资产引用版，含义由 Effect Script 决定）。\n" +
                 "例：DamageTransferEffect Object 参数[0]=伤害转移目标的 EnemyConfig 资产（直接拖入，避免拼字符串出错）")]
        public List<UnityEngine.Object> effectObjectParams = new List<UnityEngine.Object>();

#if UNITY_EDITOR
        [Tooltip("编辑器中拖入实现遗物效果的脚本文件（自动填充 effectScriptName）")]
        public MonoScript effectScript;

        /// <summary>编辑器中拖入脚本后自动同步类名到 effectScriptName，保证运行时可用</summary>
        private void OnValidate()
        {
            if (effectScript != null)
            {
                var type = effectScript.GetClass();
                if (type != null)
                    effectScriptName = type.FullName;
            }
        }
#endif
    }
}
