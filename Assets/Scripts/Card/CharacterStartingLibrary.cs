using System.Collections.Generic;
using UnityEngine;
using LightMiniGame.CardEditor;

namespace LightMiniGame.Card
{
    /// <summary>
    /// 可选：某角色的「初始牌库配置」。新建游戏 / 新角色注册时，用它一次性生成起始卡组。
    /// 配置态（只读）；运行期增删改只发生在 CharacterCardLibrary，不污染本资产。
    /// </summary>
    [CreateAssetMenu(menuName = "CardGame/Character Starting Library", fileName = "NewStartingLib")]
    public class CharacterStartingLibrary : ScriptableObject
    {
        public CharacterData character;              // 归属角色
        public List<CardEntry> startingCards;        // 初始模板列表（编辑器 CardEntry 格式；可重复，重复会生成多张独立实例）

        /// <summary>把起始卡组写入目标牌库（逐张转成运行时 CardData 后 new 独立实例）。</summary>
        public void BuildInto(CharacterCardLibrary lib)
        {
            if (lib == null) return;
            foreach (var t in startingCards)
                if (t != null) lib.Add(CardEntryAdapter.ConvertSingle(t));
        }
    }
}
