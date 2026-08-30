using System;
using LightMiniGame.CardEditor;
using UnityEngine;
using System.Collections.Generic;

namespace LightMiniGame.RelicEffects
{
    /// <summary>
    /// 「回合发牌」配置资产。
    ///
    /// 在 Inspector 里按回合组织要塞给玩家的手牌：
    ///   turns[i] = 第 i 回合要同时塞入的牌组（可含多张不同的牌，各带数量）。
    /// 只配 1 组则每回合都发同一组；配多组则按回合循环（loop=true）或发完即止（loop=false）。
    ///
    /// 创建：Project 视图右键 -> Create -> CardGame -> Turn Deal Config。
    /// 使用：把本资产拖入敌人能力 RelicData 的 Effect Object Params[0]。
    /// </summary>
    [CreateAssetMenu(fileName = "TurnDealConfig", menuName = "CardGame/Turn Deal Config")]
    public class TurnDealConfig : ScriptableObject
    {
        [Serializable]
        public class CardDeal
        {
            public CardEntry card;
            public int count = 1;
        }

        [Serializable]
        public class TurnGroup
        {
            [Tooltip("本回合要同时塞入手牌的牌与数量（可放多张不同的牌）")]
            public List<CardDeal> deals = new List<CardDeal>();
        }

        [Tooltip("按回合循环的发牌方案；只配 1 组则每回合相同")]
        public List<TurnGroup> turns = new List<TurnGroup>();

        [Tooltip("true=按回合循环发牌；false=配几回合就发几回合，之后停止")]
        public bool loop = true;
    }
}