using System.Collections.Generic;
using UnityEngine;

namespace LightMiniGame.Shop
{
    /// <summary>
    /// 玩家已拥有遗物清单（单例，仿 GlobalCardLibrary 的常驻模式）。
    /// 商店购买的遗物、战斗/事件掉落的遗物都登记到这里。
    /// 设计为轻量运行时容器，不依赖可被编辑器误改的 ScriptableObject 资产。
    /// </summary>
    public class RelicInventory : MonoBehaviour
    {
        public static RelicInventory Instance { get; private set; }

        [Header("持久化（可选）")]
        [Tooltip("存档文件名，存于 Application.persistentDataPath。留空则禁用自动存档")]
        [SerializeField] private string saveFileName = "relicInventory.json";

        private readonly List<RelicData> _owned = new List<RelicData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>确保存在唯一实例；若场景里没有挂本组件，则运行时动态创建一个常驻 GameObject。</summary>
        public static RelicInventory EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RelicInventory");
            Instance = go.AddComponent<RelicInventory>();
            return Instance;
        }

        public IReadOnlyList<RelicData> Owned => _owned;
        public int Count => _owned.Count;

        public bool Has(RelicData relic) => relic != null && _owned.Contains(relic);

        /// <summary>加入一件遗物（同遗物不重复添加）。</summary>
        public void Add(RelicData relic)
        {
            if (relic == null || _owned.Contains(relic)) return;
            _owned.Add(relic);
            Debug.Log($"[RelicInventory] 获得遗物：{relic.relicName}（共 {_owned.Count} 件）");
        }

        /// <summary>移除一件遗物。</summary>
        public void Remove(RelicData relic)
        {
            if (relic == null) return;
            if (_owned.Remove(relic))
                Debug.Log($"[RelicInventory] 失去遗物：{relic.relicName}（剩 {_owned.Count} 件）");
        }

        /// <summary>清空全部遗物（保留单例）。</summary>
        public void Clear()
        {
            if (_owned.Count == 0) return;
            _owned.Clear();
        }
    }
}
