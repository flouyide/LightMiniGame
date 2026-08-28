using System.Collections.Generic;
using System.Linq;
using LightMiniGame.Card;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局外双角色选择流程。
/// 组件由选人界面内的 CharacterSelectionCardUI 在运行时挂到最近的“选人界面”祖先节点，
/// 因此不需要为场景中的三个角色卡分别配置持久化点击事件。
/// </summary>
public class CharacterSelectionPanelUI : MonoBehaviour
{
    private const int RequiredCharacterCount = 2;

    private readonly List<CharacterSelectionCardUI> _cards = new();
    private readonly List<CharacterSelectionCardUI> _selectedCards = new();
    private List<CharacterData> _candidates = new();

    private ChapterManager _chapterManager;
    private Button _startGameButton;
    private bool _initialized;
    private bool _startingGame;

    private void Awake()
    {
        _chapterManager = FindObjectOfType<ChapterManager>();
        if (_chapterManager == null)
        {
            Debug.LogError("[CharacterSelection] 未找到 ChapterManager，无法初始化选人流程。");
            return;
        }

        // 所有 Awake 都会先于 ChapterManager.Start 执行，先暂停自动开局，等待玩家确认两名角色。
        _chapterManager.DeferStartUntilCharacterSelection();
        _candidates = _chapterManager.GameConfig != null && _chapterManager.GameConfig.characters != null
            ? new List<CharacterData>(_chapterManager.GameConfig.characters)
            : new List<CharacterData>();

        _startGameButton = FindDeepChild(transform, "开始游戏")?.GetComponent<Button>();
        if (_startGameButton == null)
        {
            Debug.LogError("[CharacterSelection] 未找到“开始游戏”按钮。");
            return;
        }

        _startGameButton.onClick.RemoveListener(StartGame);
        _startGameButton.onClick.AddListener(StartGame);
        // 用户未选满两人时按钮保持可见，仅以 Button.interactable 灰置禁用。
        _startGameButton.gameObject.SetActive(true);
        UpdateStartButton();
        _initialized = true;

        // AddComponent 可能发生在已有角色卡 Awake 之后，补扫一次以确保三张卡都完成注册。
        foreach (var card in GetComponentsInChildren<CharacterSelectionCardUI>(true))
            RegisterCard(card);
    }

    private void OnDestroy()
    {
        if (_startGameButton != null)
            _startGameButton.onClick.RemoveListener(StartGame);
    }

    public void RegisterCard(CharacterSelectionCardUI card)
    {
        if (card == null || _cards.Contains(card))
            return;

        _cards.Add(card);
        if (_initialized)
            BindCard(card);
    }

    private void Start()
    {
        // 被后加载或禁用后再启用的角色卡也在这里补齐绑定。
        foreach (var card in _cards)
            BindCard(card);
        UpdateStartButton();
    }

    private void BindCard(CharacterSelectionCardUI card)
    {
        if (card == null)
            return;

        var character = ResolveCharacter(card);
        if (character == null)
        {
            Debug.LogWarning($"[CharacterSelection] 未能根据角色卡“{card.name}”匹配 CharacterData。");
            return;
        }

        card.Configure(character, ToggleCardSelection);
    }

    private CharacterData ResolveCharacter(CharacterSelectionCardUI card)
    {
        if (card.Character != null)
            return card.Character;

        var image = card.GetComponent<Image>();
        if (image != null && image.sprite != null)
        {
            var matched = _candidates.FirstOrDefault(candidate => candidate != null && candidate.avatar == image.sprite);
            if (matched != null)
                return matched;
        }

        return _candidates.FirstOrDefault(candidate => candidate != null && candidate.displayName == card.name);
    }

    private void ToggleCardSelection(CharacterSelectionCardUI card)
    {
        if (card == null || card.Character == null)
            return;

        if (_selectedCards.Contains(card))
        {
            _selectedCards.Remove(card);
            card.SetSelected(false);
        }
        else
        {
            if (_selectedCards.Count >= RequiredCharacterCount)
            {
                Debug.Log("[CharacterSelection] 最多只能选择两名角色。");
                return;
            }

            _selectedCards.Add(card);
            card.SetSelected(true);
        }

        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (_startGameButton == null)
            return;

        bool ready = _selectedCards.Count == RequiredCharacterCount;
        _startGameButton.interactable = ready;
    }

    private void StartGame()
    {
        if (_startingGame || _selectedCards.Count != RequiredCharacterCount || _chapterManager == null)
            return;

        var gameConfig = _chapterManager.GameConfig;
        if (gameConfig == null)
        {
            Debug.LogError("[CharacterSelection] GameConfig 未配置，无法开始游戏。");
            return;
        }

        _startingGame = true;

        // 只改本次运行中的 ScriptableObject 实例，不在 Play Mode 保存资产文件。
        // ChapterManager、BookUIController、BattleManager 引用同一份 GameConfig，之后会统一读取这两名角色。
        gameConfig.characters.Clear();
        gameConfig.characters.AddRange(_selectedCards.Select(card => card.Character));

        Debug.Log($"[CharacterSelection] 已确认角色：{string.Join("、", gameConfig.characters.Select(character => character.Label))}");

        // 先关闭选人界面，再开启 BookCanvas2，使 BookUIController.OnEnable 在两名角色已写入后完成订阅与角色栏初始化。
        gameObject.SetActive(false);
        if (_chapterManager.BookCanvas != null)
            _chapterManager.BookCanvas.SetActive(true);
        else
            Debug.LogError("[CharacterSelection] ChapterManager 未配置 BookCanvas2。");

        // BookCanvas2 启用后 UI 已订阅章节事件；现在开始游戏可确保首批书页和属性事件不会遗漏。
        _chapterManager.StartGame();
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName)
                return child;
        }

        return null;
    }
}
