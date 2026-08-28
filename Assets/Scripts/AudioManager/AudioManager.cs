using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioManager : MonoSingleton<AudioManager>
{
    private static bool original = true;
    [SerializeField]
    public AudioSource musicSource;
    [SerializeField]
    public AudioSource music2Source;
    [SerializeField]
    public AudioSource soundEffectSource;


    public AudioMixer AudioMixer;
    public Slider MainVoiceSlider;
    public Slider BgmSlider;
    public Slider SfxSlider;


    private void Start()
    {
        if (original)
        {
            original = false;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(this);
        }

    }
    public void ChangeMainVoice()
    {
        float volume = -80 + MainVoiceSlider.value * 80;
        Debug.Log(volume);
        AudioMixer.SetFloat("Master", volume);
    }
    public void ChangeBgmVoice()
    {
        float volume = -80 + BgmSlider.value * 80;

        AudioMixer.SetFloat("Bgm", volume);
    }
    public void ChangeSfxVoice()
    {
        float volume = -80 + BgmSlider.value * 80;

        AudioMixer.SetFloat("Sfx", volume);
    }
    /// <summary>
    /// 播放bgm
    /// </summary>
    /// <param name="musicName"></param>
    /// <param name="volume"></param>
    public void PlayMusic(string musicName, float volume, bool needSpecialVolume = false)
    {
        if (!needSpecialVolume)
        {
            musicSource.clip = Resources.Load<AudioClip>("Music/" + musicName);
            //musicSource.volume = volume;
            musicSource.Play();
            return;
        }
        musicSource.clip = Resources.Load<AudioClip>("Music/" + musicName);
        musicSource.volume = volume;
        musicSource.Play();

    }
    public void PlayMusic2(string musicName, float volume, bool needSpecialVolume = false)
    {
        if (!needSpecialVolume)
        {
            music2Source.clip = Resources.Load<AudioClip>("Music/" + musicName);
            //music2Source.volume = volume;
            music2Source.Play();
            return;
        }
        music2Source.clip = Resources.Load<AudioClip>("Music/" + musicName);
        music2Source.volume = volume;
        music2Source.Play();
    }
    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="sfxName"></param>
    /// <param name="volume"></param>
    public void PlaySfx(string sfxName, float volume, bool needSpecialVolume = false)
    {
        if (!needSpecialVolume)
        {
            soundEffectSource.clip = Resources.Load<AudioClip>("SoundEffect/" + sfxName);
            soundEffectSource.PlayOneShot(soundEffectSource.clip);
            return;
        }
        soundEffectSource.clip = Resources.Load<AudioClip>("SoundEffect/" + sfxName);
        soundEffectSource.PlayOneShot(soundEffectSource.clip, volume);
    }

    public void StopMusic(string musicName)
    {
        musicSource.clip = Resources.Load<AudioClip>("Music/" + musicName);
        musicSource.Stop();
    }
    public void StopMusic2(string musicName)
    {
        music2Source.clip = Resources.Load<AudioClip>("Music/" + musicName);
        music2Source.Stop();
    }

    public void StopSfx(string sfxName)
    {
        soundEffectSource.clip = Resources.Load<AudioClip>("SoundEffect/" + sfxName);
        soundEffectSource.Stop();
    }


    public void StopMusicSoon()
    {
        musicSource.Stop();
    }
    public void PlayMusicSoon()
    {
        musicSource.Play();
    }

    private IEnumerator FadeMusic(float targetVolume, string musicName, float _fadeDuration)
    {
        float fadstarttime = Time.time;
        musicSource.clip = Resources.Load<AudioClip>("Music/" + musicName);
        float currentVolume = musicSource.volume;
        while (Time.time < fadstarttime + _fadeDuration)
        {
            float elapsedtime = Time.time - fadstarttime;
            float process = elapsedtime / _fadeDuration;
            musicSource.volume = Mathf.Lerp(currentVolume, targetVolume, process);

            yield return null;
        }
    }

    public void fadeMusic(float targetVolume, string musicName, float _fadeDuration)
    {
        StartCoroutine(FadeMusic(targetVolume, musicName, _fadeDuration));
    }
    private IEnumerator FadeMusic1ToMusic2(string music1Name, string music2Name, float _fadeDuration)
    {
        float fadstarttime = Time.time;
        musicSource.clip = Resources.Load<AudioClip>("Music/" + music1Name);
        float currentVolume = musicSource.volume;
        while (Time.time < fadstarttime + _fadeDuration)
        {
            float elapsedtime = Time.time - fadstarttime;
            float process = elapsedtime / _fadeDuration;
            musicSource.volume = Mathf.Lerp(currentVolume, 0, process);

            yield return null;
        }

        float fadstarttime2 = Time.time;
        musicSource.clip = Resources.Load<AudioClip>("Music/" + music2Name);
        musicSource.Play();
        float currentVolume2 = 0;
        while (Time.time < fadstarttime2 + _fadeDuration)
        {
            float elapsedtime = Time.time - fadstarttime2;
            float process = elapsedtime / _fadeDuration;
            musicSource.volume = Mathf.Lerp(currentVolume2, 1f, process);

            yield return null;
        }
    }
    public void fadeMusic1ToMusic2(string music1Name, string music2Name, float _fadeDuration)
    {
        StartCoroutine(FadeMusic1ToMusic2(music1Name, music2Name, _fadeDuration));
    }


}