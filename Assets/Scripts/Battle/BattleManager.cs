using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using TMPro;
using LightMiniGame.Card;
using LightMiniGame.CardEditor;
using LightMiniGame.Relic;
using Random = UnityEngine.Random;

/// <summary>
/// 战斗管理器（双角色回合制）。
/// 流程: 回合开始(AP=3,抽3牌) → 玩家出牌 → 玩家结束 → 敌人行动(按P跳过) → 回合结束 → 循环
/// 每个角色拥有独立的抽牌堆/弃牌堆/消耗堆。切换角色时当前手牌洗入该角色弃牌堆。
/// </summary>
public class BattleManager : MonoBehaviour
{
    // ========================================================================
    // Inspector 字段
    // ========================================================================

    [Header("角色配置")]
    [Tooltip("游戏配置（含角色列表）")]
    [SerializeField] private GameConfig gameConfig;

    [Header("卡牌编辑器初始牌组（可选）")]
    [Tooltip("如果填写，战斗开始时用这些 CardEntry 卡牌代替角色的 startingLibrary。每个角色一组。")]
    [SerializeField] private List<CardEntry> character1Cards;
    [SerializeField] private List<CardEntry> character2Cards;

    [Header("运行时属性来源（持久基础属性运行时副本）")]
    [Tooltip("ChapterManager 持有持久基础属性（力量/敏捷/吸血/暴击率/暴伤）的运行时副本，单局内跨战斗保留。战斗开始时从此读取。留空则回退到 PlayerConfig（仅初始值，不含事件累积）")]
    [SerializeField] private ChapterManager chapterManager;

    [Header("低理智特效")]
    [Tooltip("理智低于 PlayerConfig.sanityThreshold 时启用的 Volume（如信号干扰后处理），否则禁用。留空则无特效")]
    [SerializeField] private Volume lowSanityVolume;

    [Header("卡牌预制体（按类型）")]
    [SerializeField] private GameObject attackCardPrefab;
    [FormerlySerializedAs("armorCardPrefab")] [SerializeField] private GameObject skillCardPrefab;
    [FormerlySerializedAs("buffCardPrefab")] [SerializeField] private GameObject abilityCardPrefab;

    [Header("玩家属性（双角色共享）")]
    [SerializeField] private int playerMaxHP = 100;
    [SerializeField] private int playerArmor = 0;
    [SerializeField] private int playerStrength = 0;
    [SerializeField] private int playerDexterity = 0;

    [Header("玩家属性来源（持久基础属性）")]
    [Tooltip("可选：配置玩家持久基础属性（力量/敏捷/吸血/暴击率/暴击伤害），由特殊事件 ModifyAttribute 修改，战斗开始时读入替换上方临时变量")]
    [SerializeField] private PlayerConfig playerConfig;

    [Header("默认敌人（测试用）")]
    [Tooltip("默认敌人列表（含位置）；仅在 Battle 事件的 PageEventData.enemies 未指定时作为回退使用")]
    [SerializeField] private List<EnemySpawnInfo> defaultEnemies = new();

    [Header("回合设置")]
    [SerializeField] private int maxActionPoints = 3;
    [SerializeField] private int drawPerTurn = 3;
    [SerializeField] private int initialDraw = 3;
    [SerializeField] private int handLimit = 10;

    [Header("UI引用 - 玩家")]
    [SerializeField] private HandCardLayout handLayout;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI actionPointText;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private TextMeshProUGUI strengthText;
    [SerializeField] private TextMeshProUGUI dexterityText;
    [SerializeField] private Slider playerHPBar;

    [Header("UI引用 - 理智")]
    [SerializeField] private TextMeshProUGUI sanityText;
    [SerializeField] private Slider sanityBar;

    [Header("UI引用 - 敌人（多敌）")]
    [Tooltip("单个敌人视图预制体（挂 EnemyView 组件）。每个敌人生成一个实例")]
    [SerializeField] private EnemyView enemyViewPrefab;
    [Tooltip("敌人容器（RectTransform）。敌人视图按 EnemySpawnInfo.anchoredPosition 摆放在其下")]
    [SerializeField] private RectTransform enemyContainer;
    [Tooltip("相邻敌人行动之间的间隔秒数")]
    [SerializeField] private float enemyActionInterval = 0.5f;

    [Header("UI引用 - 敌人技能卡面")]
    [Tooltip("敌人出招时显示的技能卡面板（Image + 子 TextMeshProUGUI）")]
    [SerializeField] private GameObject enemySkillCard;
    [SerializeField] private Image enemySkillCardImage;
    [SerializeField] private TextMeshProUGUI enemySkillNameText;
    [SerializeField] private TextMeshProUGUI enemySkillDescText;
    [Tooltip("技能卡显示持续时间（秒）")]
    [SerializeField] private float enemySkillCardDuration = 2.5f;
    [Tooltip("技能卡淡入淡出时间（秒）")]
    [SerializeField] private float enemySkillCardFadeTime = 0.5f;

    // 敌人出牌时动态生成的玩家同款卡面（用于打断时清理，避免泄漏）
    private GameObject _enemyPlayedCard;

    [Header("UI引用 - 回合")]
    [SerializeField] private TextMeshProUGUI phaseHintText;
    [SerializeField] private Button endTurnButton;

    [Header("UI引用 - 角色切换")]
    [SerializeField] private Button switchCharacterButton;
    [SerializeField] private TextMeshProUGUI activeCharNameText;
    [SerializeField] private TextMeshProUGUI inactiveCharNameText;
    [SerializeField] private Image activeCharPortrait;
    [SerializeField] private Image inactiveCharPortrait;
    [SerializeField] private GameObject switchAvailableIndicator;
    [SerializeField] private GameObject switchUsedIndicator;

    [Header("UI引用 - 设置")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanelPrefab;

    [Header("UI引用 - 结果")]
    [Tooltip("战利品结算面板（挂在 BattleCanvas 下的 LootPanel）。战斗胜利时启用并按掉落表显示奖励按钮，点击其继续按钮回到局外（替代原 VictoryPanel）")]
    [SerializeField] private LootPanelUI lootPanel;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Button quitButton;

    [Header("UI引用 - 黑暗模式（理智转阶段）")]
    [Tooltip("全屏暗色遮罩 Image，理智转阶段时淡入。留空则不显示遮罩。")]
    [SerializeField] private Image darkOverlay;
    [Tooltip("黑暗遮罩目标透明度（0-1）")]
    [SerializeField] private float darkOverlayAlpha = 0.3f;
    [Tooltip("黑暗遮罩淡入持续时间（秒）")]
    [SerializeField] private float darkOverlayFadeDuration = 1f;

    [Header("UI引用 - 战斗背景")]
    [Tooltip("战斗背景容器 GameObject；其下的 Image 组件用于显示战斗背景（正常 / 低理智）。留空则不切换背景。")]
    [SerializeField] private GameObject background;


    // ========================================================================
    // 运行时状态
    // ========================================================================

    private class CharBattleState
    {
        public CharacterData data;
        public List<CardData> drawPile = new();
        public List<CardData> discardPile = new();
        public List<CardData> consumedPile = new();
    }

    private CharBattleState[] _chars = new CharBattleState[2];
    private int _activeCharIdx = 0;
    private bool _hasSwitchedThisTurn = false;

    private readonly List<CardData> _hand = new();
    private int _playerHP;
    private int _playerArmor;
    // 基础值（从局外读入，不被 buff 修改）
    private int _playerBaseStrength;
    private int _playerBaseDexterity;
    private int _playerBaseLifesteal;
    private int _playerBaseCritRate;
    private int _playerBaseCritDamage;
    // 旧字段保留作为运行时缓存（= 基础值，兼容旧路径直接修改）
    private int _playerStrength;
    private int _playerDexterity;
    private int _playerLifesteal;
    private int _playerCritRate;
    private int _playerCritDamage;
    private int _playerSanity;
    private int _playerMaxSanity;
    private int _sanityThreshold;   // 理智阈值：玩家理智低于此值时所有敌人进入低理智阶段
    private int _playerFortune;     // 福报值：融合重分配总值加成，无上限

    // === 伤害倍率（以百分比存储，100 = 1.0倍；从 PlayerConfig / EnemyConfig 读入） ===
    private int _playerDamageMultiplier = 100;       // 玩家造成伤害倍率（来自 PlayerConfig）
    private int _playerDamageTakenMultiplier = 100;  // 玩家受击倍率（来自 PlayerConfig）

    private GameRuleConfig _ruleConfig;

    // 遗物效果扩展：过载时所有手牌费用+1（由枪械师初始遗物 GunsmithHeatRelicEffect 驱动）
    private int _handCostBonus = 0;

    // 遗物时机事件：只在玩家手动打出的原始卡牌效果结算完成后广播，不会因自动重放再次触发。
    // 这让“复印机”等遗物可以安全地对当前打出的牌执行一次免费重放，而不会递归触发自身。
    public event Action<CardData> OnPlayerCardPlayed;

    // 玩家卡牌实际进入 consumedPile 后广播：仅 BattleRemove / PermanentRemove（旧卡为 ThisBattle / ThisRun）会触发；
    // 普通弃置到 discardPile 的卡不会触发。供“报销单据”等以“消耗牌”为条件的遗物监听。
    public event Action<CardData> OnPlayerCardConsumed;

    /// <summary>当前手牌的只读列表。遗物可更新卡牌自身的运行时字段（如费用减免），但不得调整手牌集合。</summary>
    public IReadOnlyList<CardData> HandCards => _hand;

    /// <summary>手牌加入新卡后、卡面刷新前广播。供“计算器”等需要把运行时费用状态同步到新抽手牌的遗物监听。</summary>
    public event Action OnHandCardsChanged;

    // 热度系统事件钩子（热度逻辑已迁移至枪械师遗物 GunsmithHeatRelicEffect，BattleManager 仅广播时机）
    public event Action OnAttackCardPlayed;
    public event Action OnPlayerTurnStarted;
    public event Action OnPlayerTurnEnded;
    public event Action OnCharacterSwitched;

    /// <summary>
    /// 敌人受伤前修正事件（伤害倍率结算后、护甲结算前调用）。
    /// 参数：(敌人实例, 当前伤害)，返回修正后的伤害。
    /// 供敌人能力效果（EnemyConfig.abilities）使用，如"每回合首次被命中+25%额外伤害"。
    /// </summary>
    public event Func<EnemyInstance, int, int> OnEnemyDamageModify;

    /// <summary>
    /// 敌人死亡事件（HandleEnemyFatalDamage 标记 IsDead 后广播）。
    /// 供敌人能力效果（EnemyConfig.abilities）使用，如"该敌人死亡时玩家理智-1"。
    /// </summary>
    public event Action<EnemyInstance> OnEnemyDied;

    /// <summary>
    /// 玩家致命伤拦截事件：玩家生命归零、即将判负前广播。
    /// 返回 true 表示有遗物接管了死亡（例如“续约合同”复活），跳过本场战斗失败判定；
    /// 返回 false（或无人订阅）则按原逻辑判负。供战斗遗物监听。
    /// </summary>
    public event Func<bool> OnPlayerFatalDamage;

    /// <summary>
    /// 玩家理智从阈值以上降至阈值（含）以下、进入低理智状态时广播（阈值 _sanityThreshold，
    /// 与敌人阶段切换/低理智牌库同口径）。在阶段切换判定之前触发，遗物可在回调里回理智，
    /// 从而影响本次是否真正进入低理智/黑暗阶段。理智恢复后再次跨越边界会再次广播，
    /// 是否生效由各遗物自行控制（如“速效救心丸”每场仅首次生效）。
    /// </summary>
    public event Action OnPlayerEnteredLowSanity;

    // === CardEntry 效果系统支持 ===
    private EffectExecutorV2 _effectExecutorV2;
    private TriggerSystem _triggerSystem;
    // === Buff 系统 ===
    private BuffSystem _playerBuffs;
    private BuffSystem _enemyBuffs;
    [Header("Buff 数据资产（每属性一个，Inspector 配置图标）")]
    [SerializeField] private BuffData[] buffDataAssets;

    private sealed class DisplayOnlyBuff
    {
        public BuffAttributeType attributeType;
        public int stacks;
    }

    // 仅用于 Buff UI 的外部属性来源（例如局外遗物提供的持久属性）。
    // 它们不会写入 _playerBuffs，避免再次参与 PlayerDexterity 等实际属性计算而造成重复加成。
    private readonly Dictionary<string, DisplayOnlyBuff> _playerDisplayOnlyBuffs = new();

    private readonly Dictionary<string, int> _customData = new();
    private readonly HashSet<string> _eventsThisTurn = new();
    private readonly HashSet<CardData> _recycleUsedThisTurn = new();
    private int _slackBonusDraw;
    private readonly HashSet<string> _eventsThisBattle = new();
    private readonly Dictionary<string, int> _turnCounters = new();
    private readonly Dictionary<string, int> _battleCounters = new();
    private bool _sanityPhaseTriggered;  // 理智转阶段是否已触发（防止重复触发）
    private const int SanityPhaseThreshold = 4;  // 理智转阶段阈值
    private int _baseDrawPerTurn;   // 每场战斗前的抽牌基数（来自 Inspector 的 drawPerTurn，开局捕获一次）
    private int _actionPoints;

    // === 融合（Fusion）机制 ===
    private int _fusionUsesThisTurn;    // 本回合已进行的融合次数；基础上限为 1，可由遗物按角色追加。

    private sealed class FusionUseBonus
    {
        public CharacterData owner;
        public int extraUses;
    }

    private sealed class FusionPoolBonus
    {
        public CharacterData owner;
        public int bonus;
    }

    private sealed class FusedAttackCriticalRule
    {
        public CharacterData owner;
        public int minimumSingleHitDamage;
    }

    // key 为效果实例来源；同一角色的不同遗物可叠加，切换角色时只计入当前激活角色的来源。
    private readonly Dictionary<string, FusionUseBonus> _fusionUseBonuses = new();

    // 融合重分配总值加成：由幸运戒指等遗物按来源登记，仅在持有者激活时生效。
    private readonly Dictionary<string, FusionPoolBonus> _fusionPoolBonuses = new();

    // 融合攻击必暴规则：由烧水壶等遗物按来源登记，仅在持有者激活时检查当前融合攻击牌。
    private readonly Dictionary<string, FusedAttackCriticalRule> _fusedAttackCriticalRules = new();

    [Header("融合（Fusion）进阶开关")]
    [Tooltip("进阶1：融合修改是否永久保留（跨战斗）。默认 false，可由事件/脚本开启。")]
    [SerializeField] private bool persistFusion;
    [Tooltip("进阶2：是否开放血量/血量上限进入可融合数值（低理智下锁定不可选）。默认 false，可由事件/脚本开启。")]
    [SerializeField] private bool includeHPInFusion;

    /// <summary>进阶1是否开启（可在 Inspector 或代码调控）。</summary>
    public bool PersistFusion
    {
        get => persistFusion;
        set => persistFusion = value;
    }

    /// <summary>进阶2是否开启（可在 Inspector 或代码调控）。</summary>
    public bool IncludeHPInFusion
    {
        get => includeHPInFusion;
        set => includeHPInFusion = value;
    }

    private FusionController _fusionController;
    private BattlePilePanel _pilePanel;
    private BookUIController _bookUI;   // 缓存局外 UI 控制器（战斗中更新 TopBar 文本用）
    private CardData _currentFusionCard;   // 当前正在执行效果的手牌（供 effect 读取 fusion 覆盖）

    /// <summary>开启进阶效果1：融合修改跨战斗持久化。</summary>
    public void EnableFusionPersistence() => persistFusion = true;

    /// <summary>开启进阶效果2：血量/血量上限进入可融合数值（低理智下锁定）。</summary>
    public void EnableFusionHP() => includeHPInFusion = true;

    /// <summary>低理智判定（供融合锁定血量类；与敌人阶段切换同口径，阈值 _sanityThreshold）。</summary>
    public bool IsLowSanityForFusion => _playerSanity <= _sanityThreshold;

    // === 敌人状态（1-N 个；槽位索引稳定，死亡不压缩）===
    private readonly List<EnemyInstance> _enemies = new();
    private int _selectedEnemyIndex = 0;          // 拖拽出牌时的目标敌人槽位
    private int _hoverEnemyIndex = -1;            // 拖拽卡牌当前悬停高亮的敌人槽位（无则 -1）
    private Camera _uiCameraCache;                // 拖拽命中的 UI 相机（懒解析，Overlay 时为 null）
    private Coroutine _enemyTurnRoutine;
    private int _turnCount = 1;
    private bool _isPlayerTurn = true;
    private bool _battleEnded = false;
    private bool _waitingEnemyConfirm = false;
    private Coroutine _sanityTrembleRoutine;
    private bool _listenersWired = false;
    private Image _backgroundImage;   // Background GameObject 下的背景 Image（按理智切换）

    private SettingsPanelUI _settingsPanel;
    private ChapterManager _cachedChapterManager;   // 懒缓存，供货币代理使用

    public bool IsPlayerTurn => _isPlayerTurn && !_battleEnded;

    /// <summary>战斗结束（点击 QuitButton）后通知 ChapterManager 切回局外。</summary>
    public event Action OnBattleEnded;

    /// <summary>
    /// 战斗胜负已定（EndBattle）时立即广播，参数 victory=true 表示胜利。
    /// 与 OnBattleEnded（玩家点退出按钮后才触发）区分：本事件用于胜利瞬间的掉落面板等结算 UI。
    /// </summary>
    public event Action<bool> OnBattleFinished;

    /// <summary>最近一场战斗的胜负结果（EndBattle 时更新；战斗中无意义）。</summary>
    public bool LastBattleVictory { get; private set; }

    // ========================================================================
    // 公共属性（供 BattleCardContext / EffectExecutor 使用）
    // ========================================================================

    public int PlayerHP => _playerHP;
    public int PlayerMaxHP => playerMaxHP;
    public int PlayerStrength => _playerBuffs?.GetEffectiveValue(BuffAttributeType.Strength, _playerBaseStrength) ?? _playerStrength;
    public int PlayerDexterity => _playerBuffs?.GetEffectiveValue(BuffAttributeType.Dexterity, _playerBaseDexterity) ?? _playerDexterity;
    public float PlayerCritRate => (_playerBuffs?.GetEffectiveValue(BuffAttributeType.CriticalChance, _playerBaseCritRate) ?? _playerCritRate) / 100f;
    public float PlayerCritDamage => (_playerBuffs?.GetEffectiveValue(BuffAttributeType.CriticalDamage, _playerBaseCritDamage) ?? _playerCritDamage) / 100f;
    public int PlayerSanity => _playerSanity;
    public int PlayerFortune => _playerFortune;
    public int PlayerArmor => _playerArmor;
    public int PlayerBleed => 0;
    public int ActionPoints => _actionPoints;
    /// <summary>存活敌人数量（死亡敌人不计；槽位总数见 EnemySlotCount）</summary>
    public int EnemyCount
    {
        get
        {
            int n = 0;
            foreach (var e in _enemies)
                if (e != null && !e.IsDead) n++;
            return n;
        }
    }

    /// <summary>敌人槽位总数（含已死亡；索引 0..EnemySlotCount-1 稳定对应生成顺序，死亡不压缩）</summary>
    public int EnemySlotCount => _enemies.Count;

    /// <summary>当前出牌目标敌人槽位（由拖拽释放时设置；无拖拽时保持上次值，默认 0）</summary>
    public int SelectedEnemyIndex => _selectedEnemyIndex;
    public int HandCount => _hand.Count;
    public int DrawPileCount => ActiveChar?.drawPile.Count ?? 0;
    public int DiscardPileCount => ActiveChar?.discardPile.Count ?? 0;
    public bool IsBattleEnded => _battleEnded;

    /// <summary>当前角色抽牌堆剩余牌（只读，供抽牌堆面板展示）。</summary>
    public IReadOnlyList<CardData> GetActiveDrawPile() =>
        ActiveChar != null ? ActiveChar.drawPile : System.Array.Empty<CardData>();

    /// <summary>当前角色弃牌堆（只读，供弃牌堆面板展示）。</summary>
    public IReadOnlyList<CardData> GetActiveDiscardPile() =>
        ActiveChar != null ? ActiveChar.discardPile : System.Array.Empty<CardData>();

    /// <summary>指定槽位的敌人是否存活（越界返回 false）</summary>
    public bool IsEnemyAlive(int index) => GetEnemy(index) is { IsDead: false };

    public int GetEnemyHP(int index) { var e = GetEnemy(index); return e != null && !e.IsDead ? e.HP : 0; }
    public int GetEnemyArmor(int index) { var e = GetEnemy(index); return e != null && !e.IsDead ? e.Armor : 0; }
    public int GetEnemyBleed(int index) => 0;
    public int GetEnemyArmorBreak(int index) { var e = GetEnemy(index); return e != null && !e.IsDead ? e.ArmorBreakStacks : 0; }

    /// <summary>指定槽位敌人的有效力量（含运行时增益；敌人作为效果发起者时作攻击缩放；死亡/越界返回 0）。</summary>
    public int GetEnemyStrength(int index)
    {
        var e = GetEnemy(index);
        return e != null && !e.IsDead ? e.EffectiveStrength : 0;
    }

    /// <summary>指定槽位敌人的有效敏捷（含运行时增益；敌人作为效果发起者时作格挡缩放；死亡/越界返回 0）。</summary>
    public int GetEnemyDexterity(int index)
    {
        var e = GetEnemy(index);
        return e != null && !e.IsDead ? e.EffectiveDexterity : 0;
    }

    /// <summary>按槽位索引取敌人实例（越界返回 null）</summary>
    private EnemyInstance GetEnemy(int slotIndex)
        => (slotIndex >= 0 && slotIndex < _enemies.Count) ? _enemies[slotIndex] : null;

    /// <summary>
    /// 按敌人名（EnemyConfig.enemyName）在场上的存活敌人中查找实例。
    /// excludeSlot：需要排除的槽位（-1 = 不排除）；取首个匹配且非排除的存活敌人。
    /// 供敌人能力效果（如伤害共担）定位目标，找不到返回 null。
    /// </summary>
    public EnemyInstance FindAliveEnemyByName(string enemyName, int excludeSlot = -1)
    {
        if (string.IsNullOrEmpty(enemyName)) return null;
        for (int i = 0; i < _enemies.Count; i++)
        {
            if (i == excludeSlot) continue;
            var e = _enemies[i];
            if (e == null || e.IsDead) continue;
            if (e.Config != null && e.Config.enemyName == enemyName)
                return e;
        }
        return null;
    }

    /// <summary>
    /// 按 EnemyConfig 资产引用在场上的存活敌人中查找实例。
    /// excludeSlot：需要排除的槽位（-1 = 不排除）；取首个 Config 等于指定 Config 的存活敌人。
    /// 敌人能力效果（DamageTransferEffect）按资产引用精确定位目标，比按 enemyName 字符串匹配更可靠。
    /// </summary>
    public EnemyInstance FindAliveEnemyByConfig(EnemyConfig config, int excludeSlot = -1)
    {
        if (config == null) return null;
        for (int i = 0; i < _enemies.Count; i++)
        {
            if (i == excludeSlot) continue;
            var e = _enemies[i];
            if (e == null || e.IsDead) continue;
            if (e.Config == config)
                return e;
        }
        return null;
    }

    /// <summary>局外（ChapterManager）在进入战斗前指定的出战敌人列表（含位置与行动顺序值）。
    /// 为空则回退到 Inspector 的默认 defaultEnemies。</summary>
    public List<EnemySpawnInfo> StartEnemies { get; set; }

    /// <summary>
    /// 局外（ChapterManager）在进入战斗前传入的掉落表（PageEventData.lootTable）。
    /// 战斗胜利时据此显示 LootPanel 的奖励按钮；空则按钮全隐藏（面板与继续按钮仍显示）。
    /// </summary>
    public LootTable StartLootTable { get; set; }

    /// <summary>战斗背景配置（由 Battle 事件的 PageEventData 注入）。
    /// StartNormalBattleBackground / StartLowSanityBattleBackground 分别为正常与低理智背景图，
    /// StartBackgroundSanityThreshold 为切换阈值（玩家理智 &lt;= 该值时用低理智背景）。</summary>
    public Sprite StartNormalBattleBackground { get; set; }
    public Sprite StartLowSanityBattleBackground { get; set; }
    public int StartBackgroundSanityThreshold { get; set; } = 4;

    public int GetTurnCounter(string name) => _turnCounters.TryGetValue(name, out var v) ? v : 0;
    public int GetBattleCounter(string name) => _battleCounters.TryGetValue(name, out var v) ? v : 0;

    public int GetCustomData(string key) => _customData.TryGetValue(key, out var v) ? v : 0;
    public void SetCustomData(string key, int value)
    {
        _customData[key] = value;
        if (key == "Heat") OnHeatChanged?.Invoke(value);
    }
    public void ModifyCustomData(string key, int delta)
    {
        _customData[key] = GetCustomData(key) + delta;
        if (key == "Heat") OnHeatChanged?.Invoke(_customData[key]);
    }

    public bool HasEventOccurred(string eventName) => _eventsThisTurn.Contains(eventName) || _eventsThisBattle.Contains(eventName);
    public void RecordEvent(string eventName) { _eventsThisTurn.Add(eventName); _eventsThisBattle.Add(eventName); }

    /// <summary>对指定槽位的敌人造成伤害并飘字（已死/越界则忽略）。armorBreak&gt;0 时为额外破甲伤害（无视护甲）。</summary>
    public void DealDamageToEnemy(int index, int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0)
    {
        var inst = GetEnemy(index);
        if (inst == null || inst.IsDead)
        {
            Debug.LogWarning($"[BattleManager] DealDamageToEnemy: 槽位 {index} 无效或敌人已死亡");
            return;
        }

        int actual = DealDamageToEnemy(inst, amount, ignoreArmor, armorBreak);
        Debug.Log($"[伤害] 对 {inst.Name} 造成 {actual} 伤害{(isCrit ? "（暴击）" : "")}，剩余HP: {inst.HP}");
        if (actual > 0) inst.View?.ShowDamage(actual, isCrit);
        if (inst.HP <= 0) HandleEnemyFatalDamage(inst);
        inst.View?.Refresh();
        if (!inst.IsDead) inst.View?.PlayHitFeedback();
        UpdateUI();
        CheckBattleEnd();
    }

    /// <summary>对所有存活敌人逐个结算伤害并飘字</summary>
    public void DealDamageToAllEnemies(int amount, bool ignoreArmor, bool isCrit = false, int armorBreak = 0)
    {
        foreach (var inst in _enemies)
        {
            if (inst == null || inst.IsDead) continue;
            int actual = DealDamageToEnemy(inst, amount, ignoreArmor, armorBreak);
            Debug.Log($"[伤害] 对 {inst.Name} 造成 {actual} 全体伤害{(isCrit ? "（暴击）" : "")}，剩余HP: {inst.HP}");
            if (actual > 0) inst.View?.ShowDamage(actual, isCrit);
            if (inst.HP <= 0) HandleEnemyFatalDamage(inst);
            inst.View?.Refresh();
            if (!inst.IsDead) inst.View?.PlayHitFeedback();
        }
        UpdateUI();
        CheckBattleEnd();
    }

    /// <summary>敌人 HP≤0 的处理：直接标记死亡并隐藏视图（阶段切换不重置生命值，血条唯一，无"打穿转阶段"）</summary>
    private void HandleEnemyFatalDamage(EnemyInstance inst)
    {
        if (inst == null || inst.IsDead) return;

        inst.IsDead = true;
        Debug.Log($"[BattleManager] {inst.Name}（槽位{inst.SlotIndex}）被击败");
        inst.View?.Hide();

        // 敌人能力：死亡事件（如"该敌人死亡时玩家理智-1"）
        OnEnemyDied?.Invoke(inst);
    }

    public void HealPlayer(int amount) => _playerHP = Mathf.Min(playerMaxHP, _playerHP + amount);
    public void AddPlayerArmor(int amount) => _playerArmor += amount;
    public void AddActionPoints(int amount) => _actionPoints = Mathf.Max(0, _actionPoints + amount);

    // ========================================================================
    // 融合（Fusion）读写 API（供 FusionController 回填数值）
    // ========================================================================

    /// <summary>直接设定玩家当前行动点（融合回填用，防负）。</summary>
    public void SetActionPoints(int value) => _actionPoints = Mathf.Max(0, value);

    /// <summary>直接设定玩家当前护甲（融合回填用，防负）。</summary>
    public void SetPlayerArmor(int value) => _playerArmor = Mathf.Max(0, value);

    /// <summary>直接设定玩家当前 HP（夹取 0..max）。</summary>
    public void SetPlayerHP(int value) => _playerHP = Mathf.Clamp(value, 0, playerMaxHP);

    /// <summary>直接设定玩家血量上限（≥1），同步夹取当前 HP。</summary>
    public void SetPlayerMaxHP(int value)
    {
        playerMaxHP = Mathf.Max(1, value);
        _playerHP = Mathf.Min(_playerHP, playerMaxHP);
    }

    /// <summary>
    /// 原子设定玩家血量：同时定上限与当前值（融合同时选中 hp+maxhp 时用，避免相互钳制）。
    /// </summary>
    public void SetPlayerHPAndMax(int hp, int maxHp)
    {
        playerMaxHP = Mathf.Max(1, maxHp);
        _playerHP = Mathf.Clamp(hp, 0, playerMaxHP);
    }

    /// <summary>指定槽位敌人是否存活（供融合提供方用）。</summary>
    public bool FusionIsEnemyAlive(int slot) => GetEnemy(slot) is { IsDead: false };

    /// <summary>指定槽位敌人的 HP（供融合提供方用；死亡返回 0）。</summary>
    public int FusionEnemyHP(int slot)
    {
        var e = GetEnemy(slot);
        return e != null && !e.IsDead ? e.HP : 0;
    }

    /// <summary>指定槽位敌人的最大 HP。</summary>
    public int FusionEnemyMaxHP(int slot)
    {
        var e = GetEnemy(slot);
        return e != null ? e.MaxHP : 0;
    }

    /// <summary>指定槽位敌人的护甲。</summary>
    public int FusionEnemyArmor(int slot)
    {
        var e = GetEnemy(slot);
        return e != null && !e.IsDead ? e.Armor : 0;
    }

    /// <summary>回填：设定指定槽位敌人 HP（夹取 0..MaxHP）。</summary>
    public void FusionSetEnemyHP(int slot, int value)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead) return;
        e.HP = Mathf.Clamp(value, 0, e.MaxHP);
        e.View?.Refresh();
    }

    /// <summary>回填：设定指定槽位敌人 MaxHP（≥1，同步夹取 HP）。</summary>
    public void FusionSetEnemyMaxHP(int slot, int value)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead) return;
        e.MaxHP = Mathf.Max(1, value);
        e.HP = Mathf.Min(e.HP, e.MaxHP);
        e.View?.Refresh();
    }

    /// <summary>回填：原子设定指定槽位敌人血量（同时定 HP 与 MaxHP，避免相互钳制；融合同时选 hp+maxhp 用）。</summary>
    public void FusionSetEnemyHPAndMax(int slot, int hp, int maxHp)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead) return;
        e.MaxHP = Mathf.Max(1, maxHp);
        e.HP = Mathf.Clamp(hp, 0, e.MaxHP);
        e.View?.Refresh();
    }

    /// <summary>回填：设定指定槽位敌人护甲（防负）。</summary>
    public void FusionSetEnemyArmor(int slot, int value)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead) return;
        e.Armor = Mathf.Max(0, value);
        e.View?.Refresh();
    }

    /// <summary>回填：设定指定槽位敌人当前意图卡牌伤害覆盖值（防负）。写入 EnemyInstance 的覆盖字段，不改动共享卡牌资源。</summary>
    public void FusionSetEnemyIntentDamage(int slot, int value)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead) return;
        e.IntentDamageOverride = Mathf.Max(0, value);
        e.View?.Refresh();
    }

    /// <summary>读取指定槽位敌人意图伤害覆盖值（供效果执行器把融合数值应用到实际出招；无覆盖返回 false）。</summary>
    public bool TryGetEnemyIntentOverride(int slot, out int value)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead || e.IntentDamageOverride < 0)
        {
            value = 0;
            return false;
        }
        value = e.IntentDamageOverride;
        return true;
    }

    /// <summary>读取指定槽位敌人本回合意图伤害值（覆盖值优先；否则累加本回合所有出牌的效果节点常量伤害；无卡牌返回 0）。</summary>
    public int FusionEnemyIntentDamage(int slot)
    {
        var e = GetEnemy(slot);
        if (e == null || e.IsDead) return 0;
        if (e.IntentDamageOverride >= 0) return e.IntentDamageOverride;
        var skills = e.GetCurrentSkills();
        int total = 0;
        if (skills != null)
            foreach (var s in skills)
                if (s != null) total += ComputeCardIntentDamage(s, e);
        return total;
    }

    /// <summary>按卡牌当前形态（依据玩家理智）累加所有 DealDamage 效果节点的常量伤害值，作为意图伤害预览。</summary>
    private int ComputeCardIntentDamage(CardEntry card, EnemyInstance inst)
    {
        if (card == null) return 0;
        bool lowSanity = card.ShouldUseLowSanityForm(_playerSanity);
        var nodes = card.GetEffectNodes(lowSanity);
        int total = 0;
        foreach (var n in nodes)
        {
            if (n == null || !n.enabled) continue;
            if (n.operation != EffectOperation.DealDamage) continue;
            int str = inst != null ? inst.EffectiveStrength : 0;
            int dex = inst != null ? inst.EffectiveDexterity : 0;
            total += ValueNode.ResolveCombatValue(n.value, n.operation, n.scalingMode, str, dex, isEnemy: true);
        }
        return total;
    }

    /// <summary>当前玩家货币（只读代理到 ChapterManager；战斗中其它系统不消费则无冲突）。</summary>
    public int PlayerGold
    {
        get
        {
            var cm = GetChapterManager();
            return cm != null ? cm.PlayerGold : 0;
        }
    }

    /// <summary>直接覆盖玩家货币（融合回填用，防负；战后由 ApplyBattleResult 持久）。</summary>
    public void SetPlayerGold(int value)
    {
        var cm = GetChapterManager();
        if (cm == null) return;
        int val = Mathf.Max(0, value);
        //int diff = val - cm.PlayerGold;
        if (val != 0) cm.AddGold(val);   // 复用 AddGold 以广播 UI（内部 clamp≥0）
    }

    private ChapterManager GetChapterManager()
    {
        if (_cachedChapterManager == null)
            _cachedChapterManager = chapterManager != null ? chapterManager : FindObjectOfType<ChapterManager>();
        return _cachedChapterManager;
    }

    /// <summary>本回合融合次数上限：基础 1 次，加上当前激活角色持有遗物提供的额外次数。</summary>
    public int FusionUseLimitThisTurn => GetFusionUseLimitThisTurn();

    /// <summary>本回合已经完成的融合次数。</summary>
    public int FusionUsesThisTurn => _fusionUsesThisTurn;

    /// <summary>当前激活角色是否已耗尽本回合可用的融合次数。</summary>
    public bool FusionUsedThisTurn => _fusionUsesThisTurn >= FusionUseLimitThisTurn;

    /// <summary>
    /// 按来源登记某个角色额外可进行的融合次数。extraUses 小于等于 0 时移除该来源。
    /// 同一来源会覆盖旧值，供遗物在战斗开始/结束或移除时对称登记和撤销。
    /// </summary>
    public void SetExtraFusionUses(string sourceId, CharacterData owner, int extraUses)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (owner == null || extraUses <= 0)
        {
            _fusionUseBonuses.Remove(sourceId);
        }
        else
        {
            _fusionUseBonuses[sourceId] = new FusionUseBonus
            {
                owner = owner,
                extraUses = extraUses
            };
        }

        _fusionController?.UpdateEntryInteractable();
    }

    /// <summary>
    /// 按来源登记某个角色的融合重分配总值加成。bonus 小于等于 0 时移除该来源。
    /// 同一来源会覆盖旧值，供战斗遗物在战斗开始/结束或移除时对称登记和撤销。
    /// </summary>
    public void SetFusionPoolBonus(string sourceId, CharacterData owner, int bonus)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (owner == null || bonus <= 0)
        {
            _fusionPoolBonuses.Remove(sourceId);
        }
        else
        {
            _fusionPoolBonuses[sourceId] = new FusionPoolBonus
            {
                owner = owner,
                bonus = bonus
            };
        }
    }

    /// <summary>当前激活角色在本次融合中可获得的重分配总值加成。</summary>
    public int GetActiveCharacterFusionPoolBonus()
    {
        int total = 0;
        CharacterData activeOwner = ActiveCharacterData;
        foreach (FusionPoolBonus entry in _fusionPoolBonuses.Values)
        {
            if (entry != null && entry.owner == activeOwner)
                total += Mathf.Max(0, entry.bonus);
        }
        return total;
    }

    /// <summary>
    /// 按来源登记某个角色的融合攻击必暴阈值。minimumSingleHitDamage 小于等于 0 时移除该来源。
    /// 命中规则仅影响当前带有 fusion.overrideAttack 的手牌，且持有者必须是当前激活角色。
    /// </summary>
    public void SetFusedAttackCriticalRule(string sourceId, CharacterData owner, int minimumSingleHitDamage)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (owner == null || minimumSingleHitDamage <= 0)
        {
            _fusedAttackCriticalRules.Remove(sourceId);
        }
        else
        {
            _fusedAttackCriticalRules[sourceId] = new FusedAttackCriticalRule
            {
                owner = owner,
                minimumSingleHitDamage = minimumSingleHitDamage
            };
        }
    }

    /// <summary>
    /// 当前执行中的手牌若融合覆盖了攻击值，且其单次伤害达到任一当前激活角色规则的阈值，则该段伤害必定暴击。
    /// singleHitDamage 是融合覆盖和力量缩放均已结算、但尚未乘暴伤和扣护甲的单次伤害。
    /// </summary>
    public bool IsCurrentFusedAttackGuaranteedCritical(int singleHitDamage)
    {
        if (singleHitDamage <= 0 || _currentFusionCard?.fusion?.overrideAttack != true)
            return false;

        CharacterData activeOwner = ActiveCharacterData;
        foreach (FusedAttackCriticalRule rule in _fusedAttackCriticalRules.Values)
        {
            if (rule != null && rule.owner == activeOwner &&
                singleHitDamage >= rule.minimumSingleHitDamage)
                return true;
        }

        return false;
    }

    /// <summary>标记一次融合完成（由 FusionController 在进入融合时调用）。</summary>
    public void MarkFusionUsed() => _fusionUsesThisTurn++;

    private int GetFusionUseLimitThisTurn()
    {
        int limit = 1;
        CharacterData activeOwner = ActiveCharacterData;
        foreach (FusionUseBonus bonus in _fusionUseBonuses.Values)
        {
            if (bonus != null && bonus.owner == activeOwner)
                limit += Mathf.Max(0, bonus.extraUses);
        }
        return Mathf.Max(1, limit);
    }

    /// <summary>注册/获取融合控制器（BattleManager.BeginBattle 自动创建并挂载）。</summary>
    public FusionController FusionController => _fusionController;

    /// <summary>当前正在执行效果的手牌；EffectExecutor 借此读取 fusion 覆盖。</summary>
    public CardData CurrentFusionCard => _currentFusionCard;

    // ========================================================================
    // 融合：场上数值的“原位锚点”（供 FusionController 生成高亮徽章）
    // ========================================================================

    /// <summary>玩家行动点徽章锚点（左下角 ActionPointBadge）。</summary>
    public RectTransform ActionPointAnchor => actionPointText != null ? actionPointText.rectTransform : null;

    /// <summary>玩家护甲文本锚点。</summary>
    public RectTransform ArmorAnchor => armorText != null ? armorText.rectTransform : null;

    /// <summary>玩家 HP 文本锚点。</summary>
    public RectTransform HPAnchor => hpText != null ? hpText.rectTransform : null;

    /// <summary>
    /// 定位玩家 HP 文本（格式 "当前/上限"）中指定部分数字的世界矩形。
    /// isMax=false 定位当前值（斜杠前），isMax=true 定位上限值（斜杠后）。
    /// 返回 false 表示解析失败（无可视数字）。
    /// </summary>
    public bool TryGetPlayerHPNumberRect(bool isMax, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (hpText == null) return false;
        return TryGetTmpNumberRect(hpText, isMax ? 1 : 0, out center, out size);
    }

    /// <summary>在 TMP 文本中定位第 tokenIndex 个数字 token 的世界中心/尺寸（0=第一个数字）。</summary>
    private static bool TryGetTmpNumberRect(TMPro.TextMeshProUGUI text, int tokenIndex, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (text == null || tokenIndex < 0) return false;
        text.ForceMeshUpdate(true);
        var info = text.textInfo;
        if (info == null || info.characterInfo == null || info.characterCount == 0) return false;

        string s = text.text;
        // 找第 tokenIndex 个数字 token
        int tokenSeen = 0;
        int startChar = -1, endChar = -1;
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i])) continue;
            int start = i;
            int end = i;
            while (end + 1 < s.Length && char.IsDigit(s[end + 1])) end++;
            if (tokenSeen == tokenIndex) { startChar = start; endChar = end; break; }
            tokenSeen++;
            i = end;
        }
        if (startChar < 0) return false;

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
        bool found = false;
        for (int ci = 0; ci < info.characterCount; ci++)
        {
            var ch = info.characterInfo[ci];
            if (ch.index < startChar || ch.index > endChar) continue;
            if (!ch.isVisible) continue;
            Vector3 tl = text.transform.TransformPoint(ch.topLeft);
            Vector3 tr = text.transform.TransformPoint(ch.topRight);
            Vector3 bl = text.transform.TransformPoint(ch.bottomLeft);
            Vector3 br = text.transform.TransformPoint(ch.bottomRight);
            min = Vector3.Min(min, Vector3.Min(Vector3.Min(tl, bl), Vector3.Min(tr, br)));
            max = Vector3.Max(max, Vector3.Max(Vector3.Max(tl, bl), Vector3.Max(tr, br)));
            found = true;
        }
        if (!found) return false;
        center = (Vector2)((min + max) * 0.5f);
        size = new Vector2(max.x - min.x, max.y - min.y);
        return true;
    }

    /// <summary>玩家理智文本锚点（若存在）。</summary>
    public RectTransform SanityAnchor => sanityText != null ? sanityText.rectTransform : null;

    /// <summary>指定槽位敌人的视图锚点（敌人整体；死亡返回 null）。位置取自 enemyContainer 子物体。</summary>
    public RectTransform GetEnemyAnchor(int slot)
    {
        if (enemyContainer == null || slot < 0 || slot >= enemyContainer.childCount) return null;
        return enemyContainer.GetChild(slot) as RectTransform;
    }

    /// <summary>指定槽位敌人的视图组件（供融合精确锚定护甲/意图文本；死亡/越界返回 null）。</summary>
    public EnemyView GetEnemyView(int slot)
    {
        var rt = GetEnemyAnchor(slot);
        if (rt == null) return null;
        var e = GetEnemy(slot);
        return e != null && !e.IsDead ? e.View : null;
    }

    /// <summary>指定槽位敌人的护甲文本 RectTransform（供融合原位高亮；死亡返回 null）。</summary>
    public RectTransform GetEnemyArmorAnchor(int slot)
        => GetEnemyView(slot)?.ArmorTextRect;

    /// <summary>指定槽位敌人的血量文本 RectTransform（供融合原位高亮；死亡返回 null）。</summary>
    public RectTransform GetEnemyHPAnchor(int slot)
        => GetEnemyView(slot)?.HPTextRect;

    /// <summary>定位指定槽位敌人 HP 文本中当前值/上限值的数字世界矩形（isMax=true 取上限）。</summary>
    public bool TryGetEnemyHPNumberRect(int slot, bool isMax, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        var v = GetEnemyView(slot);
        return v != null && v.TryGetEnemyHPNumberRect(isMax, out center, out size);
    }


    /// <summary>指定槽位敌人意图牌库中各小卡的 CardDisplay（供融合高亮意图数值；死亡返回空列表）。</summary>
    public List<CardDisplay> GetEnemyIntentDeckDisplays(int slot)
    {
        var v = GetEnemyView(slot);
        return v != null ? v.IntentDeckDisplays : new List<CardDisplay>();
    }

    /// <summary>指定手牌索引的卡面视图锚点（用于原位徽章定位；越界返回 null）。</summary>
    public RectTransform GetHandCardAnchor(int index)
        => handLayout != null ? handLayout.GetCardViewTransform(index) : null;

    /// <summary>指定手牌索引的 CardDisplay（越界返回 null），用于数字字符精确定位。</summary>
    public CardDisplay GetHandCardDisplay(int index)
        => handLayout != null ? handLayout.GetCardDisplay(index) : null;

    /// <summary>立即把手牌摆到目标布局（跳过手势动画），供融合读取精确坐标。返回是否成功。</summary>
    public bool SnapHandToTarget()
    {
        if (handLayout == null) return false;
        handLayout.SnapToTarget();
        return true;
    }

    /// <summary>手动触发一次理智扣除（融合进入时用，作为代价而非条件，允许负值 clamp≥0）。</summary>
    public void DeductSanityAsCost(int amount)
    {
        if (amount <= 0) return;
        ModifySanity(-amount);
    }

    /// <summary>融合完成后刷新战斗 UI（手牌 + 顶部状态 + 敌人）并刷新融合按钮。</summary>
    public void SetDirtyUI()
    {
        UpdateUI();
        RefreshHandUI();
        if (_fusionController != null) _fusionController.UpdateEntryInteractable();
    }

    public void ModifyPlayerAttribute(ModifiableAttribute attr, ModifyMethod method, int amount)
    {
        // Buff 属性路由到 BuffSystem（Add 方式）
        if (_playerBuffs != null && method == ModifyMethod.Add)
        {
            switch (attr)
            {
                case ModifiableAttribute.Strength:
                    _playerBuffs.AddBuff(BuffAttributeType.Strength, amount);
                    return;
                case ModifiableAttribute.Dexterity:
                    _playerBuffs.AddBuff(BuffAttributeType.Dexterity, amount);
                    return;
                case ModifiableAttribute.PlayerCritRate:
                    _playerBuffs.AddBuff(BuffAttributeType.CriticalChance, amount);
                    return;
                case ModifiableAttribute.PlayerCritDamage:
                    _playerBuffs.AddBuff(BuffAttributeType.CriticalDamage, amount);
                    return;
            }
        }

        int newVal = method switch
        {
            ModifyMethod.Add => GetModAttrValue(attr) + amount,
            ModifyMethod.Subtract => GetModAttrValue(attr) - amount,
            ModifyMethod.Multiply => GetModAttrValue(attr) * amount,
            ModifyMethod.Override => amount,
            _ => GetModAttrValue(attr)
        };
        SetModAttrValue(attr, newVal);
    }

    /// <summary>添加玩家 buff（供 EffectExecutorV2 调用）</summary>
    public void AddPlayerBuff(BuffAttributeType type, int stacks, int duration = 0)
    {
        _playerBuffs?.AddBuff(type, stacks, duration);
    }

    /// <summary>
    /// 设置一个仅用于玩家 Buff UI 的外部属性来源。stacks 为 0 时移除该来源。
    /// 此接口不会写入 BuffSystem，因此不会重复影响 PlayerDexterity 等实际战斗属性。
    /// </summary>
    public void SetPlayerDisplayOnlyBuff(string sourceId, BuffAttributeType type, int stacks)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (stacks == 0)
        {
            _playerDisplayOnlyBuffs.Remove(sourceId);
            return;
        }

        _playerDisplayOnlyBuffs[sourceId] = new DisplayOnlyBuff
        {
            attributeType = type,
            stacks = stacks
        };
    }

    /// <summary>
    /// 获取玩家 Buff 显示列表（供 UI 调用）。
    /// 临时战斗 Buff 与局外持久属性的显示来源按属性类型合并，但仅前者参与实际属性计算。
    /// </summary>
    public List<DisplayedBuff> GetPlayerDisplayedBuffs()
    {
        var totals = new Dictionary<BuffAttributeType, int>();

        foreach (DisplayedBuff buff in _playerBuffs?.GetDisplayedBuffs() ?? new List<DisplayedBuff>())
            totals[buff.attributeType] = totals.TryGetValue(buff.attributeType, out int current)
                ? current + buff.totalStacks
                : buff.totalStacks;

        foreach (DisplayOnlyBuff buff in _playerDisplayOnlyBuffs.Values)
        {
            if (buff == null || buff.stacks == 0) continue;
            totals[buff.attributeType] = totals.TryGetValue(buff.attributeType, out int current)
                ? current + buff.stacks
                : buff.stacks;
        }

        var result = new List<DisplayedBuff>();
        foreach (var pair in totals)
        {
            if (pair.Value == 0) continue;
            result.Add(new DisplayedBuff
            {
                attributeType = pair.Key,
                totalStacks = pair.Value
            });
        }
        return result;
    }

    /// <summary>获取 BuffData 资产（按属性类型）</summary>
    public BuffData GetBuffData(BuffAttributeType type)
    {
        if (buffDataAssets == null) return null;
        foreach (var bd in buffDataAssets)
            if (bd != null && bd.attributeType == type) return bd;
        return null;
    }

    /// <summary>对敌人施加状态效果（ArmorBreak 削减护甲）。index=-1 表示全体存活敌人；已死/越界则忽略。</summary>
    public void ApplyStatusToEnemy(int index, StatusType status, int stacks)
    {
        if (status != StatusType.ArmorBreak)
        {
            Debug.Log($"[状态] 对敌人施加 {status}（多敌人框架下暂未实现）");
            return;
        }

        if (index < 0)
        {
            foreach (var inst in _enemies)
            {
                if (inst == null || inst.IsDead) continue;
                inst.ApplyStatus(status, stacks);
                Debug.Log($"[状态] {inst.Name} 护甲 -{stacks}，剩余 {inst.Armor}");
                inst.View?.Refresh();
            }
        }
        else
        {
            var inst = GetEnemy(index);
            if (inst == null || inst.IsDead) return;
            inst.ApplyStatus(status, stacks);
            Debug.Log($"[状态] {inst.Name} 护甲 -{stacks}，剩余 {inst.Armor}");
            inst.View?.Refresh();
        }
        UpdateUI();
    }

    public void ApplyStatusToPlayer(StatusType status, int stacks)
    {
        // 路由到 BuffSystem
        if (_playerBuffs != null)
        {
            switch (status)
            {
                case StatusType.Strength:
                    _playerBuffs.AddBuff(BuffAttributeType.Strength, stacks);
                    return;
                case StatusType.Dexterity:
                    _playerBuffs.AddBuff(BuffAttributeType.Dexterity, stacks);
                    return;
                case StatusType.CritRateBoost:
                    _playerBuffs.AddBuff(BuffAttributeType.CriticalChance, stacks);
                    return;
                case StatusType.CritDamageBoost:
                    _playerBuffs.AddBuff(BuffAttributeType.CriticalDamage, stacks);
                    return;
            }
        }

        // 旧路径回退
        /*switch (status)
        {
            case StatusType.Strength: _playerStrength += stacks; break;
            case StatusType.Dexterity: _playerDexterity += stacks; break;
            case StatusType.CritRateBoost: _playerCritRate += stacks; break;
            case StatusType.CritDamageBoost: _playerCritDamage += stacks; break;
        }*/
    }

    /// <summary>对敌人移除状态效果（ArmorBreak 还原护甲）。index=-1 表示全体存活敌人；已死/越界则忽略。</summary>
    public void RemoveStatusFromEnemy(int index, StatusType status, int stacks)
    {
        if (status != StatusType.ArmorBreak)
        {
            Debug.Log($"[状态] 对敌人移除 {status}（多敌人框架下暂未实现）");
            return;
        }

        if (index < 0)
        {
            foreach (var inst in _enemies)
            {
                if (inst == null || inst.IsDead) continue;
                inst.RemoveStatus(status, stacks);
                Debug.Log($"[状态] {inst.Name} 破甲移除{stacks}层，护甲恢复至 {inst.Armor}");
                inst.View?.Refresh();
            }
        }
        else
        {
            var inst = GetEnemy(index);
            if (inst == null || inst.IsDead) return;
            inst.RemoveStatus(status, stacks);
            Debug.Log($"[状态] {inst.Name} 破甲移除{stacks}层，护甲恢复至 {inst.Armor}");
            inst.View?.Refresh();
        }
        UpdateUI();
    }

    /// <summary>对玩家移除状态效果（路由到 BuffSystem）。</summary>
    public void RemoveStatusFromPlayer(StatusType status, int stacks)
    {
        if (_playerBuffs == null) return;
        switch (status)
        {
            case StatusType.Strength:
                _playerBuffs.RemoveBuff(BuffAttributeType.Strength, stacks);
                break;
            case StatusType.Dexterity:
                _playerBuffs.RemoveBuff(BuffAttributeType.Dexterity, stacks);
                break;
            case StatusType.CritRateBoost:
                _playerBuffs.RemoveBuff(BuffAttributeType.CriticalChance, stacks);
                break;
            case StatusType.CritDamageBoost:
                _playerBuffs.RemoveBuff(BuffAttributeType.CriticalDamage, stacks);
                break;
            default:
                Debug.Log($"[状态] 对玩家移除 {status}（暂未实现）");
                break;
        }
    }

    public int RequestSelectCardFromHand(string prompt) => -1; // 简化：暂不支持运行时选牌
    public void DiscardHandCard(int index)
    {
        if (index < 0 || index >= _hand.Count) return;
        ActiveChar.discardPile.Add(_hand[index]);
        _hand.RemoveAt(index);
        RefreshHandUI();
    }

    // ========================================================================
    // 融合：手牌数值读取/回写（写进 CardData.fusion 覆盖层，显示+打出同时生效）
    // ========================================================================

    /// <summary>取指定索引手牌的 CardData（越界返回 null），供融合提供方读取数值。</summary>
    public CardData GetHandCardData(int index)
        => (index >= 0 && index < _hand.Count) ? _hand[index] : null;

    /// <summary>保证卡牌已有融合覆盖层。</summary>
    private static FusionCardDelta EnsureFusion(CardData card)
    {
        if (card.fusion == null) card.fusion = new FusionCardDelta();
        return card.fusion;
    }

    /// <summary>回填：重设手牌费用（融合）并刷新手牌。</summary>
    public void SetHandCardCost(int index, int value)
    {
        var card = GetHandCardData(index);
        if (card == null) return;
        var f = EnsureFusion(card);
        f.overrideCost = true;
        f.cost = Mathf.Max(0, value);
        RefreshHandUI();
    }

    /// <summary>回填：重设手牌攻击值（融合）并刷新。</summary>
    public void SetHandCardAttack(int index, int value)
    {
        var card = GetHandCardData(index);
        if (card == null) return;
        var f = EnsureFusion(card);
        f.overrideAttack = true;
        f.attackValue = Mathf.Max(0, value);
        RefreshHandUI();
    }

    /// <summary>回填：重设手牌护甲值（融合）并刷新。</summary>
    public void SetHandCardArmor(int index, int value)
    {
        var card = GetHandCardData(index);
        if (card == null) return;
        var f = EnsureFusion(card);
        f.overrideArmor = true;
        f.armorValue = Mathf.Max(0, value);
        RefreshHandUI();
    }

    /// <summary>回填：重设手牌增益值（融合）并刷新。注意：增益覆盖目前未接入效果执行，仅记录展示值。</summary>
    public void SetHandCardBuff(int index, int value)
    {
        var card = GetHandCardData(index);
        if (card == null) return;
        var f = EnsureFusion(card);
        f.overrideBuff = true;
        f.buffValue = Mathf.Max(0, value);
        RefreshHandUI();
    }

    /// <summary>回填：重设手牌抽牌数（融合）并刷新。</summary>
    public void SetHandCardDraw(int index, int value)
    {
        var card = GetHandCardData(index);
        if (card == null) return;
        var f = EnsureFusion(card);
        f.overrideDraw = true;
        f.drawCount = Mathf.Max(0, value);
        RefreshHandUI();
    }

    /// <summary>回填：重设手牌回费数（融合）并刷新。</summary>
    public void SetHandCardRestore(int index, int value)
    {
        var card = GetHandCardData(index);
        if (card == null) return;
        var f = EnsureFusion(card);
        f.overrideRestore = true;
        f.restoreAP = Mathf.Max(0, value);
        RefreshHandUI();
    }

    /// <summary>手牌每张是否可融合地面（保底避免无意义。当前所有手牌均可参与费用/攻击/护甲）。</summary>
    public bool HandCardHasAttack(CardData card)
    {
        if (card == null) return false;
        if (card.sourceEntry != null)
            return card.sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Attack;
        return card.cardType == CardType.Attack;
    }

    public bool HandCardHasArmor(CardData card)
    {
        if (card == null) return false;
        if (card.sourceEntry != null)
            return card.sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Skill;
        return card.cardType == CardType.Skill;
    }

    private int GetModAttrValue(ModifiableAttribute attr) => attr switch
    {
        ModifiableAttribute.Strength => _playerStrength,
        ModifiableAttribute.Dexterity => _playerDexterity,
        ModifiableAttribute.PlayerCritRate => _playerCritRate,
        ModifiableAttribute.PlayerCritDamage => _playerCritDamage,
        ModifiableAttribute.MaxHP => playerMaxHP,
        ModifiableAttribute.CurrentHP => _playerHP,
        ModifiableAttribute.DrawPerTurn => drawPerTurn,
        ModifiableAttribute.EnergyPerTurn => maxActionPoints,
        ModifiableAttribute.CurrentSanity => _playerSanity,
        ModifiableAttribute.MaxSanity => _playerMaxSanity,
        ModifiableAttribute.Fortune => _playerFortune,
        ModifiableAttribute.PlayerDamageMultiplier => _playerDamageMultiplier,
        ModifiableAttribute.PlayerDamageTakenMultiplier => _playerDamageTakenMultiplier,
        // EnemyDamageMultiplier / EnemyDamageTakenMultiplier 已迁入 EnemyConfig（个体倍率），不再全局可改
        _ => 0
    };

    private void SetModAttrValue(ModifiableAttribute attr, int value)
    {
        switch (attr)
        {
            case ModifiableAttribute.Strength: _playerStrength = value; break;
            case ModifiableAttribute.Dexterity: _playerDexterity = value; break;
            case ModifiableAttribute.PlayerCritRate: _playerCritRate = value; break;
            case ModifiableAttribute.PlayerCritDamage: _playerCritDamage = value; break;
            case ModifiableAttribute.MaxHP: playerMaxHP = value; break;
            case ModifiableAttribute.CurrentHP: _playerHP = Mathf.Clamp(value, 0, playerMaxHP); break;
            case ModifiableAttribute.DrawPerTurn: drawPerTurn = value; break;
            case ModifiableAttribute.EnergyPerTurn: maxActionPoints = value; break;
            case ModifiableAttribute.CurrentSanity: ModifySanity(value - _playerSanity); break;
            case ModifiableAttribute.MaxSanity: _playerMaxSanity = value; break;
            case ModifiableAttribute.Fortune: SetPlayerFortune(value); break;
            case ModifiableAttribute.PlayerDamageMultiplier: _playerDamageMultiplier = value; break;
            case ModifiableAttribute.PlayerDamageTakenMultiplier: _playerDamageTakenMultiplier = value; break;
            // EnemyDamageMultiplier / EnemyDamageTakenMultiplier 已迁入 EnemyConfig（个体倍率），不再全局可改
        }
    }

    private CharBattleState ActiveChar => _chars[_activeCharIdx];
    private CharBattleState InactiveChar => _chars[1 - _activeCharIdx];

    /// <summary>当前激活角色的 CharacterData（供遗物效果判断"某角色是否激活"）。</summary>
    public CharacterData ActiveCharacterData => ActiveChar?.data;

    /// <summary>
    /// 局外（ChapterManager）在进入战斗前指定的【起始】激活/未激活角色（CharacterData）。
    /// 为空则按默认：characters[0] 激活、characters[1] 未激活。
    /// 注意：本类已有的 ActiveChar/InactiveChar（CharBattleState）是战斗中“当前”角色状态，
    /// 会随切换角色变化；此处 StartActiveChar/StartInactiveChar 仅作起始指派。
    /// </summary>
    public CharacterData StartActiveChar { get; set; }
    public CharacterData StartInactiveChar { get; set; }

    // ========================================================================
    // 生命周期
    // ========================================================================

    // 战斗由 ChapterManager 在进入战斗时显式调用 BeginBattle() 启动；
    // BattleCanvas 默认禁用，故场景加载时 Start() 不会自动开战（保持空实现）。
    private void Start()
    {
        // 空实现：真正初始化放在 BeginBattle()
    }

    /// <summary>绑定一次性 UI 监听（仅执行一次）。</summary>
    private void WireListeners()
    {
        // LootPanel 是 BattleCanvas 下的嵌套 prefab；字段未在场景接线时自动从包含未激活对象的场景中补找。
        // 防止 prefab 更新或场景引用丢失时，战斗胜利无法弹出结算面板。
        if (lootPanel == null)
            lootPanel = FindObjectOfType<LootPanelUI>(true);

        if (handLayout != null)
        {
            handLayout.SetCardClickCallback(OnCardClicked);
            handLayout.SetCardDropCallback(OnCardDropped);
            handLayout.SetCardDragOverCallback(SetCardDragOver);
            handLayout.SetCardPrefabs(attackCardPrefab, skillCardPrefab, abilityCardPrefab);
        }
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (switchCharacterButton != null)
            switchCharacterButton.onClick.AddListener(OnSwitchCharacterClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
        // 战利品面板的继续按钮与 QuitButton 等价：回写局外属性并结束战斗（回到局外）
        if (lootPanel != null)
            lootPanel.OnContinueClicked += OnQuitClicked;
    }

    /// <summary>
    /// 由 ChapterManager 在进入战斗后调用：绑定监听（一次性），启动战斗。
    /// 每次进入战斗都应调用一次（战斗结束后会重新启用 BattleCanvas 并再次 BeginBattle）。
    /// </summary>
    public void BeginBattle()
    {
        if (!_listenersWired) { WireListeners(); _listenersWired = true; }
        _baseDrawPerTurn = drawPerTurn;   // 捕获抽牌基数（Inspector 配置），避免逐场战斗累加

        // 融合控制器：首次创建并挂载（后续复用），由 UpdateUI 驱动其按钮可用态
        if (_fusionController == null)
        {
            _fusionController = gameObject.AddComponent<FusionController>();
            _fusionController.Setup(this);
        }
        if (_pilePanel == null)
            _pilePanel = gameObject.AddComponent<BattlePilePanel>();
        _pilePanel.Bind(this, attackCardPrefab, skillCardPrefab, abilityCardPrefab);
        _fusionUsesThisTurn = 0;       // 每场战斗重置
        StartBattle();
    }

    private void Update()
    {
        // 测试：按 1 降低 1 点理智
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ModifySanity(-1);
        // 测试：按 2 增加 1 点福报
        if (Input.GetKeyDown(KeyCode.Alpha2))
            ModifyFortune(1);
    }

    // ========================================================================
    // 战斗初始化
    // ========================================================================

    /// <summary>
    /// 从出生配置生成 1-N 个敌人（含视图生成与位置摆放）。
    /// 来源优先级：StartEnemies（局外注入）→ defaultEnemies（Inspector 默认）。
    /// 重进战斗时先清理上一场残留的敌人视图。
    /// </summary>
    private void InitEnemies()
    {
        // 清理上一场残留的敌人视图（敌人越打越多的防泄漏兜底）
        if (enemyContainer != null)
        {
            for (int i = enemyContainer.childCount - 1; i >= 0; i--)
                Destroy(enemyContainer.GetChild(i).gameObject);
        }
        _enemies.Clear();

        var spawnList = (StartEnemies != null && StartEnemies.Count > 0) ? StartEnemies : defaultEnemies;
        if (spawnList == null || spawnList.Count == 0)
        {
            Debug.LogWarning("[BattleManager] 未配置任何敌人（StartEnemies 为空且默认 defaultEnemies 为空）");
            return;
        }

        foreach (var info in spawnList)
        {
            if (info == null || info.config == null)
            {
                Debug.LogWarning("[BattleManager] 敌人出生配置存在空项（config 未指定），已跳过");
                continue;
            }

            var cfg = info.config;
            var inst = new EnemyInstance
            {
                SlotIndex = _enemies.Count,
                Config = cfg,
                ActionOrder = cfg.actionPriority,
                MaxHP = cfg.maxHP,
                HP = cfg.maxHP,
                Armor = cfg.armor,
                Strength = cfg.strength,
                Dexterity = cfg.dexterity,
                Phase = 1,
                TurnInCycle = 0,
                LockedCharIdx = -1,
                IsDead = false,
            };

            if (enemyViewPrefab != null && enemyContainer != null)
            {
                var view = Instantiate(enemyViewPrefab, enemyContainer);
                var rect = view.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = info.anchoredPosition;
                inst.View = view;
                view.SetCardPrefabs(attackCardPrefab, skillCardPrefab, abilityCardPrefab);
                view.Bind(inst);
            }
            else
            {
                Debug.LogWarning("[BattleManager] enemyViewPrefab / enemyContainer 未配置，敌人将无 UI 显示");
            }

            _enemies.Add(inst);
            Debug.Log($"[BattleManager] 生成敌人[{inst.SlotIndex}] {cfg.enemyName}: {inst.HP}/{inst.MaxHP} HP, {inst.Armor} 护甲, 行动顺序 {inst.ActionOrder} @ {info.anchoredPosition}");
        }
    }

    public void StartBattle()
    {
        // 解析战斗背景 Image（Background GameObject 下，含自身与子物体）
        _backgroundImage = background != null ? background.GetComponentInChildren<Image>() : null;

        if (gameConfig == null || gameConfig.characters == null || gameConfig.characters.Count < 2)
        {
            Debug.LogError("[BattleManager] GameConfig 未配置或角色不足2个，无法开始战斗");
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            _chars[i] = new CharBattleState { data = gameConfig.characters[i] };
            BuildStartingDeck(_chars[i]);
            ShuffleDrawPile(_chars[i]);
        }

        // 起始激活角色：优先用局外传入的 StartActiveChar，否则默认 characters[0]
        int startIdx = 0;
        if (StartActiveChar != null && gameConfig.characters != null)
        {
            int idx = gameConfig.characters.IndexOf(StartActiveChar);
            if (idx >= 0) startIdx = idx;
        }
        _activeCharIdx = startIdx;
        _hasSwitchedThisTurn = false;
        _turnCount = 1;
        _playerArmor = 0;
        _sanityPhaseTriggered = false;

        // 读入持久基础属性（单局内跨战斗保留，存于 ChapterManager 运行时副本；资产 PlayerConfig 仅作初始值）
        ChapterManager cm = chapterManager != null ? chapterManager : FindObjectOfType<ChapterManager>();
        if (cm != null)
        {
            playerMaxHP = cm.PlayerMaxHP;
            _playerHP = cm.PlayerHP;
            maxActionPoints = cm.PlayerMaxActionPoints;
            _baseDrawPerTurn = cm.PlayerDrawPerTurn;
            _playerMaxSanity = cm.PlayerMaxSanity;
            _playerSanity = cm.PlayerSanity;
            _sanityThreshold = cm.PlayerSanityThreshold;
            _playerFortune = cm.PlayerFortune;
            _playerStrength = cm.PlayerStrength;
            _playerDexterity = cm.PlayerDexterity;
            _playerLifesteal = cm.PlayerLifesteal;
            _playerCritRate = cm.PlayerCritRate;
            _playerCritDamage = cm.PlayerCritDamage;
            _playerDamageMultiplier = cm.PlayerDamageMultiplier;
            _playerDamageTakenMultiplier = cm.PlayerDamageTakenMultiplier;
            Debug.Log($"[BattleManager] 读入持久属性(来自ChapterManager) HP:{_playerHP}/{playerMaxHP} AP:{maxActionPoints} 抽牌:{_baseDrawPerTurn} 理智:{_playerSanity}/{_playerMaxSanity} 福报:{_playerFortune} 力量:{_playerStrength} 敏捷:{_playerDexterity} 吸血:{_playerLifesteal} 暴击率:{_playerCritRate} 暴伤:{_playerCritDamage}");
        }
        else if (playerConfig != null)
        {
            // 回退：直接用资产初始值（不含事件累积，仅作安全网）
            playerMaxHP = playerConfig.maxHP;
            _playerHP = playerConfig.startHP;
            maxActionPoints = playerConfig.maxActionPoints;
            _baseDrawPerTurn = playerConfig.drawPerTurn;
            _playerMaxSanity = playerConfig.maxSanity;
            _playerSanity = playerConfig.startSanity;
            _sanityThreshold = playerConfig.sanityThreshold;
            _playerFortune = Mathf.Max(0, playerConfig.startFortune);
            _playerStrength = playerConfig.strength;
            _playerDexterity = playerConfig.dexterity;
            _playerLifesteal = playerConfig.lifesteal;
            _playerCritRate = playerConfig.critRate;
            _playerCritDamage = playerConfig.critDamage;
            _playerDamageMultiplier = playerConfig.playerDamageMultiplier;
            _playerDamageTakenMultiplier = playerConfig.playerDamageTakenMultiplier;
            Debug.LogWarning("[BattleManager] 未找到 ChapterManager，回退读入 PlayerConfig 初始值（无跨战斗累积）");
        }
        else
        {
            _playerHP = playerMaxHP;
            _playerMaxSanity = 10;
            _playerSanity = 10;
            _sanityThreshold = 4;
            _playerFortune = 0;
            _playerDamageMultiplier = 100;
            _playerDamageTakenMultiplier = 100;
            _playerDexterity = 0;
            Debug.LogWarning("[BattleManager] 未配置 ChapterManager / PlayerConfig，持久属性为 0");
        }

        // 初始理智就绪后刷新低理智特效开关（进战斗时理智已低于阈值也要开）
        UpdateLowSanityVolume();

        // 同步基础值到 base 字段
        _playerBaseStrength = _playerStrength;
        _playerBaseDexterity = _playerDexterity;
        _playerBaseLifesteal = _playerLifesteal;
        _playerBaseCritRate = _playerCritRate;
        _playerBaseCritDamage = _playerCritDamage;

        // 初始化 Buff 系统
        _playerBuffs = new BuffSystem();
        _playerBuffs.SetMinValue(BuffAttributeType.Strength, int.MinValue);     // 力量可负
        _playerBuffs.SetMinValue(BuffAttributeType.Dexterity, int.MinValue);    // 敏捷可负
        _playerBuffs.SetMinValue(BuffAttributeType.Recovery, 0);                 // 回复最小0
        _playerBuffs.SetMinValue(BuffAttributeType.LifeSteal, 0);                // 吸血最小0
        _playerBuffs.SetMinValue(BuffAttributeType.CriticalChance, 0);           // 暴击率最小0
        _playerBuffs.SetMinValue(BuffAttributeType.CriticalDamage, 2);           // 暴伤最小2
        _enemyBuffs = new BuffSystem(); // 敌人使用相同约束（可后续扩展）

        // 设置卡面描述属性解析提供者（力量/敏捷变化时卡面数值实时更新）
        CardDisplay.PlayerStrengthProvider = () => PlayerStrength;
        CardDisplay.PlayerDexterityProvider = () => PlayerDexterity;

        // 从出生配置（StartEnemies 或默认 defaultEnemies）生成 1-N 个敌人
        InitEnemies();
        _battleEnded = false;
        _isPlayerTurn = true;

        // 重新进入战斗时复位上一场结束状态（胜利/失败面板、按钮可用性）
        if (lootPanel != null) lootPanel.gameObject.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (endTurnButton != null) endTurnButton.interactable = true;
        if (switchCharacterButton != null) switchCharacterButton.interactable = true;

        _hand.Clear();
        _actionPoints = maxActionPoints;

        // 初始化效果系统
        _ruleConfig = GameRuleConfig.Load();
        var ctx = new BattleCardContext(this);
        _triggerSystem = new TriggerSystem(null);
        _effectExecutorV2 = new EffectExecutorV2(ctx, _triggerSystem);
        // 通过反射替换 _triggerSystem 内部 executor（避免创建两个实例）
        // 更简单的方式：用同一个实例，先创建 executor 再注入
        _triggerSystem = new TriggerSystem(_effectExecutorV2);
        // 关键：让 executor 指向正确的 triggerSystem（第二个实例）
        // EffectExecutorV2 的 _triggerSystem 是 readonly，需要重新创建
        _effectExecutorV2 = new EffectExecutorV2(ctx, _triggerSystem);

        // 热度系统：_customData["Heat"] 初始化为 0（热度逻辑由枪械师遗物 GunsmithHeatRelicEffect 驱动）
        _customData["Heat"] = 0;
        _customData["PlayerDamageMultiplier"] = _playerDamageMultiplier;
        _customData["PlayerDamageTakenMultiplier"] = _playerDamageTakenMultiplier;

        // 初始化计数器
        _turnCounters["CardsPlayed"] = 0;
        _turnCounters["AttackCardsPlayed"] = 0;
        _turnCounters["AttacksPerformed"] = 0;
        _turnCounters["CriticalHits"] = 0;
        _turnCounters["DamageTaken"] = 0;
        _turnCounters["DamageDealt"] = 0;
        _turnCounters["HeatGained"] = 0;
        _turnCounters["HeatLost"] = 0;
        _turnCounters["CharactersSwitched"] = 0;
        _turnCounters["EnemiesKilled"] = 0;
        _turnCounters["BlockGained"] = 0;
        _turnCounters["CardsDrawn"] = 0;
        _turnCounters["CardsDiscarded"] = 0;
        _turnCounters["CardsExhausted"] = 0;

        _triggerSystem.OnCombatStart();

        // 遗物效果：战斗开始钩子（枪械师热度系统在此重置/启动）
        RelicEffectManager.Instance?.NotifyBattleStart(this);

        // 敌人能力效果：扫描所有敌人的 abilities，按 RelicData 去重反射实例化并启动
        InitEnemyAbilityEffects();

        DrawCards(drawPerTurn);

        UpdateCharacterSwitchUI();
        UpdateUI();
        ApplyBackground();   // 按初始理智设置战斗背景

        Debug.Log($"[BattleManager] 战斗开始！角色1: {ActiveChar.data?.Label}, 角色2: {InactiveChar.data?.Label}");
    }

    private void BuildStartingDeck(CharBattleState state)
    {
        var charData = state.data;
        if (charData == null) return;

        // 优先使用运行时牌库（GlobalCardLibrary，含商店购买的卡），回退到 CardEntry / startingLibrary
        var globalLib = GlobalCardLibrary.Instance;
        if (globalLib != null && globalLib.IsRegistered(charData))
        {
            var cards = globalLib.GetCards(charData);
            foreach (var inst in cards)
            {
                if (inst != null && inst.template != null)
                    state.drawPile.Add(inst.template);
            }
            if (state.drawPile.Count > 0)
            {
                var cardNames = string.Join(", ", cards);
                Debug.Log($"[BattleManager] {charData.Label} (id={charData.characterId}) 使用运行时牌库: {state.drawPile.Count} 张 [{cardNames}]");
                return;
            }
        }

        // 其次：卡牌编辑器的 CardEntry 初始牌组
        // 按 characterId 匹配 Inspector 配置的卡组（而非固定索引）
        List<CardEntry> entryCards = null;
        if (gameConfig != null && gameConfig.characters.Count >= 2 && charData != null)
        {
            if (charData == gameConfig.characters[0]) entryCards = character1Cards;
            else if (charData == gameConfig.characters[1]) entryCards = character2Cards;
        }
        if (entryCards == null) entryCards = state == _chars[0] ? character1Cards : character2Cards;
        if (entryCards != null && entryCards.Count > 0)
        {
            var cardDataList = CardEntryAdapter.ConvertToCardData(entryCards);
            foreach (var cd in cardDataList)
                state.drawPile.Add(cd);
            Debug.Log($"[BattleManager] {charData?.Label} 初始牌组(CardEntry): {state.drawPile.Count} 张");
            return;
        }
        
        // 回退：使用旧 CardData 初始牌库
        if (charData.startingLibrary == null) return;

        foreach (var card in charData.startingLibrary.startingCards)
        {
            if (card != null)
                state.drawPile.Add(CardEntryAdapter.ConvertSingle(card));
        }

        Debug.Log($"[BattleManager] {charData.Label} 初始牌组(CardData): {state.drawPile.Count} 张");
    }

    // ========================================================================
    // 抽牌
    // ========================================================================

    public void DrawCards(int count)
    {
        var activeChar = ActiveChar;
        for (int i = 0; i < count; i++)
        {
            if (_hand.Count >= handLimit) break;

            if (activeChar.drawPile.Count == 0)
            {
                if (activeChar.discardPile.Count == 0) break;
                activeChar.drawPile = new List<CardData>(activeChar.discardPile);
                activeChar.discardPile.Clear();
                ShuffleDrawPile(activeChar);
            }

            var drawn = activeChar.drawPile[0];
            drawn.extraCost = _handCostBonus;   // 继承当前过载费用加成
            _hand.Add(drawn);
            activeChar.drawPile.RemoveAt(0);
        }
        OnHandCardsChanged?.Invoke();
        RefreshHandUI();
    }

    // ========================================================================
    // 出牌
    // ========================================================================

    private void OnCardClicked(int handIndex)
    {
        if (!_isPlayerTurn || _battleEnded) return;
        PlayCard(handIndex);
    }

    /// <summary>计算卡牌本次出牌的实际费用。遗物减免已写入 CardData.relicCostReduction，最低 0。</summary>
    private int ResolveCardPlayCost(CardData card)
    {
        return card != null ? card.GetEffectiveCost() : 0;
    }

    public bool PlayCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= _hand.Count) return false;
        if (!_isPlayerTurn || _battleEnded) return false;

        var card = _hand[handIndex];
        int cost = ResolveCardPlayCost(card);
        int paidAP = Mathf.Min(_actionPoints, cost);
        int missing = cost - paidAP;
        if (missing > 0)
        {
            int goldCost = missing * 5;
            if (!card.HasKeyword(KeywordType.Bribe) || PlayerGold < goldCost)
                return false;
            SetPlayerGold(PlayerGold - goldCost);
        }

        _actionPoints -= paidAP;

        // 热度系统：打出攻击牌时通知枪械师遗物（由遗物负责加热度）
        if (card.sourceEntry != null && card.sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Attack)
        {
            OnAttackCardPlayed?.Invoke();
        }

        // 计数器：出牌
        _turnCounters["CardsPlayed"]++;
        if (card.sourceEntry != null)
        {
            if (card.sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Attack) _turnCounters["AttackCardsPlayed"]++;
        }
        _customData["ActualPaidCost"] = cost;

        // 提前确定形态（供 ApplyCardEffects 和 HandleCardConsumption 共用）
        if (card.sourceEntry != null)
        {
            int sanityThreshold = _ruleConfig != null ? _ruleConfig.sanity.lowSanityThreshold : 4;
            card.isLowSanityForm = card.sourceEntry.ShouldUseLowSanityForm(_playerSanity, sanityThreshold);
            Debug.Log($"[BattleManager] 卡牌={card.sourceEntry.cardName} 理智={_playerSanity} 阈值={sanityThreshold} isLowSanityForm={card.isLowSanityForm} 存在形式={card.sourceEntry.GetExistence(card.isLowSanityForm)}");
        }

        _currentFusionCard = card;   // 让 EffectExecutor 在执行效果时读取此卡融合覆盖
        ApplyCardEffects(card);
        _currentFusionCard = null;   // 执行完清除避免串扰

        // 原始出牌效果结算完成后通知遗物。自动重放不走此事件，避免“复印机”重复递归。
        // 此时卡牌仍在手牌区，遗物可复用相同 CardData 重放其效果；随后才按原逻辑消耗原卡。
        if (!_battleEnded)
            OnPlayerCardPlayed?.Invoke(card);

        HandleCardConsumption(card);

        TryAttachAccessoryToHost(card);

        if (card.HasKeyword(KeywordType.Consult))
            DrawCards(1);

        if (card.HasKeyword(KeywordType.WatchTarget))
            ApplyWatchTargetToAllEnemies();

        bool recycleToHand = card.HasKeyword(KeywordType.Recycle) && !_recycleUsedThisTurn.Contains(card);
        if (recycleToHand)
        {
            _recycleUsedThisTurn.Add(card);
        }
        else
        {
            HandleCardConsumption(card);
            if (_hand.Contains(card))
                _hand.Remove(card);
            else if (handIndex >= 0 && handIndex < _hand.Count && _hand[handIndex] == card)
                _hand.RemoveAt(handIndex);
            else
                _hand.Remove(card);
        }

        bool slack = card.HasKeyword(KeywordType.Slack);
        if (slack)
            _slackBonusDraw++;

        RefreshHandUI();

        UpdateUI();
        CheckBattleEnd();
        if (slack && !_battleEnded && _isPlayerTurn)
            OnEndTurnClicked();
        return true;
    }

    /// <summary>拖拽出牌：对指定敌人槽位出牌。targetEnemyIndex 为 0..N-1 时覆盖本次目标，结束后恢复。</summary>
    public bool PlayCard(int handIndex, int targetEnemyIndex)
    {
        int prev = _selectedEnemyIndex;
        if (targetEnemyIndex >= 0) _selectedEnemyIndex = targetEnemyIndex;
        bool ok = PlayCard(handIndex);
        _selectedEnemyIndex = prev;
        return ok;
    }

    /// <summary>拖拽结束回调：攻击牌命中敌人区域出牌；增益/防御牌拖到中央出牌区释放即出牌（无需选敌）。由 CardDragHandler 调用。</summary>
    private void OnCardDropped(int handIndex, Vector2 screenPos)
    {
        if (!_isPlayerTurn || _battleEnded) return;
        if (handIndex < 0 || handIndex >= _hand.Count) return;

        var card = _hand[handIndex];
        // 记录本次拖拽最后悬停高亮的敌人（在 ClearEnemyHover 之前缓存，作为释放位置微移的兜底）
        int lastHovered = _hoverEnemyIndex;
        int slot = GetEnemySlotAtScreenPosition(screenPos);

        if (RequiresEnemyTarget(card))
        {
            // 攻击牌：拖到敌人身上（或刚悬停高亮的敌人上）释放
            if (slot < 0) slot = lastHovered;
            ClearEnemyHover();
            if (slot < 0) return;   // 未命中任何敌人，卡牌弹回
            PlayCard(handIndex, slot);
        }
        else
        {
            // 增益/防御牌：拖到中央出牌区放开即出牌，不选择敌人
            ClearEnemyHover();
            if (!IsInPlayZone(screenPos)) return;   // 仍拖在手牌区则弹回
            PlayCard(handIndex, -1);   // -1：不修改目标（此类牌无需敌目标）
        }
    }

    /// <summary>
    /// 拖拽过程中逐帧更新悬停高亮：仅攻击牌命中哪个敌人就高亮哪个，切走/离开时取消。
    /// 增益/防御牌不锁定敌人，不加高亮。
    /// </summary>
    public void SetCardDragOver(int handIndex, Vector2 screenPos)
    {
        bool targeting = handIndex >= 0 && handIndex < _hand.Count && RequiresEnemyTarget(_hand[handIndex]);
        if (!targeting)
        {
            ClearEnemyHover();   // 非攻击牌拖动时不锁定敌人
            return;
        }
        int slot = GetEnemySlotAtScreenPosition(screenPos);
        if (slot == _hoverEnemyIndex) return;
        ClearEnemyHover();
        if (slot >= 0)
        {
            _hoverEnemyIndex = slot;
            GetEnemy(slot)?.View?.SetHighlighted(true);
        }
    }

    /// <summary>取消当前悬停高亮。</summary>
    private void ClearEnemyHover()
    {
        if (_hoverEnemyIndex >= 0)
            GetEnemy(_hoverEnemyIndex)?.View?.SetHighlighted(false);
        _hoverEnemyIndex = -1;
    }

    /// <summary>该卡是否需要对敌人选目标（攻击牌为是，增益/防御牌为否）。</summary>
    private bool RequiresEnemyTarget(CardData card)
    {
        if (card == null) return false;
        if (card.sourceEntry != null)
            return card.sourceEntry.cardType == LightMiniGame.CardEditor.CardType.Attack;
        return card.cardType == CardType.Attack;
    }

    /// <summary>屏幕坐标是否落在中央出牌区（手牌区上沿以上、敌人上方的中间战场）。</summary>
    private bool IsInPlayZone(Vector2 screenPos)
    {
        RectTransform handRect = handLayout != null ? handLayout.GetComponent<RectTransform>() : null;
        if (handRect == null)
            return screenPos.y > Screen.height * 0.3f;   // 兜底：无手牌区时取屏幕上方 70%

        var corners = new Vector3[4];
        handRect.GetWorldCorners(corners);
        float handTopY = corners[1].y;   // Overlay 画布下世界坐标==屏幕坐标
        return screenPos.y > handTopY;
    }

    /// <summary>返回屏幕坐标命中的存活敌人槽位索引；未命中返回 -1。</summary>
    public int GetEnemySlotAtScreenPosition(Vector2 screenPos)
    {
        if (_uiCameraCache == null && enemyContainer != null)
            _uiCameraCache = enemyContainer.GetComponentInParent<Canvas>()?.rootCanvas?.worldCamera;
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e == null || e.IsDead || e.View == null) continue;
            var rect = e.View.GetComponent<RectTransform>();
            if (rect == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, _uiCameraCache))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// 免费重放一张已打出的卡牌：仅再次结算其效果，不额外扣行动点、不增加出牌计数、
    /// 不触发 OnPlayerCardPlayed，也不会移动牌堆或再次消耗原卡。
    /// 调用方须在原始出牌效果结算完成后调用；攻击牌会沿用原始出牌已经选定的目标。
    /// </summary>
    public bool ReplayCardEffects(CardData card)
    {
        if (card == null || _battleEnded) return false;

        _currentFusionCard = card;
        try
        {
            ApplyCardEffects(card);
        }
        finally
        {
            _currentFusionCard = null;
        }

        Debug.Log($"[BattleManager] 免费重放卡牌效果：{card.cardName}");
        return true;
    }

    /// <summary>
    /// 将指定卡牌的独立运行时副本加入当前手牌。
    /// 复制发生在原牌的 OnPlayerCardPlayed 回调期间时，原牌仍暂留手牌区；若原牌不是“循环”牌，
    /// 则允许临时超过手牌上限 1 张，因为原牌会在同一次 PlayCard 调用内立刻离开手牌。
    /// </summary>
    public bool CopyCardToHand(CardData source)
    {
        if (source == null || _battleEnded) return false;

        bool sourceWillLeaveHand = _hand.Contains(source) && !source.HasKeyword(KeywordType.Recycle);
        if (_hand.Count >= handLimit && !sourceWillLeaveHand)
        {
            Debug.Log($"[BattleManager] 手牌已满，无法复制卡牌：{source.cardName}");
            return false;
        }

        CardData copy = Instantiate(source);
        copy.name = $"{source.name}(Copy)";
        copy.isLowSanityForm = source.isLowSanityForm;
        copy.fusion = source.fusion;
        copy.extraCost = _handCostBonus;
        copy.relicCostReduction = 0;
        copy.attachedEffectNodes = source.attachedEffectNodes != null
            ? source.attachedEffectNodes.ConvertAll(node => node != null ? node.Clone() : null)
            : null;

        _hand.Add(copy);
        OnHandCardsChanged?.Invoke();
        RefreshHandUI();
        Debug.Log($"[BattleManager] 复制卡牌到手牌：{source.cardName}");
        return true;
    }

    private void ApplyCardEffects(CardData card)
    {
        if (card.sourceEntry != null)
        {
            var entry = card.sourceEntry;

            // 执行 EffectNode 列表（统一路径，能力卡和普通卡都走这里）
            // 形态已在 PlayCard 中提前确定
            if (entry.HasEffectNodes(card.isLowSanityForm)
                || (card.attachedEffectNodes != null && card.attachedEffectNodes.Count > 0))
            {
                var nodes = card.GetEffectNodes(card.isLowSanityForm);
                _triggerSystem?.FireEvent(TriggerEvent.OnCardPlayed);
                _effectExecutorV2.ExecuteEffectList(nodes);
                UpdateUI();
                CheckBattleEnd();
                return;
            }

            Debug.LogWarning($"[BattleManager] 卡牌 {entry.cardName} 没有 EffectNode 配置，跳过效果执行");
            UpdateUI();
            return;
        }

        // 回退：无 CardEntry 的旧 CardData（直接走旧的硬编码路径）
        switch (card.cardType)
        {
            case CardType.Attack: ApplyAttackCard(card); break;
            case CardType.Skill: ApplyArmorCard(card); break;
            case CardType.Ability: ApplyBuffCard(card); break;
            return;
        }

        // 回退：旧路径（无 CardEntry 的 CardData）
        switch (card.cardType)
        {
            case CardType.Attack: ApplyAttackCard(card); break;
            case CardType.Skill: ApplyArmorCard(card); break;
            case CardType.Ability: ApplyBuffCard(card); break;
        }
    }

    private void ApplyAttackCard(CardData card)
    {
        int baseDamage = card.EffectiveAttack;   // 融合覆盖优先
        if (card.attackValueType == ValueType.AttributeBased)
            baseDamage += GetAttributeValue(card.attackAttribute);

        int attackCount = card.attackCount;
        bool ignoreArmor = card.ignoreArmor;

        // 旧版 CardData 路径：目标固定为槽位 0（出牌目标选择逻辑未实现）
        var target = GetEnemy(SelectedEnemyIndex);
        int totalDamageDealt = 0;
        for (int i = 0; i < attackCount; i++)
        {
            totalDamageDealt += DealDamageToEnemy(target, baseDamage, ignoreArmor);
        }

        if (totalDamageDealt > 0 && target != null)
        {
            target.View?.ShowDamage(totalDamageDealt);
            if (target.HP <= 0) HandleEnemyFatalDamage(target);
            target.View?.Refresh();
            if (target != null && !target.IsDead) target.View?.PlayHitFeedback();
        }
        else if (target != null && !target.IsDead)
        {
            target.View?.PlayHitFeedback();
        }
    }

    private void ApplyArmorCard(CardData card)
    {
        int armor = card.EffectiveArmor;   // 融合覆盖优先
        if (card.armorValueType == ValueType.AttributeBased)
            armor += GetAttributeValue(card.armorAttribute);

        _playerArmor += armor;
    }

    private void ApplyBuffCard(CardData card)
    {
        foreach (var effect in card.buffEffects)
        {
            int totalValue = effect.value * card.buffStacks;
            switch (effect.effectType)
            {
                case BuffEffectType.IncreaseAttribute:
                    IncreaseAttribute(effect.targetAttribute, totalValue);
                    break;
                case BuffEffectType.RestoreActionPoints:
                    _actionPoints += totalValue;
                    break;
                case BuffEffectType.DrawCards:
                    DrawCards(totalValue);
                    break;
                case BuffEffectType.GainArmor:
                    _playerArmor += totalValue;
                    break;
                case BuffEffectType.HealHP:
                    _playerHP = Mathf.Min(playerMaxHP, _playerHP + totalValue);
                    break;
            }
        }
    }

    private void HandleCardConsumption(CardData card)
    {
        // 如果有关联的 CardEntry，按当前形态选择存在形式
        if (card.sourceEntry != null)
        {
            var ex = card.sourceEntry.GetExistence(card.isLowSanityForm);
            Debug.Log($"[BattleManager] HandleCardConsumption: {card.sourceEntry.cardName} isLowSanityForm={card.isLowSanityForm} existence={ex}");
            switch (ex)
            {
                case CardExistence.Normal:
                    ActiveChar.discardPile.Add(card);
                    break;
                case CardExistence.BattleRemove:
                case CardExistence.PermanentRemove:
                    ActiveChar.consumedPile.Add(card);
                    OnPlayerCardConsumed?.Invoke(card);
                    break;
            }
            return;
        }

        // 回退：旧 CardData 的固定 consumeType
        switch (card.consumeType)
        {
            case ConsumeType.None:
                ActiveChar.discardPile.Add(card);
                break;
            case ConsumeType.ThisBattle:
            case ConsumeType.ThisRun:
                ActiveChar.consumedPile.Add(card);
                OnPlayerCardConsumed?.Invoke(card);
                break;
        }
    }

    /// <summary>
    /// 配件是词条、主机是卡牌。带「配件」词条的卡打出时，若手牌里还有名为「主机」的卡，
    /// 则把本卡效果叠到那张主机上（不限层数）。须在本卡移出手牌之前调用。
    /// </summary>
    private void TryAttachAccessoryToHost(CardData accessory)
    {
        if (accessory == null || !accessory.HasKeyword(KeywordType.Accessory)) return;

        CardData host = null;
        foreach (var c in _hand)
        {
            if (c != null && c != accessory && c.IsHostCard)
            {
                host = c;
                break;
            }
        }
        if (host == null) return;

        var nodes = accessory.GetEffectNodes(accessory.isLowSanityForm);
        if (nodes == null || nodes.Count == 0) return;

        if (host.attachedEffectNodes == null)
            host.attachedEffectNodes = new List<EffectNode>();
        host.attachedEffectNodes.AddRange(nodes);
        Debug.Log($"[BattleManager] 配件「{accessory.cardName}」效果已叠加到主机「{host.cardName}」（现 {host.attachedEffectNodes.Count} 条附加效果）");
    }

    // ========================================================================
    // 伤害计算
    // ========================================================================

    /// <summary>对指定敌人结算伤害（玩家造成伤害倍率 × 敌人受击倍率 → 破甲/护甲 → 扣血），返回实际伤害。
    /// 玩家倍率来自 PlayerConfig（全局），敌人受击倍率来自该敌人 EnemyConfig（个体）。
    /// 不触发飘字/阶段/结束判定，由调用方处理。</summary>
    private int DealDamageToEnemy(EnemyInstance inst, int damage, bool ignoreArmor, int armorBreak = 0)
    {
        if (inst == null || inst.IsDead) return 0;

        // 应用伤害倍率：最终伤害 = 基础伤害 * 玩家造成伤害倍率 * 敌人受击倍率
        float mult = PercentToFactor(_playerDamageMultiplier) * PercentToFactor(inst.Config != null ? inst.Config.damageTakenMultiplier : 100);
        damage = Mathf.RoundToInt(damage * mult);
        int armorBreakScaled = Mathf.RoundToInt(Mathf.Max(0, armorBreak) * mult);

        // 敌人能力：受伤前修正（倍率之后、护甲之前；如"每回合首次被命中+25%"）
        if (OnEnemyDamageModify != null)
        {
            foreach (Func<EnemyInstance, int, int> modify in OnEnemyDamageModify.GetInvocationList())
            {
                try { damage = modify(inst, damage); }
                catch (Exception ex) { Debug.LogError($"[BattleManager] OnEnemyDamageModify 监听者抛出异常: {ex}"); }
            }
        }

        return inst.TakeDamage(damage, ignoreArmor, armorBreakScaled);
    }

    /// <summary>
    /// 百分比倍率 → 乘数。约定 100 = 1.0 倍。
    /// 0/负数视为未配置（按 100）；1 视为误把「1.0 倍」写成了 1，否则普通攻击会四舍五入成 0。
    /// </summary>
    private static float PercentToFactor(int percent)
    {
        if (percent <= 1) return 1f;
        return percent / 100f;
    }

    /// <summary>给指定槽位敌人叠加护甲（敌人自护盾/给友军护盾），越界/死亡忽略。</summary>
    public void AddEnemyArmor(int slotIndex, int amount)
    {
        var e = GetEnemy(slotIndex);
        if (e == null || e.IsDead || amount <= 0) return;
        e.AddArmor(amount);
        e.View?.Refresh();
        UpdateUI();
    }

    // ========================================================================
    // 敌人能力效果（EnemyConfig.abilities → RelicData.effectScriptName 反射实例化）
    // 与玩家遗物（RelicEffectManager）平行的战斗内生命周期：StartBattle 启动 / EndBattle 清理。
    // ========================================================================

    /// <summary>已实例化的敌人能力效果（按 RelicData 去重；效果内部自行管理多个宿主敌人）。</summary>
    private readonly List<EnemyAbilityEffectEntry> _enemyAbilityEffects = new List<EnemyAbilityEffectEntry>();

    private class EnemyAbilityEffectEntry
    {
        public LightMiniGame.Shop.RelicData relic;
        public IRelicEffect effect;
    }

    /// <summary>扫描所有敌人的能力表，按 RelicData 去重实例化效果并调用 OnBattleStart。</summary>
    private void InitEnemyAbilityEffects()
    {
        ShutdownEnemyAbilityEffects(victory: false);   // 清理上一场残留

        var seen = new HashSet<LightMiniGame.Shop.RelicData>();
        foreach (var e in _enemies)
        {
            var abilities = e?.Config?.abilities;
            if (abilities == null) continue;

            foreach (var ab in abilities)
            {
                var relic = ab?.relic;
                if (relic == null || !seen.Add(relic)) continue;

                var effect = InstantiateEnemyAbilityEffect(relic);
                if (effect == null) continue;
                _enemyAbilityEffects.Add(new EnemyAbilityEffectEntry { relic = relic, effect = effect });
            }
        }

        if (_enemyAbilityEffects.Count == 0) return;

        var chapter = GetChapterManager();
        foreach (var entry in _enemyAbilityEffects)
        {
            try
            {
                entry.effect.OnBattleStart(new RelicEffectContext
                {
                    owner = null,   // 敌人能力无角色归属
                    relic = entry.relic,
                    battle = this,
                    chapter = chapter,
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManager] 敌人能力 '{entry.relic.name}' 的 OnBattleStart 抛出异常: {ex}");
            }
        }
        Debug.Log($"[BattleManager] 敌人能力效果已启动：{_enemyAbilityEffects.Count} 个");
    }

    /// <summary>战斗结束：对所有敌人能力效果调用 OnBattleEnd 并清空列表。</summary>
    private void ShutdownEnemyAbilityEffects(bool victory)
    {
        if (_enemyAbilityEffects.Count == 0) return;

        var chapter = GetChapterManager();
        foreach (var entry in _enemyAbilityEffects)
        {
            try
            {
                entry.effect.OnBattleEnd(new RelicEffectContext
                {
                    owner = null,
                    relic = entry.relic,
                    battle = this,
                    chapter = chapter,
                }, victory);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManager] 敌人能力 '{entry.relic.name}' 的 OnBattleEnd 抛出异常: {ex}");
            }
        }
        _enemyAbilityEffects.Clear();
    }

    /// <summary>按 RelicData.effectScriptName 反射实例化敌人能力效果（与 RelicEffectManager 同一套规则）。</summary>
    private static IRelicEffect InstantiateEnemyAbilityEffect(LightMiniGame.Shop.RelicData relic)
    {
        if (string.IsNullOrEmpty(relic.effectScriptName))
        {
            Debug.LogWarning($"[BattleManager] 敌人能力 '{relic.relicName}' 未配置效果脚本（effectScriptName 为空），仅作展示");
            return null;
        }

        var type = Type.GetType(relic.effectScriptName);
        if (type == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(relic.effectScriptName);
                if (type != null) break;
            }
        }

        if (type == null || !typeof(IRelicEffect).IsAssignableFrom(type))
        {
            Debug.LogError($"[BattleManager] 敌人能力 '{relic.relicName}' 的效果类 '{relic.effectScriptName}' 不存在或未实现 IRelicEffect");
            return null;
        }

        try { return Activator.CreateInstance(type) as IRelicEffect; }
        catch (Exception ex)
        {
            Debug.LogError($"[BattleManager] 敌人能力 '{relic.relicName}' 的效果类实例化失败（需要无参构造）: {ex}");
            return null;
        }
    }

    /// <summary>监控目标：给场上所有存活敌人运行时力量+1（开战从 EnemyConfig.strength 拷入，不写回资产）。</summary>
    private void ApplyWatchTargetToAllEnemies()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e == null || e.IsDead) continue;
            e.Strength += 1;
            e.View?.Refresh();
            RefreshEnemyIntentDeck(e);
        }
        UpdateUI();
    }

    /// <summary>
    /// 给指定槽位敌人施加属性增益（敌人自buff）。仅支持敌人模型存在的属性（力量/敏捷），
    /// 其它属性敌人不具此概念，返回 false（调用方可忽略或回退）。
    /// </summary>
    public bool ApplyEnemyAttributeBuff(int slotIndex, LightMiniGame.CardEditor.PlayerAttributeType attr, int delta)
    {
        var e = GetEnemy(slotIndex);
        if (e == null || e.IsDead) return false;

        switch (attr)
        {
            case LightMiniGame.CardEditor.PlayerAttributeType.Strength:
                e.Strength += delta;
                e.View?.Refresh();
                // 力量变化后立即刷新意图牌（不受 _isPlayerTurn 守卫限制，敌人回合 buff 也要实时反映到卡面数值）
                RefreshEnemyIntentDeck(e);
                UpdateUI();
                return true;
            case LightMiniGame.CardEditor.PlayerAttributeType.Dexterity:
                e.Dexterity += delta;
                e.View?.Refresh();
                RefreshEnemyIntentDeck(e);
                UpdateUI();
                return true;
            default:
                // 敌人暂没有 血量/暴击/倍率 等运行时增益的概念，不支持则忽略
                Debug.Log($"[BattleManager] 敌人({e.Name}) 不支持属性 {attr} 的运行时增益，已忽略");
                return false;
        }
    }

    /// <summary>公开包装：敌人攻击牌/效果调用，对玩家结算伤害（沿用原敌人伤害语义）。</summary>
    public void DealDamageToPlayer(int damage, int sourceEnemySlot)
    {
        var inst = GetEnemy(sourceEnemySlot);
        DealDamageToPlayer(damage, inst);
    }

    private void DealDamageToPlayer(int damage, EnemyInstance inst)
    {
        // 伤害值在 EffectExecutor 里已按出牌者力量结算，这里不再叠加，避免 {N+力量} 双算。
        if (inst != null && inst.Config != null)
        {
            int enemyDealtMult = inst.Config.damageDealtMultiplier;
            float mult = PercentToFactor(enemyDealtMult) * PercentToFactor(_playerDamageTakenMultiplier);
            damage = Mathf.RoundToInt(damage * mult);
        }
        else
        {
            damage = Mathf.RoundToInt(damage * PercentToFactor(_playerDamageTakenMultiplier));
        }
        int actualDamage = damage;
        if (_playerArmor > 0)
        {
            int absorbed = Mathf.Min(_playerArmor, damage);
            _playerArmor -= absorbed;
            actualDamage -= absorbed;
        }
        _playerHP -= actualDamage;
        if (_playerHP < 0) _playerHP = 0;

        // 玩家受伤统一一条路径（飘字/事件/死亡判定）
        if (actualDamage > 0)
            OnPlayerDamaged(actualDamage);
    }

    /// <summary>玩家受伤的统一处理入口（飘字提示 + 事件记录）。</summary>
    private void OnPlayerDamaged(int damage)
    {
        _triggerSystem?.FireEvent(TriggerEvent.OnDamageTaken);
        UpdateUI();
        if (_playerHP <= 0)
            HandlePlayerDefeat();
    }

    /// <summary>
    /// 玩家死亡统一处理：先给遗物一次“复活/接管死亡”的机会（OnPlayerFatalDamage）；
    /// 有接管者复活则刷新 UI 并继续战斗，否则按原逻辑判负。
    /// </summary>
    private void HandlePlayerDefeat()
    {
        if (TryReviveFromFatal())
        {
            UpdateUI();
            return;
        }
        EndBattle(false);
    }

    /// <summary>遍历致命伤拦截回调，首个返回 true 者生效（复活玩家），其余不再触发。</summary>
    private bool TryReviveFromFatal()
    {
        if (OnPlayerFatalDamage == null) return false;
        foreach (Func<bool> handler in OnPlayerFatalDamage.GetInvocationList())
        {
            try
            {
                if (handler()) return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManager] 致命伤拦截回调抛出异常: {ex}");
            }
        }
        return false;
    }

    /// <summary>
    /// 修改理智值。delta 为正则恢复，为正则降低。降至 0 以下时置为 0。
    /// 当理智从 >阈值 降至 ≤阈值 时触发转阶段（每场战斗仅触发一次）。
    /// </summary>
    public void ModifySanity(int delta)
    {
        if (delta == 0) return;
        int prev = _playerSanity;
        _playerSanity = Mathf.Clamp(_playerSanity + delta, 0, _playerMaxSanity);

        // 进入低理智状态（从 >阈值 降至 ≤阈值）时广播，供遗物在阶段切换判定前回理智。
        if (prev > _sanityThreshold && _playerSanity <= _sanityThreshold)
            OnPlayerEnteredLowSanity?.Invoke();

        // 理智变化后刷新低理智特效开关
        UpdateLowSanityVolume();

        // 降至阈值 → 触发黑暗阶段
        if (!_sanityPhaseTriggered && prev > SanityPhaseThreshold && _playerSanity <= SanityPhaseThreshold)
        {
            _sanityPhaseTriggered = true;
            OnSanityPhaseTransition();
        }

        // 理智变化时检查每个存活敌人的阶段切换（各敌人独立判定，互不影响）+ 同步低理智牌库标记
        foreach (var e in _enemies)
        {
            if (e == null || e.IsDead) continue;
            // 一进入/退出低理智即切换出牌牌库（即便不触发阶段切换）
            bool low = _playerSanity <= _sanityThreshold;
            if (e.UseLowSanityPool != low)
            {
                e.UseLowSanityPool = low;
                e.ResetDrawnSkill();   // 牌库变化 → 清空已抽，重新随机下一回合出牌
                RefreshEnemyIntentDeck(e);
                e.View?.Refresh();
                Debug.Log($"[BattleManager] {e.Name}（槽位{e.SlotIndex}）低理智牌库 {(low ? "启用(phase2)" : "关闭(phase1)")}");
            }
            if (e.CheckPhaseSwitch(_playerSanity, _sanityThreshold))
            {
                e.View?.Refresh();
                Debug.Log($"[BattleManager] {e.Name}（槽位{e.SlotIndex}）理智触发阶段切换 → 阶段{e.Phase}，HP保持 {e.HP}/{e.MaxHP}");
            }
        }

        UpdateUI();
        ApplyBackground();   // 理智变化实时切换背景
    }

    /// <summary>覆盖福报值（防负，无上限）。</summary>
    public void SetPlayerFortune(int value)
    {
        _playerFortune = Mathf.Max(0, value);
    }

    /// <summary>修改福报值。delta 为正则增加；结果钳到 ≥0。</summary>
    public void ModifyFortune(int delta)
    {
        if (delta == 0) return;
        SetPlayerFortune(_playerFortune + delta);
        UpdateUI();
    }

    /// <summary>
    /// 理智低于阈值（_playerSanity &lt; _sanityThreshold，与敌人阶段切换同口径）时启用
    /// lowSanityVolume（如信号干扰后处理），否则禁用。在战斗属性初始化与每次理智变化后调用。
    /// </summary>
    private void UpdateLowSanityVolume()
    {
        if (lowSanityVolume != null)
            lowSanityVolume.enabled = _playerSanity <= _sanityThreshold;
    }

    /// <summary>理智转阶段钩子（理智降至阈值 4 时触发，每场战斗仅一次）</summary>
    protected virtual void OnSanityPhaseTransition()
    {
        Debug.Log($"[BattleManager] 理智转阶段触发！理智 {_playerSanity}/{_playerMaxSanity}（阈值 {SanityPhaseThreshold}）");

        // 1. 升级所有卡牌效果
        UpgradeAllCardsForDarkMode();

        // 2. 全屏暗色遮罩淡入
        if (darkOverlay != null)
            StartCoroutine(DarkOverlayFadeRoutine());

        // 3. 理智条颤抖效果
        if (sanityBar != null)
        {
            if (_sanityTrembleRoutine != null) StopCoroutine(_sanityTrembleRoutine);
            _sanityTrembleRoutine = StartCoroutine(SanityTrembleRoutine());
        }

        UpdateUI();
    }

    /// <summary>
    /// 升级所有牌堆中的卡牌效果（使用每张牌配置的升级数据）。
    /// 涵盖手牌、两角色抽牌堆、弃牌堆、消耗堆。
    /// </summary>
    private void UpgradeAllCardsForDarkMode()
    {
        // 手牌
        foreach (var card in _hand)
            UpgradeSingleCard(card);

        // 双角色的牌堆
        for (int ci = 0; ci < 2; ci++)
        {
            if (_chars[ci] == null) continue;
            foreach (var card in _chars[ci].drawPile)
                UpgradeSingleCard(card);
            foreach (var card in _chars[ci].discardPile)
                UpgradeSingleCard(card);
            foreach (var card in _chars[ci].consumedPile)
                UpgradeSingleCard(card);
        }

        // 刷新手牌 UI（重新应用数据以显示升级后的描述）
        RefreshHandUI();
        Debug.Log("[BattleManager] 所有卡牌已升级为低理智形态");
    }

    /// <summary>
    /// 单张卡牌低理智化：仅设置 isLowSanityForm 标记。
    /// 升级后的效果由 EffectExecutor 通过 card.GetEffectNodes(true) 自动读取 CardEntry.lowSanityEffectNodes。
    /// </summary>
    private void UpgradeSingleCard(CardData card)
    {
        if (card == null) return;
        if (card.isLowSanityForm) return;

        card.isLowSanityForm = true;
    }

    /// <summary>全屏暗色遮罩淡入协程</summary>
    private IEnumerator DarkOverlayFadeRoutine()
    {
        darkOverlay.gameObject.SetActive(true);
        Color target = new Color(0.05f, 0.02f, 0.08f, darkOverlayAlpha);
        Color start = darkOverlay.color;
        start.a = 0f;
        darkOverlay.color = start;

        float elapsed = 0f;
        while (elapsed < darkOverlayFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / darkOverlayFadeDuration;
            darkOverlay.color = Color.Lerp(start, target, t);
            yield return null;
        }
        darkOverlay.color = target;
    }

    /// <summary>理智条颤抖效果：持续在原始位置上做小幅随机偏移，理智恢复后停止</summary>
    private IEnumerator SanityTrembleRoutine()
    {
        var rt = sanityBar.GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;
        float intensity = 3f;
        var fill = sanityBar.fillRect?.GetComponent<UnityEngine.UI.Image>();

        while (_playerSanity <= SanityPhaseThreshold && !_battleEnded)
        {
            float ox = Random.Range(-intensity, intensity);
            float oy = Random.Range(-intensity, intensity);
            rt.anchoredPosition = basePos + new Vector2(ox, oy);

            if (fill != null)
            {
                Color darkTint = new Color(0.5f, 0.3f, 0.6f, 1f);
                fill.color = Color.Lerp(fill.color, darkTint, 0.1f);
            }

            yield return null;
        }

        rt.anchoredPosition = basePos;
        if (fill != null) fill.color = new Color(0.3f, 0.5f, 0.85f, 1f);
        _sanityTrembleRoutine = null;
    }

    /// <summary>
    /// 敌人伤害飘字已迁入 EnemyView.ShowDamage（每个敌人视图各自飘字，锚点为视图的 damageAnchor）。
    /// </summary>

    // ========================================================================
    // 属性
    // ========================================================================

    private int GetAttributeValue(PlayerAttributeType attr) => attr switch
    {
        PlayerAttributeType.Strength => _playerStrength,
        PlayerAttributeType.Dexterity => _playerDexterity,
        _ => 0
    };

    private void IncreaseAttribute(PlayerAttributeType attr, int value)
    {
        switch (attr)
        {
            case PlayerAttributeType.Strength: _playerStrength += value; break;
            case PlayerAttributeType.Dexterity: _playerDexterity += value; break;
            case PlayerAttributeType.Vitality: playerMaxHP += value; _playerHP += value; break;
            case PlayerAttributeType.Agility: drawPerTurn += value; break;
        }
    }

    private bool IsCardPlayable(CardData card)
    {
        if (card == null) return false;
        int cost = ResolveCardPlayCost(card);
        if (_actionPoints >= cost) return true;
        int missing = cost - _actionPoints;
        return card.HasKeyword(KeywordType.Bribe) && PlayerGold >= missing * 5;
    }

    // ========================================================================
    // 角色切换
    // ========================================================================

    private void OnSwitchCharacterClicked()
    {
        if (!_isPlayerTurn || _battleEnded) return;
        if (_hasSwitchedThisTurn)
        {
            Debug.Log("[BattleManager] 本场战斗已切换过角色");
            return;
        }
        SwitchCharacter();
    }

    private void SwitchCharacter()
    {
        var oldChar = ActiveChar;
        foreach (var card in _hand)
            oldChar.discardPile.Add(card);
        _hand.Clear();

        // 挂起当前角色的能力
        // 触发器系统角色切换

        _activeCharIdx = 1 - _activeCharIdx;
        _hasSwitchedThisTurn = true;

        // 计数器：切换角色
        _turnCounters["CharactersSwitched"]++;

        // 热度系统：通知枪械师遗物已切换角色（由遗物标记本回合按切换衰减量衰减）
        OnCharacterSwitched?.Invoke();

        // 恢复切换后角色的能力
        // (TriggerSystem handles suspend/resume)

        // 触发器系统角色切换
        _triggerSystem?.OnCharacterSwitch(_activeCharIdx);

        DrawCards(drawPerTurn);

        UpdateCharacterSwitchUI();
        UpdateUI();

        Debug.Log($"[BattleManager] 切换到角色: {ActiveChar.data?.Label}，抽{drawPerTurn}张牌");
    }

    private void UpdateCharacterSwitchUI()
    {
        Debug.Log($"[UpdateCharacterSwitchUI] activeIdx={_activeCharIdx}, ActiveChar={ActiveChar?.data?.displayName}, InactiveChar={InactiveChar?.data?.displayName}");
        if (ActiveChar?.data != null)
        {
            if (activeCharNameText != null)
                activeCharNameText.text = ActiveChar.data.displayName;
            if (activeCharPortrait != null && ActiveChar.data.avatar != null)
                activeCharPortrait.sprite = ActiveChar.data.avatar;
        }
        if (InactiveChar?.data != null)
        {
            if (inactiveCharNameText != null)
                inactiveCharNameText.text = InactiveChar.data.displayName;
            if (inactiveCharPortrait != null && InactiveChar.data.avatar != null)
                inactiveCharPortrait.sprite = InactiveChar.data.avatar;
        }

        bool canSwitch = _isPlayerTurn && !_battleEnded && !_hasSwitchedThisTurn;
        if (switchCharacterButton != null)
            switchCharacterButton.interactable = canSwitch;
        if (switchAvailableIndicator != null)
            switchAvailableIndicator.SetActive(canSwitch);
        if (switchUsedIndicator != null)
            switchUsedIndicator.SetActive(_hasSwitchedThisTurn);
    }

    // ========================================================================
    // 设置面板
    // ========================================================================

    private void OnSettingsClicked()
    {
        if (_settingsPanel == null && settingsPanelPrefab != null)
        {
            var go = Instantiate(settingsPanelPrefab, transform.parent);
            _settingsPanel = go.GetComponent<SettingsPanelUI>();
        }
        _settingsPanel?.Show();
    }

    /// <summary>
    /// QuitButton 回调：把战斗后的玩家属性写回局外系统（ChapterManager），
    /// 并通知战斗结束（由 ChapterManager 切回 BookCanvas 并推进章节）。
    /// </summary>
    private void OnQuitClicked()
    {
        var cm = chapterManager != null ? chapterManager : FindObjectOfType<ChapterManager>();
        if (cm != null)
        {
            cm.ApplyBattleResult(
                _playerHP, playerMaxHP,
                _playerSanity, _playerMaxSanity,
                _playerStrength, _playerDexterity, _playerLifesteal,
                _playerCritRate, _playerCritDamage,
                maxActionPoints, drawPerTurn,
                _playerDamageMultiplier, _playerDamageTakenMultiplier,
                _playerFortune,
                ActiveChar?.data, InactiveChar?.data);   // 把战斗结束时的激活/未激活角色同步回局外
        }
        OnBattleEnded?.Invoke();
    }

    // ========================================================================
    // 回合流程
    // ========================================================================

    private void OnEndTurnClicked()
    {
        if (!_isPlayerTurn || _battleEnded) return;
        EndPlayerTurn();
    }

    private void EndPlayerTurn()
    {
        _isPlayerTurn = false;

        var activeChar = ActiveChar;
        foreach (var card in _hand)
            activeChar.discardPile.Add(card);
        _hand.Clear();
        RefreshHandUI();

        _actionPoints = 0;

        if (endTurnButton != null) endTurnButton.interactable = false;
        if (switchCharacterButton != null) switchCharacterButton.interactable = false;
        UpdateCharacterSwitchUI();

        // 回合结束时触发能力
        // (TriggerSystem handles turn end)

        // 热度系统：通知枪械师遗物回合结束（由遗物执行衰减与过热判定）
        OnPlayerTurnEnded?.Invoke();

        // 统一触发器系统回合结束
        _triggerSystem?.OnTurnEnd();

        // Buff 系统回合结束（递减持续回合，移除过期 buff）
        _playerBuffs?.OnTurnEnd();
        _enemyBuffs?.OnTurnEnd();

        StartEnemyTurn();
    }

    // ========================================================================
    // 遗物效果扩展：热度驱动（热度逻辑已迁移至枪械师遗物 GunsmithHeatRelicEffect）
    // BattleManager 仅保留费用附加应用 + 触发器/计数器工具方法 + 热度变化广播，供遗物调用。
    // ========================================================================
    /// <summary>过热阈值（遗物效果读取用，默认 25）。</summary>
    public int OverheatThreshold => _ruleConfig != null ? _ruleConfig.heat.overheatThreshold : 25;
    /// <summary>每张攻击牌增加的热度（默认 3）。</summary>
    public int HeatGainPerAttackCard => _ruleConfig != null ? _ruleConfig.heat.heatGainedPerAttackCard : 3;
    /// <summary>正常回合热度衰减量（默认 1）。</summary>
    public int NormalHeatDecayPerTurn => _ruleConfig != null ? _ruleConfig.heat.normalHeatDecayPerTurn : 1;
    /// <summary>切换角色回合热度衰减量（默认 6）。</summary>
    public int SwitchedHeatDecayPerTurn => _ruleConfig != null ? _ruleConfig.heat.switchedCharacterHeatDecayPerTurn : 6;

    /// <summary>热度变化时广播当前热度值（在 SetCustomData/ModifyCustomData 对 "Heat" 键集中触发，遗物订阅以驱动过载）。</summary>
    public event Action<int> OnHeatChanged;

    /// <summary>设置所有手牌费用附加（过载时 +1）。新抽到的牌也会继承该加成。</summary>
    public void SetHandCostBonus(int bonus)
    {
        _handCostBonus = Mathf.Max(0, bonus);
        foreach (var c in _hand) if (c != null) c.extraCost = _handCostBonus;
        RefreshHandUI();
    }

    /// <summary>供遗物效果触发战斗事件（如 OnHeatGained/OnHeatReduced/OnOverload）。</summary>
    public void FireTrigger(TriggerEvent ev) => _triggerSystem?.FireEvent(ev);
    /// <summary>供遗物效果更新回合计数器（如 HeatGained/HeatLost）。</summary>
    public void SetTurnCounter(string name, int value) => _turnCounters[name] = value;

    /// <summary>敌人回合：所有存活敌人按行动顺序轮流行动（不同时行动），全部结束后回到玩家回合</summary>
    private void StartEnemyTurn()
    {
        if (_battleEnded) return;
        if (_enemyTurnRoutine != null) StopCoroutine(_enemyTurnRoutine);
        _enemyTurnRoutine = StartCoroutine(RunEnemyTurnCoroutine());
    }

    /// <summary>
    /// 敌人回合协程：按 GetEnemyActionOrder 的顺序逐个让存活敌人行动；
    /// 每个敌人行动之间间隔 enemyActionInterval 秒；玩家中途死亡立即中止；
    /// 全部行动结束后进入玩家回合。
    /// </summary>
    private IEnumerator RunEnemyTurnCoroutine()
    {
        _waitingEnemyConfirm = false;
        _isPlayerTurn = false;
        if (phaseHintText != null)
            phaseHintText.text = "敌人回合";
        // 敌人回合开始：重置每个存活敌人的护甲为 0（同玩家每回合清护甲），再刷新意图预览
        foreach (var e in _enemies)
        {
            if (e == null || e.IsDead) continue;
            e.ResetArmorOnTurnStart();
        }
        UpdateUI();

        foreach (int slot in GetEnemyActionOrder())
        {
            if (_battleEnded) break;
            var inst = GetEnemy(slot);
            if (inst == null || inst.IsDead) continue;   // 死亡跳过

            // 行动前阶段判定（理智阈值 → 阶段2；理智恢复 → 阶段1；生命值不变）
            if (inst.CheckPhaseSwitch(_playerSanity, _sanityThreshold))
            {
                inst.View?.Refresh();
                RefreshEnemyIntentDeck(inst);   // 阶段切换后重新抽取并展示下一回合出牌
                Debug.Log($"[BattleManager] {inst.Name}（槽位{slot}）阶段切换 → 阶段{inst.Phase}，HP {inst.HP}/{inst.MaxHP}");
            }

            var skills = inst.GetCurrentSkills();
            if (skills != null && skills.Count > 0)
            {
                foreach (var skill in skills)
                {
                    if (_battleEnded || inst == null || inst.IsDead) break;
                    if (skill == null) continue;
                    yield return StartCoroutine(ExecuteEnemySkillCoroutine(inst, skill));
                }
            }
            else
            {
                // 无技能配置：空过敌人回合（不造成伤害）
                Debug.Log($"[BattleManager] {inst.Name} 无可用技能，空过本回合");
            }

            inst.TurnInCycle++;   // 技能轮转计数（每次行动 +1）
            // 行动完毕：意图回写为下个技能的预览（轮转计数已推进，预览的是下回合动作）

            UpdateUI();
            if (_playerHP <= 0)
            {
                HandlePlayerDefeat();
                break;
            }

            // 相邻敌人行动间隔
            if (enemyActionInterval > 0f)
                yield return new WaitForSeconds(enemyActionInterval);
        }

        _enemyTurnRoutine = null;
        if (!_battleEnded)
            StartPlayerTurn();
    }

    /// <summary>
    /// 敌人行动顺序（槽位索引迭代）：按各敌人 EnemyConfig.actionPriority 升序（数值小的先行动）；
    /// 相同 actionPriority 的敌人每回合从中随机先后（组内洗牌）。
    /// 产出是全体槽位的一个排列——每个敌人每回合恰好行动一次，已行动过的不会再次行动。
    /// </summary>
    private IEnumerable<int> GetEnemyActionOrder()
    {
        // 按行动顺序值分组（顺序值 → 槽位列表）
        var groups = new SortedDictionary<int, List<int>>();
        for (int i = 0; i < _enemies.Count; i++)
        {
            int order = _enemies[i] != null ? _enemies[i].ActionOrder : 0;
            if (!groups.TryGetValue(order, out var list))
            {
                list = new List<int>();
                groups[order] = list;
            }
            list.Add(i);
        }

        // 组间按顺序值升序（SortedDictionary 自动保证）；组内每回合随机洗牌
        foreach (var kv in groups)
        {
            var list = kv.Value;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            foreach (int slot in list)
                yield return slot;
        }
    }

    /// <summary>敌人技能执行协程：显示卡面 → 等待 → 执行效果 → 隐藏卡面。
    /// 回合推进由外层 RunEnemyTurnCoroutine 统一负责（本协程不再调 StartPlayerTurn）。</summary>
    private IEnumerator ExecuteEnemySkillCoroutine(EnemyInstance inst, CardEntry skill)
    {
        // 用玩家同款卡面（BattleCard 预制体 + CardRequest）展示敌人出牌，尽量贴近玩家视角。
        GameObject shownCard = ShowEnemyPlayedCard(skill, inst);
        bool showFallbackPanel = shownCard == null && enemySkillCard != null;

        if (shownCard != null)
        {
            _enemyPlayedCard = shownCard;
            yield return StartCoroutine(FadeCanvasGroup(shownCard, 0f, 1f, enemySkillCardFadeTime));
            yield return new WaitForSeconds(enemySkillCardDuration);
            yield return StartCoroutine(FadeCanvasGroup(shownCard, 1f, 0f, enemySkillCardFadeTime));
            if (_enemyPlayedCard == shownCard) _enemyPlayedCard = null;
            Destroy(shownCard);
        }
        else if (showFallbackPanel)
        {
            // 回退：无可用卡面预制体时沿用旧简单面板（图 + 名字 + 描述）
            enemySkillCard.SetActive(true);
            if (enemySkillCardImage != null)
            {
                enemySkillCardImage.sprite = skill.cardArt;
                enemySkillCardImage.color = skill.cardArt != null
                    ? new Color(1, 1, 1, 1f)
                    : new Color(0.05f, 0.03f, 0.1f, 0.95f);
            }
            if (enemySkillNameText != null)
            {
                enemySkillNameText.text = skill.cardName;
                enemySkillNameText.color = new Color(1f, 0.95f, 0.7f, 1f);
            }
            if (enemySkillDescText != null)
            {
                enemySkillDescText.text = skill.GetResolvedDescription(false, inst?.EffectiveStrength ?? 0, inst?.EffectiveDexterity ?? 0, true);
                enemySkillDescText.color = new Color(0.9f, 0.85f, 0.95f, 1f);
            }

            yield return StartCoroutine(FadeEnemySkillCard(0f, 1f, enemySkillCardFadeTime));
            yield return new WaitForSeconds(enemySkillCardDuration);
            yield return StartCoroutine(FadeEnemySkillCard(1f, 0f, enemySkillCardFadeTime));
            enemySkillCard.SetActive(false);
        }
        else
        {
            // 无卡面时，用意图文本显示卡名并等待
            yield return new WaitForSeconds(2f);
        }

        // 执行卡牌效果（对玩家结算）
        ExecuteEnemySkill(inst, skill);
    }

    /// <summary>
    /// 用玩家同款卡面预制体 + 卡牌编辑器 CardEntry 生成一张“敌人出牌展示卡”，
    /// 挂在敌人技能卡面板同父节点下并居中。返回该卡 GameObject；若无可用预制体返回 null。
    /// </summary>
    private GameObject ShowEnemyPlayedCard(CardEntry skill, EnemyInstance inst = null)
    {
        if (skill == null) return null;

        var prefab = skill.cardType switch
        {
            LightMiniGame.CardEditor.CardType.Attack => attackCardPrefab,
            LightMiniGame.CardEditor.CardType.Skill => skillCardPrefab,
            LightMiniGame.CardEditor.CardType.Ability => abilityCardPrefab,
            _ => attackCardPrefab
        };
        if (prefab == null) return null;

        var parent = enemySkillCard != null ? enemySkillCard.transform.parent : transform;
        var go = Instantiate(prefab, parent);
        var rt = go.transform as RectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * 1.6f;   // 放大展示

        var display = go.GetComponent<CardDisplay>();
        if (display != null)
        {
            // 传入敌人当前力量/敏捷，使卡面描述显示属性增幅后的实际数值
            if (inst != null)
                display.SetEnemyAttributeContext(inst.EffectiveStrength, inst.EffectiveDexterity);
            display.ApplyCardEntry(skill, _playerSanity <= _sanityThreshold);
        }
        else
        {
            Destroy(go);
            return null;
        }

        // 敌人出的牌置为不可出/禁用交互，仅作展示
        display.SetPlayable(true);
        var drag = go.GetComponent<CardDragHandler>();
        if (drag != null) drag.enabled = false;
        var hover = go.GetComponent<CardHoverEffect>();
        if (hover != null) hover.enabled = false;

        return go;
    }

    /// <summary>通用淡入/淡出（对带 CanvasGroup 的 GameObject；缺则补一个）。</summary>
    private IEnumerator FadeCanvasGroup(GameObject go, float from, float to, float duration)
    {
        if (go == null) yield break;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            cg.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        cg.alpha = to;
    }

    /// <summary>
    /// 执行指定敌人打出的卡牌效果（对玩家/敌人自身结算）。
    /// 把「发起者（出牌者）= 该敌人」传给 EffectExecutorV2，效果节点按相对目标解析：
    /// 当前角色/所有角色 → 玩家；效果发起者 → 该敌人自己。
    /// </summary>
    private void ExecuteEnemySkill(EnemyInstance inst, CardEntry skill)
    {
        if (inst == null || skill == null) return;

        bool lowSanity = _playerSanity <= _sanityThreshold;
        var nodes = skill.GetEffectNodes(lowSanity);
        if (nodes == null || nodes.Count == 0)
        {
            Debug.Log($"[BattleManager] {inst.Name} 出牌：{skill.cardName}（无效果节点，空结算）");
            inst.View?.Refresh();
            return;
        }

        Debug.Log($"[BattleManager] {inst.Name} 出牌：{skill.cardName}（发起者=槽位{inst.SlotIndex}，{nodes.Count}个效果）");
        _effectExecutorV2.ExecuteEffectListAsEnemy(inst.SlotIndex, nodes);

        inst.View?.Refresh();
        UpdateUI();
    }

    /// <summary>阶段切换判定已迁入 EnemyInstance.CheckPhaseSwitch（每个敌人独立维护阶段/凝视/轮转状态）；
    /// 立绘切换由 EnemyView.Refresh 按实例阶段自动处理。</summary>

    /// <summary>
    /// 根据当前玩家理智值切换战斗背景：
    /// 理智 &lt;= StartBackgroundSanityThreshold 且配置了低理智背景时，显示 lowSanityBattleBackground，
    /// 否则显示 normalBattleBackground。两者都为空则保持 Background 上既有 Sprite。
    /// </summary>
    private void ApplyBackground()
    {
        if (_backgroundImage == null) return;
        Sprite bg = (_playerSanity <= StartBackgroundSanityThreshold && StartLowSanityBattleBackground != null)
            ? StartLowSanityBattleBackground
            : StartNormalBattleBackground;
        if (bg != null)
            _backgroundImage.sprite = bg;
    }

    /// <summary>技能卡面淡入/淡出</summary>
    private IEnumerator FadeEnemySkillCard(float fromAlpha, float toAlpha, float duration)
    {
        if (enemySkillCardImage == null && enemySkillNameText == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);

            if (enemySkillCardImage != null)
            {
                var c = enemySkillCardImage.color; c.a = alpha; enemySkillCardImage.color = c;
            }
            if (enemySkillNameText != null)
            {
                var c = enemySkillNameText.color; c.a = alpha; enemySkillNameText.color = c;
            }
            if (enemySkillDescText != null)
            {
                var c = enemySkillDescText.color; c.a = alpha; enemySkillDescText.color = c;
            }
            yield return null;
        }
    }

    private void StartPlayerTurn()
    {
        _turnCount++;
        _actionPoints = maxActionPoints;
        _playerArmor = 0;
        _hasSwitchedThisTurn = false;
        _fusionUsesThisTurn = 0;       // 每回合重置融合使用
        _eventsThisTurn.Clear(); // 清除本回合事件

        // 重置本回合计数器
        ResetTurnCounters();

        // 随机抽牌：玩家回合开始时清空所有敌人本回合已抽卡牌，
        // 触发意图预览(GetCurrentSkill)即时随机抽一张并缓存，直至敌人回合实抽使用同一张。
        foreach (var e in _enemies)
            if (e != null) e.ResetDrawnSkill();

        // (TriggerSystem handles turn start)
        // (TriggerSystem handles via OnTurnStart)
        _triggerSystem?.OnTurnStart(); // 统一触发器系统回合开始

        // 热度系统：通知枪械师遗物回合开始（由遗物重置本回合过载标记等）
        OnPlayerTurnStarted?.Invoke();

        DrawCards(drawPerTurn + _slackBonusDraw);
        _slackBonusDraw = 0;
        _recycleUsedThisTurn.Clear();
        _isPlayerTurn = true;

        if (endTurnButton != null) endTurnButton.interactable = true;
        UpdateCharacterSwitchUI();
        UpdateUI();

        Debug.Log($"[BattleManager] 回合 {_turnCount} 开始，当前角色: {ActiveChar.data?.Label}");
    }

    /// <summary>重置本回合计数器</summary>
    private void ResetTurnCounters()
    {
        var keys = new List<string>(_turnCounters.Keys);
        foreach (var k in keys)
        {
            // 只重置以 ThisTurn 结尾的计数器
            if (k.Contains("ThisTurn") || k == "CardsPlayed" || k == "AttackCardsPlayed" ||
                k == "AttacksPerformed" || k == "CriticalHits" || k == "DamageTaken" ||
                k == "DamageDealt" || k == "HeatGained" || k == "HeatLost" ||
                k == "CharactersSwitched" || k == "EnemiesKilled" || k == "BlockGained" ||
                k == "CardsDrawn" || k == "CardsDiscarded" || k == "CardsExhausted")
            {
                _turnCounters[k] = 0;
            }
        }
    }

    // ========================================================================
    // 战斗结束
    // ========================================================================

    /// <summary>检查战斗结束条件：玩家死亡→失败；所有敌人都死亡→胜利（无敌人时不触发自动胜利）</summary>
    private void CheckBattleEnd()
    {
        if (_battleEnded) return;

        if (_playerHP <= 0)
        {
            HandlePlayerDefeat();
            return;
        }

        if (_enemies.Count == 0) return;
        foreach (var e in _enemies)
            if (e != null && !e.IsDead) return;   // 还有存活敌人
        EndBattle(true);
    }

    private void EndBattle(bool victory)
    {
        _battleEnded = true;
        _isPlayerTurn = false;
        _waitingEnemyConfirm = false;
        _playerBuffs?.Clear();
        _enemyBuffs?.Clear();
        if (_pilePanel != null && _pilePanel.IsOpen)
            _pilePanel.Hide();

        // 遗物效果：战斗结束钩子（枪械师热度系统在此清理）
        RelicEffectManager.Instance?.NotifyBattleEnd(this, victory);

        // 敌人能力效果：战斗结束钩子并清理
        ShutdownEnemyAbilityEffects(victory);

        if (_enemyTurnRoutine != null) { StopCoroutine(_enemyTurnRoutine); _enemyTurnRoutine = null; }
        if (_sanityTrembleRoutine != null) { StopCoroutine(_sanityTrembleRoutine); _sanityTrembleRoutine = null; }
        if (enemySkillCard != null) enemySkillCard.SetActive(false);   // 兜底：敌人行动被打断时隐藏技能卡
        if (_enemyPlayedCard != null) { Destroy(_enemyPlayedCard); _enemyPlayedCard = null; } // 兜底：清理敌人出牌展示卡
        // 胜利：启用战利品结算面板（替代原 VictoryPanel），按掉落表显示奖励按钮；
        // 点击面板上的继续按钮 → OnQuitClicked → 回到局外。失败仍用 defeatPanel。
        if (victory && lootPanel != null)
        {
            lootPanel.gameObject.SetActive(true);
            lootPanel.ShowForLootTable(StartLootTable);
        }
        if (defeatPanel != null) defeatPanel.SetActive(!victory);
        if (endTurnButton != null) endTurnButton.interactable = false;
        if (switchCharacterButton != null) switchCharacterButton.interactable = false;
        if (phaseHintText != null) phaseHintText.text = "";
        Debug.Log(victory ? "[BattleManager] 战斗胜利！" : "[BattleManager] 战斗失败！");

        // 胜负已定：广播给掉落面板等结算 UI（ChapterManager 订阅，胜利时弹 LootPanel）
        LastBattleVictory = victory;
        OnBattleFinished?.Invoke(victory);
    }

    // ========================================================================
    // 牌堆操作
    // ========================================================================

    private void ShuffleDrawPile(CharBattleState state)
    {
        for (int i = state.drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (state.drawPile[i], state.drawPile[j]) = (state.drawPile[j], state.drawPile[i]);
        }
    }

    // ========================================================================
    // UI 更新
    // ========================================================================

    private void RefreshHandUI()
    {
        if (handLayout != null)
            handLayout.UpdateHand(_hand, IsCardPlayable);
    }

    /// <summary>刷新敌人意图牌展示：只显示已抽取、下一回合（或本回合尚未打出）会打出的卡。</summary>
    private void RefreshEnemyIntentDeck(EnemyInstance e)
    {
        if (e == null || e.IsDead || e.View == null) return;
        e.View.ShowIntentDeck(e.GetCurrentSkills(), _playerSanity <= _sanityThreshold, e.EffectiveStrength, e.EffectiveDexterity);
    }

    /// <summary>取指定敌人的意图文本（预览本回合将出的所有技能卡名；无技能配置时空过）。</summary>
    private string GetEnemyIntentText(EnemyInstance inst)
    {
        if (inst == null || inst.IsDead) return "";
        if (inst.Config == null) return "…";
        var skills = inst.GetCurrentSkills();
        if (skills == null || skills.Count == 0) return "…";   // 无卡牌配置：空过本回合
        if (skills.Count == 1) return skills[0] != null ? skills[0].cardName : "…";
        var sb = new System.Text.StringBuilder();
        foreach (var s in skills)
            sb.Append(s != null ? s.cardName : "?").Append(' ');
        return sb.ToString().TrimEnd();
    }

    private void UpdateUI()
    {
        if (hpText != null) hpText.text = $"{_playerHP}/{playerMaxHP}";
        if (actionPointText != null) actionPointText.text = _actionPoints.ToString();
        if (armorText != null) armorText.text = _playerArmor > 0 ? $"{_playerArmor}" : "";

        // 玩家回合：刷新每个存活敌人的意图预览（下一回合将打出的卡）
        if (_isPlayerTurn && !_battleEnded)
        {
            foreach (var e in _enemies)
            {
                if (e == null || e.IsDead) continue;
                RefreshEnemyIntentDeck(e);
            }
        }

        if (strengthText != null) strengthText.text = _playerStrength > 0 ? $"力量: {_playerStrength}" : "";
        if (dexterityText != null) dexterityText.text = _playerDexterity > 0 ? $"敏捷: {_playerDexterity}" : "";

        if (playerHPBar != null)
        {
            playerHPBar.maxValue = playerMaxHP;
            playerHPBar.value = _playerHP;
        }

        if (sanityText != null) sanityText.text = $"{_playerSanity}/{_playerMaxSanity}";
        if (sanityBar != null)
        {
            sanityBar.maxValue = _playerMaxSanity;
            sanityBar.value = _playerSanity;
        }

        // 敌人 UI（血条/名字/护甲/立绘/凝视）由各 EnemyView 自刷（Bind/Refresh），这里不再集中管理

        if (handLayout != null)
        {
            handLayout.RefreshPlayable(IsCardPlayable);
            handLayout.RefreshCardDisplays();
        }

        // 刷新融合入口按钮可用态（每回合一次 / 非玩家回合时置灰）
        if (_fusionController != null)
            _fusionController.UpdateEntryInteractable();
        if (_pilePanel != null)
            _pilePanel.RefreshDrawIcon();

        // 同步更新局外 TopBar 文本（进入战斗后 BookCanvas 的 TopBar 仍保持显示）
        if (_bookUI == null)
            _bookUI = FindObjectOfType<BookUIController>();
        if (_bookUI != null)
            _bookUI.UpdateTopBarBattleStats(_playerHP, playerMaxHP, PlayerGold, _playerSanity, _playerMaxSanity, _playerFortune);
    }
}
