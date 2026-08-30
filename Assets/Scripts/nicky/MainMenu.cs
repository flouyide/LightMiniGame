using System.Collections;
using LightMiniGame.Card;
using LightMiniGame.Shop;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text hintText;
    public GameObject popupPanel;
    public Button closeButton;
    public Button startButton;

    [Header("Fade Settings")]
    [Tooltip("一次完整淡入淡出周期的时间（秒）")]
    public float fadeCycleDuration = 2.0f;   // 新增配置项

    private bool isPopupActive = false;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        AudioManager.Instance.PlayMusic("yongqi",1f);
        hintText.gameObject.SetActive(true);
        popupPanel.SetActive(false);

        closeButton.onClick.AddListener(ClosePopup);
        startButton.onClick.AddListener(StartGame);

        fadeCoroutine = StartCoroutine(FadeHintText());
    }

    private void Update()
    {
        if (Input.anyKeyDown && !isPopupActive)
        {
            ShowPopup();
        }
    }

    public void ShowPopup()
    {
        AudioManager.Instance.PlaySfx("PhoneTip", 1f);
        isPopupActive = true;
        hintText.gameObject.SetActive(false);
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        popupPanel.SetActive(true);
    }

    public void ClosePopup()
    {
        isPopupActive = false;
        popupPanel.SetActive(false);
        hintText.gameObject.SetActive(true);
        fadeCoroutine = StartCoroutine(FadeHintText());
    }

    public void StartGame()
    {
        // 两个库都是 DontDestroyOnLoad 常驻单例，重开一局前必须清除上一局数据；
        // 首次进入时 Instance 为 null（Book 场景里 ChapterManager 会负责初始化），无需清除。
        if (GlobalCardLibrary.Instance != null)
        {
            GlobalCardLibrary.Instance.ClearAll();
            Debug.Log("[MainMenu] 已清除上一局的玩家牌库");
        }
        if (GlobalRelicInventory.Instance != null)
        {
            GlobalRelicInventory.Instance.ClearAll();
            Debug.Log("[MainMenu] 已清除上一局的遗物库");
        }

        SceneManager.LoadScene("Book");
    }

    private IEnumerator FadeHintText()
    {
        while (true)
        {
            // 使用配置的周期时间
            float t = Mathf.PingPong(Time.time / fadeCycleDuration, 1f);
            float alpha = Mathf.Lerp(0.1f, 1f, t);

            Color color = hintText.color;
            color.a = alpha;
            hintText.color = color;

            yield return null;
        }
    }
}