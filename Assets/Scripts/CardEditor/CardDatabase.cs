using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LightMiniGame.CardEditor
{
    /// <summary>
    /// 卡牌数据库 —— 管理所有卡牌的 ScriptableObject。
    /// 编辑器通过此数据库进行卡牌的增删改查和批量验证。
    /// </summary>
    [CreateAssetMenu(menuName = "CardEditor/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        public const string ResourcePath = "CardEditor/CardDatabase";

        [Tooltip("所有卡牌列表")]
        public List<CardEntry> cards = new List<CardEntry>();

        /// <summary>按 cardId 查找卡牌</summary>
        public CardEntry FindById(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            return cards.FirstOrDefault(c => c.cardId == cardId);
        }

        /// <summary>按名称查找卡牌</summary>
        public CardEntry FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return cards.FirstOrDefault(c => c.cardName == name);
        }

        /// <summary>检查 cardId 是否重复</summary>
        public bool IsIdDuplicate(string cardId, CardEntry exclude = null)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            return cards.Any(c => c != exclude && c.cardId == cardId);
        }

        /// <summary>添加卡牌</summary>
        public void Add(CardEntry card)
        {
            if (card != null && !cards.Contains(card))
                cards.Add(card);
        }

        /// <summary>移除卡牌</summary>
        public void Remove(CardEntry card)
        {
            if (card != null)
                cards.Remove(card);
        }

        /// <summary>按品级筛选</summary>
        public List<CardEntry> FilterByGrade(CardGrade grade)
            => cards.Where(c => c.grade == grade).ToList();

        /// <summary>按类型筛选</summary>
        public List<CardEntry> FilterByType(CardType type)
            => cards.Where(c => c.cardType == type).ToList();

        /// <summary>搜索卡牌</summary>
        public List<CardEntry> Search(string keyword, CardGrade? grade = null, CardType? type = null, int? cost = null, CardKeyword? kw = null)
        {
            var query = cards.AsEnumerable();
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(c => c.cardName != null && c.cardName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (grade.HasValue) query = query.Where(c => c.grade == grade.Value);
            if (type.HasValue) query = query.Where(c => c.cardType == type.Value);
            if (cost.HasValue) query = query.Where(c => c.baseCost == cost.Value);
            if (kw.HasValue) query = query.Where(c => c.keyword == kw.Value);
            return query.ToList();
        }

        /// <summary>从 Resources 加载</summary>
        public static CardDatabase Load() => Resources.Load<CardDatabase>(ResourcePath);
    }
}
