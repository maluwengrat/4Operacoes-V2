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
    private bool consultando = false;
    private float intervalo = 2f;
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
        if (!aguardando || consultando) return;

        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f)
        {
            timer = intervalo;
            consultando = true;
            if (FirebaseManager.instance != null)
                FirebaseManager.instance.ConsultarStatusSala(OnStatusRecebido);
            else
                consultando = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Abrir painel de espera
    // ─────────────────────────────────────────────────────────────────

    public void AbrirEspera(string codigoSala)
    {
        painelEspera.SetActive(true);
        aguardando = true;
        consultando = false;
        timer = 0f;

        if (textoCodigoEspera != null)
            textoCodigoEspera.text = "SALA: " + codigoSala;

        if (textoEspera != null)
            textoEspera.text = "Aguardando o professor\n iniciar a partida...";
    }

    void FecharEspera()
    {
        painelEspera.SetActive(false);
        aguardando = false;
        consultando = false;
    }

    void SairDaEspera()
    {
        FecharEspera();
        if (SalaManager.instance != null)
            SalaManager.instance.salaPanel.SetActive(false);
        if (GameManager.instance != null)
            GameManager.instance.VoltarAoMenu(); // também remove o aluno da sala
    }

    // ─────────────────────────────────────────────────────────────────
    // Status da sala
    // status: "aguardando", "jogando", "finalizada", "encerrada",
    //         "null" (sala apagada) ou null (falha de rede nessa consulta)
    // ─────────────────────────────────────────────────────────────────

    void OnStatusRecebido(string status)
    {
        consultando = false;
        if (!aguardando) return;
        if (status == null) return; // falha momentânea de rede: tenta de novo no próximo ciclo

        if (status == "jogando")
        {
            IniciarJogoAluno();
            return;
        }

        if (status == "encerrada" || status == "finalizada" || status == "null")
        {
            if (FeedbackManager.instance != null)
                FeedbackManager.instance.MostrarMensagem("A sala foi encerrada pelo professor.", new Color(1f, 0.3f, 0.1f));
            SairDaEspera();
            return;
        }

        // Ainda aguardando: anima o texto com pontos
        contadorPontos = (contadorPontos + 1) % 4;
        string pontos = new string('.', contadorPontos);
        if (textoEspera != null)
            textoEspera.text = "Aguardando o professor\niniciar a partida" + pontos;
    }

    void IniciarJogoAluno()
    {
        aguardando = false;
        FecharEspera();

        if (SalaManager.instance != null)
            SalaManager.instance.salaPanel.SetActive(false);

        if (GameManager.instance != null)
            GameManager.instance.IniciarJogo();

        Debug.Log("Partida iniciada pelo professor!\n Aluno começando...");
    }
}