using System;
using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEngine;
using UnityEngine.Events;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 全局遗物库 / 单一入口（仿 GlobalCardLibrary）。
    /// 持有游戏中所有角色的独立遗物库，所有增删改都必须先指定目标角色，再路由到对应遗物库，
    /// 从 API 层面保证「按角色隔离」——与卡牌系统完全一致。
    ///
    /// 用法：把本组件挂到一个常驻 GameObject 上（或 ChapterManager 初始化时 EnsureInstance），
    /// 通过 GlobalRelicInventory.Instance 访问。
    ///   inventory.RegisterCharacter(warrior);
    ///   inventory.Add(warrior, ironRing);          // 加一件
    ///   inventory.Remove(warrior, ironRing);       // 删一件
    ///   inventory.GetRelics(warrior);              // 该角色全部遗物（IReadOnlyList&lt;RelicData&gt;）
    /// </summary>
    public class GlobalRelicInventory : MonoBehaviour
    {
        public static GlobalRelicInventory Instance { get; private set; }

        private readonly List<CharacterRelicLibrary> _libraries = new List<CharacterRelicLibrary>();
        private readonly Dictionary<string, CharacterRelicLibrary> _index = new Dictionary<string, CharacterRelicLibrary>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);   // 常驻：跨场景/重载保留遗物库（避免切场景丢遗物）
            // Dictionary 不被 Unity 序列化，从已序列化的 _libraries 重建索引
            _index.Clear();
            foreach (var lib in _libraries)
                if (lib != null && lib.owner != null) _index[lib.owner.characterId] = lib;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>确保存在唯一实例；若场景里没有挂本组件，则运行时动态创建一个常驻 GameObject。</summary>
        public static GlobalRelicInventory EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GlobalRelicInventory");
            Instance = go.AddComponent<GlobalRelicInventory>();
            return Instance;
        }

        // ===== 角色遗物库注册 / 注销（多角色同时存在）=====

        /// <summary>为某角色建立独立遗物库；重复注册返回已有遗物库，不覆盖。</summary>
        public CharacterRelicLibrary RegisterCharacter(CharacterData character)
        {
            if (character == null) return null;
            if (_index.TryGetValue(character.characterId, out var existing)) return existing;
            var lib = new CharacterRelicLibrary(character);
            _libraries.Add(lib);
            _index[character.characterId] = lib;
            return lib;
        }

        /// <summary>移除某角色的遗物库（角色退场/存档卸载时调用）。</summary>
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

        public CharacterRelicLibrary GetLibrary(CharacterData character)
            => character != null && _index.TryGetValue(character.characterId, out var lib) ? lib : null;

        public IReadOnlyList<CharacterRelicLibrary> AllLibraries => _libraries;

        // ===== 全局 CRUD（必须指定角色，路由到对应遗物库）=====

        public void Add(CharacterData character, RelicData relic)
        {
            if (relic == null) return;
            var lib = RegisterCharacter(character);
            lib?.Add(relic);
        }

        public void Remove(CharacterData character, RelicData relic)
        {
            var lib = GetLibrary(character);
            lib?.Remove(relic);
        }

        public bool Has(CharacterData character, RelicData relic)
        {
            var lib = GetLibrary(character);
            return lib != null && lib.Has(relic);
        }

        public IReadOnlyList<RelicData> GetRelics(CharacterData character)
        {
            var lib = GetLibrary(character);
            return lib != null ? lib.Relics : null;
        }

        public int GetCount(CharacterData character)
            => GetLibrary(character)?.Count ?? 0;
    }

    /// <summary>
    /// 单角色遗物库：持有某角色拥有的全部遗物（RelicData 列表）。
    /// 每个角色一个实例，互不影响。遗物直接持有 RelicData（无需如卡牌般的实例层）。
    /// 设计为普通可序列化类（运行时 new），不依赖可被编辑器误改的 SO 资产。
    /// </summary>
    [Serializable]
    public class CharacterRelicLibrary
    {
        public CharacterData owner;                  // 归属角色（只读标识，作为 key）
        public List<RelicData> relics = new List<RelicData>();
        public UnityEvent OnChanged;                 // 本角色遗物库增删后广播（供未来 UI 监听）

        public CharacterRelicLibrary(CharacterData owner) { this.owner = owner; }

        public int Count => relics.Count;
        public IReadOnlyList<RelicData> Relics => relics;

        public bool Has(RelicData r) => r != null && relics.Contains(r);

        public void Add(RelicData r)
        {
            if (r == null) return;
            relics.Add(r);
            OnChanged?.Invoke();
        }

        public void Remove(RelicData r)
        {
            if (r == null) return;
            if (relics.Remove(r)) OnChanged?.Invoke();
        }

        /// <summary>清空全部遗物（保留 owner）。</summary>
        public void Clear()
        {
            if (relics.Count == 0) return;
            relics.Clear();
            OnChanged?.Invoke();
        }
    }
}
