using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VidasManager : MonoBehaviour
{
    public static VidasManager instance;

    [Header("Corações")]
    public Image coracao1;
    public Image coracao2;
    public Image coracao3;

    private int vidasAtual = 3;
    private const int vidasMax = 3;

    private Color corAtiva = new Color(0.86f, 0.20f, 0.20f, 1f);
    private Color corInativa = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    void Awake() { instance = this; }

    public void ResetarVidas()
    {
        vidasAtual = vidasMax;
        AtualizarVisuais();
    }

    public bool PerdervVida()
    {
        vidasAtual--;
        AtualizarVisuais();

        Image coracaoPerdido = vidasAtual == 2 ? coracao3
                             : vidasAtual == 1 ? coracao2
                             : coracao1;

        StartCoroutine(PiscarCoracao(coracaoPerdido));

        return vidasAtual <= 0;
    }

    public int GetVidas() => vidasAtual;

    void AtualizarVisuais()
    {
        coracao1.color = vidasAtual >= 1 ? corAtiva : corInativa;
        coracao2.color = vidasAtual >= 2 ? corAtiva : corInativa;
        coracao3.color = vidasAtual >= 3 ? corAtiva : corInativa;
    }

    IEnumerator PiscarCoracao(Image coracao)
    {
        for (int i = 0; i < 6; i++)
        {
            coracao.color = Color.white;
            yield return new WaitForSecondsRealtime(0.1f);
            coracao.color = corInativa;
            yield return new WaitForSecondsRealtime(0.1f);
        }
        AtualizarVisuais();
    }
}