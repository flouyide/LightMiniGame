using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LightMiniGame.Card
{
    /// <summary>
    /// 单角色牌库：持有某角色拥有的全部卡牌（平铺的 CardInstance 列表）。
    /// 每个角色一个实例，互不影响。提供本角色范围内的 CRUD 与变更广播。
    /// 设计为普通可序列化类（运行时 new），不依赖可被编辑器误改的 SO 资产。
    /// </summary>
    [Serializable]
    public class CharacterCardLibrary
    {
        public CharacterData owner;                  // 归属角色（只读标识，作为 key）
        public List<CardInstance> cards = new List<CardInstance>();
        public UnityEvent OnChanged;                 // 本角色牌库增删改后广播（供未来 UI 监听）

        public CharacterCardLibrary(CharacterData owner) { this.owner = owner; }

        public int Count => cards.Count;

        /// <summary>
        /// 添加一张卡：每次调用都新建一个独立的 CardInstance（哪怕模板与已有卡完全相同），
        /// 拥有自己的 instanceId 与覆盖层 —— 满足「同名多张各自独立、可单独修改」。
        /// </summary>
        public void Add(CardData template)
        {
            if (template == null) return;
            if (owner != null && owner.maxLibrarySize > 0 && cards.Count >= owner.maxLibrarySize)
            {
                Debug.LogWarning($"[CharacterCardLibrary] {owner.Label} 牌库已满（{owner.maxLibrarySize}），添加被忽略");
                return;
            }
            cards.Add(new CardInstance(template));   // 新 instanceId → 与任何已有卡都是不同实体
            OnChanged?.Invoke();
        }

        /// <summary>按 instanceId 删除一张卡（精确，不影响其它同名卡/模板）。</summary>
        public void Remove(string instanceId)
        {
            int before = cards.Count;
            cards.RemoveAll(c => c.instanceId == instanceId);
            if (cards.Count != before) OnChanged?.Invoke();
        }

        /// <summary>按 instanceId 写入覆盖层（修改该卡属性，不影响模板/其它副本）。</summary>
        public void ApplyOverride(string instanceId, CardOverride o)
        {
            var c = cards.Find(x => x.instanceId == instanceId);
            if (c != null) { c.overrideData = o; OnChanged?.Invoke(); }
        }

        public CardInstance Get(string instanceId) => cards.Find(x => x.instanceId == instanceId);
        public IReadOnlyList<CardInstance> All => cards;

        /// <summary>清空全部卡（保留 owner）。</summary>
        public void Clear()
        {
            if (cards.Count == 0) return;
            cards.Clear();
            OnChanged?.Invoke();
        }
    }
}
