using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LightMiniGame.CardEditor;

/// <summary>
/// 单个敌人的战斗视图（MonoBehaviour，挂在 EnemyView.prefab 上）：
/// 立绘 / 名字 / HP条 / 护甲 / 意图 / 凝视值 / 伤害飘字 / 出牌库横向预览。
/// 由 BattleManager 按 EnemySpawnInfo 实例化到 EnemyContainer 下，并与 EnemyInstance 绑定（Bind）。
/// 每个敌人一个实例，各自维护自己的 UI，互不干扰。
/// </summary>
public class EnemyView : MonoBehaviour
{
    [Header("UI 引用（prefab 内接线）")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Slider hpBar;
    [SerializeField] private TextMeshProUGUI armorText;

    [Header("Buff 栏（血条正下方左侧）")]
    [Tooltip("单个 buff 图标预制体（含 Image + TMP）。留空则运行时用色块+字生成")]
    [SerializeField] private GameObject buffIconPrefab;
    [SerializeField] private Sprite fatigueIcon;
    [SerializeField] private Sprite armorBreakIcon;
    [SerializeField] private Sprite strengthIcon;

    [Header("伤害飘字")]
    [Tooltip("飘字出生点（空 RectTransform）。留空则绕本视图中心一圈随机")]
    [SerializeField] private RectTransform damageAnchor;
    [Tooltip("飘字模板（含 TextMeshProUGUI 的 GameObject，可为 prefab 内隐藏的模板子物体，运行时克隆）")]
    [SerializeField] private GameObject damagePopupPrefab;
    [Tooltip("飘字随机环的内半径（相对敌人中心）")]
    [SerializeField] private float damagePopupMinRadius = 110f;
    [Tooltip("飘字随机环的外半径（相对敌人中心）")]
    [SerializeField] private float damagePopupMaxRadius = 175f;
    [Tooltip("突然出现后停留秒数，再开始淡出")]
    [SerializeField] private float damagePopupHold = 0.2f;
    [Tooltip("淡出持续秒数")]
    [SerializeField] private float damagePopupFade = 0.55f;

    [Header("出牌意图预览")]
    [Tooltip("意图卡面最大纵向总高（避免过高超出屏幕；超出则整体缩小）")]
    [SerializeField] private float deckMaxHeight = 400f;
    [Tooltip("牌库展示容器的横向偏移（正数=右侧，用于放到怪物头右边）")]
    [SerializeField] private float deckXOffset = 220f;
    [Tooltip("牌库展示容器的纵向偏移（正数=上方，靠近头部）")]
    [SerializeField] private float deckYOffset = 10f;
    [Tooltip("牌库卡牌基础缩放（配合牌数缩放，越大越清晰）")]
    [SerializeField] private float deckBaseScale = 0.48f;
    [Tooltip("牌库卡牌之间间距（越小越紧凑）")]
    [SerializeField] private float deckGap = 4f;

    private EnemyInstance _inst;
    private readonly List<GameObject> _livePopups = new List<GameObject>();
    private bool _highlighted = false;
    private Coroutine _shakeRoutine;
    private Vector2 _portraitRestPos;
    private bool _portraitRestPosCaptured;

    // 玩家同款卡面预制体（由 BattleManager 注入），用于渲染敌人出牌库小卡
    private GameObject _attackCardPrefab;
    private GameObject _skillCardPrefab;
    private GameObject _abilityCardPrefab;

    // 出牌牌库预览容器（首次展示时创建）
    private RectTransform _deckRoot;
    private readonly List<GameObject> _deckCards = new List<GameObject>();

    // 血条下方 buff 栏
    private RectTransform _buffDeckRoot;
    private readonly List<GameObject> _buffIconPool = new List<GameObject>();

    /// <summary>绑定运行时实例并全量刷新显示</summary>
    public void Bind(EnemyInstance inst)
    {
        _inst = inst;
        gameObject.SetActive(true);
        Refresh();
    }

    /// <summary>敌人护甲文本的 RectTransform（融合原位高亮锚点用）。</summary>
    public RectTransform ArmorTextRect => armorText != null ? armorText.rectTransform : null;

    /// <summary>敌人血量文本的 RectTransform（融合原位高亮锚点用，显示如 "65/65"）。</summary>
    public RectTransform HPTextRect => hpText != null ? hpText.rectTransform : null;

    /// <summary>
    /// 定位敌人 HP 文本（格式 "当前/上限"）中指定部分数字的世界矩形。
    /// isMax=false 定位当前值（斜杠前），isMax=true 定位上限值（斜杠后）。
    /// </summary>
    public bool TryGetEnemyHPNumberRect(bool isMax, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;
        if (hpText == null) return false;

        hpText.ForceMeshUpdate(true);
        var info = hpText.textInfo;
        if (info == null || info.characterInfo == null || info.characterCount == 0) return false;

        string s = hpText.text;
        int tokenSeen = 0;
        int startChar = -1, endChar = -1;
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i])) continue;
            int start = i;
            int end = i;
            while (end + 1 < s.Length && char.IsDigit(s[end + 1])) end++;
            if (tokenSeen == (isMax ? 1 : 0)) { startChar = start; endChar = end; break; }
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
            Vector3 tl = hpText.transform.TransformPoint(ch.topLeft);
            Vector3 tr = hpText.transform.TransformPoint(ch.topRight);
            Vector3 bl = hpText.transform.TransformPoint(ch.bottomLeft);
            Vector3 br = hpText.transform.TransformPoint(ch.bottomRight);
            min = Vector3.Min(min, Vector3.Min(Vector3.Min(tl, bl), Vector3.Min(tr, br)));
            max = Vector3.Max(max, Vector3.Max(Vector3.Max(tl, bl), Vector3.Max(tr, br)));
            found = true;
        }
        if (!found) return false;
        center = (Vector2)((min + max) * 0.5f);
        size = new Vector2(max.x - min.x, max.y - min.y);
        return true;
    }

    /// <summary>意图牌库中每张小卡的 CardDisplay 列表（供融合高亮意图数值用）。</summary>
    public List<CardDisplay> IntentDeckDisplays
    {
        get
        {
            var list = new List<CardDisplay>();
            foreach (var go in _deckCards)
            {
                if (go == null) continue;
                var d = go.GetComponent<CardDisplay>();
                if (d != null) list.Add(d);
            }
            return list;
        }
    }


    /// <summary>
    /// 标记/取消该敌人为当前受击对象（拖拽卡牌悬停其上时高亮）。
    /// 通过临时染色立绘实现，取消高亮时恢复原色。
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted) return;
        _highlighted = highlighted;
        if (portraitImage == null) return;
        portraitImage.color = _highlighted
            ? new Color(1f, 1f, 0.45f)
            : Color.white;
    }

    /// <summary>从绑定实例拉取最新状态重绘（HP/护甲/立绘/凝视/名字）。受伤、阶段切换后调用。</summary>
    public void Refresh()
    {
        if (_inst == null) return;
        var cfg = _inst.Config;

        if (nameText != null) nameText.text = _inst.Name;
        if (hpText != null) hpText.text = $"{_inst.HP}/{_inst.MaxHP}";
        if (hpBar != null) hpBar.value = _inst.MaxHP > 0 ? Mathf.Clamp01((float)_inst.HP / _inst.MaxHP) : 0f;
        if (armorText != null) armorText.text = _inst.Armor > 0 ? $"{_inst.Armor}" : "";

        RefreshBuffDeck();

        if (portraitImage != null && cfg != null)
        {
            var sprite = (_inst.Phase == 2 && cfg.phase2Portrait != null) ? cfg.phase2Portrait : cfg.phase1Portrait;
            if (sprite != null) portraitImage.sprite = sprite;
            //if (_shakeRoutine == null)
                //portraitImage.color = PortraitRestColor();
        }
    }


    /// <summary>血条正下方左侧：疲惫 / 破甲 / 力量。0 层不显示。</summary>
    private void RefreshBuffDeck()
    {
        EnsureBuffDeck();
        if (_buffDeckRoot == null || _inst == null) return;

        var buffs = _inst.GetDisplayedBuffs();
        _buffDeckRoot.gameObject.SetActive(buffs.Count > 0);

        for (int i = 0; i < buffs.Count; i++)
        {
            GameObject iconGo;
            if (i < _buffIconPool.Count && _buffIconPool[i] != null)
            {
                iconGo = _buffIconPool[i];
                iconGo.SetActive(true);
            }
            else
            {
                iconGo = CreateBuffIcon();
                if (iconGo == null) return;
                if (i < _buffIconPool.Count) _buffIconPool[i] = iconGo;
                else _buffIconPool.Add(iconGo);
            }
            ApplyBuffIcon(iconGo, buffs[i]);
        }
        for (int i = buffs.Count; i < _buffIconPool.Count; i++)
        {
            if (_buffIconPool[i] != null) _buffIconPool[i].SetActive(false);
        }
    }

    private void EnsureBuffDeck()
    {
        if (_buffDeckRoot != null) return;

        var hpRt = hpBar != null ? hpBar.transform as RectTransform : transform.Find("HPBar") as RectTransform;
        var go = new GameObject("BuffDeck", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _buffDeckRoot = go.GetComponent<RectTransform>();
        _buffDeckRoot.SetParent(transform, false);

        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        _buffDeckRoot.pivot = new Vector2(0f, 1f);
        if (hpRt != null)
        {
            _buffDeckRoot.anchorMin = hpRt.anchorMin;
            _buffDeckRoot.anchorMax = hpRt.anchorMax;
            float width = hpRt.rect.width > 1f ? hpRt.rect.width : hpRt.sizeDelta.x;
            float height = hpRt.rect.height > 1f ? hpRt.rect.height : hpRt.sizeDelta.y;
            float left = hpRt.anchoredPosition.x - width * hpRt.pivot.x;
            float bottom = hpRt.anchoredPosition.y - height * hpRt.pivot.y;
            _buffDeckRoot.anchoredPosition = new Vector2(left, bottom - 2f);
            _buffDeckRoot.sizeDelta = new Vector2(width, 36f);
        }
        else
        {
            _buffDeckRoot.anchorMin = _buffDeckRoot.anchorMax = new Vector2(0.5f, 1f);
            _buffDeckRoot.anchoredPosition = new Vector2(-213f, -82f);
            _buffDeckRoot.sizeDelta = new Vector2(438f, 36f);
        }

        _buffDeckRoot.SetSiblingIndex(hpRt != null ? hpRt.GetSiblingIndex() + 1 : 0);
    }

    private GameObject CreateBuffIcon()
    {
        if (buffIconPrefab == null)
        {
#if UNITY_EDITOR
            buffIconPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Battle/BuffIcon.prefab");
#endif
        }
        if (buffIconPrefab != null)
            return Instantiate(buffIconPrefab, _buffDeckRoot);

        var go = new GameObject("BuffIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(_buffDeckRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(32f, 32f);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 32f;
        le.preferredHeight = 32f;
        le.minWidth = 32f;
        le.minHeight = 32f;

        var icon = go.GetComponent<Image>();
        icon.raycastTarget = true;
        icon.preserveAspect = true;

        var labelGo = new GameObject("StackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(1f, 0f);
        labelRt.anchorMax = new Vector2(1f, 0f);
        labelRt.pivot = new Vector2(1f, 0f);
        labelRt.anchoredPosition = Vector2.zero;
        labelRt.sizeDelta = new Vector2(24f, 20f);
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        if (hpText != null)
        {
            tmp.font = hpText.font;
            if (hpText.fontSharedMaterial != null)
                tmp.fontSharedMaterial = hpText.fontSharedMaterial;
        }
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return go;
    }

    private void ApplyBuffIcon(GameObject iconGo, DisplayedBuff buff)
    {
        if (iconGo == null) return;
        var iconImage = iconGo.GetComponent<Image>();
        if (iconImage == null) iconImage = iconGo.GetComponentInChildren<Image>();
        var stackText = iconGo.GetComponentInChildren<TextMeshProUGUI>();

        var sprite = buff.customIcon != null ? buff.customIcon : ResolveBuffSprite(buff.attributeType);
        if (iconImage != null)
        {
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color = buff.hideStacks
                    ? new Color(0.7f, 0.55f, 0.2f, 1f)
                    : BuffFallbackColor(buff.attributeType);
            }
            iconImage.raycastTarget = true;
        }

        if (stackText != null)
        {
            if (buff.hideStacks)
            {
                stackText.text = "";
            }
            else
            {
                stackText.text = buff.totalStacks.ToString();
                bool debuff = buff.attributeType == BuffAttributeType.Fatigue
                    || buff.attributeType == BuffAttributeType.ArmorBreak
                    || buff.totalStacks < 0;
                stackText.color = debuff
                    ? new Color(0.9f, 0.3f, 0.3f, 1f)
                    : new Color(0.3f, 0.9f, 0.3f, 1f);
            }
            stackText.raycastTarget = false;
        }

        var battle = Object.FindObjectOfType<BattleManager>();
        BuffIconHover.Bind(iconGo, buff, battle);
    }

    private Sprite ResolveBuffSprite(BuffAttributeType type)
    {
        Sprite assigned = type switch
        {
            BuffAttributeType.Fatigue => fatigueIcon,
            BuffAttributeType.ArmorBreak => armorBreakIcon,
            BuffAttributeType.Strength => strengthIcon,
            _ => null
        };
        if (assigned != null) return assigned;
        return LoadBuiltinBuffSprite(type);
    }

    private static readonly Dictionary<BuffAttributeType, Sprite> BuiltinBuffSprites = new();
    private static bool _builtinBuffSpritesTried;

    private static Sprite LoadBuiltinBuffSprite(BuffAttributeType type)
    {
        if (!_builtinBuffSpritesTried)
        {
            _builtinBuffSpritesTried = true;
#if UNITY_EDITOR
            BuiltinBuffSprites[BuffAttributeType.Fatigue] =
                BuffData.LoadBuiltinIcon(BuffAttributeType.Fatigue);
            BuiltinBuffSprites[BuffAttributeType.ArmorBreak] =
                BuffData.LoadBuiltinIcon(BuffAttributeType.ArmorBreak);
            BuiltinBuffSprites[BuffAttributeType.Strength] =
                BuffData.LoadBuiltinIcon(BuffAttributeType.Strength);
#endif
        }
        return BuiltinBuffSprites.TryGetValue(type, out var s) ? s : null;
    }

    private static Color BuffFallbackColor(BuffAttributeType type) => type switch
    {
        BuffAttributeType.Fatigue => new Color(0.55f, 0.4f, 0.15f, 1f),
        BuffAttributeType.ArmorBreak => new Color(0.75f, 0.45f, 0.15f, 1f),
        BuffAttributeType.Strength => new Color(0.85f, 0.25f, 0.2f, 1f),
        _ => new Color(0.5f, 0.5f, 0.5f, 1f)
    };

    /// <summary>注入玩家同款卡面预制体（出牌牌库预览用），由 BattleManager 生成敌人时调用。</summary>
    public void SetCardPrefabs(GameObject attack, GameObject skill, GameObject ability)
    {
        _attackCardPrefab = attack;
        _skillCardPrefab = skill;
        _abilityCardPrefab = ability;
    }

    /// <summary>隐藏/清空出牌牌库预览。</summary>
    public void ClearIntentDeck()
    {
        foreach (var c in _deckCards)
            if (c != null) Destroy(c);
        _deckCards.Clear();
        if (_deckRoot != null) _deckRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 在敌人立绘旁纵向展示下一回合将打出的卡牌（small casino 小卡）。
    /// 自动按牌数缩放/限高，避免多张重叠拥挤；多敌人各自在各自立绘旁，互不重叠。
    /// lowSanity=true 时卡面用低理智（升级）形态显示（费用/描述随 lowSanity 变）。
    /// </summary>
    public void ShowIntentDeck(List<CardEntry> deck, bool lowSanity = false, int enemyStrength = 0, int enemyDexterity = 0, EnemyInstance fusionSource = null)
    {
        // 清空上一批
        foreach (var c in _deckCards)
            if (c != null) Destroy(c);
        _deckCards.Clear();

        if (deck == null || deck.Count == 0)
        {
            if (_deckRoot != null) _deckRoot.gameObject.SetActive(false);
            return;
        }

        if (_deckRoot == null)
        {
            var go = new GameObject("IntentDeck", typeof(RectTransform));
            _deckRoot = go.GetComponent<RectTransform>();
        }

        // 先按敌人本地坐标摆好，再提到所有立绘之上的独立层，避免后生成的敌人画像盖住先手的意图牌
        _deckRoot.SetParent(transform, false);
        _deckRoot.gameObject.SetActive(true);
        _deckRoot.anchorMin = _deckRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _deckRoot.pivot = new Vector2(0.5f, 0.5f);
        _deckRoot.anchoredPosition = new Vector2(deckXOffset, deckYOffset);

        // 逐个实例化卡面
        List<(GameObject go, float w, float h)> cards = new List<(GameObject, float, float)>();
        for (int i = 0; i < deck.Count; i++)
        {
            var entry = deck[i];
            if (entry == null) continue;
            var prefab = entry.cardType switch
            {
                LightMiniGame.CardEditor.CardType.Attack => _attackCardPrefab,
                LightMiniGame.CardEditor.CardType.Skill => _skillCardPrefab,
                LightMiniGame.CardEditor.CardType.Ability => _abilityCardPrefab,
                _ => _attackCardPrefab
            };
            if (prefab == null) continue;

            var cardGo = Instantiate(prefab, _deckRoot);
            var display = cardGo.GetComponent<CardDisplay>();
            if (display != null)
            {
                // 先设置敌人属性上下文，再应用卡牌数据（ApplyCardEntry 内部会调用 UpdateDisplay 生成描述）
                display.SetEnemyAttributeContext(enemyStrength, enemyDexterity);
                display.SetIntentSkillIndex(i);
                display.ApplyCardEntry(entry, lowSanity);
                var fusion = fusionSource != null ? fusionSource.GetSkillFusion(i) : null;
                if (fusion != null && fusion.HasAny)
                    display.ApplyFusionOverlay(fusion);
            }

            // 禁用交互仅展示
            var drag = cardGo.GetComponent<CardDragHandler>();
            if (drag != null) drag.enabled = false;
            var hover = cardGo.GetComponent<CardHoverEffect>();
            if (hover != null) hover.enabled = false;

            // 悬停放大预览（仅展示交互：鼠标移入放大到原尺寸清晰查看，移出恢复）
            cardGo.AddComponent<IntentCardHover>();

            var cardRect = cardGo.GetComponent<RectTransform>();
            float w = cardRect != null ? cardRect.rect.width : 148f;
            float h = cardRect != null ? cardRect.rect.height : 200f;
            cards.Add((cardGo, w, h));
            _deckCards.Add(cardGo);
        }

        if (cards.Count == 0)
        {
            _deckRoot.gameObject.SetActive(false);
            return;
        }

        // 纵向布局：总高超 deckMaxHeight 则整体再缩小，保持不重叠
        float gap = deckGap;
        float totalH = 0f;
        foreach (var c in cards) totalH += c.h;
        totalH += gap * (cards.Count - 1);

        float scale = deckBaseScale;
        if (totalH * scale > deckMaxHeight)
            scale = deckMaxHeight / totalH;

        // 逐卡定位：以缩放后的实际尺寸纵向排布。相邻卡的中心距 = scale * (h + gap)。
        float scaledH = cards[0].h * scale;
        float curY = ((totalH / 2f) * scale) - scaledH / 2f;   // 首卡中心（居中对齐，从上到下）
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            var ct = c.go.transform as RectTransform;
            ct.anchorMin = ct.anchorMax = new Vector2(0.5f, 0.5f);
            ct.pivot = new Vector2(0.5f, 0.5f);
            ct.anchoredPosition = new Vector2(0f, curY);
            ct.localScale = Vector3.one * scale;
            curY -= (c.h + gap) * scale;   // 向下排列
        }

        AttachDeckAbovePortraits();
    }

    /// <summary>
    /// 把意图牌挂到 EnemyContainer 的下一个兄弟节点上，保证所有意图牌都画在所有敌人立绘之上。
    /// </summary>
    private void AttachDeckAbovePortraits()
    {
        if (_deckRoot == null) return;
        var overlay = EnsureIntentDeckOverlay();
        if (overlay == null) return;
        _deckRoot.SetParent(overlay, true);
        _deckRoot.SetAsLastSibling();
    }

    private Transform EnsureIntentDeckOverlay()
    {
        var container = transform.parent;
        if (container == null) return null;
        var canvasParent = container.parent;
        if (canvasParent == null) return container;

        Transform overlay = canvasParent.Find("IntentDeckOverlay");
        if (overlay == null)
        {
            var go = new GameObject("IntentDeckOverlay", typeof(RectTransform));
            overlay = go.transform;
            overlay.SetParent(canvasParent, false);
            var rt = overlay as RectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        if (FusionController.IsOpen && FusionController.PanelTransform != null
            && FusionController.PanelTransform.parent == canvasParent)
        {
            int afterPanel = FusionController.PanelTransform.GetSiblingIndex() + 1;
            if (overlay.GetSiblingIndex() != afterPanel)
                overlay.SetSiblingIndex(afterPanel);
            var entry = canvasParent.Find("FusionEntryButton");
            if (entry != null)
                entry.SetAsLastSibling();
            return overlay;
        }

        int afterContainer = container.GetSiblingIndex() + 1;
        if (overlay.GetSiblingIndex() != afterContainer)
            overlay.SetSiblingIndex(afterContainer);
        return overlay;
    }

    private void OnDestroy()
    {
        if (_deckRoot != null)
            Destroy(_deckRoot.gameObject);
    }

    /// <summary>
    /// 玩家出牌打到该敌人时，在敌人周围一圈随机位置突然弹出 "-X"，停留后淡出。
    /// 多次伤害可同时存在，互不打断。
    /// </summary>
    public void ShowDamage(int amount, bool isCrit = false)
    {
        if (amount <= 0) return;

        var parent = transform as RectTransform;
        if (parent == null) return;

        GameObject go;
        TextMeshProUGUI text;
        if (damagePopupPrefab != null)
        {
            go = Instantiate(damagePopupPrefab, parent);
            text = go.GetComponent<TextMeshProUGUI>();
            if (text == null) text = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null)
            {
                Debug.LogWarning("[EnemyView] damagePopupPrefab 缺少 TextMeshProUGUI 组件");
                Destroy(go);
                return;
            }
        }
        else
        {
            go = new GameObject("DamagePopup");
            go.transform.SetParent(parent, false);
            var created = go.AddComponent<RectTransform>();
            created.sizeDelta = new Vector2(160f, 56f);
            text = go.AddComponent<TextMeshProUGUI>();
            if (hpText != null)
            {
                text.font = hpText.font;
                if (hpText.fontSharedMaterial != null)
                    text.fontSharedMaterial = hpText.fontSharedMaterial;
            }
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        go.SetActive(true);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float minR = Mathf.Max(0f, damagePopupMinRadius);
        float maxR = Mathf.Max(minR, damagePopupMaxRadius);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Random.Range(minR, maxR);
        rt.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        text.raycastTarget = false;
        text.fontStyle = FontStyles.Bold;
        text.margin = Vector4.zero;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableWordWrapping = false;
        text.fontSize = isCrit ? 40 : 34;
        text.text = isCrit ? $"-{amount}!" : $"-{amount}";
        Color color = isCrit ? new Color(1f, 0.82f, 0.15f, 1f) : new Color(1f, 0.28f, 0.22f, 1f);
        text.color = color;
        ApplyWhiteOutline(text);

        _livePopups.Add(go);
        StartCoroutine(DamagePopupRoutine(go, text, color));
    }

    /// <summary>受击反馈：立绘短暂震动 + 闪白。</summary>
    public void PlayHitFeedback()
    {
        if (portraitImage == null) return;
        var rt = portraitImage.rectTransform;
        if (rt == null) return;

        if (!_portraitRestPosCaptured)
        {
            _portraitRestPos = rt.anchoredPosition;
            _portraitRestPosCaptured = true;
        }

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        rt.anchoredPosition = _portraitRestPos;
        _shakeRoutine = StartCoroutine(HitShakeRoutine(rt));
    }

    /// <summary>死亡：清掉飘字并隐藏整个视图（尸体不保留在场上）</summary>
    public void Hide()
    {
        StopAllCoroutines();
        _shakeRoutine = null;
        if (portraitImage != null && _portraitRestPosCaptured)
            portraitImage.rectTransform.anchoredPosition = _portraitRestPos;
        foreach (var p in _livePopups)
            if (p != null) Destroy(p);
        _livePopups.Clear();
        ClearIntentDeck();
        gameObject.SetActive(false);
    }

    private IEnumerator HitShakeRoutine(RectTransform rt)
    {
        const float duration = 0.22f;
        const float magnitude = 18f;
        Color restColor = PortraitRestColor();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damp = 1f - Mathf.Clamp01(elapsed / duration);
            rt.anchoredPosition = _portraitRestPos + Random.insideUnitCircle * (magnitude * damp);
            if (portraitImage != null)
                portraitImage.color = Color.Lerp(Color.white, restColor, 1f - damp);
            yield return null;
        }
        rt.anchoredPosition = _portraitRestPos;
        if (portraitImage != null)
            portraitImage.color = restColor;
        _shakeRoutine = null;
    }

    private Color PortraitRestColor()
    {
        if (_highlighted) return new Color(1f, 1f, 0.45f);
        return Color.white;
    }

    /// <summary>
    /// 白色描边：当前伤害字用的是像素 SDF 字体，材质 Outline 几乎看不见，
    /// 所以用八方向错位复制一层白字，保证任意字体都有一圈描边。
    /// </summary>
    private static readonly Vector2[] OutlineDirs =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, -1f), new Vector2(0f, 1f),
        new Vector2(-1f, -1f), new Vector2(1f, -1f),
        new Vector2(-1f, 1f), new Vector2(1f, 1f)
    };

    private static void ApplyWhiteOutline(TextMeshProUGUI source, float pixelOffset = 0.7f)
    {
        if (source == null) return;

        var mat = source.fontMaterial;
        if (mat != null)
        {
            mat.EnableKeyword("OUTLINE_ON");
            if (mat.HasProperty("_OutlineWidth"))
                mat.SetFloat("_OutlineWidth", 0.02f);
            if (mat.HasProperty("_FaceDilate"))
                mat.SetFloat("_FaceDilate", 0.1f);
            if (mat.HasProperty("_OutlineColor"))
                mat.SetColor("_OutlineColor", Color.white);
            source.fontMaterial = mat;
            source.UpdateMeshPadding();
        }

        for (int i = 0; i < OutlineDirs.Length; i++)
        {
            var stroke = new GameObject("Stroke");
            stroke.transform.SetParent(source.transform, false);
            stroke.transform.SetAsFirstSibling();
            var rt = stroke.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            Vector2 shift = OutlineDirs[i] * pixelOffset;
            rt.offsetMin = shift;
            rt.offsetMax = shift;
            var tmp = stroke.AddComponent<TextMeshProUGUI>();
            tmp.font = source.font;
            tmp.fontSharedMaterial = source.fontSharedMaterial;
            tmp.fontSize = source.fontSize;
            tmp.fontStyle = source.fontStyle;
            tmp.alignment = source.alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.margin = Vector4.zero;
            tmp.text = source.text;
            tmp.color = Color.white;
        }
    }

    private IEnumerator DamagePopupRoutine(GameObject go, TextMeshProUGUI text, Color baseColor)
    {
        // 突然出现：已是满透明度，先停一瞬再淡出
        if (damagePopupHold > 0f)
            yield return new WaitForSeconds(damagePopupHold);

        float fade = Mathf.Max(0.05f, damagePopupFade);
        float elapsed = 0f;
        var labels = go != null ? go.GetComponentsInChildren<TextMeshProUGUI>(true) : null;
        while (elapsed < fade)
        {
            if (text == null) break;
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fade);
            if (labels != null)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    var label = labels[i];
                    if (label == null) continue;
                    Color c = label == text ? baseColor : Color.white;
                    label.color = new Color(c.r, c.g, c.b, alpha);
                }
            }
            yield return null;
        }

        if (go != null)
        {
            _livePopups.Remove(go);
            Destroy(go);
        }
    }

    /// <summary>
    /// 敌人意图牌库小卡的悬停放大查看：
    /// 鼠标移入时放大并置顶（保持原位），移出后恢复。
    /// 用射线命中判断（含子物体上的融合高亮），避免点到数字时卡牌缩回去。
    /// </summary>
    private class IntentCardHover : MonoBehaviour
    {
        private Vector3 _origScale;
        private int _origSibling;
        private bool _enlarged;
        private const float EnlargeScale = 2.6f;
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>(16);

        private void LateUpdate()
        {
            if (IsPointerOverThisCard())
                Enlarge();
            else
                Shrink();
        }

        private bool IsPointerOverThisCard()
        {
            if (EventSystem.current == null) return false;
            var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            Hits.Clear();
            EventSystem.current.RaycastAll(ped, Hits);
            for (int i = 0; i < Hits.Count; i++)
            {
                var hover = Hits[i].gameObject.GetComponentInParent<IntentCardHover>();
                if (hover == null) continue;
                return hover == this;
            }
            return false;
        }

        private void Enlarge()
        {
            if (_enlarged) return;
            var rt = transform as RectTransform;
            if (rt == null) return;
            _enlarged = true;
            _origSibling = transform.GetSiblingIndex();
            _origScale = transform.localScale;
            transform.SetAsLastSibling();
            transform.localScale = _origScale * EnlargeScale;
        }

        private void Shrink()
        {
            if (!_enlarged) return;
            _enlarged = false;
            transform.SetSiblingIndex(_origSibling);
            transform.localScale = _origScale;
        }
    }
}