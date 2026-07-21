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
    public GameObject powerUpEscudoPrefab;
    public GameObject powerUpTempoLentoPrefab;

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
    public Button btnTutorial;
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

    private int correctAnswer = 0;
    private int score = 0;
    private bool jogoIniciado = false;
    private int faseAtual = 1;
    private int ondasCompletas = 0;
    private bool isBossWave = false;
    private string perguntaAtual = "";
    private int faseAoErrar = 1;
    private bool faseAprovada = false;

    private const int totalEnemies = 5;
    private const int ondasPorFase = 10;

    private float tempoInicioFase = 0f;
    private bool tempoLentoAtivo = false;
    private float tempoLentoTimer = 0f;
    private float tempoLentoMultiplicador = 0.5f;

    private float timerOnda = 0f;
    private float tempoLimiteOnda = 30f;
    private bool timerAtivo = false;

    private int powerUpsSpawnadosNaFase = 0;
    private const int maxPowerUpsPorFase = 2;

    private string[] nomesFase = { "", "Adicao", "Subtracao", "Divisao", "Multiplicacao" };

    public bool IsPanelAtivo() => faseCompletaPanel.activeSelf || gameOverPanel.activeSelf;
    public bool JogoRodando() => jogoIniciado && !IsPanelAtivo();

    public enum ModoJogo { Solo, Turma, Tutorial }
    public static ModoJogo modoAtual = ModoJogo.Solo;

    public int GetFaseAtual() => faseAtual;
    public int GetCorrectAnswer() => correctAnswer;
    public void ResumarJogo() { Time.timeScale = 1f; }

    public void ReducirTimer(float segundos)
    {
        timerOnda -= segundos;
        if (timerOnda < 3f) timerOnda = 3f;
        AtualizarTimerUI();
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
            IniciarJogo();
        });

        btnTurma.onClick.AddListener(() =>
        {
            GameManager.modoAtual = ModoJogo.Turma;
            MostrarSomente(null);
            if (SalaManager.instance != null)
                SalaManager.instance.AbrirPainelSala();
        });

        btnTutorial.onClick.AddListener(() =>
        {
            GameManager.modoAtual = ModoJogo.Tutorial;
            MostrarSomente(null);
            hudPanel.SetActive(false);
            if (TutorialManager.instance != null)
                TutorialManager.instance.AbrirTutorial();
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

    // ← Update FORA do Start, no nível correto da classe
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
            float fatorTimer = tempoLentoAtivo ? tempoLentoMultiplicador : 1f;
            timerOnda -= Time.deltaTime * fatorTimer;
            AtualizarTimerUI();

            if (timerOnda <= 0f)
            {
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
        jogoIniciado = false;
        timerAtivo = false;
        historicoContas.Clear();
        historicoAcertos.Clear();
        operacoesErradas.Clear();
        perguntasUsadas.Clear();
        score = 0; faseAtual = 1; ondasCompletas = 0; isBossWave = false;
        hudPanel.SetActive(false);
        timerText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        MostrarSomente(menuPrincipalPanel);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetMainMenu();
        if (SoundManager.instance != null) SoundManager.instance.PararMusica();
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
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null) player.gameObject.SetActive(true);

        MostrarSomente(null);
        hudPanel.SetActive(true);

        score = 0; faseAtual = 1; ondasCompletas = 0; jogoIniciado = false;
        historicoContas.Clear();
        historicoAcertos.Clear();
        operacoesErradas.Clear();
        perguntasUsadas.Clear();
        powerUpsSpawnadosNaFase = 0;
        tempoInicioFase = Time.time;

        AtualizarUI();
        jogoIniciado = true;
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetStage(faseAtual);
        if (VidasManager.instance != null) VidasManager.instance.ResetarVidas();

        SpawnWave();
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
        ondasCompletas = 0;
        isBossWave = false;
        historicoContas.Clear();
        historicoAcertos.Clear();
        operacoesErradas.Clear();
        perguntasUsadas.Clear();
        powerUpsSpawnadosNaFase = 0;
        tempoInicioFase = Time.time;

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
        powerUpsSpawnadosNaFase = 0;
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
        powerUpsSpawnadosNaFase = 0;
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

    void EnviarResultado(bool concluiuFase)
    {
        if (FirebaseManager.instance == null) return;

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

        FirebaseManager.instance.SalvarResultado(
            faseAtual, score, acertos, erros,
            aproveitamento, tempoTotal,
            operacoesJson, concluiuFase);
    }

    public void AtivarTempoLento(float duracao)
    {
        tempoLentoAtivo = true;
        tempoLentoTimer = duracao;
    }

    void TentarSpawnPowerUp()
    {
        if (powerUpsSpawnadosNaFase >= maxPowerUpsPorFase) return;
        if (ondasCompletas <= 1) return;
        if (isBossWave) return;
        if (Random.Range(0f, 1f) > 0.25f) return;

        float x = Random.Range(-4f, 4f);
        Vector3 pos = new Vector3(x, 5f, 0);

        GameObject go = new GameObject("PowerUp");
        go.transform.position = pos;

        PowerUp pu = go.AddComponent<PowerUp>();
        pu.tipo = Random.Range(0, 2) == 0 ? PowerUp.Tipo.Escudo : PowerUp.Tipo.TempoLento;
        pu.duracao = 8f;

        powerUpsSpawnadosNaFase++;
    }

    void SpawnWave()
    {
        if (ondasCompletas == 0)
        {
            perguntasUsadas.Clear();
            powerUpsSpawnadosNaFase = 0;
        }

        tempoLentoAtivo = false;
        tempoLentoTimer = 0f;
        isBossWave = (ondasCompletas >= ondasPorFase - 1);
        float tempoBase = isBossWave ? tempoLimiteOnda * 1.5f : tempoLimiteOnda;
        timerOnda = Mathf.Max(10f, tempoBase - (ondasCompletas * 3f));
        timerAtivo = true;
        timerText.gameObject.SetActive(true);

        if (isBossWave) SpawnBoss();
        else SpawnInimigosNormais();
    }

    void SpawnInimigosNormais()
    {
        if (SoundManager.instance != null) SoundManager.instance.VoltarMusicaFase();
        GerarPergunta(out int a, out int b);
        SpawnInimigosComNumeros(0.6f + (faseAtual * 0.15f) + (ondasCompletas * 0.05f));
    }

    void SpawnBoss()
    {
        if (SoundManager.instance != null) SoundManager.instance.TocarMusicaBoss();
        GerarPerguntaBoss();
        SpawnInimigosComNumeros(0.3f + (faseAtual * 0.08f) + (ondasCompletas * 0.03f));
    }

    void SpawnInimigosComNumeros(float velocidade)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("enemyPrefab é NULL! Conecte no Inspector.");
            return;
        }

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
        for (int i = 0; i < totalEnemies; i++)
        {
            Vector3 pos = new Vector3(posX[i], 3f + (i * 2f), 0);
            GameObject go = Instantiate(enemyPrefab, pos, Quaternion.identity);

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 15;

            Enemy e = go.GetComponent<Enemy>();
            e.SetNumber(nums[i]);
            e.speed = velocidade;
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
                    perguntaAtual = $"{a} + {b} = ";
                    break;
                case 2:
                    correctAnswer = Random.Range(1, 9); b = Random.Range(1, 9);
                    a = correctAnswer + b;
                    perguntaAtual = $"{a} - {b} = ";
                    break;
                case 3:
                    correctAnswer = Random.Range(1, 9); b = Random.Range(2, 9);
                    a = b * correctAnswer;
                    perguntaAtual = $"{a} ÷ {b} = ";
                    break;
                case 4:
                    a = Random.Range(1, 4); b = Random.Range(1, 9 / a + 1);
                    correctAnswer = a * b;
                    perguntaAtual = $"{a} × {b} = ";
                    break;
            }
            tentativas++;
        } while (perguntasUsadas.Contains(perguntaAtual) && tentativas < 30);

        perguntasUsadas.Add(perguntaAtual);
        questionText.gameObject.SetActive(true);
        if (bossQuestionText != null) bossQuestionText.gameObject.SetActive(false);
        questionText.text = perguntaAtual;
        historicoContas.Add(perguntaAtual + correctAnswer);
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
                perguntaAtual = $"{a} + {b} + {c} = ";
                break;
            case 2:
                correctAnswer = Random.Range(1, 5);
                b = Random.Range(1, 4); c = Random.Range(1, 4);
                a = correctAnswer + b + c;
                perguntaAtual = $"{a} - {b} - {c} = ";
                break;
            case 3:
                b = Random.Range(2, 5);
                int quoc = Random.Range(1, 4);
                a = b * quoc; c = Random.Range(1, 9 - quoc + 1);
                correctAnswer = quoc + c;
                perguntaAtual = $"({a} ÷ {b}) + {c} = ";
                break;
            case 4:
                a = Random.Range(1, 3); b = Random.Range(1, 3);
                c = Random.Range(1, 9 - (a * b) + 1);
                correctAnswer = (a * b) + c;
                perguntaAtual = $"({a} × {b}) + {c} = ";
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
    }

    float[] GerarPosicoesX(int quantidade)
    {
        float[] pos = new float[quantidade];
        float larg = 18f;
        float espac = larg / quantidade;
        float inicio = -larg / 2f + espac / 2f;
        for (int i = 0; i < quantidade; i++)
            pos[i] = inicio + (i * espac) + Random.Range(-0.2f, 0.2f);
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

        if (number == correctAnswer)
        {
            timerAtivo = false;
            timerText.gameObject.SetActive(false);

            score += isBossWave ? 50 * faseAtual : 10 * faseAtual;

            FeedbackManager.instance.MostrarAcerto();
            if (EfeitosManager.instance != null) EfeitosManager.instance.EfeitoAcerto(Vector3.zero);

            historicoAcertos.Add(true);
            ondasCompletas++;
            AtualizarUI();
            TentarSpawnPowerUp();

            foreach (var e in FindAll<Enemy>())
                if (e != null) Destroy(e.gameObject);

            if (PoderManager.instance != null)
                PoderManager.instance.RegistrarAcerto(isBossWave, timerOnda);

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
            operacoesErradas.Add(perguntaAtual + correctAnswer);

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.TemEscudo())
            {
                player.UsarEscudo();
                FeedbackManager.instance.MostrarMensagem("ESCUDO BLOQUEOU!", new Color(0.3f, 0.8f, 1f));
                if (EfeitosManager.instance != null) EfeitosManager.instance.ShakeCamera();
                foreach (var e in FindAll<Enemy>()) if (e != null) Destroy(e.gameObject);
                Invoke(nameof(SpawnWave), 1.5f);
            }
            else
            {
                FeedbackManager.instance.MostrarErro(perguntaAtual, correctAnswer);
                historicoAcertos.Add(false);
                faseAoErrar = faseAtual;

                if (EfeitosManager.instance != null)
                {
                    EfeitosManager.instance.FlashErro();
                    EfeitosManager.instance.ShakeCamera();
                }

                if (PoderManager.instance != null)
                    PoderManager.instance.RegistrarErro();

                bool gameOver = VidasManager.instance != null
                    ? VidasManager.instance.PerdervVida()
                    : true;

                if (gameOver)
                    IniciarSequenciaGameOver();
                else
                {
                    FeedbackManager.instance.MostrarMensagem(
                        $"VIDA PERDIDA! {VidasManager.instance.GetVidas()} restante(s)",
                        new Color(1f, 0.3f, 0.1f));
                    Invoke(nameof(ReiniciarFaseAtual), 2f);
                }
            }
        }
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

        if (EfeitosManager.instance != null) EfeitosManager.instance.EfeitoPassarFase();

        hudPanel.SetActive(false);
        if (BackgroundManager.Instance != null) BackgroundManager.Instance.SetNextLevel();

        int acertos = 0;
        foreach (var a in historicoAcertos) if (a) acertos++;
        int erros = historicoAcertos.Count - acertos;
        int percentual = historicoAcertos.Count > 0
            ? Mathf.RoundToInt((float)acertos / historicoAcertos.Count * 100) : 0;

        faseAprovada = percentual == 100;
        EnviarResultado(faseAprovada);

        faseTituloText.text = faseAprovada ? $"FASE {faseAtual} COMPLETA!" : "Tente Novamente!";

        string resumo = $"Aproveitamento: {percentual}%  |  Acertos: {acertos}  Erros: {erros}\n\n";
        if (!faseAprovada) resumo += "Acerte TODAS as questões para avançar!\n\n";
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

        if (EfeitosManager.instance != null) EfeitosManager.instance.EfeitoPassarFase();

        hudPanel.SetActive(false);

        int acertos = 0;
        foreach (var a in historicoAcertos) if (a) acertos++;
        int percentual = historicoAcertos.Count > 0
            ? Mathf.RoundToInt((float)acertos / historicoAcertos.Count * 100) : 0;

        faseAprovada = true;
        EnviarResultado(true);

        faseTituloText.text = "PARABÉNS!";
        faseDescText.text = $"Você completou todas as fases!\n\nPontuação final: {score}\nAproveitamento: {percentual}%";

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
        timerText.text = $"{Mathf.CeilToInt(timerOnda)}s";
        timerText.color = timerOnda > 10f ? Color.white : new Color(1f, 0.3f, 0.1f);
    }

    static T[] FindAll<T>() where T : Object
        => GameObject.FindObjectsByType<T>(FindObjectsInactive.Exclude);
}