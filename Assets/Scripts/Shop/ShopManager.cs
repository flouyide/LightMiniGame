using System;
using System.Collections.Generic;
using System.Linq;
using LightMiniGame.Card;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>商店操作结果。</summary>
    public enum ShopResult
    {
        Success,
        NotEnoughGold,
        SoldOut,
        NoRemovalsLeft,
        LibraryFull,
        Invalid
    }

    /// <summary>商店里的一张在售卡牌条目（含价格 / 是否已售）。</summary>
    [Serializable]
    public class ShopCardEntry
    {
        public CardData card;
        public CharacterData ownerCharacter;   // 买到后进入哪个角色的牌库
        public int price;
        public bool sold;
    }

    /// <summary>商店里的一件在售遗物条目（含价格 / 是否已售 / 归属角色）。</summary>
    [Serializable]
    public class ShopRelicEntry
    {
        public RelicData relic;
        public int price;
        public bool sold;
        public CharacterData ownerCharacter;   // 买到后进入哪个角色的遗物库（从对应角色的遗物池中抽出）
    }

    /// <summary>
    /// 商店核心控制器（单例，仿 GlobalCardLibrary 的常驻模式）。
    /// 职责：
    ///  - 每次进店从「总牌库 / 总遗物库」随机抽取商品；
    ///  - 按 Character1 / Character2 比例拆分卡牌抽取；
    ///  - 购买卡牌 / 遗物、删牌（含价格与次数限制）；
    ///  - 货币统一走 ChapterManager.PlayerGold（通过 SpendGold 扣减）。
    ///
    /// 使用：BookUIController 在开局时 Init 绑定 ChapterManager；玩家进入商店页时调用 OpenShop(characters)。
    /// 之后 ShopPanelUI 读取 CardStock / RelicStock / GetRemovableCards 渲染，并调用 BuyCard / BuyRelic / RemoveCard。
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }
        
        public ICardDrawStrategy CardDrawStrategy { get; set; } = new UniformCardDraw();

        /// <summary>商店库存或货币变化后广播（ShopPanelUI 监听以刷新）。</summary>
        public event Action OnStockChanged;

        private ChapterManager _chapterManager;
        private List<CharacterData> _characters = new List<CharacterData>();
        
        [Tooltip("商店全局配置：商品数量、角色卡牌比例、删牌服务等")]
        public ShopConfig shopConfig;
        [Tooltip("总牌库：每个角色的全部卡牌")]
        public MasterCardLibrary masterCardLibrary;
        [Tooltip("总遗物库：游戏内所有遗物")]
        public MasterRelicLibrary masterRelicLibrary;

        [Header("品级刷新概率（卡牌与遗物共用，按金/银/铜权重比例归一化）")]
        [Tooltip("铜品级的刷新概率（权重）")]
        public float bronzeGradeProbability = 60f;
        [Tooltip("银品级的刷新概率（权重）")]
        public float silverGradeProbability = 30f;
        [Tooltip("金品级的刷新概率（权重）")]
        public float goldGradeProbability = 10f;

        /// <summary>获取品级刷新权重（金/银/铜）。供卡牌抽取策略与遗物抽取共用。</summary>
        public float GetGradeWeight(CardGrade grade) => grade switch
        {
            CardGrade.Gold => goldGradeProbability,
            CardGrade.Silver => silverGradeProbability,
            _ => bronzeGradeProbability,
        };

        private readonly List<ShopCardEntry> _cardStock = new List<ShopCardEntry>();
        private readonly List<ShopRelicEntry> _relicStock = new List<ShopRelicEntry>();
        private int _usedRemovalsThisShop;   // 本店已用删牌次数（开新店重置，per-shop 语义）
        private int _globalRemovals;        // 全局累计删牌次数（跨店递增、永不清零），用于费用全局上涨
        private float _discountRatio = 1f;  // 当前商店的统一商品折扣比例：1=不打折。
        // 已获得的“内部福利”对之后每一间普通商店持续生效；特价商店不使用该折扣。
        private float _regularShopDiscountRatio = 1f;

        // ===== 单例 =====
        // 优先复用场景中已放好的 ShopManager（其 Inspector 上的配置才会被读取）；
        // 若场景里没有，则运行时动态创建一个空实例（此时需自行在代码里配置）。
        public static ShopManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<ShopManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }
            var go = new GameObject("ShopManager");
            Instance = go.AddComponent<ShopManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>绑定金币来源（ChapterManager）。可重复调用，幂等。</summary>
        public void Init(ChapterManager cm)
        {
            _chapterManager = cm;
        }

        // ===== 配置（Inspector 拖入；为空时由调用方在抽卡/抽遗物处告警）=====
        private ShopConfig Config => shopConfig;
        private MasterCardLibrary CardLib => masterCardLibrary;
        private MasterRelicLibrary RelicLib => masterRelicLibrary;

        // ===== 对外只读状态 =====
        public int PlayerGold => _chapterManager != null ? _chapterManager.PlayerGold : 0;
        public bool CanAfford(int amount) => PlayerGold >= amount;

        // 本店剩余可删次数（每次开新店重置，per-shop 语义）
        public int RemovalsRemaining => Mathf.Max(0, (Config != null ? Config.removalCount : 0) - _usedRemovalsThisShop);
        // 当前删牌费用：随【全局累计】删牌次数上涨（跨店持续递增），再应用当前商店的统一商品折扣。
        public int CurrentRemovalPrice
        {
            get
            {
                int basePrice = (Config != null ? Config.removalBasePrice : 0)
                                + (Config != null ? Config.removalPriceStep : 0) * _globalRemovals;
                return Mathf.RoundToInt(basePrice * _discountRatio);
            }
        }

        public IReadOnlyList<ShopCardEntry> CardStock => _cardStock;
        public IReadOnlyList<ShopRelicEntry> RelicStock => _relicStock;
        public float CurrentDiscountRatio => _discountRatio;

        // ===== 进店：重新随机抽取全部商品 =====
        // discountRatio: 本次开店的基础折扣比例（0~1）。
        // isRegularShop: true=普通商店；false=事件触发的特价商店。内部福利持续作用于每一间普通商店。
        public void OpenShop(List<CharacterData> characters, float discountRatio = 1f, bool isRegularShop = true)
        {
            _discountRatio = Mathf.Clamp(discountRatio, 0f, 1f);
            if (isRegularShop)
            {
                // 内部福利和普通商店基础折扣取更优惠的一档；不消耗，之后的普通商店继续生效。
                _discountRatio = Mathf.Min(_discountRatio, _regularShopDiscountRatio);
            }

            _characters = characters ?? new List<CharacterData>();
            _usedRemovalsThisShop = 0;   // 仅重置本店计数；_globalRemovals 保留，费用继续上涨
            GlobalCardLibrary.EnsureInstance();

            // 按品级概率刷新：用本组件的金/银/铜概率字段构建加权抽取策略（每次开张重读，Inspector 改动即时生效）
            CardDrawStrategy = new GradeWeightedCardDraw(GetGradeWeight);

            DrawCards();
            DrawRelics();
            OnStockChanged?.Invoke();

            Debug.Log($"[ShopManager] 开张：卡牌 {_cardStock.Count} 张，遗物 {_relicStock.Count} 件，可删牌 {RemovalsRemaining} 次，折扣 {_discountRatio:P0}");
        }

        /// <summary>
        /// 登记“内部福利”对之后所有普通商店持续生效的统一商品折扣。
        /// 特价商店不使用该折扣；重复获得时保留更低价格比例，不叠乘。
        /// </summary>
        public void RegisterRegularShopDiscount(float discountRatio)
        {
            float clampedRatio = Mathf.Clamp(discountRatio, 0f, 1f);
            _regularShopDiscountRatio = Mathf.Min(_regularShopDiscountRatio, clampedRatio);
            Debug.Log($"[ShopManager] 已登记所有普通商店折扣：{_regularShopDiscountRatio:P0}");
        }

        // ===== 抽卡：按 Character1 / Character2 比例拆分 =====
        private void DrawCards()
        {
            _cardStock.Clear();
            var cfg = Config;
            var master = CardLib;
            if (cfg == null || master == null)
            {
                Debug.LogWarning("[ShopManager] 缺少 ShopConfig 或 MasterCardLibrary，无法抽卡");
                return;
            }

            int total = Mathf.Max(0, cfg.shopCardCount);
            CharacterData c1 = _characters.Count > 0 ? _characters[0] : null;
            CharacterData c2 = _characters.Count > 1 ? _characters[1] : null;

            int c1Count = Mathf.RoundToInt(total * cfg.character1CardRatio);
            int c2Count = total - c1Count;

            var exclude = new HashSet<CardData>();
            var drawn = new List<(CardData card, CharacterData owner)>();

            // 角色1 比例
            drawn.AddRange(DrawFromPool(master, c1, c1Count, exclude));
            // 角色2 比例
            drawn.AddRange(DrawFromPool(master, c2, c2Count, exclude));
            // 数量不足：从「全部角色池」补足（角色1/2 池为空或卡片不够时）
            if (drawn.Count < total)
                drawn.AddRange(DrawFromAllPools(master, total - drawn.Count, exclude));

            foreach (var (card, owner) in drawn)
            {
                _cardStock.Add(new ShopCardEntry
                {
                    card = card,
                    ownerCharacter = owner,
                    price = card != null ? Mathf.RoundToInt(card.value * _discountRatio) : 0,
                });
            }
        }

        private List<(CardData card, CharacterData owner)> DrawFromPool(
            MasterCardLibrary master, CharacterData ch, int count, HashSet<CardData> exclude)
        {
            var result = new List<(CardData, CharacterData)>();
            if (ch == null || count <= 0) return result;
            var pool = master.GetCards(ch);
            if (pool == null || pool.Count == 0) return result;

            var picked = CardDrawStrategy.Draw(pool, count, exclude);
            foreach (var c in picked)
            {
                exclude.Add(c);
                result.Add((c, ch));
            }
            return result;
        }

        private List<(CardData card, CharacterData owner)> DrawFromAllPools(
            MasterCardLibrary master, int count, HashSet<CardData> exclude)
        {
            var result = new List<(CardData, CharacterData)>();
            if (count <= 0 || master == null || master.pools == null) return result;

            foreach (var pool in master.pools)
            {
                if (pool == null || pool.character == null) continue;
                var picked = CardDrawStrategy.Draw(master.GetCards(pool.character), count - result.Count, exclude);
                foreach (var c in picked)
                {
                    exclude.Add(c);
                    result.Add((c, pool.character));
                }
                if (result.Count >= count) break;
            }
            return result;
        }

        // ===== 抽遗物（按角色分池，每个角色独立抽取：只排除自己已拥有，互不影响）=====
        private void DrawRelics()
        {
            _relicStock.Clear();
            var cfg = Config;
            var master = RelicLib;
            if (cfg == null || master == null)
            {
                Debug.LogWarning("[ShopManager] 缺少 ShopConfig 或 MasterRelicLibrary，无法抽遗物");
                return;
            }

            int total = Mathf.Max(0, cfg.shopRelicCount);
            CharacterData c1 = _characters.Count > 0 ? _characters[0] : null;
            CharacterData c2 = _characters.Count > 1 ? _characters[1] : null;

            int c1Count = Mathf.RoundToInt(total * cfg.character1RelicRatio);
            int c2Count = total - c1Count;

            // 每个角色独立抽取：只排除自己已拥有的遗物，抽出的结果不影响其它角色（可抽到相同遗物）
            var drawn = new List<(RelicData relic, CharacterData owner)>();
            drawn.AddRange(DrawRelicsForCharacter(master, c1, c1Count));
            drawn.AddRange(DrawRelicsForCharacter(master, c2, c2Count));

            foreach (var (relic, owner) in drawn)
            {
                _relicStock.Add(new ShopRelicEntry
                {
                    relic = relic,
                    ownerCharacter = owner,
                    price = relic != null ? Mathf.RoundToInt(relic.value * _discountRatio) : 0,
                });
            }

            if (drawn.Count < total)
                Debug.Log($"[ShopManager] 可售遗物仅 {drawn.Count} 件（目标 {total}）");
        }

        /// <summary>
        /// 单个角色独立抽遗物：仅排除该角色「自己」已拥有的遗物（不并入其它角色，互不影响）。
        /// 先从自己池抽；数量不足再从全部角色池补足（补足部分仍只排除自己已拥有，且归属回写为本角色）。
        /// </summary>
        private List<(RelicData relic, CharacterData owner)> DrawRelicsForCharacter(
            MasterRelicLibrary master, CharacterData ch, int count)
        {
            var result = new List<(RelicData, CharacterData)>();
            if (ch == null || count <= 0) return result;

            // 仅该角色自己已拥有的遗物（不并入其它角色，从而保证角色间互不影响）
            var exclude = new HashSet<RelicData>();
            var gri = GlobalRelicInventory.Instance;
            if (gri != null)
            {
                var owned = gri.GetRelics(ch);
                if (owned != null) foreach (var r in owned) if (r != null) exclude.Add(r);
            }

            // 先抽自己池
            result.AddRange(DrawRelicsFromPool(master, ch, count, exclude));
            // 数量不足：从全部角色池补足（仍只排除自己已拥有；补足项归属回写为本角色，不影响其它角色抽取）
            if (result.Count < count)
            {
                var filled = DrawRelicsFromAllPools(master, count - result.Count, exclude);
                foreach (var (r, _) in filled) result.Add((r, ch));
            }
            return result;
        }

        private List<(RelicData relic, CharacterData owner)> DrawRelicsFromPool(
            MasterRelicLibrary master, CharacterData ch, int count, HashSet<RelicData> exclude)
        {
            var result = new List<(RelicData, CharacterData)>();
            if (ch == null || count <= 0) return result;
            var pool = master.GetRelics(ch);
            if (pool == null || pool.Count == 0) return result;

            var candidates = new List<RelicData>();
            foreach (var r in pool)
                if (r != null && !exclude.Contains(r)) candidates.Add(r);
            // 品级加权抽取：每件先按金/银/铜概率掷品级，再在组内均匀取
            while (result.Count < count && candidates.Count > 0)
            {
                var r = GradeWeightedPick.Pick(candidates, c => c.grade, GetGradeWeight);
                candidates.Remove(r);
                exclude.Add(r);
                result.Add((r, ch));
            }
            return result;
        }

        private List<(RelicData relic, CharacterData owner)> DrawRelicsFromAllPools(
            MasterRelicLibrary master, int count, HashSet<RelicData> exclude)
        {
            var result = new List<(RelicData, CharacterData)>();
            if (count <= 0 || master == null || master.pools == null) return result;

            // 汇总全部角色池的候选（跨池按品级加权抽取，归属为候选项自带的角色）
            var merged = new List<(RelicData relic, CharacterData owner)>();
            foreach (var pool in master.pools)
            {
                if (pool == null || pool.character == null) continue;
                foreach (var r in pool.relics)
                    if (r != null && !exclude.Contains(r)) merged.Add((r, pool.character));
            }

            while (result.Count < count && merged.Count > 0)
            {
                var pick = GradeWeightedPick.Pick(merged, m => m.relic.grade, GetGradeWeight);
                merged.Remove(pick);
                exclude.Add(pick.relic);
                result.Add(pick);
            }
            return result;
        }

        // ===== 购买卡牌 =====
        public ShopResult BuyCard(ShopCardEntry entry)
        {
            if (entry == null || entry.card == null) return ShopResult.Invalid;
            if (entry.sold) return ShopResult.SoldOut;
            if (!CanAfford(entry.price)) return ShopResult.NotEnoughGold;

            // 牌库容量检查
            if (entry.ownerCharacter != null && entry.ownerCharacter.maxLibrarySize > 0
                && GlobalCardLibrary.Instance != null
                && GlobalCardLibrary.Instance.GetCardCount(entry.ownerCharacter) >= entry.ownerCharacter.maxLibrarySize)
            {
                return ShopResult.LibraryFull;
            }

            if (!Spend(entry.price)) return ShopResult.NotEnoughGold;

            GlobalCardLibrary.EnsureInstance();
            GlobalCardLibrary.Instance.AddCard(entry.ownerCharacter, entry.card);
            entry.sold = true;
            OnStockChanged?.Invoke();
            return ShopResult.Success;
        }

        // ===== 购买遗物 =====
        public ShopResult BuyRelic(ShopRelicEntry entry)
        {
            if (entry == null || entry.relic == null) return ShopResult.Invalid;
            if (entry.sold) return ShopResult.SoldOut;
            if (!CanAfford(entry.price)) return ShopResult.NotEnoughGold;

            if (!Spend(entry.price)) return ShopResult.NotEnoughGold;

            // 归入对应角色的遗物库（仿 BuyCard 的 ownerCharacter 路由）
            GlobalRelicInventory.EnsureInstance();
            if (entry.ownerCharacter != null)
                GlobalRelicInventory.Instance.Add(entry.ownerCharacter, entry.relic);
            entry.sold = true;
            OnStockChanged?.Invoke();
            return ShopResult.Success;
        }

        // ===== 删牌 =====
        public ShopResult RemoveCard(CardInstance card, CharacterData owner)
        {
            if (card == null || owner == null) return ShopResult.Invalid;
            if (RemovalsRemaining <= 0) return ShopResult.NoRemovalsLeft;

            int price = CurrentRemovalPrice;
            if (!CanAfford(price)) return ShopResult.NotEnoughGold;
            if (!Spend(price)) return ShopResult.NotEnoughGold;

            GlobalCardLibrary.EnsureInstance();
            GlobalCardLibrary.Instance.RemoveCard(owner, card.instanceId);
            _usedRemovalsThisShop++;
            _globalRemovals++;
            OnStockChanged?.Invoke();
            return ShopResult.Success;
        }

        // ===== 当前可删的卡（跨所有角色牌库）=====
        public List<(CardInstance card, CharacterData owner)> GetRemovableCards()
        {
            var result = new List<(CardInstance, CharacterData)>();
            if (GlobalCardLibrary.Instance == null) return result;
            foreach (var lib in GlobalCardLibrary.Instance.AllLibraries)
            {
                if (lib == null) continue;
                foreach (var c in lib.All)
                    result.Add((c, lib.owner));
            }
            return result;
        }

        // ===== 内部：扣钱（走 ChapterManager，自动触发金币 UI 刷新）=====
        private bool Spend(int amount)
        {
            if (_chapterManager == null) return false;
            return _chapterManager.SpendGold(amount);
        }
    }
}
