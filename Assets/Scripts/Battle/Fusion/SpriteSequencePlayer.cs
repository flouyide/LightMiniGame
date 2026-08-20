using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 简单精灵序列播放器：按固定帧间隔循环切换 Image.sprite。
/// 用于融合选中动画（Selected.anim 的 Sprite 帧序列：0.2s/帧循环）。
/// </summary>
public class SpriteSequencePlayer : MonoBehaviour
{
    private Image _img;
    private Sprite[] _frames;
    private float _interval = 0.2f;
    private float _timer;
    private int _idx;

    /// <summary>初始化帧序列与帧间隔（秒）。首帧立即显示。</summary>
    public void Init(Sprite[] frames, float intervalSec)
    {
        _img = GetComponent<Image>();
        _frames = frames;
        _interval = intervalSec;
        ResetPlay();
    }

    /// <summary>从头开始播放：显示第 0 帧并清零计时。</summary>
    public void ResetPlay()
    {
        _idx = 0;
        _timer = 0f;
        if (_img != null && _frames != null && _frames.Length > 0)
            _img.sprite = _frames[0];
    }

    private void Update()
    {
        if (_frames == null || _frames.Length == 0 || _img == null) return;
        _timer += Time.deltaTime;
        if (_timer >= _interval)
        {
            _timer -= _interval;
            _idx = (_idx + 1) % _frames.Length;
            _img.sprite = _frames[_idx];
        }
    }
}