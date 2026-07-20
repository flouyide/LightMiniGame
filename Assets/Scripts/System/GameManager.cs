using System.Collections;
using System.Collections.Generic;
using LightMiniGame.Card;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("初始牌库")]
    [Tooltip("所有角色的初始牌库配置。每个资产对应一名角色（含其 template 列表），" +
             "开局时逐个注册并填充到 GlobalCardLibrary，形成按角色隔离的独立牌库。")]
    public List<CharacterStartingLibrary> startingLibraries = new List<CharacterStartingLibrary>();

    void Start()
    {
        // 1) 确保全局牌库实例存在（场景未挂载时自动创建常驻对象）
        GlobalCardLibrary.EnsureInstance();

        // 2) 逐个角色构建独立牌库（BuildFromStartingLibrary 内部已完成 RegisterCharacter + 逐张 Add）
        foreach (var sl in startingLibraries)
        {
            if (sl != null)
                GlobalCardLibrary.Instance.BuildFromStartingLibrary(sl);
        }
    }

}
