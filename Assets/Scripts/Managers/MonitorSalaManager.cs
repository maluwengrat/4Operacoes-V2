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

    [Header("Lista de alunos")]
    public Transform contentContainer;
    public GameObject itemAlunoPrefab;

    private float intervaloAtualizacao = 3f;
    private float timerAtualizacao = 0f;
    private bool monitorAtivo = false;

    void Awake() { instance = this; }

    void Start()
    {
        if (painelMonitor != null)
            painelMonitor.SetActive(false);

        if (btnIniciarPartida != null)
            btnIniciarPartida.onClick.AddListener(IniciarPartida);

        if (btnEncerrarSala != null)
            btnEncerrarSala.onClick.AddListener(EncerrarSala);
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

        if (tituloMonitor != null)
            tituloMonitor.text = "SALA: " + codigoSala;

        if (btnIniciarPartida != null)
            btnIniciarPartida.gameObject.SetActive(true);
    }

    public void FecharMonitor()
    {
        painelMonitor.SetActive(false);
        monitorAtivo = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Atualizar lista de alunos
    // ─────────────────────────────────────────────────────────────────

    void AtualizarLista()
    {
        string codigo = FirebaseManager.instance.codigoSalaAtual;
        if (string.IsNullOrEmpty(codigo)) return;
        StartCoroutine(BuscarAlunos(codigo));
    }

    IEnumerator BuscarAlunos(string codigo)
    {
        string url = "https://mathshooter-6c0f7-default-rtdb.firebaseio.com"
                   + "/salas/" + codigo + "/jogadores.json";

        var req = UnityEngine.Networking.UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            yield break;

        string json = req.downloadHandler.text;
        if (json == "null" || string.IsNullOrEmpty(json))
        {
            if (textoAlunosConectados != null)
                textoAlunosConectados.text = "Nenhum aluno conectado ainda...";
            yield break;
        }

        foreach (Transform filho in contentContainer)
            Destroy(filho.gameObject);

        List<DadosAlunoSala> alunos = ParsearAlunos(json);

        if (textoAlunosConectados != null)
            textoAlunosConectados.text = alunos.Count + " aluno(s) conectado(s)";

        alunos.Sort((a, b) => b.pontos.CompareTo(a.pontos));

        for (int i = 0; i < alunos.Count; i++)
        {
            GameObject item = Instantiate(itemAlunoPrefab, contentContainer);
            ItemAlunoSala ia = item.GetComponent<ItemAlunoSala>();
            if (ia != null)
                ia.Preencher(i + 1, alunos[i].nome, alunos[i].pontos, alunos[i].respondeu);
        }
    }

    List<DadosAlunoSala> ParsearAlunos(string json)
    {
        var lista = new List<DadosAlunoSala>();
        if (json == "null" || string.IsNullOrEmpty(json)) return lista;

        int i = 0;
        while (i < json.Length)
        {
            int abre = json.IndexOf('{', i);
            if (abre < 0) break;

            int nivel = 1;
            int fim = abre + 1;
            while (fim < json.Length && nivel > 0)
            {
                if (json[fim] == '{') nivel++;
                else if (json[fim] == '}') nivel--;
                fim++;
            }

            string bloco = json.Substring(abre, fim - abre);

            if (bloco.Contains("\"nome\""))
            {
                try
                {
                    DadosAlunoSala d = new DadosAlunoSala();
                    d.nome = ExtrairString(bloco, "nome");
                    d.pontos = ExtrairInt(bloco, "pontos");
                    d.respondeu = bloco.Contains("\"respondeu\":true");
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
    // Ações do professor
    // ─────────────────────────────────────────────────────────────────

    void IniciarPartida()
    {
        string codigo = FirebaseManager.instance.codigoSalaAtual;

        FirebaseManager.instance.IniciarJogoNaSala(codigo, (sucesso) =>
        {
            if (sucesso)
            {
                if (btnIniciarPartida != null)
                    btnIniciarPartida.gameObject.SetActive(false);

                if (textoAlunosConectados != null)
                    textoAlunosConectados.text += "\n\nPartida em andamento!";

                Debug.Log("Partida iniciada com sucesso!");
            }
            else
            {
                Debug.LogError("Erro ao iniciar partida.");
            }
        });
    }

    void EncerrarSala()
    {
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
    public string nome;
    public int pontos;
    public bool respondeu;
}