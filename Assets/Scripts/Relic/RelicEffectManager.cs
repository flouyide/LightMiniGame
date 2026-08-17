using System;
using System.Collections.Generic;
using LightMiniGame.Card;   // CharacterData
using LightMiniGame.Shop;   // RelicData
using UnityEngine;

namespace LightMiniGame.Relic
{
    /// <summary>
    /// 遗物效果管理器（单例）。职责：
    /// 1) 监听 GlobalRelicInventory 的遗物增删事件；
    /// 2) 按 RelicData.effectScriptName（类名全名，含命名空间）反射实例化 IRelicEffect；
    /// 3) 在生命周期（获得/移除/战斗开始/战斗结束）调用对应效果方法；
    /// 4) 缓存已实例化效果，避免重复创建与重复触发 OnGain。
    ///
    /// 装配：无需手动挂载，[RuntimeInitializeOnLoadMethod] 会在场景加载前自动创建并订阅事件。
    /// 战斗钩子由 BattleManager.StartBattle / EndBattle 调用 NotifyBattleStart / NotifyBattleEnd。
    /// </summary>
    public class RelicEffectManager : MonoBehaviour
    {
        public static RelicEffectManager Instance { get; private set; }

        private class Entry
        {
            public CharacterData owner;
            public RelicData relic;
            public IRelicEffect effect;
        }

        // key: 角色名_遗物名，保证每个 (角色, 遗物) 组合只实例化一次
        private readonly Dictionary<string, Entry> _activeEffects = new Dictionary<string, Entry>();

        private static string Key(CharacterData owner, RelicData relic)
            => $"{owner?.name ?? "?"}_{relic?.name ?? "?"}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate() => EnsureInstance();

        public static RelicEffectManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RelicEffectManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RelicEffectManager>();
            Subscribe();
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
            DontDestroyOnLoad(gameObject);
            Subscribe();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Unsubscribe();
                Instance = null;
            }
        }

        private static void Subscribe()
        {
            GlobalRelicInventory.OnRelicAdded += Instance.HandleRelicAdded;
            GlobalRelicInventory.OnRelicRemoved += Instance.HandleRelicRemoved;
        }

        private static void Unsubscribe()
        {
            GlobalRelicInventory.OnRelicAdded -= Instance.HandleRelicAdded;
            GlobalRelicInventory.OnRelicRemoved -= Instance.HandleRelicRemoved;
        }

        // ===== 遗物增删 =====
        private void HandleRelicAdded(CharacterData owner, RelicData relic)
        {
            if (owner == null || relic == null) return;
            var key = Key(owner, relic);
            if (_activeEffects.ContainsKey(key)) return;   // 已实例化，跳过

            var effect = InstantiateEffect(relic);
            if (effect == null) return;

            _activeEffects[key] = new Entry { owner = owner, relic = relic, effect = effect };
            Safe(() => effect.OnGain(MakeContext(owner, relic, null)),
                 $"遗物 {relic.relicName} 的 OnGain 抛出异常");
        }

        private void HandleRelicRemoved(CharacterData owner, RelicData relic)
        {
            if (owner == null || relic == null) return;
            var key = Key(owner, relic);
            if (!_activeEffects.TryGetValue(key, out var entry)) return;

            Safe(() => entry.effect.OnLost(MakeContext(owner, relic, null)),
                 $"遗物 {relic.relicName} 的 OnLost 抛出异常");
            _activeEffects.Remove(key);
        }

        // ===== 战斗生命周期钩子（供 BattleManager 调用）=====
        public void NotifyBattleStart(BattleManager battle)
        {
            if (battle == null) return;
            foreach (var kvp in _activeEffects)
            {
                var e = kvp.Value;
                Safe(() => e.effect.OnBattleStart(MakeContext(e.owner, e.relic, battle)),
                     $"遗物 {e.relic?.relicName} 的 OnBattleStart 抛出异常");
            }
        }

        public void NotifyBattleEnd(BattleManager battle, bool victory)
        {
            if (battle == null) return;
            foreach (var kvp in _activeEffects)
            {
                var e = kvp.Value;
                Safe(() => e.effect.OnBattleEnd(MakeContext(e.owner, e.relic, battle), victory),
                     $"遗物 {e.relic?.relicName} 的 OnBattleEnd 抛出异常");
            }
        }

        // ===== 反射实例化 =====
        private static IRelicEffect InstantiateEffect(RelicData relic)
        {
            if (string.IsNullOrEmpty(relic.effectScriptName))
                return null;   // 未配置效果脚本：遗物仅作收集/展示，不报错

            var type = FindType(relic.effectScriptName);
            if (type == null)
            {
                Debug.LogError($"[RelicEffectManager] 找不到效果类 '{relic.effectScriptName}'（遗物 {relic.relicName}）。" +
                               "请确认类名含命名空间正确，且类已实现 IRelicEffect。");
                return null;
            }

            if (!typeof(IRelicEffect).IsAssignableFrom(type))
            {
                Debug.LogError($"[RelicEffectManager] 类 '{relic.effectScriptName}' 未实现 IRelicEffect，无法作为遗物效果。");
                return null;
            }

            try
            {
                return Activator.CreateInstance(type) as IRelicEffect;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelicEffectManager] 实例化效果类 '{relic.effectScriptName}' 失败（需要无参构造函数）: {ex}");
                return null;
            }
        }

        /// <summary>按全名查找类型：先试 Type.GetType，再遍历已加载程序集兜底。</summary>
        private static Type FindType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // ===== 辅助 =====
        private RelicEffectContext MakeContext(CharacterData owner, RelicData relic, BattleManager battle)
            => new RelicEffectContext
            {
                owner = owner,
                relic = relic,
                battle = battle,
                chapter = FindObjectOfType<ChapterManager>(),
            };

        private static void Safe(Action action, string msg)
        {
            try { action(); }
            catch (Exception ex) { Debug.LogError($"[RelicEffectManager] {msg}: {ex}"); }
        }
    }
}
