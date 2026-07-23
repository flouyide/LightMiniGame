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

    /// <summary>商店里的一件在售遗物条目（含价格 / 是否已售）。</summary>
    [Serializable]
    public class ShopRelicEntry
    {
        public RelicData relic;
        public int price;
        public bool sold;
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

        private ShopConfig _config;
        private bool _configLoaded;
        private MasterCardLibrary _cardLibrary;
        private bool _cardLibraryLoaded;
        private MasterRelicLibrary _relicLibrary;
        private bool _relicLibraryLoaded;

        private readonly List<ShopCardEntry> _cardStock = new List<ShopCardEntry>();
        private readonly List<ShopRelicEntry> _relicStock = new List<ShopRelicEntry>();
        private int _usedRemovals;

        // ===== 单例 =====
        public static ShopManager EnsureInstance()
        {
            if (Instance != null) return Instance;
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

        // ===== 配置 / 资源（Resources.Load，仅首次加载；缺失则缓存 null 走空库存）=====
        private ShopConfig Config
        {
            get
            {
                if (!_configLoaded) { _config = ShopConfig.Load(); _configLoaded = true; }
                return _config;
            }
        }
        private MasterCardLibrary CardLib
        {
            get
            {
                if (!_cardLibraryLoaded) { _cardLibrary = MasterCardLibrary.Load(); _cardLibraryLoaded = true; }
                return _cardLibrary;
            }
        }
        private MasterRelicLibrary RelicLib
        {
            get
            {
                if (!_relicLibraryLoaded) { _relicLibrary = MasterRelicLibrary.Load(); _relicLibraryLoaded = true; }
                return _relicLibrary;
            }
        }

        // ===== 对外只读状态 =====
        public int PlayerGold => _chapterManager != null ? _chapterManager.PlayerGold : 0;
        public bool CanAfford(int amount) => PlayerGold >= amount;

        public int RemovalsRemaining => Mathf.Max(0, (Config != null ? Config.removalCount : 0) - _usedRemovals);
        public int CurrentRemovalPrice => (Config != null ? Config.removalBasePrice : 0)
                                          + (Config != null ? Config.removalPriceStep : 0) * _usedRemovals;

        public IReadOnlyList<ShopCardEntry> CardStock => _cardStock;
        public IReadOnlyList<ShopRelicEntry> RelicStock => _relicStock;

        // ===== 进店：重新随机抽取全部商品 =====
        public void OpenShop(List<CharacterData> characters)
        {
            _characters = characters ?? new List<CharacterData>();
            _usedRemovals = 0;
            GlobalCardLibrary.EnsureInstance();

            DrawCards();
            DrawRelics();
            OnStockChanged?.Invoke();

            Debug.Log($"[ShopManager] 开张：卡牌 {_cardStock.Count} 张，遗物 {_relicStock.Count} 件，可删牌 {RemovalsRemaining} 次");
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
                    price = card != null ? card.value : 0,
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
                var picked = CardDrawStrategy.Draw(pool.cards, count - result.Count, exclude);
                foreach (var c in picked)
                {
                    exclude.Add(c);
                    result.Add((c, pool.character));
                }
                if (result.Count >= count) break;
            }
            return result;
        }

        // ===== 抽遗物 =====
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
            var pool = master.allRelics ?? new List<RelicData>();

            // 洗牌后取前 total 张
            var bag = new List<RelicData>(pool);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            int n = Mathf.Min(total, bag.Count);
            for (int i = 0; i < n; i++)
            {
                _relicStock.Add(new ShopRelicEntry
                {
                    relic = bag[i],
                    price = bag[i] != null ? bag[i].value : 0,
                });
            }
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

            RelicInventory.EnsureInstance();
            RelicInventory.Instance.Add(entry.relic);
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
            _usedRemovals++;
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
