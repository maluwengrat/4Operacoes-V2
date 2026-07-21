using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SalaEsperaManager : MonoBehaviour
{
    public static SalaEsperaManager instance;

    [Header("Painel")]
    public GameObject painelEspera;
    public TextMeshProUGUI textoEspera;
    public TextMeshProUGUI textoCodigoEspera;
    public Button btnSairEspera;

    private bool aguardando = false;
    private float intervalo = 3f;
    private float timer = 0f;
    private int contadorPontos = 0;

    void Awake() { instance = this; }

    void Start()
    {
        if (painelEspera != null)
            painelEspera.SetActive(false);

        if (btnSairEspera != null)
            btnSairEspera.onClick.AddListener(SairDaEspera);
    }

    void Update()
    {
        if (!aguardando) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = intervalo;
            StartCoroutine(VerificarStatusSala());
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Abrir painel de espera
    // ─────────────────────────────────────────────────────────────────

    public void AbrirEspera(string codigoSala)
    {
        painelEspera.SetActive(true);
        aguardando = true;
        timer = 0f;

        if (textoCodigoEspera != null)
            textoCodigoEspera.text = "SALA: " + codigoSala;

        if (textoEspera != null)
            textoEspera.text = "Aguardando o professor\niniciar a partida...";
    }

    void FecharEspera()
    {
        painelEspera.SetActive(false);
        aguardando = false;
    }

    void SairDaEspera()
    {
        FecharEspera();
        if (SalaManager.instance != null)
            SalaManager.instance.salaPanel.SetActive(false);
        if (GameManager.instance != null)
            GameManager.instance.VoltarAoMenu();
    }

    // ─────────────────────────────────────────────────────────────────
    // Verificar status da sala no Firebase
    // ─────────────────────────────────────────────────────────────────

    IEnumerator VerificarStatusSala()
    {
        string codigo = FirebaseManager.instance.codigoSalaAtual;
        if (string.IsNullOrEmpty(codigo)) yield break;

        string url = "https://mathshooter-6c0f7-default-rtdb.firebaseio.com"
                   + "/salas/" + codigo + "/status.json";

        var req = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            yield break;

        string status = req.downloadHandler.text.Replace("\"", "").Trim();

        // Anima o texto de espera com pontos
        contadorPontos = (contadorPontos + 1) % 4;
        string pontos = new string('.', contadorPontos);
        if (textoEspera != null)
            textoEspera.text = "Aguardando o professor\niniciar a partida" + pontos;

        if (status == "jogando")
            IniciarJogoAluno();
    }

    void IniciarJogoAluno()
    {
        aguardando = false;
        FecharEspera();

        if (SalaManager.instance != null)
            SalaManager.instance.salaPanel.SetActive(false);

        if (GameManager.instance != null)
            GameManager.instance.IniciarJogo();

        Debug.Log("Partida iniciada pelo professor! Aluno começando...");
    }
}