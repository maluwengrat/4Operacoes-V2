using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipPoder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Configuração")]
    public string nomePoder = "";
    public string descricaoPoder = "";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
            TooltipManager.instance.MostrarTooltip(nomePoder, descricaoPoder, transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
            TooltipManager.instance.EsconderTooltip();
    }
}