using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemAlunoSala : MonoBehaviour
{
    public TextMeshProUGUI txtPosicao;
    public TextMeshProUGUI txtNome;
    public TextMeshProUGUI txtPontos;
    public Image fundoStatus;

    public void Preencher(int posicao, string nome, int pontos, bool respondeu, bool acertou)
    {
        if (txtPosicao != null) txtPosicao.text = posicao + "º";
        if (txtNome != null) txtNome.text = nome;
        if (txtPontos != null) txtPontos.text = pontos + " pts";

        if (fundoStatus == null) return;

        if (!respondeu)
            fundoStatus.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // cinza: ainda respondendo
        else if (acertou)
            fundoStatus.color = new Color(0.2f, 0.8f, 0.3f, 0.8f); // verde: acertou
        else
            fundoStatus.color = new Color(0.9f, 0.2f, 0.2f, 0.8f); // vermelho: errou
    }
}