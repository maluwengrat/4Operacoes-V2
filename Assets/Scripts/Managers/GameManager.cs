using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Runtime.InteropServices;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Prefabs")]
    public GameObject enemyPrefab;

    [Header("HUD")]
    public GameObject hudPanel;
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI bossQuestionText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI faseText;
    public TextMeshProUGUI timerText;
    public GameObject vidasPanel;

    [Header("Menu Principal")]
    public GameObject menuPrincipalPanel;
    public Button btnIniciar;
    public Button btnRanking;

    [Header("Modo de Jogo")]
    public GameObject modoJogoPanel;
    public Button btnSolo;
    public Button btnTurma;
    public Button btnVoltarModo;

    [Header("Fase Completa")]
    public GameObject faseCompletaPanel;
    public TextMeshProUGUI faseTituloText;
    public TextMeshProUGUI faseDescText;
    public Button btnContinuar;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public Button btnJogarNovamente;

    private List<string> historicoContas = new();
    private List<bool> historicoAcertos = new();
    private List<string> operacoesErradas = new();
    private HashSet<string> perguntasUsadas = new HashSet<string>();
    private Dictionary<int, int> acertosPorFase = new();
    private Dictionary<int, int> totalRespostasPorFase = new();

    private int correctAnswer = 0;
    private int score = 0;
    private bool jogoIniciado = false;
    private int faseAtual = 1;
    private int ondasCompletas = 0;
    private bool isBossWave = false;
    private string perguntaAtual = "";
    private int faseAoErrar = 1;
    private bool faseAprovada = false;

    // ── Modo Turma ──────────────────────────────────────────────────────
    private int turmaPerguntaIndex = -1;
    private bool turmaRespondeuAtual = false;
    private bool turmaPerguntaAtualBoss = false;
    private bool turmaFinalizada = false;
    private int respondeuIndexReentrada = -1; // pergunta que o aluno já tinha respondido antes de recarregar
    private double turmaFimRealtime = 0;      // Time.realtimeSinceStartupAsDouble em que a pergunta termina

    // Duração do timer na turma (enviada ao aluno junto com a pergunta)
    public const float TurmaTempoNormal = 30f;
    public const float TurmaTempoBoss = 45f;

    // Pontuação na turma (por fase). Ajuste aqui se quiser outro valor para o boss.
    private const int PontosNormalTurma = 10;
    private const int PontosBossTurma = 50;

    // true = professor marcou "inserir pergunta manualmente".
    // Começa true (seguro) até o FirebaseManager informar o valor real.
    private bool turmaModoManual = true;

    private const int totalEnemies = 5;
    private const int ondasPorFase = 7;

    private float tempoPerguntaInicio = 0f;
    private float tempoInicioFase = 0f;
    private float tempoInicioQuestao = 0f;
    private bool tempoLentoAtivo = false;
    private float tempoLentoTimer = 0f;
    private float tempoLentoMultiplicador = 0.5f;

    private float timerOnda = 0f;
    private float tempoLimiteOnda = 30f;
    private bool timerAtivo = false;
    public bool timerPausadoExterno = false;

    private string[] nomesFase = { "", "Adicao", "Subtracao", "Divisao", "Multiplicacao" };

    public bool IsPanelAtivo() => faseCompletaPanel.activeSelf || gameOverPanel.activeSelf;
    public bool JogoRodando() => jogoIniciado && !IsPanelAtivo();

    public enum ModoJogo { Solo, Turma, Tutorial }
    public static ModoJogo modoAtual = ModoJogo.Solo;

    public int GetFaseAtual() => faseAtual;
    public int GetCorrectAnswer() => correctAnswer;
    public void ResumarJogo() { Time.timeScale = 1f; }

    // O FirebaseManager chama isso com o valor da checkbox do professor
    public void DefinirModoManualTurma(bool manual)
    {
        turmaModoManual = manual;
    }

    public void ReducirTimer(float segundos)
    {
        if (modoAtual == ModoJogo.Turma)
        {
            if (!timerAtivo) return;
            double agora = Time.realtimeSinceStartupAsDouble;
            turmaFimRealtime -= segundos;
            if (turmaFimRealtime < agora + 3.0) turmaFimRealtime = agora + 3.0;
            timerOnda = (float)(turmaFimRealtime - agora);
        }
        else
        {
            timerOnda -= segundos;
            if (timerOnda < 3f) timerOnda = 3f;
        }
        AtualizarTimerUI();
    }

    void SalaEncerradaPeloProfessor()
    {
        if (!jogoIniciado && !turmaFinalizada) return;

        if (FeedbackManager.instance != null)
            FeedbackManager.instance.MostrarMensagem("O professor encerrou a sala.", new Color(1f, 0.3f, 0.1f));

        VoltarAoMenu();
    }

    void Awake() { instance = this; }

    void Start()
    {
        Random.InitState(System.DateTime.Now.Millisecond + System.DateTime.Now.Second * 1000);

        Time.timeScale = 1f;
        score = 0; faseAtual = 1; ondasCompletas = 0;
        jogoIniciado = false;

        btnIniciar.onClick.AddListener(AbrirModoJogo);
        btnContinuar.onClick.AddListener(AcaoBtnContinuar);
        btnJogarNovamente.onClick.AddListener(ReiniciarDaFase);
        btnVoltarModo.onClick.AddListener(FecharModoJogo);

        btnSolo.onClick.AddListener(() =>
        {
            GameManager.modoAtual = ModoJogo.Solo;
            MostrarSomente(null);

            if (TutorialManager.instance != null)
                TutorialManager.instance.PerguntarTutorialSolo();
            else
                IniciarJogo();
        });

        btnTurma.onClick.AddListener(() =>
        {
            GameManager.modoAtual = ModoJogo.Turma;
            MostrarSomente(null);
            if (SalaManager.instance != null)
                SalaManager.instance.AbrirPainelSala();
        });

        if (btnRanking != null)
            btnRanking.onClick.AddListener(() =>
            {
                if (RankingManager.instance != null)
                    RankingManager.instance.AbrirRanking();
            });

        MostrarSomente(menuPrincipalPanel);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetMainMenu();

        hudPanel.SetActive(false);
        timerText.gameObject.SetActive(false);

        if (bossQuestionText != null)
            bossQuestionText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (tempoLentoAtivo)
        {
            tempoLentoTimer -= Time.deltaTime;
            if (tempoLentoTimer <= 0f)
                tempoLentoAtivo = false;
        }

        if (timerAtivo && jogoIniciado)
        {
            if (modoAtual == ModoJogo.Turma)
            {
                // Na turma o timer segue o relógio real (não para com timeScale = 0),
                // então pausar o jogo não dá tempo extra.
                float dt = Time.unscaledDeltaTime;
                if (timerPausadoExterno) turmaFimRealtime += dt;
                else if (tempoLentoAtivo) turmaFimRealtime += dt * (1f - tempoLentoMultiplicador);

                timerOnda = (float)(turmaFimRealtime - Time.realtimeSinceStartupAsDouble);
            }
            else if (!timerPausadoExterno)
            {
                float fatorTimer = tempoLentoAtivo ? tempoLentoMultiplicador : 1f;
                timerOnda -= Time.deltaTime * fatorTimer;
            }

            AtualizarTimerUI();

            if (!timerPausadoExterno && timerOnda <= 0f)
            {
                // Na turma o tempo esgotado não tira vida nem dá game over
                if (modoAtual == ModoJogo.Turma)
                {
                    TurmaTempoEsgotado();
                    return;
                }

                timerAtivo = false;
                timerText.gameObject.SetActive(false);
                foreach (var e in FindAll<Enemy>()) Destroy(e.gameObject);

                if (EfeitosManager.instance != null)
                {
                    EfeitosManager.instance.FlashErro();
                    EfeitosManager.instance.ShakeCamera();
                }

                faseAoErrar = faseAtual;

                bool gameOver = VidasManager.instance != null
                    ? VidasManager.instance.PerdervVida()
                    : true;

                if (gameOver)
                {
                    FeedbackManager.instance.MostrarMensagem("TEMPO ESGOTADO!", new Color(1f, 0.3f, 0.1f));
                    IniciarSequenciaGameOver();
                }
                else
                {
                    FeedbackManager.instance.MostrarMensagem(
                        $"TEMPO ESGOTADO! {VidasManager.instance.GetVidas()} vida(s) restante(s)",
                        new Color(1f, 0.3f, 0.1f));
                    Invoke(nameof(ReiniciarFaseAtual), 2f);
                }
            }
        }
    }

    public void AbrirModoJogo() { MostrarSomente(modoJogoPanel); }
    void FecharModoJogo() { MostrarSomente(menuPrincipalPanel); }

    public void VoltarAoMenu()
    {
        CancelInvoke();
        Time.timeScale = 1f;

        modoAtual = ModoJogo.Solo;

        jogoIniciado = false;
        timerAtivo = false;
        turmaFinalizada = false;
        turmaPerguntaIndex = -1;
        turmaRespondeuAtual = false;
        historicoContas.Clear();
        historicoAcertos.Clear();
        operacoesErradas.Clear();
        perguntasUsadas.Clear();
        score = 0; faseAtual = 1; ondasCompletas = 0; isBossWave = false;
        hudPanel.SetActive(false);
        timerText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null) player.gameObject.SetActive(false);
        foreach (var e in FindAll<Enemy>()) Destroy(e.gameObject);

        MostrarSomente(menuPrincipalPanel);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetMainMenu();
        if (SoundManager.instance != null) SoundManager.instance.PararMusica();

        if (FirebaseManager.instance != null)
        {
            FirebaseManager.instance.SairDaSala();
            FirebaseManager.instance.PararTodasEscutasDePartida();
        }

        PoderManager.instance?.ResetarPoderes();
        PoderManager.instance?.PararEscuta();
    }

    void MostrarSomente(GameObject painel)
    {
        menuPrincipalPanel.SetActive(painel == menuPrincipalPanel);
        faseCompletaPanel.SetActive(painel == faseCompletaPanel);
        gameOverPanel.SetActive(painel == gameOverPanel);
        modoJogoPanel.SetActive(painel == modoJogoPanel);

        if (RankingManager.instance != null)
            RankingManager.instance.rankingPanel.SetActive(
                painel == RankingManager.instance.rankingPanel);

        if (PauseManager.instance != null)
            PauseManager.instance.btnPause.gameObject.SetActive(painel == null);
    }

    public void IniciarJogo()
    {
        GameResultSender.instance?.IniciarNovaPartida();
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null) player.gameObject.SetActive(true);

        MostrarSomente(null);
        hudPanel.SetActive(true);

        score = 0; faseAtual = 1; ondasCompletas = 0; jogoIniciado = false;
        turmaFinalizada = false;
        historicoContas.Clear();
        historicoAcertos.Clear();
        operacoesErradas.Clear();
        perguntasUsadas.Clear();
        acertosPorFase.Clear();
        totalRespostasPorFase.Clear();
        tempoInicioFase = Time.time;
        PoderManager.instance?.ResetarPoderes();

        // Aluno que recarregou a página no meio da partida: retoma os pontos
        respondeuIndexReentrada = -1;
        if (modoAtual == ModoJogo.Turma && FirebaseManager.instance != null
            && FirebaseManager.instance.reentradaPendente)
        {
            score = FirebaseManager.instance.pontosReentrada;
            respondeuIndexReentrada = FirebaseManager.instance.respondeuIndexReentrada;
            FirebaseManager.instance.reentradaPendente = false;
        }

        AtualizarUI();
        jogoIniciado = true;
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetStage(faseAtual);
        if (VidasManager.instance != null) VidasManager.instance.ResetarVidas();

        if (PoderManager.instance != null)
        {
            PoderManager.instance.ResetarPoderes();
            PoderManager.instance.IniciarEscutaSeNecessario();
        }

        if (modoAtual == ModoJogo.Turma)
            IniciarModoTurma();
        else
            SpawnWave();
    }

    // ─────────────────────────────────────────────────────────────────
    // Modo Turma (aluno): só recebe a pergunta do professor
    // ─────────────────────────────────────────────────────────────────

    void IniciarModoTurma()
    {
        turmaPerguntaIndex = -1;
        turmaRespondeuAtual = false;
        turmaFinalizada = false;
        timerAtivo = false;
        timerText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(true);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        questionText.text = "Aguardando o professor...";

        foreach (var e in FindAll<Enemy>()) if (e != null) Destroy(e.gameObject);

        if (FirebaseManager.instance != null)
            FirebaseManager.instance.IniciarEscutaPerguntas(
                OnNovaPerguntaTurma, SalaEncerradaPeloProfessor, TurmaFinalizada);
    }

    void OnNovaPerguntaTurma(PerguntaTurma p)
    {
        if (!jogoIniciado || turmaFinalizada || p.index == turmaPerguntaIndex) return;

        turmaPerguntaIndex = p.index;
        // Se o aluno já tinha respondido essa pergunta antes de recarregar, não responde de novo
        turmaRespondeuAtual = (p.index == respondeuIndexReentrada);
        turmaPerguntaAtualBoss = p.boss;

        if (p.fase != faseAtual && p.fase >= 1 && p.fase <= 4)
        {
            faseAtual = p.fase;
            if (SoundManager.instance != null) SoundManager.instance.TocarMusicaFase(faseAtual);
            if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetStage(faseAtual);
        }

        foreach (var e in FindAll<Enemy>()) if (e != null) Destroy(e.gameObject);

        correctAnswer = p.resposta;
        perguntaAtual = p.enunciado;

        // Tempo restante pelo relógio do servidor: todos os celulares terminam juntos,
        // inclusive quem recebeu a pergunta com atraso ou entrou depois.
        float restante = p.duracao;
        var fb = FirebaseManager.instance;
        if (fb != null && fb.RelogioSincronizado && p.inicioMs > 0)
        {
            double decorrido = (fb.ServerNowMs() - p.inicioMs) / 1000.0;
            restante = (float)(p.duracao - decorrido);
        }
        restante = Mathf.Min(restante, p.duracao);

        AtualizarUI();

        // Pergunta que já acabou (entrou tarde) ou que esse aluno já respondeu
        if (restante <= 1f || turmaRespondeuAtual)
        {
            turmaRespondeuAtual = true;
            timerAtivo = false;
            timerText.gameObject.SetActive(false);
            questionText.gameObject.SetActive(true);
            if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
            questionText.text = turmaModoManual
                ? "Aguardando o professor..."
                : "Aguarde a próxima pergunta...";
            return;
        }

        if (p.boss && bossQuestionText != null)
        {
            bossQuestionText.gameObject.SetActive(true);
            questionText.gameObject.SetActive(false);
            bossQuestionText.text = p.enunciado;
        }
        else
        {
            questionText.gameObject.SetActive(true);
            if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
            questionText.text = p.enunciado;
        }

        tempoPerguntaInicio = Time.time;
        tempoInicioQuestao = Time.time;

        IniciarTimerTurma(restante);

        float velocidade = p.boss ? (0.8f + faseAtual * 0.15f) : (0.6f + faseAtual * 0.15f);
        SpawnInimigosComNumeros(velocidade);
    }

    // Liga o timer da turma com o tempo restante calculado
    void IniciarTimerTurma(float restante)
    {
        tempoLentoAtivo = false;
        tempoLentoTimer = 0f;
        turmaFimRealtime = Time.realtimeSinceStartupAsDouble + restante;
        timerOnda = restante;
        timerAtivo = true;
        timerText.gameObject.SetActive(true);
        AtualizarTimerUI();
    }

    // O timer zerou
    void TurmaTempoEsgotado()
    {
        timerAtivo = false;
        timerText.gameObject.SetActive(false);
        foreach (var e in FindAll<Enemy>()) if (e != null) Destroy(e.gameObject);

        // Trava: a partir daqui o aluno não pode mais responder essa pergunta
        bool naoRespondeu = !turmaRespondeuAtual;
        turmaRespondeuAtual = true;

        // Sem pontos nesta rodada, mas sem penalidade (nada de vida, RegistrarErro etc.)
        if (naoRespondeu && FeedbackManager.instance != null)
            FeedbackManager.instance.MostrarMensagem("TEMPO ESGOTADO! Sem pontos nesta rodada.", new Color(1f, 0.3f, 0.1f));

        questionText.gameObject.SetActive(true);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        questionText.text = turmaModoManual
            ? "Tempo encerrado. Aguardando o professor..."
            : "Tempo encerrado. Próxima pergunta já vem...";
    }

    // O professor terminou todas as fases: mostra o resultado e o ranking da turma
    void TurmaFinalizada()
    {
        if (turmaFinalizada) return;
        turmaFinalizada = true;

        jogoIniciado = false;
        timerAtivo = false;
        timerText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        foreach (var e in FindAll<Enemy>()) if (e != null) Destroy(e.gameObject);

        if (FeedbackManager.instance != null) FeedbackManager.instance.Esconder();
        hudPanel.SetActive(false);

        if (SoundManager.instance != null)
        {
            SoundManager.instance.TocarFaseCompleta();
            SoundManager.instance.PararMusica();
        }

        faseAprovada = true;
        faseTituloText.text = "FIM DA PARTIDA!";
        faseDescText.text = "Sua pontuação: " + score + "\n\nCarregando ranking da turma...";

        TextMeshProUGUI btnTexto = btnContinuar.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTexto != null) btnTexto.text = "Voltar ao Menu";

        MostrarSomente(faseCompletaPanel);

        var fb = FirebaseManager.instance;
        if (fb == null || string.IsNullOrEmpty(fb.codigoSalaAtual)) return;

        string meuId = fb.userId;
        fb.BuscarJogadoresSala(fb.codigoSalaAtual, lista =>
        {
            if (!turmaFinalizada) return; // o aluno já saiu da tela

            lista.Sort((a, b) => b.pontos.CompareTo(a.pontos));

            var sb = new System.Text.StringBuilder();
            sb.Append("Sua pontuação: ").Append(score).Append("\n\n");
            sb.Append("Ranking da turma:\n");

            int minhaPosicao = -1;
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i].userId == meuId) minhaPosicao = i + 1;
                if (i < 5)
                    sb.Append(i + 1).Append(". ").Append(lista[i].nome)
                      .Append(" - ").Append(lista[i].pontos).Append("\n");
            }

            if (minhaPosicao > 5)
                sb.Append("\nSua posição: ").Append(minhaPosicao).Append("º de ").Append(lista.Count);

            faseDescText.text = sb.ToString();
        });
    }

    void ReiniciarJogo()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReiniciarDaFase()
    {
        Time.timeScale = 1f;
        score = 0;
        faseAtual = faseAoErrar;
        GameResultSender.instance?.IncrementarTentativa(faseAtual);
        ondasCompletas = 0;
        isBossWave = false;
        historicoContas.Clear();
        historicoAcertos.Clear();
        operacoesErradas.Clear();
        perguntasUsadas.Clear();
        tempoInicioFase = Time.time;

        // Recomeço depois do game over: poderes e sequência de acertos voltam ao zero
        PoderManager.instance?.ResetarPoderes();

        MostrarSomente(null);
        hudPanel.SetActive(true);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.transform.position = new Vector3(0f, -4f, 0f);
            player.transform.rotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;
            player.ResetarPlayer();
        }

        if (SoundManager.instance != null) SoundManager.instance.TocarMusicaFase(faseAtual);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetStage(faseAtual);

        jogoIniciado = true;
        AtualizarUI();
        SpawnWave();
    }

    public void AcaoBtnContinuar()
    {
        // Fim da partida da turma: o botão só volta ao menu
        if (turmaFinalizada)
        {
            VoltarAoMenu();
            return;
        }

        if (faseAprovada) ProximaFase();
        else ReiniciarFaseAtual();
    }

    void ProximaFase()
    {
        faseAtual++;
        ondasCompletas = 0;
        isBossWave = false;
        perguntasUsadas.Clear();
        operacoesErradas.Clear();
        tempoInicioFase = Time.time;
        Time.timeScale = 1f;

        MostrarSomente(null);
        hudPanel.SetActive(true);

        if (SoundManager.instance != null) SoundManager.instance.TocarMusicaFase(faseAtual);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetStage(faseAtual);

        jogoIniciado = true;
        AtualizarUI();
        SpawnWave();
    }

    void ReiniciarFaseAtual()
    {
        ondasCompletas = 0;
        isBossWave = false;
        perguntasUsadas.Clear();
        operacoesErradas.Clear();
        tempoInicioFase = Time.time;
        Time.timeScale = 1f;
        historicoContas.Clear();
        historicoAcertos.Clear();

        MostrarSomente(null);
        hudPanel.SetActive(true);

        if (SoundManager.instance != null) SoundManager.instance.TocarMusicaFase(faseAtual);

        jogoIniciado = true;
        AtualizarUI();
        SpawnWave();
    }

    public void EnviarResultadoParcial()
    {
        if (GameResultSender.instance == null) return;

        int acertos = 0;
        foreach (var a in historicoAcertos) if (a) acertos++;
        int erros = historicoAcertos.Count - acertos;
        int aproveitamento = historicoAcertos.Count > 0
            ? Mathf.RoundToInt((float)acertos / historicoAcertos.Count * 100) : 0;
        int tempoTotal = Mathf.RoundToInt(Time.time - tempoInicioFase);

        var erradasEscapadas = new List<string>();
        foreach (var op in operacoesErradas)
            erradasEscapadas.Add("\"" + op.Replace("\"", "\\\"") + "\"");
        string operacoesJson = "[" + string.Join(",", erradasEscapadas) + "]";

        GameResultSender.instance.Enviar(
            faseAtual, score, acertos, erros,
            aproveitamento, tempoTotal,
            operacoesJson, false
        );
    }

    void EnviarResultado(bool concluiuFase)
    {
        if (GameResultSender.instance == null) return;

        int acertos = 0;
        foreach (var a in historicoAcertos) if (a) acertos++;
        int erros = historicoAcertos.Count - acertos;
        int aproveitamento = historicoAcertos.Count > 0
            ? Mathf.RoundToInt((float)acertos / historicoAcertos.Count * 100) : 0;
        int tempoTotal = Mathf.RoundToInt(Time.time - tempoInicioFase);

        var erradasEscapadas = new List<string>();
        foreach (var op in operacoesErradas)
            erradasEscapadas.Add("\"" + op.Replace("\"", "\\\"") + "\"");
        string operacoesJson = "[" + string.Join(",", erradasEscapadas) + "]";

        GameResultSender.instance.Enviar(
            faseAtual, score, acertos, erros,
            aproveitamento, tempoTotal,
            operacoesJson, concluiuFase
        );
    }

    public void AtivarTempoLento(float duracao)
    {
        tempoLentoAtivo = true;
        tempoLentoTimer = duracao;
    }

    // ─────────────────────────────────────────────────────────────────
    // Modo Solo
    // ─────────────────────────────────────────────────────────────────

    void SpawnWave()
    {
        if (ondasCompletas == 0)
        {
            perguntasUsadas.Clear();
        }

        tempoLentoAtivo = false;
        tempoLentoTimer = 0f;
        isBossWave = (ondasCompletas == 5) || (ondasCompletas == 6);

        float tempoBase;
        if (!isBossWave)
            tempoBase = tempoLimiteOnda;
        else if (ondasCompletas == 5)
            tempoBase = tempoLimiteOnda * 1.8f;
        else
            tempoBase = tempoLimiteOnda * 1.2f;

        timerOnda = Mathf.Max(10f, tempoBase - (ondasCompletas * 3f));
        timerAtivo = true;
        timerText.gameObject.SetActive(true);

        if (isBossWave) SpawnBoss();
        else SpawnInimigosNormais();
    }

    void SpawnInimigosNormais()
    {
        if (SoundManager.instance != null)
            SoundManager.instance.VoltarMusicaFase();

        GerarPergunta(out int a, out int b);
        SpawnInimigosComNumeros(0.6f + (faseAtual * 0.15f) + (ondasCompletas * 0.05f));
    }

    void SpawnBoss()
    {
        if (SoundManager.instance != null)
            SoundManager.instance.TocarMusicaBoss();

        GerarPerguntaBoss();

        bool bossFinal = (ondasCompletas == 6);
        float velocidade = bossFinal
            ? 0.5f + (faseAtual * 0.1f)
            : 0.8f + (faseAtual * 0.15f);

        SpawnInimigosComNumeros(velocidade);
    }

    void SpawnInimigosComNumeros(float velocidade)
    {
        var usados = new HashSet<int> { correctAnswer };
        int[] nums = new int[totalEnemies];
        nums[0] = correctAnswer;

        for (int i = 1; i < totalEnemies; i++)
        {
            int wrong; int tentativas = 0;
            do
            {
                wrong = correctAnswer + Random.Range(-8, 9);
                if (wrong < 0) wrong = correctAnswer + Random.Range(1, 9);
                if (wrong == 0) wrong = 1;
                tentativas++;
                if (tentativas > 50) { wrong = correctAnswer + i + 1; break; }
            } while (usados.Contains(wrong));
            usados.Add(wrong);
            nums[i] = wrong;
        }

        for (int i = 0; i < nums.Length; i++)
        {
            int j = Random.Range(i, nums.Length);
            (nums[i], nums[j]) = (nums[j], nums[i]);
        }

        float[] posX = GerarPosicoesX(totalEnemies);
        float[] posY = GerarPosicoesY(totalEnemies);
        for (int i = 0; i < totalEnemies; i++)
        {
            Vector3 pos = new Vector3(posX[i], posY[i], 0);
            GameObject go = Instantiate(enemyPrefab, pos, Quaternion.identity);

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 15;

            Enemy e = go.GetComponent<Enemy>();
            e.SetNumber(nums[i]);
            e.speed = velocidade;
            e.SetColunaFixa(posX[i]);
        }
    }

    void GerarPergunta(out int a, out int b)
    {
        a = 0; b = 0;
        int tentativas = 0;

        do
        {
            switch (faseAtual)
            {
                case 1:
                    a = Random.Range(1, 5); b = Random.Range(1, 9 - a + 1);
                    correctAnswer = a + b;
                    perguntaAtual = $"{a} + {b}";
                    break;
                case 2:
                    correctAnswer = Random.Range(1, 9); b = Random.Range(1, 9);
                    a = correctAnswer + b;
                    perguntaAtual = $"{a} - {b}";
                    break;
                case 3:
                    correctAnswer = Random.Range(1, 9); b = Random.Range(2, 9);
                    a = b * correctAnswer;
                    perguntaAtual = $"{a} ÷ {b}";
                    break;
                case 4:
                    a = Random.Range(1, 4); b = Random.Range(1, 9 / a + 1);
                    correctAnswer = a * b;
                    perguntaAtual = $"{a} × {b}";
                    break;
            }
            tentativas++;
        } while (perguntasUsadas.Contains(perguntaAtual) && tentativas < 30);

        perguntasUsadas.Add(perguntaAtual);

        questionText.gameObject.SetActive(true);
        if (bossQuestionText != null)
            bossQuestionText.gameObject.SetActive(false);

        questionText.text = perguntaAtual;
        historicoContas.Add(perguntaAtual + correctAnswer);
        tempoPerguntaInicio = Time.time;
        tempoInicioQuestao = Time.time;
    }

    void GerarPerguntaBoss()
    {
        int a, b, c;

        switch (faseAtual)
        {
            case 1:
                a = Random.Range(1, 4); b = Random.Range(1, 4);
                c = Random.Range(1, 9 - a - b + 1);
                correctAnswer = a + b + c;
                perguntaAtual = $"{a} + {b} + {c}";
                break;
            case 2:
                correctAnswer = Random.Range(1, 5);
                b = Random.Range(1, 4); c = Random.Range(1, 4);
                a = correctAnswer + b + c;
                perguntaAtual = $"{a} - {b} - {c}";
                break;
            case 3:
                b = Random.Range(0, 2) == 0 ? 8 : 9;
                int quoc = Random.Range(1, 5);
                a = b * quoc;
                c = Random.Range(1, 6);
                correctAnswer = quoc + c;
                perguntaAtual = $"({a} ÷ {b}) + {c}";
                break;
            case 4:
                a = Random.Range(0, 2) == 0 ? 8 : 9;
                b = Random.Range(1, 5);
                c = Random.Range(1, 6);
                correctAnswer = (a * b) + c;
                perguntaAtual = $"({a} × {b}) + {c}";
                break;
            default:
                GerarPergunta(out a, out b);
                return;
        }

        if (bossQuestionText != null)
        {
            bossQuestionText.gameObject.SetActive(true);
            questionText.gameObject.SetActive(false);
            bossQuestionText.text = perguntaAtual;
        }
        else
        {
            questionText.gameObject.SetActive(true);
            questionText.text = perguntaAtual;
        }

        historicoContas.Add(perguntaAtual + correctAnswer);
        tempoPerguntaInicio = Time.time;
        tempoInicioQuestao = Time.time;
    }

    float[] GerarPosicoesX(int quantidade)
    {
        float[] pos = new float[quantidade];
        float larg = 22f;
        float espac = larg / quantidade;
        float inicio = -larg / 2f + espac / 2f;
        for (int i = 0; i < quantidade; i++)
            pos[i] = inicio + (i * espac) + Random.Range(-0.3f, 0.3f);

        for (int i = 0; i < pos.Length; i++)
        {
            int j = Random.Range(i, pos.Length);
            (pos[i], pos[j]) = (pos[j], pos[i]);
        }
        return pos;
    }

    float[] GerarPosicoesY(int quantidade)
    {
        float[] pos = new float[quantidade];
        float espac = (8f - 3f) / quantidade;
        for (int i = 0; i < quantidade; i++)
            pos[i] = 3f + (i * espac) + Random.Range(-0.2f, 0.2f);

        for (int i = 0; i < pos.Length; i++)
        {
            int j = Random.Range(i, pos.Length);
            (pos[i], pos[j]) = (pos[j], pos[i]);
        }
        return pos;
    }

    public void CheckAnswer(int number)
    {
        if (!jogoIniciado) return;

        if (modoAtual == ModoJogo.Turma)
        {
            CheckAnswerTurma(number);
            return;
        }

        if (!totalRespostasPorFase.ContainsKey(faseAtual)) totalRespostasPorFase[faseAtual] = 0;
        totalRespostasPorFase[faseAtual]++;

        if (number == correctAnswer)
        {
            if (!acertosPorFase.ContainsKey(faseAtual)) acertosPorFase[faseAtual] = 0;
            acertosPorFase[faseAtual]++;

            float tempoResposta = Time.time - tempoPerguntaInicio;

            timerAtivo = false;
            timerText.gameObject.SetActive(false);

            score += isBossWave ? 50 * faseAtual : 10 * faseAtual;
            FirebaseManager.instance?.AtualizarPontosSala(score, faseAtual);

            FeedbackManager.instance.MostrarAcerto();

            if (EfeitosManager.instance != null)
                EfeitosManager.instance.EfeitoAcerto(Vector3.zero);

            historicoAcertos.Add(true);
            GameResultSender.instance?.EnviarQuestaoAtual(
               faseAtual, nomesFase[faseAtual], ondasCompletas + 1,
               perguntaAtual, correctAnswer.ToString(), number.ToString(),
               true, Time.time - tempoInicioQuestao
           );
            ondasCompletas++;
            AtualizarUI();

            if (TutorialManager.instance != null && TutorialManager.instance.modoGuiado)
                TutorialManager.instance.NotificarAcerto();

            // Poderes no Solo: o acerto soma na sequência e pode carregar um poder.
            // (No tutorial guiado quem controla os poderes é o TutorialManager.)
            bool tutorialGuiado = TutorialManager.instance != null && TutorialManager.instance.modoGuiado;
            if (!tutorialGuiado && PoderManager.instance != null)
                PoderManager.instance.RegistrarAcerto(isBossWave, tempoResposta);

            foreach (var e in FindAll<Enemy>())
                if (e != null) Destroy(e.gameObject);

            if (ondasCompletas >= ondasPorFase)
            {
                CancelInvoke();
                if (faseAtual >= 4) Invoke(nameof(VitoriaFinal), 1.5f);
                else Invoke(nameof(FaseCompleta), 1.5f);
            }
            else
            {
                Invoke(nameof(SpawnWave), 2f);
            }
        }
        else
        {
            timerAtivo = false;
            timerText.gameObject.SetActive(false);

            // Registra operação errada
            operacoesErradas.Add(perguntaAtual + correctAnswer);

            GameResultSender.instance?.EnviarQuestaoAtual(
               faseAtual, nomesFase[faseAtual], ondasCompletas + 1,
               perguntaAtual, correctAnswer.ToString(), number.ToString(),
               false, Time.time - tempoInicioQuestao
           );

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.TemEscudo())
            {
                player.UsarEscudo();
                FeedbackManager.instance.MostrarMensagem("ESCUDO BLOQUEOU!", new Color(0.3f, 0.8f, 1f));
                if (EfeitosManager.instance != null)
                    EfeitosManager.instance.ShakeCamera();

                foreach (var e in FindAll<Enemy>())
                    if (e != null) Destroy(e.gameObject);

                Invoke(nameof(SpawnWave), 1.5f);
            }
            else
            {
                FeedbackManager.instance.MostrarErro(perguntaAtual, correctAnswer);
                historicoAcertos.Add(false);
                faseAoErrar = faseAtual;

                if (PoderManager.instance != null)
                    PoderManager.instance.RegistrarErro();

                if (EfeitosManager.instance != null)
                {
                    EfeitosManager.instance.FlashErro();
                    EfeitosManager.instance.ShakeCamera();
                }

                foreach (var e in FindAll<Enemy>())
                    if (e != null) Destroy(e.gameObject);

                bool gameOverPorErro = VidasManager.instance != null
                    ? VidasManager.instance.PerdervVida()
                    : true;

                if (gameOverPorErro)
                {
                    IniciarSequenciaGameOver();
                }
                else
                {
                    Invoke(nameof(ReiniciarFaseAtual), 1.5f);
                }
            }
        }
    }

    void CheckAnswerTurma(int number)
    {
        if (turmaRespondeuAtual || turmaFinalizada) return; // também bloqueia depois que o tempo acabou
        turmaRespondeuAtual = true;

        bool acertou = (number == correctAnswer);

        if (acertou)
        {
            score += (turmaPerguntaAtualBoss ? PontosBossTurma : PontosNormalTurma) * faseAtual;
            FeedbackManager.instance.MostrarAcerto();
            if (EfeitosManager.instance != null)
                EfeitosManager.instance.EfeitoAcerto(Vector3.zero);

            if (PoderManager.instance != null)
                PoderManager.instance.RegistrarAcerto(turmaPerguntaAtualBoss, Time.time - tempoPerguntaInicio);
        }
        else
        {
            FeedbackManager.instance.MostrarErro(perguntaAtual, correctAnswer);
            if (EfeitosManager.instance != null)
            {
                EfeitosManager.instance.FlashErro();
                EfeitosManager.instance.ShakeCamera();
            }

            if (PoderManager.instance != null)
                PoderManager.instance.RegistrarErro();
        }

        // Grava o número da pergunta respondida: o painel do professor compara com a pergunta atual
        FirebaseManager.instance?.AtualizarRespostaTurma(score, faseAtual, acertou, turmaPerguntaIndex);

        foreach (var e in FindAll<Enemy>()) if (e != null) Destroy(e.gameObject);

        AtualizarUI();
        // O timer NÃO é desligado aqui; segue contando até zerar
        questionText.gameObject.SetActive(true);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        questionText.text = turmaModoManual
            ? "Aguardando o professor..."
            : "Aguarde a próxima pergunta...";
    }

    void IniciarSequenciaGameOver()
    {
        jogoIniciado = false;
        timerAtivo = false;
        timerText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        foreach (var e in FindAll<Enemy>()) Destroy(e.gameObject);

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.IniciarMorte();
        else ExecutarGameOver();
    }

    public void ExecutarGameOver()
    {
        EnviarResultado(false);
        FeedbackManager.instance.Esconder();
        hudPanel.SetActive(false);
        MostrarSomente(null);
        Time.timeScale = 0f;
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetGameOver();
        finalScoreText.text = "Pontuação: " + score;
        MostrarSomente(gameOverPanel);
    }

    void FaseCompleta()
    {
        CancelInvoke();
        FeedbackManager.instance.Esconder();

        if (SoundManager.instance != null)
        {
            SoundManager.instance.TocarFaseCompleta();
            SoundManager.instance.PararMusica();
        }

        if (EfeitosManager.instance != null)
            EfeitosManager.instance.EfeitoPassarFase();

        hudPanel.SetActive(false);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetNextLevel();

        int acertos = 0;
        foreach (var a in historicoAcertos) if (a) acertos++;
        int erros = historicoAcertos.Count - acertos;
        int percentual = historicoAcertos.Count > 0
            ? Mathf.RoundToInt((float)acertos / historicoAcertos.Count * 100) : 0;

        faseAprovada = percentual == 100;

        // Envia resultado (fase concluída)
        EnviarResultado(faseAprovada);

        faseTituloText.text = faseAprovada
            ? $"FASE {faseAtual} COMPLETA!"
            : "Tente Novamente!";

        string resumo = $"Aproveitamento: {percentual}%  |  Acertos: {acertos}  Erros: {erros}\n\n";
        if (!faseAprovada)
            resumo += "Acerte TODAS as questões para avançar!\n\n";
        resumo += "Cálculos da fase:\n";
        for (int i = 0; i < historicoContas.Count; i++)
        {
            string icone = (i < historicoAcertos.Count && historicoAcertos[i]) ? "[+] " : "[-] ";
            resumo += icone + historicoContas[i] + "\n";
        }
        faseDescText.text = resumo;

        historicoContas.Clear();
        historicoAcertos.Clear();

        TextMeshProUGUI btnTexto = btnContinuar.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTexto != null)
            btnTexto.text = faseAprovada ? "Continuar" : "Tentar Novamente";

        MostrarSomente(faseCompletaPanel);
        Invoke(nameof(PausarJogo), 0.6f);
    }

    void VitoriaFinal()
    {
        CancelInvoke();
        FeedbackManager.instance.Esconder();

        if (SoundManager.instance != null)
        {
            SoundManager.instance.TocarFaseCompleta();
            SoundManager.instance.PararMusica();
        }

        if (EfeitosManager.instance != null)
            EfeitosManager.instance.EfeitoPassarFase();

        hudPanel.SetActive(false);

        int acertos = 0;
        foreach (var a in historicoAcertos) if (a) acertos++;
        int percentual = historicoAcertos.Count > 0
            ? Mathf.RoundToInt((float)acertos / historicoAcertos.Count * 100) : 0;

        faseAprovada = true;

        faseTituloText.text = "PARABÉNS!";
        faseDescText.text = $"Você completou todas as fases!\n\n"
                          + $"Pontuação final: {score}\n"
                          + $"Aproveitamento: {percentual}%";

        // ── Monta e envia o relatório final (planilha nova) ──────────────
        int pf1 = acertosPorFase.GetValueOrDefault(1, 0);
        int pf2 = acertosPorFase.GetValueOrDefault(2, 0);
        int pf3 = acertosPorFase.GetValueOrDefault(3, 0);
        int pf4 = acertosPorFase.GetValueOrDefault(4, 0);

        int tr1 = totalRespostasPorFase.GetValueOrDefault(1, 0);
        int tr2 = totalRespostasPorFase.GetValueOrDefault(2, 0);
        int tr3 = totalRespostasPorFase.GetValueOrDefault(3, 0);
        int tr4 = totalRespostasPorFase.GetValueOrDefault(4, 0);

        float pc1 = tr1 > 0 ? (float)pf1 / tr1 : 0f;
        float pc2 = tr2 > 0 ? (float)pf2 / tr2 : 0f;
        float pc3 = tr3 > 0 ? (float)pf3 / tr3 : 0f;
        float pc4 = tr4 > 0 ? (float)pf4 / tr4 : 0f;

        int tent1 = GameResultSender.instance != null ? GameResultSender.instance.GetTentativaFinal(1) : 1;
        int tent2 = GameResultSender.instance != null ? GameResultSender.instance.GetTentativaFinal(2) : 1;
        int tent3 = GameResultSender.instance != null ? GameResultSender.instance.GetTentativaFinal(3) : 1;
        int tent4 = GameResultSender.instance != null ? GameResultSender.instance.GetTentativaFinal(4) : 1;

        int pontuacaoTotal = pf1 + pf2 + pf3 + pf4;
        int totalRespostas = tr1 + tr2 + tr3 + tr4;
        float percentTotalPontos = totalRespostas > 0 ? (float)pontuacaoTotal / totalRespostas : 0f;

        int somaTentativas = tent1 + tent2 + tent3 + tent4;
        float percentTotalFases = somaTentativas > 0 ? 4f / somaTentativas : 0f;

        GameResultSender.instance?.EnviarRelatorioFinal(
            pf1, pc1, tent1,
            pf2, pc2, tent2,
            pf3, pc3, tent3,
            pf4, pc4, tent4,
            pontuacaoTotal, percentTotalPontos, percentTotalFases
        );
        // ───────────────────────────────────────────────────────────────

        historicoContas.Clear();
        historicoAcertos.Clear();

        TextMeshProUGUI btnTexto = btnContinuar.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTexto != null) btnTexto.text = "Jogar Novamente";

        btnContinuar.onClick.RemoveAllListeners();
        btnContinuar.onClick.AddListener(ReiniciarJogo);

        MostrarSomente(faseCompletaPanel);
        Invoke(nameof(PausarJogo), 0.6f);
    }

    void PausarJogo() { Time.timeScale = 0f; }

    void AtualizarUI()
    {
        scoreText.text = "Pontos: " + score;
        faseText.text = $"Fase {faseAtual} - {nomesFase[faseAtual]}";
    }

    void AtualizarTimerUI()
    {
        if (timerText == null) return;
        timerText.text = $"{Mathf.Max(0, Mathf.CeilToInt(timerOnda))}s";
        timerText.color = timerOnda > 10f ? Color.white : new Color(1f, 0.3f, 0.1f);
    }

    static T[] FindAll<T>() where T : Object
        => GameObject.FindObjectsByType<T>(FindObjectsInactive.Include);
}