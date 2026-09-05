using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipPoder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Configuração")]
    public string nomePoder = "";

    [Tooltip("Descrição usada no modo Solo e no Tutorial (efeito direto no seu próprio jogo).")]
    public string descricaoPoder = "";

    [Tooltip("Descrição usada no modo Turma (efeito no adversário). Se deixar vazio, usa a descrição de cima em todos os modos.")]
    public string descricaoPoderTurma = "";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.instance == null) return;

        bool ehTurma = GameManager.modoAtual == GameManager.ModoJogo.Turma;
        string descricao = (ehTurma && !string.IsNullOrEmpty(descricaoPoderTurma))
            ? descricaoPoderTurma
            : descricaoPoder;

        TooltipManager.instance.MostrarTooltip(nomePoder, descricao, transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.instance != null)
            TooltipManager.instance.EsconderTooltip();
    }
}