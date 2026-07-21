using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager instance;

    private const string FIREBASE_URL = "https://mathshooter-6c0f7-default-rtdb.firebaseio.com";

    public string userId = "";
    public string nomeJogador = "Jogador";
    public string codigoSalaAtual = "";

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            GerarUserId();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        System.Net.ServicePointManager.ServerCertificateValidationCallback =
            (sender, certificate, chain, errors) => true;
        Debug.Log("FirebaseManager iniciado. UserId: " + userId);
    }

    void GerarUserId()
    {
        if (PlayerPrefs.HasKey("userId"))
            userId = PlayerPrefs.GetString("userId");
        else
        {
            userId = System.DateTime.Now.Ticks.ToString() + Random.Range(1000, 9999);
            PlayerPrefs.SetString("userId", userId);
            PlayerPrefs.Save();
        }
        Debug.Log("ID do jogador: " + userId);
    }

    // ─────────────────────────────────────────────────────────────────
    // Salvar resultado de uma fase
    // ─────────────────────────────────────────────────────────────────

    public void SalvarResultado(int fase, int pontos, int acertos, int erros,
                                 int aproveitamento, int tempoSegundos,
                                 string operacoesErradas, bool concluiu)
    {
        StartCoroutine(EnviarResultado(fase, pontos, acertos, erros,
                                       aproveitamento, tempoSegundos,
                                       operacoesErradas, concluiu));
    }

    IEnumerator EnviarResultado(int fase, int pontos, int acertos, int erros,
                                 int aproveitamento, int tempoSegundos,
                                 string operacoesErradas, bool concluiu)
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string json = "{"
            + "\"nome\":\"" + nomeJogador + "\","
            + "\"fase\":" + fase + ","
            + "\"pontos\":" + pontos + ","
            + "\"acertos\":" + acertos + ","
            + "\"erros\":" + erros + ","
            + "\"aproveitamento\":" + aproveitamento + ","
            + "\"tempo\":" + tempoSegundos + ","
            + "\"operacoesErradas\":" + operacoesErradas + ","
            + "\"concluiu\":" + (concluiu ? "true" : "false") + ","
            + "\"data\":\"" + timestamp + "\""
            + "}";

        string url = FIREBASE_URL + "/resultados/" + userId + "/fase" + fase + ".json";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("Resultado salvo no Firebase! Fase " + fase);
        else
            Debug.LogError("Erro ao salvar: " + req.error);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ranking
    // ─────────────────────────────────────────────────────────────────

    public void BuscarRanking(System.Action<List<DadosJogador>> callback)
    {
        StartCoroutine(CarregarRanking(callback));
    }

    IEnumerator CarregarRanking(System.Action<List<DadosJogador>> callback)
    {
        string url = FIREBASE_URL + "/resultados.json";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Erro ao buscar ranking: " + req.error);
            callback(new List<DadosJogador>());
            yield break;
        }

        string resposta = req.downloadHandler.text;
        Debug.Log("JSON recebido do Firebase: " + resposta);

        List<DadosJogador> lista = ParsearRanking(resposta);
        Debug.Log("Jogadores parseados: " + lista.Count);

        lista.Sort((a, b) => b.pontos.CompareTo(a.pontos));
        callback(lista);
    }

    List<DadosJogador> ParsearRanking(string json)
    {
        var lista = new List<DadosJogador>();
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
                    DadosJogador d = new DadosJogador();
                    d.nome = ExtrairValorString(bloco, "nome");
                    d.pontos = ExtrairValorInt(bloco, "pontos");
                    d.acertos = ExtrairValorInt(bloco, "acertos");
                    d.erros = ExtrairValorInt(bloco, "erros");
                    d.aproveitamento = ExtrairValorInt(bloco, "aproveitamento");
                    d.fase = ExtrairValorInt(bloco, "fase");

                    if (!string.IsNullOrEmpty(d.nome))
                        lista.Add(d);
                }
                catch { }
            }

            i = fim;
        }

        return lista;
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers JSON
    // ─────────────────────────────────────────────────────────────────

    string ExtrairValorString(string json, string chave)
    {
        string busca = "\"" + chave + "\":\"";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return "";
        inicio += busca.Length;
        int fim = json.IndexOf("\"", inicio);
        if (fim < 0) return "";
        return json.Substring(inicio, fim - inicio);
    }

    int ExtrairValorInt(string json, string chave)
    {
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return 0;
        inicio += busca.Length;
        int fim = inicio;
        while (fim < json.Length && (char.IsDigit(json[fim]) || json[fim] == '-')) fim++;
        string valor = json.Substring(inicio, fim - inicio);
        int.TryParse(valor, out int resultado);
        return resultado;
    }

    // ─────────────────────────────────────────────────────────────────
    // Sistema de Salas
    // ─────────────────────────────────────────────────────────────────

    string GerarCodigoSala()
    {
        string caracteres = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string codigo = "";
        for (int i = 0; i < 6; i++)
            codigo += caracteres[Random.Range(0, caracteres.Length)];
        return codigo;
    }

    public void CriarSala(System.Action<string> callback)
    {
        string codigo = GerarCodigoSala();
        StartCoroutine(CriarSalaCoroutine(codigo, callback));
    }

    IEnumerator CriarSalaCoroutine(string codigo, System.Action<string> callback)
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string json = "{"
            + "\"status\":\"aguardando\","
            + "\"faseAtual\":1,"
            + "\"criadoEm\":\"" + timestamp + "\""
            + "}";

        string url = FIREBASE_URL + "/salas/" + codigo + ".json";
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            codigoSalaAtual = codigo;
            Debug.Log("Sala criada: " + codigo);
            callback(codigo);
        }
        else
        {
            Debug.LogError("Erro ao criar sala: " + req.error);
            callback(null);
        }
    }

    public void EntrarNaSala(string codigo, System.Action<bool> callback)
    {
        StartCoroutine(EntrarNaSalaCoroutine(codigo.ToUpper(), callback));
    }

    IEnumerator EntrarNaSalaCoroutine(string codigo, System.Action<bool> callback)
    {
        string urlVerifica = FIREBASE_URL + "/salas/" + codigo + "/status.json";
        Debug.Log("Verificando sala na URL: " + urlVerifica);

        UnityWebRequest reqVerifica = UnityWebRequest.Get(urlVerifica);
        yield return reqVerifica.SendWebRequest();

        Debug.Log("Resposta Firebase: '" + reqVerifica.downloadHandler.text + "'");
        Debug.Log("HTTP Status: " + reqVerifica.responseCode);

        if (reqVerifica.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Erro de rede: " + reqVerifica.error);
            callback(false);
            yield break;
        }

        string resposta = reqVerifica.downloadHandler.text.Trim();

        if (resposta == "null" || string.IsNullOrEmpty(resposta))
        {
            Debug.LogWarning("Sala não encontrada: " + codigo);
            callback(false);
            yield break;
        }

        codigoSalaAtual = codigo;
        string json = "{"
            + "\"nome\":\"" + nomeJogador + "\","
            + "\"pontos\":0,"
            + "\"respondeu\":false,"
            + "\"acertou\":false"
            + "}";

        string urlJogador = FIREBASE_URL + "/salas/" + codigo + "/jogadores/" + userId + ".json";
        UnityWebRequest req = new UnityWebRequest(urlJogador, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Entrou na sala: " + codigo);
            callback(true);
        }
        else
        {
            Debug.LogError("Erro ao registrar jogador: " + req.error);
            callback(false);
        }
    }

    public void IniciarJogoNaSala(string codigo, System.Action<bool> callback)
    {
        StartCoroutine(IniciarJogoCoroutine(codigo, callback));
    }

    IEnumerator IniciarJogoCoroutine(string codigo, System.Action<bool> callback)
    {
        string url = FIREBASE_URL + "/salas/" + codigo + "/status.json";
        string json = "\"jogando\"";

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        callback(req.result == UnityWebRequest.Result.Success);
    }

    public void IniciarEsperaDeJogo(System.Action onJogoIniciado)
    {
        StartCoroutine(PollingStatusSala(onJogoIniciado));
    }

    IEnumerator PollingStatusSala(System.Action onJogoIniciado)
    {
        while (true)
        {
            string url = FIREBASE_URL + "/salas/" + codigoSalaAtual + "/status.json";
            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string status = req.downloadHandler.text.Trim().Replace("\"", "");
                Debug.Log("Status da sala: " + status);

                if (status == "jogando")
                {
                    onJogoIniciado?.Invoke();
                    yield break;
                }
            }

            yield return new WaitForSeconds(3f);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Jogadores da sala (placar do professor)
    // ─────────────────────────────────────────────────────────────────

    public void BuscarJogadoresSala(string codigo, System.Action<List<DadosJogador>> callback)
    {
        StartCoroutine(BuscarJogadoresCoroutine(codigo, callback));
    }

    IEnumerator BuscarJogadoresCoroutine(string codigo, System.Action<List<DadosJogador>> callback)
    {
        string url = FIREBASE_URL + "/salas/" + codigo + "/jogadores.json";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            callback(new List<DadosJogador>());
            yield break;
        }

        string json = req.downloadHandler.text;
        var lista = new List<DadosJogador>();

        if (json == "null" || string.IsNullOrEmpty(json))
        {
            callback(lista);
            yield break;
        }

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
                    DadosJogador d = new DadosJogador();
                    d.nome = ExtrairValorString(bloco, "nome");
                    d.pontos = ExtrairValorInt(bloco, "pontos");
                    d.fase = ExtrairValorInt(bloco, "fase");
                    if (!string.IsNullOrEmpty(d.nome))
                        lista.Add(d);
                }
                catch { }
            }

            i = fim;
        }

        callback(lista);
    }

    public void AtualizarPontosSala(int pontos, int fase)
    {
        if (string.IsNullOrEmpty(codigoSalaAtual)) return;
        StartCoroutine(AtualizarPontosCoroutine(pontos, fase));
    }

    IEnumerator AtualizarPontosCoroutine(int pontos, int fase)
    {
        string json = "{\"pontos\":" + pontos + ",\"fase\":" + fase + "}";
        string url = FIREBASE_URL + "/salas/" + codigoSalaAtual + "/jogadores/" + userId + ".json";

        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
    }

    // ─────────────────────────────────────────────────────────────────
    // Poderes (modo Turma)
    // ─────────────────────────────────────────────────────────────────

    public void EnviarPoderParaAdversario(string tipoPoder)
    {
        if (string.IsNullOrEmpty(codigoSalaAtual)) return;
        StartCoroutine(EnviarPoderCoroutine(tipoPoder));
    }

    IEnumerator EnviarPoderCoroutine(string tipoPoder)
    {
        string timestamp = System.DateTime.Now.Ticks.ToString();
        string json = "{\"tipo\":\"" + tipoPoder + "\","
                    + "\"de\":\"" + userId + "\","
                    + "\"timestamp\":\"" + timestamp + "\"}";

        string url = FIREBASE_URL + "/salas/" + codigoSalaAtual + "/poderes/" + timestamp + ".json";

        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("Poder enviado: " + tipoPoder);
        else
            Debug.LogError("Erro ao enviar poder: " + req.error);
    }

    public void IniciarEscutaPoderes(System.Action<string> onPoderRecebido)
    {
        StartCoroutine(PollingPoderes(onPoderRecebido));
    }

    IEnumerator PollingPoderes(System.Action<string> onPoderRecebido)
    {
        string ultimoTimestamp = "";

        while (true)
        {
            string url = FIREBASE_URL + "/salas/" + codigoSalaAtual + "/poderes.json";
            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                if (json != "null" && !string.IsNullOrEmpty(json))
                {
                    string timestamp = ExtrairValorString(json, "timestamp");
                    string tipo = ExtrairValorString(json, "tipo");
                    string de = ExtrairValorString(json, "de");

                    if (!string.IsNullOrEmpty(tipo)
                        && timestamp != ultimoTimestamp
                        && de != userId)
                    {
                        ultimoTimestamp = timestamp;
                        onPoderRecebido?.Invoke(tipo);
                    }
                }
            }

            yield return new WaitForSeconds(2f);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────
// Estrutura de dados de um jogador no ranking
// ─────────────────────────────────────────────────────────────────────

public class DadosJogador
{
    public string nome;
    public int pontos;
    public int acertos;
    public int erros;
    public int aproveitamento;
    public int fase;
}