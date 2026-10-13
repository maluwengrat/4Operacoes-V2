using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonitorSalaManager : MonoBehaviour
{
    public static MonitorSalaManager instance;

    [Header("Painel")]
    public GameObject painelMonitor;
    public TextMeshProUGUI tituloMonitor;
    public TextMeshProUGUI textoAlunosConectados;
    public Button btnIniciarPartida;
    public Button btnEncerrarSala;

    [Header("Controle de Pergunta (Modo Turma)")]
    public Button btnProximaPergunta;
    public Button btnProximaFase;
    public Toggle toggleModoManual;
    public TextMeshProUGUI textoRodadaAtual;
    public TextMeshProUGUI textoFaseAtual;
    public TextMeshProUGUI textoPerguntaAtual;
    public TextMeshProUGUI textoRespostaAtual;
    public TextMeshProUGUI textoContagem;

    [Header("Painel Pergunta Manual")]
    public GameObject painelPerguntaManual;
    public Toggle toggleBossManual;
    public TMP_InputField inputNumeroCManual;
    public TextMeshProUGUI textoFaseOperacaoManual;
    public TextMeshProUGUI textoErroManual;

    public TMP_InputField inputEnunciadoManual;
    public TMP_InputField inputRespostaManual;
    public Button btnConfirmarPerguntaManual;
    public Button btnCancelarPerguntaManual;

    [Header("Lista de alunos / Ranking")]
    public Transform contentContainer;
    public GameObject itemAlunoPrefab;

    private float intervaloAtualizacao = 3f;
    private float timerAtualizacao = 0f;
    private bool monitorAtivo = false;
    private bool partidaIniciada = false;
    private bool partidaFinalizada = false;
    private bool esperandoProfessor = false; // liberou, mas o professor ainda não mandou a pergunta (manual)

    private int faseAtualProfessor = 1;
    private int perguntaIndexAtual = -1;
    private int contadorPerguntaFase = 0;
    private const int rodadasPorFase = 7;
    private readonly HashSet<string> perguntasUsadasProfessor = new HashSet<string>();

    // Os alunos calculam o tempo pelo relógio do servidor (a partir do envio),
    // então só sobra uma folga curta para a última resposta chegar ao banco.
    private const float margemSincronia = 2f;
    private bool aguardandoLiberar = false;
    private float liberaEm = 0f; // Time.realtimeSinceStartup

    // Sinal de vida dos alunos: guarda o último "visto" e quando ele mudou.
    // O WebGL pausa a aba em segundo plano, por isso a tolerância é generosa.
    private readonly Dictionary<string, (string visto, float mudouEm)> vistoPorAluno
        = new Dictionary<string, (string, float)>();
    private const float tempoSemSinalParaSumir = 40f;

    // Evita buscas simultâneas e reconstrução desnecessária da lista
    private bool buscandoAlunos = false;
    private string ultimaAssinaturaLista = null;

    private readonly string[] nomesFaseProfessor = { "", "Adição", "Subtração", "Divisão", "Multiplicação" };
    private readonly string[] simbolosOperacao = { "", "+", "-", "÷", "×" };

    void Awake() { instance = this; }

    void Start()
    {
        if (painelMonitor != null) painelMonitor.SetActive(false);
        if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);
        if (textoContagem != null) textoContagem.text = "";

        if (toggleModoManual != null)
            toggleModoManual.onValueChanged.AddListener(OnToggleModoManualMudou);

        if (btnIniciarPartida != null) btnIniciarPartida.onClick.AddListener(IniciarPartida);
        if (btnEncerrarSala != null) btnEncerrarSala.onClick.AddListener(EncerrarSala);

        if (btnProximaPergunta != null) btnProximaPergunta.onClick.AddListener(ProximaPerguntaClicked);
        if (btnProximaFase != null) btnProximaFase.onClick.AddListener(ProximaFaseClicked);

        if (btnConfirmarPerguntaManual != null)
            btnConfirmarPerguntaManual.onClick.AddListener(ConfirmarPerguntaManual);
        if (btnCancelarPerguntaManual != null)
            btnCancelarPerguntaManual.onClick.AddListener(() =>
                painelPerguntaManual.SetActive(false));
    }

    void Update()
    {
        if (!monitorAtivo) return;

        timerAtualizacao -= Time.unscaledDeltaTime;
        if (timerAtualizacao <= 0f)
        {
            // enquanto espera resposta, consulta mais rápido
            timerAtualizacao = aguardandoLiberar ? 1.5f : intervaloAtualizacao;
            AtualizarLista();
        }

        if (aguardandoLiberar)
        {
            float restante = liberaEm - Time.realtimeSinceStartup;
            if (restante > 0f)
            {
                if (textoContagem != null)
                    textoContagem.text = "Próxima em " + Mathf.CeilToInt(restante) + "s";
            }
            else
            {
                LiberarProximaPergunta();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Toggle / liberar
    // ─────────────────────────────────────────────────────────────────

    void OnToggleModoManualMudou(bool manual)
    {
        var fb = FirebaseManager.instance;
        if (fb != null && !string.IsNullOrEmpty(fb.codigoSalaAtual))
            fb.DefinirModoManualSala(fb.codigoSalaAtual, manual);

        if (!partidaIniciada || partidaFinalizada) return;

        // Estava esperando o professor e ele desmarcou: o jogo assume agora
        if (!manual && esperandoProfessor)
        {
            esperandoProfessor = false;
            if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);
            AvancarAutomatico();
        }
    }

    bool FimDeJogo() => faseAtualProfessor >= 4 && contadorPerguntaFase >= rodadasPorFase;

    // Chamado quando o tempo acaba OU quando todos responderam
    void LiberarProximaPergunta()
    {
        aguardandoLiberar = false;
        if (textoContagem != null) textoContagem.text = "";

        if (FimDeJogo())
        {
            FinalizarPartida();
            return;
        }

        bool manual = toggleModoManual != null && toggleModoManual.isOn;
        if (manual)
        {
            esperandoProfessor = true;
            if (btnProximaPergunta != null) btnProximaPergunta.interactable = true;
            if (btnProximaFase != null) btnProximaFase.interactable = true;
        }
        else
        {
            AvancarAutomatico();
        }
    }

    // Última pergunta da última fase terminou: avisa os alunos
    void FinalizarPartida()
    {
        partidaFinalizada = true;
        aguardandoLiberar = false;
        esperandoProfessor = false;

        if (btnProximaPergunta != null) btnProximaPergunta.interactable = false;
        if (btnProximaFase != null) btnProximaFase.interactable = false;
        if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);
        if (textoContagem != null) textoContagem.text = "Todas as fases concluídas";

        var fb = FirebaseManager.instance;
        if (fb != null && !string.IsNullOrEmpty(fb.codigoSalaAtual))
            fb.FinalizarSalaNoServidor(fb.codigoSalaAtual);
    }

    // ─────────────────────────────────────────────────────────────────
    // Abrir / Fechar monitor
    // ─────────────────────────────────────────────────────────────────

    public void AbrirMonitor(string codigoSala)
    {
        painelMonitor.SetActive(true);
        monitorAtivo = true;
        timerAtualizacao = 0f;
        partidaIniciada = false;
        partidaFinalizada = false;
        aguardandoLiberar = false;
        esperandoProfessor = false;
        buscandoAlunos = false;
        ultimaAssinaturaLista = null;
        faseAtualProfessor = 1;
        perguntaIndexAtual = -1;
        contadorPerguntaFase = 0;
        perguntasUsadasProfessor.Clear();
        vistoPorAluno.Clear();

        if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);
        if (tituloMonitor != null) tituloMonitor.text = "SALA: " + codigoSala;
        if (btnIniciarPartida != null) btnIniciarPartida.gameObject.SetActive(true);
        if (btnProximaPergunta != null) btnProximaPergunta.interactable = false;
        if (btnProximaFase != null) btnProximaFase.interactable = false;
        if (textoPerguntaAtual != null) textoPerguntaAtual.text = "—";
        if (textoRespostaAtual != null) textoRespostaAtual.text = "";
        if (textoContagem != null) textoContagem.text = "";
        AtualizarTextoFase();
    }

    public void FecharMonitor()
    {
        CancelInvoke();
        painelMonitor.SetActive(false);
        monitorAtivo = false;
        partidaIniciada = false;
        partidaFinalizada = false;
        aguardandoLiberar = false;
        esperandoProfessor = false;
        buscandoAlunos = false;
        ultimaAssinaturaLista = null;
        vistoPorAluno.Clear();
        if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);
    }

    void AtualizarTextoFase()
    {
        if (textoFaseAtual != null) textoFaseAtual.text = "Fase " + faseAtualProfessor;
        if (textoRodadaAtual != null) textoRodadaAtual.text = $"Rodada 0/{rodadasPorFase}";
        AtualizarCabecalhoManual();
    }

    void AtualizarCabecalhoManual()
    {
        if (textoFaseOperacaoManual != null)
        {
            int proximaRodada = Mathf.Min(contadorPerguntaFase + 1, rodadasPorFase);
            textoFaseOperacaoManual.text = "Fase " + faseAtualProfessor + " - "
                + nomesFaseProfessor[faseAtualProfessor]
                + " (" + simbolosOperacao[faseAtualProfessor] + ")"
                + $"  |  Rodada {proximaRodada}/{rodadasPorFase}";
        }
        if (textoErroManual != null) textoErroManual.text = "";
    }

    void IniciarPartida()
    {
        if (FirebaseManager.instance == null) return;
        string codigo = FirebaseManager.instance.codigoSalaAtual;

        FirebaseManager.instance.IniciarJogoNaSala(codigo, (sucesso) =>
        {
            if (!sucesso)
            {
                Debug.LogError("Erro ao iniciar partida.");
                return;
            }

            partidaIniciada = true;

            if (btnIniciarPartida != null) btnIniciarPartida.gameObject.SetActive(false);
            if (btnProximaPergunta != null) btnProximaPergunta.interactable = true;
            if (btnProximaFase != null) btnProximaFase.interactable = true;

            if (textoAlunosConectados != null)
                textoAlunosConectados.text += "\n\nPartida em andamento!";

            bool manualInicial = toggleModoManual == null || toggleModoManual.isOn;
            FirebaseManager.instance.DefinirModoManualSala(codigo, manualInicial);

            // Já dispara a primeira pergunta pra não deixar a turma parada
            EnviarProximaPerguntaOuAbrirManual();
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Próxima pergunta / próxima fase
    // ─────────────────────────────────────────────────────────────────

    void ProximaPerguntaClicked()
    {
        if (!partidaIniciada || partidaFinalizada) return;
        if (aguardandoLiberar) return;
        EnviarProximaPerguntaOuAbrirManual();
    }

    void ProximaFaseClicked()
    {
        if (!partidaIniciada || partidaFinalizada) return;
        if (aguardandoLiberar) return;

        if (faseAtualProfessor >= 4)
        {
            if (textoContagem != null) textoContagem.text = "Última fase em andamento";
            return;
        }

        faseAtualProfessor++;
        perguntasUsadasProfessor.Clear();
        contadorPerguntaFase = 0;
        AtualizarTextoFase();
        EnviarProximaPerguntaOuAbrirManual();
    }

    // No modo automático, decide o que vem depois que a rodada terminou
    void AvancarAutomatico()
    {
        if (!partidaIniciada || partidaFinalizada) return;

        if (contadorPerguntaFase >= rodadasPorFase)
        {
            if (faseAtualProfessor >= 4)
            {
                FinalizarPartida();
                return;
            }
            ProximaFaseClicked(); // fecha a fase e já envia a primeira da próxima
            return;
        }

        EnviarProximaPerguntaOuAbrirManual();
    }

    void EnviarProximaPerguntaOuAbrirManual()
    {
        bool manual = toggleModoManual != null && toggleModoManual.isOn;
        bool ehBoss = contadorPerguntaFase == 5 || contadorPerguntaFase == 6;

        if (manual)
        {
            if (inputEnunciadoManual != null) inputEnunciadoManual.text = "";
            if (inputRespostaManual != null) inputRespostaManual.text = "";
            if (inputNumeroCManual != null) inputNumeroCManual.text = "";
            if (toggleBossManual != null) toggleBossManual.isOn = ehBoss;
            if (painelPerguntaManual != null) painelPerguntaManual.SetActive(true);
            esperandoProfessor = true;
            AtualizarCabecalhoManual();
        }
        else
        {
            string enunciado; int resposta;
            if (ehBoss)
                GerarPerguntaBossAutomatica(faseAtualProfessor, out enunciado, out resposta);
            else
                GerarPerguntaAutomatica(faseAtualProfessor, out enunciado, out resposta);

            EnviarPergunta(enunciado, resposta, ehBoss);
        }
    }

    // Se o envio automático falhar, tenta de novo em vez de deixar a turma parada
    void RepetirEnvioAutomatico()
    {
        if (!monitorAtivo || !partidaIniciada || partidaFinalizada || aguardandoLiberar) return;
        bool manual = toggleModoManual != null && toggleModoManual.isOn;
        if (manual) return;
        AvancarAutomatico();
    }

    void ConfirmarPerguntaManual()
    {
        if (textoErroManual != null) textoErroManual.text = "";

        string textoA = inputEnunciadoManual != null ? inputEnunciadoManual.text.Trim() : "";
        string textoB = inputRespostaManual != null ? inputRespostaManual.text.Trim() : "";
        bool ehBoss = toggleBossManual != null && toggleBossManual.isOn;

        if (!int.TryParse(textoA, out int a) || !int.TryParse(textoB, out int b))
        {
            MostrarErroManual("Preencha os dois primeiros números.");
            return;
        }

        int c = 0;
        if (ehBoss)
        {
            string textoC = inputNumeroCManual != null ? inputNumeroCManual.text.Trim() : "";
            if (!int.TryParse(textoC, out c))
            {
                MostrarErroManual("Pergunta de boss precisa do terceiro número também.");
                return;
            }
        }

        string enunciado;
        int resposta;

        switch (faseAtualProfessor)
        {
            case 1: // Adição
                if (ehBoss) { enunciado = $"{a} + {b} + {c}"; resposta = a + b + c; }
                else { enunciado = $"{a} + {b}"; resposta = a + b; }
                break;

            case 2: // Subtração
                if (ehBoss) { enunciado = $"{a} - {b} - {c}"; resposta = a - b - c; }
                else { enunciado = $"{a} - {b}"; resposta = a - b; }
                break;

            case 3: // Divisão
                if (b == 0) { MostrarErroManual("Não é possível dividir por zero."); return; }
                if (a % b != 0) { MostrarErroManual("O primeiro número precisa ser divisível pelo segundo (sem deixar resto)."); return; }
                if (ehBoss) { enunciado = $"({a} ÷ {b}) + {c}"; resposta = (a / b) + c; }
                else { enunciado = $"{a} ÷ {b}"; resposta = a / b; }
                break;

            case 4: // Multiplicação
                if (ehBoss) { enunciado = $"({a} × {b}) + {c}"; resposta = (a * b) + c; }
                else { enunciado = $"{a} × {b}"; resposta = a * b; }
                break;

            default:
                MostrarErroManual("Fase inválida.");
                return;
        }

        if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);
        EnviarPergunta(enunciado, resposta, ehBoss);
    }

    void MostrarErroManual(string mensagem)
    {
        if (textoErroManual != null)
            textoErroManual.text = mensagem;
        else
            Debug.LogWarning("[MonitorSalaManager] " + mensagem);
    }

    void EnviarPergunta(string enunciado, int resposta, bool boss)
    {
        if (FirebaseManager.instance == null) return;
        string codigo = FirebaseManager.instance.codigoSalaAtual;

        perguntaIndexAtual++;
        contadorPerguntaFase++;
        esperandoProfessor = false;

        float duracao = boss ? GameManager.TurmaTempoBoss : GameManager.TurmaTempoNormal;

        // Trava os botões já no clique (evita clique duplo). O prazo provisório é
        // generoso; o valor certo entra quando o envio for confirmado.
        aguardandoLiberar = true;
        liberaEm = Time.realtimeSinceStartup + duracao + margemSincronia + 5f;
        if (btnProximaPergunta != null) btnProximaPergunta.interactable = false;
        if (btnProximaFase != null) btnProximaFase.interactable = false;

        FirebaseManager.instance.EnviarProximaPergunta(
            codigo, enunciado, resposta, faseAtualProfessor, perguntaIndexAtual, boss, duracao, (sucesso) =>
            {
                if (!sucesso)
                {
                    Debug.LogError("Erro ao enviar pergunta.");
                    aguardandoLiberar = false;
                    perguntaIndexAtual--;
                    contadorPerguntaFase--;
                    if (btnProximaPergunta != null) btnProximaPergunta.interactable = true;
                    if (btnProximaFase != null) btnProximaFase.interactable = true;

                    bool manual = toggleModoManual != null && toggleModoManual.isOn;
                    if (!manual)
                    {
                        if (textoContagem != null) textoContagem.text = "Falha ao enviar. Tentando de novo...";
                        Invoke(nameof(RepetirEnvioAutomatico), 2f);
                    }
                    else if (textoContagem != null)
                    {
                        textoContagem.text = "Falha ao enviar a pergunta. Tente de novo.";
                    }
                    return;
                }

                // A contagem passa a valer a partir da confirmação do envio
                liberaEm = Time.realtimeSinceStartup + duracao + margemSincronia;

                if (textoPerguntaAtual != null)
                    textoPerguntaAtual.text = (boss ? "[BOSS] " : "") + enunciado;
                if (textoRespostaAtual != null)
                    textoRespostaAtual.text = "Resposta: " + resposta;
                if (textoRodadaAtual != null)
                    textoRodadaAtual.text = $"Rodada {contadorPerguntaFase}/{rodadasPorFase}" + (boss ? " (BOSS)" : "");
            });
    }

    void GerarPerguntaAutomatica(int fase, out string enunciado, out int resposta)
    {
        int a = 0, b = 0;
        resposta = 0;
        enunciado = "";
        int tentativas = 0;

        do
        {
            switch (fase)
            {
                case 1:
                    a = Random.Range(1, 5); b = Random.Range(1, 9 - a + 1);
                    resposta = a + b;
                    enunciado = $"{a} + {b}";
                    break;
                case 2:
                    resposta = Random.Range(1, 9); b = Random.Range(1, 9);
                    a = resposta + b;
                    enunciado = $"{a} - {b}";
                    break;
                case 3:
                    resposta = Random.Range(1, 9); b = Random.Range(2, 9);
                    a = b * resposta;
                    enunciado = $"{a} ÷ {b}";
                    break;
                case 4:
                default:
                    a = Random.Range(1, 4); b = Random.Range(1, 9 / a + 1);
                    resposta = a * b;
                    enunciado = $"{a} × {b}";
                    break;
            }
            tentativas++;
        } while (perguntasUsadasProfessor.Contains(enunciado) && tentativas < 30);

        perguntasUsadasProfessor.Add(enunciado);
    }

    void GerarPerguntaBossAutomatica(int fase, out string enunciado, out int resposta)
    {
        int a, b, c;

        switch (fase)
        {
            case 1:
                a = Random.Range(1, 4); b = Random.Range(1, 4);
                c = Random.Range(1, 9 - a - b + 1);
                resposta = a + b + c;
                enunciado = $"{a} + {b} + {c}";
                break;
            case 2:
                resposta = Random.Range(1, 5);
                b = Random.Range(1, 4); c = Random.Range(1, 4);
                a = resposta + b + c;
                enunciado = $"{a} - {b} - {c}";
                break;
            case 3:
                b = Random.Range(0, 2) == 0 ? 8 : 9;
                int quoc = Random.Range(1, 5);
                a = b * quoc;
                c = Random.Range(1, 6);
                resposta = quoc + c;
                enunciado = $"({a} ÷ {b}) + {c}";
                break;
            case 4:
            default:
                a = Random.Range(0, 2) == 0 ? 8 : 9;
                b = Random.Range(1, 5);
                c = Random.Range(1, 6);
                resposta = (a * b) + c;
                enunciado = $"({a} × {b}) + {c}";
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Lista de alunos / ranking ao vivo
    // ─────────────────────────────────────────────────────────────────

    void AtualizarLista()
    {
        if (FirebaseManager.instance == null) return;
        string codigo = FirebaseManager.instance.codigoSalaAtual;
        if (string.IsNullOrEmpty(codigo)) return;
        StartCoroutine(BuscarAlunos(codigo));
    }

    IEnumerator BuscarAlunos(string codigo)
    {
        if (buscandoAlunos) yield break;
        buscandoAlunos = true;
        yield return ProcessarBuscaAlunos(codigo);
        buscandoAlunos = false;
    }

    IEnumerator ProcessarBuscaAlunos(string codigo)
    {
        if (FirebaseManager.instance == null) yield break;
        if (!FirebaseManager.instance.EstaAutenticado)
            yield return new WaitUntil(() => FirebaseManager.instance.EstaAutenticado);

        string json = null;
        yield return FirebaseManager.instance.LerJogadoresJson(codigo, r => json = r);

        if (json == null)
        {
            Debug.LogWarning("[MonitorSalaManager] Erro ao buscar alunos.");
            yield break;
        }

        // Se o monitor foi fechado/reaberto durante a busca, descarta o resultado
        if (!monitorAtivo || FirebaseManager.instance.codigoSalaAtual != codigo) yield break;

        if (json == "null" || string.IsNullOrEmpty(json))
        {
            if (textoAlunosConectados != null)
                textoAlunosConectados.text = "Nenhum aluno conectado ainda...";
            LimparLista();
            ultimaAssinaturaLista = "";
            yield break;
        }

        List<DadosAlunoSala> alunos = ParsearAlunos(json);

        // Remove da lista quem parou de mandar sinal de vida (fechou a aba, perdeu conexão)
        alunos.RemoveAll(a =>
        {
            if (string.IsNullOrEmpty(a.visto)) return false; // sem sinal (cliente antigo): mantém

            if (!vistoPorAluno.TryGetValue(a.userId, out var anterior) || anterior.visto != a.visto)
            {
                vistoPorAluno[a.userId] = (a.visto, Time.realtimeSinceStartup);
                return false;
            }
            return Time.realtimeSinceStartup - anterior.mudouEm > tempoSemSinalParaSumir;
        });

        // Respondeu = gravou o número da pergunta atual (não precisa de reset entre perguntas)
        foreach (var a in alunos)
            a.respondeu = perguntaIndexAtual >= 0 && a.respondeuIndex == perguntaIndexAtual;

        if (textoAlunosConectados != null)
            textoAlunosConectados.text = alunos.Count + " aluno(s) conectado(s)";

        // Ranking: maior pontuação primeiro
        alunos.Sort((a, b) => b.pontos.CompareTo(a.pontos));

        // Só reconstrói a lista na tela se algo mudou (evita piscar)
        var sb = new System.Text.StringBuilder();
        foreach (var a in alunos)
            sb.Append(a.userId).Append('|').Append(a.pontos).Append('|')
              .Append(a.respondeu ? 1 : 0).Append('|').Append(a.acertou ? 1 : 0).Append(';');
        string assinatura = sb.ToString();

        if (assinatura != ultimaAssinaturaLista)
        {
            ultimaAssinaturaLista = assinatura;
            LimparLista();

            for (int i = 0; i < alunos.Count; i++)
            {
                GameObject item = Instantiate(itemAlunoPrefab, contentContainer);
                ItemAlunoSala ia = item.GetComponent<ItemAlunoSala>();
                if (ia != null)
                    ia.Preencher(i + 1, alunos[i].nome, alunos[i].pontos,
                                 alunos[i].respondeu, alunos[i].respondeu && alunos[i].acertou);
            }
        }

        // Todos responderam antes do tempo? Libera já.
        if (aguardandoLiberar && partidaIniciada && alunos.Count > 0)
        {
            bool todos = true;
            foreach (var a in alunos)
                if (!a.respondeu) { todos = false; break; }

            if (todos) LiberarProximaPergunta();
        }
    }

    void LimparLista()
    {
        foreach (Transform filho in contentContainer)
            Destroy(filho.gameObject);
    }

    List<DadosAlunoSala> ParsearAlunos(string json)
    {
        var lista = new List<DadosAlunoSala>();
        if (json == "null" || string.IsNullOrEmpty(json)) return lista;

        int i = 0;
        while (i < json.Length)
        {
            int aspasInicio = json.IndexOf('"', i);
            if (aspasInicio < 0) break;
            int aspasFim = json.IndexOf('"', aspasInicio + 1);
            if (aspasFim < 0) break;
            string chave = json.Substring(aspasInicio + 1, aspasFim - aspasInicio - 1);

            int doisPontos = json.IndexOf(':', aspasFim);
            if (doisPontos < 0) break;

            int abreValor = doisPontos + 1;
            while (abreValor < json.Length && json[abreValor] == ' ') abreValor++;

            if (abreValor >= json.Length || json[abreValor] != '{')
            {
                i = aspasFim + 1;
                continue;
            }

            int nivel = 1;
            int fim = abreValor + 1;
            while (fim < json.Length && nivel > 0)
            {
                if (json[fim] == '{') nivel++;
                else if (json[fim] == '}') nivel--;
                fim++;
            }

            string bloco = json.Substring(abreValor, fim - abreValor);

            if (bloco.Contains("\"nome\""))
            {
                try
                {
                    DadosAlunoSala d = new DadosAlunoSala();
                    d.userId = chave;
                    d.nome = ExtrairString(bloco, "nome");
                    d.pontos = ExtrairInt(bloco, "pontos");
                    d.respondeuIndex = bloco.Contains("\"respondeuIndex\":") ? ExtrairInt(bloco, "respondeuIndex") : -1;
                    d.acertou = bloco.Contains("\"acertou\":true");
                    d.visto = ExtrairNumero(bloco, "visto");
                    if (!string.IsNullOrEmpty(d.nome))
                        lista.Add(d);
                }
                catch { }
            }

            i = fim;
        }

        return lista;
    }

    string ExtrairString(string json, string chave)
    {
        string busca = "\"" + chave + "\":\"";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return "";
        inicio += busca.Length;
        int fim = json.IndexOf("\"", inicio);
        return fim < 0 ? "" : json.Substring(inicio, fim - inicio);
    }

    int ExtrairInt(string json, string chave)
    {
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return 0;
        inicio += busca.Length;
        int fim = inicio;
        while (fim < json.Length && (char.IsDigit(json[fim]) || json[fim] == '-')) fim++;
        int.TryParse(json.Substring(inicio, fim - inicio), out int resultado);
        return resultado;
    }

    string ExtrairNumero(string json, string chave)
    {
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return "";
        inicio += busca.Length;
        int fim = inicio;
        while (fim < json.Length && char.IsDigit(json[fim])) fim++;
        return json.Substring(inicio, fim - inicio);
    }

    // ─────────────────────────────────────────────────────────────────
    // Encerrar sala
    // ─────────────────────────────────────────────────────────────────

    void EncerrarSala()
    {
        if (FirebaseManager.instance != null && !string.IsNullOrEmpty(FirebaseManager.instance.codigoSalaAtual))
            FirebaseManager.instance.EncerrarSalaNoServidor(FirebaseManager.instance.codigoSalaAtual);

        FecharMonitor();
        if (SalaManager.instance != null)
            SalaManager.instance.salaPanel.SetActive(false);
        if (GameManager.instance != null)
            GameManager.instance.VoltarAoMenu();
    }
}

public class DadosAlunoSala
{
    public string userId;
    public string nome;
    public int pontos;
    public bool respondeu;          // calculado pelo painel (respondeuIndex == pergunta atual)
    public int respondeuIndex = -1; // número da última pergunta que o aluno respondeu
    public bool acertou;
    public string visto = "";
}