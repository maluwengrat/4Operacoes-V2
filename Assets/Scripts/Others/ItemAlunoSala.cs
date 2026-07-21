using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemAlunoSala : MonoBehaviour
{
    public TextMeshProUGUI txtPosicao;
    public TextMeshProUGUI txtNome;
    public TextMeshProUGUI txtPontos;
    public Image fundoStatus;

    public void Preencher(int posicao, string nome, int pontos, bool respondeu)
    {
        if (txtPosicao != null) txtPosicao.text = posicao + "º";
        if (txtNome != null) txtNome.text = nome;
        if (txtPontos != null) txtPontos.text = pontos + " pts";

        // Verde se respondeu, cinza se ainda não respondeu
        if (fundoStatus != null)
            fundoStatus.color = respondeu
                ? new Color(0.2f, 0.8f, 0.3f, 0.8f)
                : new Color(0.3f, 0.3f, 0.3f, 0.5f);
    }
}