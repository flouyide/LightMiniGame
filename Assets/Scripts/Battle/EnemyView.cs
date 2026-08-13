using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个敌人的战斗视图（MonoBehaviour，挂在 EnemyView.prefab 上）：
/// 立绘 / 名字 / HP条 / 护甲 / 意图 / 凝视值 / 伤害飘字。
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

    private EnemyInstance _inst;
    private Coroutine _popupRoutine;
    private bool _highlighted = false;

    /// <summary>绑定运行时实例并全量刷新显示</summary>
    public void Bind(EnemyInstance inst)
    {
        _inst = inst;
        gameObject.SetActive(true);
        Refresh();
        SetIntent("");
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
