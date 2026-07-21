using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemRanking : MonoBehaviour
{
    public TextMeshProUGUI txtPosicao;
    public TextMeshProUGUI txtNome;
    public TextMeshProUGUI txtPontos;
    public TextMeshProUGUI txtAcertos;
    public Image fundoPosicao;

    public void Preencher(int posicao, string nome, int pontos, int acertos, int aproveitamento)
    {
        txtPosicao.text = posicao + "º";
        txtNome.text = nome;
        txtPontos.text = pontos + " pts";
        txtAcertos.text = aproveitamento + "%";

        // Cores especiais para top 3
        if (fundoPosicao != null)
        {
            if (posicao == 1) fundoPosicao.color = new Color(1f, 0.84f, 0f);     // Ouro
            else if (posicao == 2) fundoPosicao.color = new Color(0.75f, 0.75f, 0.75f); // Prata
            else if (posicao == 3) fundoPosicao.color = new Color(0.8f, 0.5f, 0.2f);    // Bronze
            else fundoPosicao.color = new Color(0.2f, 0.2f, 0.3f);               // Normal
        }
    }
}