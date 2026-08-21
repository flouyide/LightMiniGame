using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private TextMeshProUGUI intentText;

    [Header("伤害飘字")]
    [Tooltip("飘字出生点（空 RectTransform）")]
    [SerializeField] private RectTransform damageAnchor;
    [Tooltip("飘字模板（含 TextMeshProUGUI 的 GameObject，可为 prefab 内隐藏的模板子物体，运行时克隆）")]
    [SerializeField] private GameObject damagePopupPrefab;

    [Header("出牌牌库意图预览")]
    [Tooltip("牌库卡面最大横向总宽（避免过宽/跨敌人重叠；超出则整体缩小）")]
    [SerializeField] private float deckMaxWidth = 340f;
    [Tooltip("牌库展示容器的纵向偏移（立绘下方，负数=下方）")]
    [SerializeField] private float deckYOffset = -152f;
    [Tooltip("牌库卡牌基础缩放（配合牌数缩放，越大越清晰）")]
    [SerializeField] private float deckBaseScale = 0.8f;
    [Tooltip("牌库卡牌之间间距（越小越紧凑）")]
    [SerializeField] private float deckGap = 8f;

    private EnemyInstance _inst;
    private Coroutine _popupRoutine;
    private bool _highlighted = false;

    // 玩家同款卡面预制体（由 BattleManager 注入），用于渲染敌人出牌库小卡
    private GameObject _attackCardPrefab;
    private GameObject _skillCardPrefab;
    private GameObject _abilityCardPrefab;

    // 出牌牌库预览容器（首次展示时创建）
    private RectTransform _deckRoot;
    private readonly List<GameObject> _deckCards = new List<GameObject>();

    /// <summary>绑定运行时实例并全量刷新显示</summary>
    public void Bind(EnemyInstance inst)
    {
        _inst = inst;
        gameObject.SetActive(true);
        Refresh();
        SetIntent("");
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

    /// <summary>敌人意图文本的 RectTransform（融合原位高亮锚点用）。</summary>
    public RectTransform IntentTextRect => intentText != null ? intentText.rectTransform : null;

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
            : (_inst != null && _inst.Phase == 2 ? Color.red : Color.white);
    }

    /// <summary>从绑定实例拉取最新状态重绘（HP/护甲/立绘/凝视/名字）。受伤、阶段切换后调用。</summary>
    public void Refresh()
    {
        if (_inst == null) return;
        var cfg = _inst.Config;

        if (nameText != null) nameText.text = _inst.Name;
        if (hpText != null) hpText.text = $"{_inst.HP}/{_inst.MaxHP}";
        if (hpBar != null) hpBar.value = _inst.MaxHP > 0 ? Mathf.Clamp01((float)_inst.HP / _inst.MaxHP) : 0f;
        if (armorText != null) armorText.text = _inst.Armor > 0 ? $"护甲: {_inst.Armor}" : "";

        if (portraitImage != null && cfg != null)
        {
            var sprite = (_inst.Phase == 2 && cfg.phase2Portrait != null) ? cfg.phase2Portrait : cfg.phase1Portrait;
            if (sprite != null) portraitImage.sprite = sprite;
            // 阶段2红色高亮（沿用原单敌人逻辑）
            portraitImage.color = _inst.Phase == 2 ? Color.red : Color.white;
        }
    }

    /// <summary>设置意图文本（玩家回合预览下个技能名；敌人回合由 BattleManager 控制）</summary>
    public void SetIntent(string text)
    {
        if (intentText != null) intentText.text = text ?? "";
    }

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
    /// 在敌人立绘下方横向展示当前阶段整个出牌牌库（small casino 小卡）。
    /// 自动按牌数缩放/限宽，避免多张重叠拥挤；多敌人各自在各自立绘下方，互不重叠。
    /// lowSanity=true 时卡面用低理智（升级）形态显示（费用/描述随 lowSanity 变）。
    /// </summary>
    public void ShowIntentDeck(List<CardEntry> deck, bool lowSanity = false)
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
            go.transform.SetParent(transform, false);
            _deckRoot = go.GetComponent<RectTransform>();
        }
        _deckRoot.gameObject.SetActive(true);

        // 定位：立绘下方居中
        _deckRoot.anchorMin = _deckRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _deckRoot.pivot = new Vector2(0.5f, 0.5f);
        _deckRoot.anchoredPosition = new Vector2(0f, deckYOffset);

        // 逐个实例化卡面
        List<(GameObject go, float w)> cards = new List<(GameObject, float)>();
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
            if (display != null) display.ApplyCardEntry(entry, lowSanity);

            // 禁用交互仅展示
            var drag = cardGo.GetComponent<CardDragHandler>();
            if (drag != null) drag.enabled = false;
            var hover = cardGo.GetComponent<CardHoverEffect>();
            if (hover != null) hover.enabled = false;

            var cardRect = cardGo.GetComponent<RectTransform>();
            float w = cardRect != null ? cardRect.rect.width : 148f;
            cards.Add((cardGo, w));
            _deckCards.Add(cardGo);
        }

        if (cards.Count == 0)
        {
            _deckRoot.gameObject.SetActive(false);
            return;
        }

        // 横向布局：总宽超 deckMaxWidth 则整体再缩小，保持不重叠
        float gap = deckGap;
        float totalW = 0f;
        foreach (var c in cards) totalW += c.w;
        totalW += gap * (cards.Count - 1);

        float scale = deckBaseScale;
        if (totalW * scale > deckMaxWidth)
            scale = deckMaxWidth / totalW;

        // 逐卡定位：以缩放后的实际尺寸排布。注意 localScale 会同时缩放位置偏移，
        // 因此相邻卡的中心距 = (缩放后卡宽 + 缩放后间距) = scale * (w + gap)。
        float scaledW = cards[0].w * scale;
        float curX = -((totalW / 2f) * scale) + scaledW / 2f;   // 首卡中心（居中对齐）
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            var ct = c.go.transform as RectTransform;
            ct.anchorMin = ct.anchorMax = new Vector2(0.5f, 0.5f);
            ct.pivot = new Vector2(0.5f, 0.5f);
            ct.anchoredPosition = new Vector2(curX, 0f);
            ct.localScale = Vector3.one * scale;
            curX += (c.w + gap) * scale;   // 注意乘以 scale：位置随缩放同步，保证视觉紧凑无空隙
        }
    }

    /// <summary>飘字显示伤害数字（从 BattleManager.ShowEnemyDamage 迁入，锚点改为本视图的 damageAnchor）</summary>
    public void ShowDamage(int amount, bool isCrit = false)
    {
        if (amount <= 0) return;
        if (damagePopupPrefab == null || damageAnchor == null) return;

        var go = Instantiate(damagePopupPrefab, damageAnchor);
        var rect = go.GetComponent<RectTransform>();
        if (rect != null) rect.anchoredPosition = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) text = go.GetComponentInChildren<TextMeshProUGUI>();
        if (text == null)
        {
            Debug.LogWarning("[EnemyView] damagePopupPrefab 缺少 TextMeshProUGUI 组件");
            Destroy(go);
            return;
        }

        text.text = isCrit ? $"{amount}!" : amount.ToString();
        text.color = isCrit ? new Color(1f, 0.8f, 0.1f, 1f) : new Color(1f, 0.35f, 0.2f, 1f);
        text.gameObject.SetActive(true);

        if (_popupRoutine != null) StopCoroutine(_popupRoutine);
        _popupRoutine = StartCoroutine(DamagePopupRoutine(text));
    }

    /// <summary>死亡：停止飘字并隐藏整个视图（尸体不保留在场上）</summary>
    public void Hide()
    {
        if (_popupRoutine != null)
        {
            StopCoroutine(_popupRoutine);
            _popupRoutine = null;
        }
        ClearIntentDeck();
        gameObject.SetActive(false);
    }

    private IEnumerator DamagePopupRoutine(TextMeshProUGUI text)
    {
        if (text == null) { _popupRoutine = null; yield break; }
        var rect = text.GetComponent<RectTransform>();
        Vector2 startPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        const float duration = 0.8f;
        float elapsed = 0f;
        Color baseColor = text.color;

        while (elapsed < duration)
        {
            if (text == null) { _popupRoutine = null; yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (rect != null)
                rect.anchoredPosition = startPos + new Vector2(0f, 60f * t);

            float alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        if (text != null) Destroy(text.gameObject);
        _popupRoutine = null;
    }
}