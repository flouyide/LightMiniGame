using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicControl : MonoBehaviour
{
    public void UIClick()
    {
        AudioManager.Instance.PlaySfx("ClickUI",1f);
    }
    public void UIClick2()
    {
        AudioManager.Instance.PlaySfx("UIClick", 1f);
    }
    public void PlayChapterBgm()
    {
        AudioManager.Instance.PlayMusic("Chapter",1f);

    }
    public void PlayShuyeSFX()
    {
        AudioManager.Instance.PlaySfx("shuye", 1f);
    }
}
