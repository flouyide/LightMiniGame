using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Buff 显示组件 —— 挂在场景中，负责刷新玩家/敌人的 buff 图标列表。
/// 每个 buff 显示一个图标 + 层数文本（正数绿色，负数红色，0 不显示）。
/// 层数显示在图标下方居中。
/// </summary>
public class BuffDisplay : MonoBehaviour
{
    [Tooltip("单个 buff 图标的预制体（需含 Image + TextMeshProUGUI）")]
    [SerializeField] private GameObject buffIconPrefab;

    [Tooltip("图标容器（HorizontalLayoutGroup）")]
    [SerializeField] private Transform buffContainer;

    [Tooltip("是否为玩家方（true=玩家，false=敌人）")]
    [SerializeField] private bool isPlayer = true;

    private BattleManager _battle;
    private readonly List<GameObject> _iconPool = new();

    private void Start()
    {
        _battle = FindObjectOfType<BattleManager>();
    }

    /// <summary>刷新 buff 显示</summary>
    public void Refresh()
    {
        if (_battle == null) _battle = FindObjectOfType<BattleManager>();
        if (_battle == null || buffContainer == null) return;

        var buffs = isPlayer ? _battle.GetPlayerDisplayedBuffs() : new List<DisplayedBuff>();

        // 隐藏多余图标
        while (_iconPool.Count > buffs.Count)
        {
            var last = _iconPool[_iconPool.Count - 1];
            if (last != null) last.SetActive(false);
            _iconPool.RemoveAt(_iconPool.Count - 1);
        }

        // 显示/创建图标
        for (int i = 0; i < buffs.Count; i++)
        {
            var buff = buffs[i];
            GameObject iconObj;
            if (i < _iconPool.Count)
            {
                iconObj = _iconPool[i];
                iconObj.SetActive(true);
            }
            else
            {
                if (buffIconPrefab == null) return;
                iconObj = Instantiate(buffIconPrefab, buffContainer);
                _iconPool.Add(iconObj);
            }

            // 图标 Image（第一个子对象或自身）
            var iconImage = iconObj.GetComponentInChildren<Image>();
            var stackText = iconObj.GetComponentInChildren<TextMeshProUGUI>();

            var buffData = _battle.GetBuffData(buff.attributeType);
            if (iconImage != null)
            {
                Sprite icon = buffData != null ? buffData.icon : null;
                if (icon == null) icon = BuffData.LoadBuiltinIcon(buff.attributeType);
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                }
            }

            if (stackText != null)
            {
                stackText.text = buff.totalStacks.ToString();
                bool debuff = BuffData.IsDebuff(buff.attributeType) || buff.totalStacks < 0;
                stackText.color = debuff
                    ? new Color(0.9f, 0.3f, 0.3f, 1f)
                    : new Color(0.3f, 0.9f, 0.3f, 1f);
                stackText.raycastTarget = false;
            }

            BuffIconHover.Bind(iconObj, buff, _battle);
        }
    }

    private void Update()
    {
        Refresh();
    }
}
