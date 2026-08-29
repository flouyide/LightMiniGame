using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
public class Tips : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Content")]
    public GameObject Tip;


    public void OnPointerEnter(PointerEventData eventData)
    {
        Tip.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Tip.SetActive(false);
    }
}