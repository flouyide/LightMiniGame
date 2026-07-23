using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LightMiniGame.Card
{
    /// <summary>
    /// 全局牌库 / 单一入口。
    /// 持有游戏中所有角色的独立牌库，所有增删改都必须先指定目标角色，再路由到对应牌库，
    /// 从 API 层面保证「按角色隔离」。
    ///
    /// 用法：把本组件挂到一个常驻 GameObject 上（或 ChapterManager），通过 GlobalCardLibrary.Instance 访问。
    /// 典型流程：
    ///   library.RegisterCharacter(warrior);
    ///   library.AddCard(warrior, strikeTemplate);          // 加一张
    ///   library.UpdateCard(warrior, id, override);         // 改一张（只改该实例）
    ///   library.RemoveCard(warrior, id);                   // 删一张
    /// </summary>
    public class GlobalCardLibrary : MonoBehaviour
    {
        public static GlobalCardLibrary Instance { get; private set; }

        [Header("持久化（可选）")]
        [Tooltip("存档文件名，存于 Application.persistentDataPath。留空则禁用自动存档路径提示")]
        [SerializeField] private string saveFileName = "cardLibrary.json";

        private readonly List<CharacterCardLibrary> _libraries = new List<CharacterCardLibrary>();
        private readonly Dictionary<string, CharacterCardLibrary> _index = new Dictionary<string, CharacterCardLibrary>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null || transform.root == transform) { /* 允许挂在场景根 */ }
            // 常驻：跨场景保留（如需要）。如不想跨场景，可去掉下面这行。
            DontDestroyOnLoad(gameObject);

            // Dictionary 不被 Unity 序列化，从已序列化的 _libraries 重建索引（避免场景重载后 GetLibrary 失效）
            _index.Clear();
            foreach (var lib in _libraries)
                if (lib != null && lib.owner != null) _index[lib.owner.characterId] = lib;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 确保存在唯一实例：若场景里没有挂本组件，则运行时动态创建一个常驻 GameObject。
        /// 在游戏启动最早期调用一次即可；之后用 GlobalCardLibrary.Instance 访问。
        /// </summary>
        public static GlobalCardLibrary EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GlobalCardLibrary");
            Instance = go.AddComponent<GlobalCardLibrary>();
            return Instance;
        }

        // ===== 角色牌库注册 / 注销（多角色同时存在）=====

        /// <summary>为某角色建立独立牌库；重复注册返回已有牌库，不覆盖。</summary>
        public CharacterCardLibrary RegisterCharacter(CharacterData character)
        {
            if (character == null) return null;
            if (_index.TryGetValue(character.characterId, out var existing)) return existing;
            var lib = new CharacterCardLibrary(character);
            _libraries.Add(lib);
            _index[character.characterId] = lib;
            return lib;
        }

        /// <summary>移除某角色的牌库（角色退场/存档卸载时调用）。</summary>
        public void UnregisterCharacter(CharacterData character)
        {
            if (character == null) return;
            if (_index.TryGetValue(character.characterId, out var lib))
            {
                _libraries.Remove(lib);
                _index.Remove(character.characterId);
            }
        }

        public bool IsRegistered(CharacterData character)
            => character != null && _index.ContainsKey(character.characterId);

        public CharacterCardLibrary GetLibrary(CharacterData character)
            => character != null && _index.TryGetValue(character.characterId, out var lib) ? lib : null;

        public IReadOnlyList<CharacterCardLibrary> AllLibraries => _libraries;

        // ===== 全局 CRUD（必须指定角色，路由到对应牌库）=====

        public void AddCard(CharacterData character, CardData template)
            => GetLibrary(character)?.Add(template);

        public void RemoveCard(CharacterData character, string instanceId)
            => GetLibrary(character)?.Remove(instanceId);

        public void UpdateCard(CharacterData character, string instanceId, CardOverride o)
            => GetLibrary(character)?.ApplyOverride(instanceId, o);

        public int GetCardCount(CharacterData character)
            => GetLibrary(character)?.Count ?? 0;

        public IReadOnlyList<CardInstance> GetCards(CharacterData character)
            => GetLibrary(character)?.All;

        public CardInstance GetCard(CharacterData character, string instanceId)
            => GetLibrary(character)?.Get(instanceId);

        /// <summary>从初始牌库配置（可选）构建某角色的起始卡组。角色须先 RegisterCharacter。</summary>
        public void BuildFromStartingLibrary(CharacterStartingLibrary start)
        {
            if (start == null || start.character == null) return;
            var lib = RegisterCharacter(start.character);
            foreach (var t in start.startingCards) lib.Add(t);
        }

        // ===== 持久化（可选）：按角色序列化，模板按 asset name 还原 =====
        // 说明：运行时 ScriptableObject 不暴露 guid，这里用 CardData.name 作模板 key。
        // 调用方需提供 templateRegistry（name -> CardData），例如启动时 Resources.LoadAll<CardData> 构建。

        [Serializable] private class SavedCard
        {
            public string instanceId;
            public string templateName;     // CardData.name
            public CardOverride overrideData;
        }

        [Serializable] private class SavedLibrary
        {
            public string characterId;
            public List<SavedCard> cards = new List<SavedCard>();
        }

        [Serializable] private class SavedRoot
        {
            public List<SavedLibrary> libraries = new List<SavedLibrary>();
        }

        /// <summary>保存到 persistentDataPath/saveFileName。registry: CardData.name -> CardData。</summary>
        public void Save(IReadOnlyDictionary<string, CardData> registry = null)
        {
            var root = new SavedRoot();
            foreach (var lib in _libraries)
            {
                var sl = new SavedLibrary { characterId = lib.owner != null ? lib.owner.characterId : "" };
                foreach (var c in lib.cards)
                {
                    sl.cards.Add(new SavedCard
                    {
                        instanceId = c.instanceId,
                        templateName = c.template != null ? c.template.name : "",
                        overrideData = c.overrideData,
                    });
                }
                root.libraries.Add(sl);
            }
            var json = JsonUtility.ToJson(root, true);
            var path = Path.Combine(Application.persistentDataPath, saveFileName);
            File.WriteAllText(path, json);
            Debug.Log($"[GlobalCardLibrary] 已保存牌库到 {path}");
        }

        /// <summary>从存档读取并重建（会清空当前所有角色牌库后重建）。registry 必须能解析模板 name。</summary>
        public void Load(IReadOnlyDictionary<string, CardData> registry)
        {
            var path = Path.Combine(Application.persistentDataPath, saveFileName);
            if (!File.Exists(path)) { Debug.LogWarning($"[GlobalCardLibrary] 存档不存在：{path}"); return; }

            var root = JsonUtility.FromJson<SavedRoot>(File.ReadAllText(path));
            if (root == null) return;

            _libraries.Clear();
            _index.Clear();

            foreach (var sl in root.libraries)
            {
                var character = FindCharacterById(sl.characterId);
                if (character == null) { Debug.LogWarning($"[GlobalCardLibrary] 跳过未知角色 {sl.characterId}"); continue; }
                var lib = RegisterCharacter(character);
                foreach (var sc in sl.cards)
                {
                    CardData template = null;
                    if (!string.IsNullOrEmpty(sc.templateName) && registry != null)
                        registry.TryGetValue(sc.templateName, out template);
                    var inst = new CardInstance(template) { instanceId = sc.instanceId };
                    inst.overrideData = sc.overrideData ?? new CardOverride();
                    lib.cards.Add(inst);
                }
                lib.OnChanged?.Invoke();
            }
            Debug.Log($"[GlobalCardLibrary] 已从存档重建 {_libraries.Count} 个角色牌库");
        }

        // 角色身份在存档里以 characterId 记录；运行时需由调用方把已知 CharacterData 注入此字典
        private static Dictionary<string, CharacterData> _characterRegistry = new Dictionary<string, CharacterData>();
        public static void RegisterCharacterIdentity(CharacterData c)
        {
            if (c != null) _characterRegistry[c.characterId] = c;
        }
        private static CharacterData FindCharacterById(string id)
            => _characterRegistry.TryGetValue(id, out var c) ? c : null;
    }
}
