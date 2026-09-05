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

    private int faseAtualProfessor = 1;
    private int perguntaIndexAtual = -1;
    private int contadorPerguntaFase = 0;
    private const int rodadasPorFase = 7;
    private readonly HashSet<string> perguntasUsadasProfessor = new HashSet<string>();

    private readonly string[] nomesFaseProfessor = { "", "Adição", "Subtração", "Divisão", "Multiplicação" };
    private readonly string[] simbolosOperacao = { "", "+", "-", "÷", "×" };

    private List<string> ultimosUserIds = new List<string>();

    void Awake() { instance = this; }

    void Start()
    {
        if (painelMonitor != null) painelMonitor.SetActive(false);
        if (painelPerguntaManual != null) painelPerguntaManual.SetActive(false);

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

        timerAtualizacao -= Time.deltaTime;
        if (timerAtualizacao <= 0f)
        {
            timerAtualizacao = intervaloAtualizacao;
            AtualizarLista();
        }
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
        faseAtualProfessor = 1;
        perguntaIndexAtual = -1;
        contadorPerguntaFase = 0;
        perguntasUsadasProfessor.Clear();

        if (tituloMonitor != null) tituloMonitor.text = "SALA: " + codigoSala;
        if (btnIniciarPartida != null) btnIniciarPartida.gameObject.SetActive(true);
        if (btnProximaPergunta != null) btnProximaPergunta.interactable = false;
        if (btnProximaFase != null) btnProximaFase.interactable = false;
        if (textoPerguntaAtual != null) textoPerguntaAtual.text = "—";
        if (textoRespostaAtual != null) textoRespostaAtual.text = "";
        AtualizarTextoFase();
    }

    public void FecharMonitor()
    {
        painelMonitor.SetActive(false);
        monitorAtivo = false;
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

            // Já dispara a primeira pergunta pra não deixar a turma parada
            EnviarProximaPerguntaOuAbrirManual();
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Próxima pergunta / próxima fase
    // ─────────────────────────────────────────────────────────────────

    void ProximaPerguntaClicked()
    {
        if (!partidaIniciada) return;
        EnviarProximaPerguntaOuAbrirManual();
    }

    void ProximaFaseClicked()
    {
        if (!partidaIniciada) return;
        if (faseAtualProfessor < 4) faseAtualProfessor++;
        perguntasUsadasProfessor.Clear();
        contadorPerguntaFase = 0; 
        AtualizarTextoFase();
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

        FirebaseManager.instance.ResetarRespostasAlunos(codigo, new List<string>(ultimosUserIds));

        FirebaseManager.instance.EnviarProximaPergunta(
            codigo, enunciado, resposta, faseAtualProfessor, perguntaIndexAtual, boss, (sucesso) =>
            {
                if (!sucesso)
                {
                    Debug.LogError("Erro ao enviar pergunta.");
                    return;
                }

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
        if (FirebaseManager.instance == null) yield break;
        if (!FirebaseManager.instance.EstaAutenticado)
            yield return new WaitUntil(() => FirebaseManager.instance.EstaAutenticado);

        string url = FirebaseManager.instance.ConstruirUrlComAuth(
             "https://mathshooter-6c0f7-default-rtdb.firebaseio.com/salas/" + codigo + "/jogadores.json");

        var req = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[MonitorSalaManager] Erro ao buscar alunos: " + req.error);
                yield break;
            }

        string json = req.downloadHandler.text;

        foreach (Transform filho in contentContainer)
            Destroy(filho.gameObject);

        if (json == "null" || string.IsNullOrEmpty(json))
        {
            if (textoAlunosConectados != null)
                textoAlunosConectados.text = "Nenhum aluno conectado ainda...";
            ultimosUserIds.Clear();
            yield break;
        }

        List<DadosAlunoSala> alunos = ParsearAlunos(json);
        ultimosUserIds = alunos.ConvertAll(d => d.userId);

        if (textoAlunosConectados != null)
            textoAlunosConectados.text = alunos.Count + " aluno(s) conectado(s)";

        // Ranking: maior pontuação primeiro
        alunos.Sort((a, b) => b.pontos.CompareTo(a.pontos));

        for (int i = 0; i < alunos.Count; i++)
        {
            GameObject item = Instantiate(itemAlunoPrefab, contentContainer);
            ItemAlunoSala ia = item.GetComponent<ItemAlunoSala>();
            if (ia != null)
                ia.Preencher(i + 1, alunos[i].nome, alunos[i].pontos, alunos[i].respondeu, alunos[i].acertou);
        }
    }

    // Igual ao parser do FirebaseManager (chave = userId, bloco = dados),
    // com a diferença de também extrair "acertou" pro painel ao vivo.
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
                    d.respondeu = bloco.Contains("\"respondeu\":true");
                    d.acertou = bloco.Contains("\"acertou\":true");
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

// ─────────────────────────────────────────────────────────────────────
// Dados de aluno na sala
// ─────────────────────────────────────────────────────────────────────

public class DadosAlunoSala
{
    public string userId;
    public string nome;
    public int pontos;
    public bool respondeu;
    public bool acertou;
}