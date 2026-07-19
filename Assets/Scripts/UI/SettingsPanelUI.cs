using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置 / 选项面板。挂在 SettingsPanel.prefab 的 SettingsPanel 根物体上。
/// 面板的 UI（Canvas / 遮罩 / 面板 / 标题 / 三个按钮）由 prefab 提供，
/// 本脚本只负责逻辑：绑定按钮、显隐、暂停 / 恢复游戏、并拦截背后交互。
///   - 继续游戏：关闭面板并恢复游戏
///   - 重新开始：从第一章重新开始游戏
///   - 退出游戏：退出到桌面（编辑器下停止运行）
/// 打开时暂停（Time.timeScale = 0），关闭时恢复（Time.timeScale = 1）。
///
/// 打开期间：除本面板内的 3 个按钮外，场景里所有其它可交互组件（PageCard 的进入 / 删除、
/// 设置按钮、下一章按钮等）都会被禁用，玩家只能操作选项面板，无法点到游戏里的其它按钮。
/// 关闭时恢复它们原来的可交互状态。
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    [Header("UI 引用（由 SettingsPanel.prefab 绑定）")]
    [SerializeField] private GameObject panel;        // 整体开关对象（prefab 中的 SettingsCanvas）
    [SerializeField] private Button continueButton;   // 继续游戏
    [SerializeField] private Button restartButton;    // 重新开始
    [SerializeField] private Button quitButton;       // 退出游戏

    private ChapterManager _chapterManager;

    // 记录被本面板临时禁用的背后可交互组件，关闭时还原
    private readonly List<Selectable> _disabledBackground = new();

    private void Awake()
    {
        // 初始隐藏面板（prefab 默认激活，避免一开局就弹窗）
        if (panel != null)
            panel.SetActive(false);

        // 绑定三个按钮的点击逻辑
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    /// <summary>
    /// 由 BookUIController 注入 ChapterManager，供“重新开始”使用
    /// </summary>
    public void Init(ChapterManager chapterManager)
    {
        _chapterManager = chapterManager;
    }

    /// <summary>
    /// 打开设置面板并暂停游戏，同时禁用背后所有交互
    /// </summary>
    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        DisableBackgroundInteractables();
        Time.timeScale = 0f;
        Debug.Log("[SettingsPanelUI] 设置面板打开，游戏已暂停，背后交互已禁用");
    }

    /// <summary>
    /// 关闭设置面板并恢复游戏，同时恢复背后交互
    /// </summary>
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        EnableBackgroundInteractables();
        Time.timeScale = 1f;
        Debug.Log("[SettingsPanelUI] 设置面板关闭，游戏已恢复，背后交互已还原");
    }

    /// <summary>
    /// 禁用“本面板之外”的所有可交互组件，确保弹出期间只能点面板内的按钮。
    /// 做法：遍历场景内所有 Selectable（按钮等），跳过属于本选项面板的，其余全部置为不可交互。
    /// </summary>
    private void DisableBackgroundInteractables()
    {
        _disabledBackground.Clear();

        // 收集本面板自身及其子物体下的所有可交互组件，避免误伤面板的 3 个按钮
        var ownSelectables = new List<Selectable>();
        if (panel != null)
            panel.GetComponentsInChildren(ownSelectables);
        GetComponentsInChildren(ownSelectables);

        var all = FindObjectsOfType<Selectable>();
        foreach (var s in all)
        {
            if (s == null || !s.interactable)
                continue;
            if (ownSelectables.Contains(s))
                continue;

            _disabledBackground.Add(s);
            s.interactable = false;
        }
    }

    /// <summary>
    /// 还原被禁用的背后可交互组件
    /// </summary>
    private void EnableBackgroundInteractables()
    {
        foreach (var s in _disabledBackground)
        {
            if (s != null)
                s.interactable = true;
        }
        _disabledBackground.Clear();
    }

    private void OnContinueClicked()
    {
        Hide();
    }

    private void OnRestartClicked()
    {
        // 先恢复时间，避免重开后仍处于暂停状态
        Time.timeScale = 1f;
        if (_chapterManager != null)
            _chapterManager.StartGame();
        Hide();
    }

    private void OnQuitClicked()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
