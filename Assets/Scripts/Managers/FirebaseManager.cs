using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager instance;

    private const string FIREBASE_URL = "https://mathshooter-6c0f7-default-rtdb.firebaseio.com";

    [Header("Autenticação (Firebase Anonymous Auth)")]
    [Tooltip("Web API Key do projeto — Configurações do Projeto > Geral, no console do Firebase.")]
    public string firebaseApiKey = "";

    public string userId = "";
    public string nomeJogador = "Jogador";
    public string codigoSalaAtual = "";

    // ── Estado da autenticação ──────────────────────────────────────────
    private string idToken = "";
    private string refreshToken = "";
    private float tokenExpiraEm = 0f; // Time.unscaledTime em que o token expira
    private bool autenticado = false;

    public bool EstaAutenticado => autenticado;

    // Referências das coroutines de "escuta" que ficam rodando durante uma
    // partida Turma (poderes recebidos + encerramento da sala + perguntas).
    private Coroutine coroutinePoderes;
    private Coroutine coroutineEncerramento;
    private Coroutine coroutinePerguntas;

    [DllImport("__Internal")]
    private static extern System.IntPtr GetNomeDaURL();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            GerarUserId();
            ObterNomeDaURL();
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
        StartCoroutine(IniciarAutenticacao());
    }

    void GerarUserId()
    {
        string chavePlayerPrefs = "userId_" + Application.dataPath.GetHashCode();

        if (PlayerPrefs.HasKey(chavePlayerPrefs))
            userId = PlayerPrefs.GetString(chavePlayerPrefs);
        else
        {
            userId = System.DateTime.Now.Ticks.ToString() + Random.Range(1000, 9999);
            PlayerPrefs.SetString(chavePlayerPrefs, userId);
            PlayerPrefs.Save();
        }
        Debug.Log("ID do jogador: " + userId);
    }

    void ObterNomeDaURL()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            System.IntPtr ptr = GetNomeDaURL();
            string nome = Marshal.PtrToStringUTF8(ptr);
            if (!string.IsNullOrEmpty(nome))
                nomeJogador = nome;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[FirebaseManager] Não foi possível ler nome da URL: " + e.Message);
        }
#endif
        Debug.Log("Nome do jogador: " + nomeJogador);
    }

    // ─────────────────────────────────────────────────────────────────
    // Autenticação (Anonymous Auth)
    // ─────────────────────────────────────────────────────────────────

    string ChaveRefreshPlayerPrefs() => "firebaseRefreshToken_" + Application.dataPath.GetHashCode();

    IEnumerator IniciarAutenticacao()
    {
        if (string.IsNullOrEmpty(firebaseApiKey))
        {
            Debug.LogError("[FirebaseManager] firebaseApiKey não configurada no Inspector — nenhuma chamada ao Firebase vai funcionar.");
            yield break;
        }

        // DIAGNÓSTICO: confirma exatamente o que está no campo do Inspector
        Debug.Log("[FirebaseManager][DEBUG] firebaseApiKey (tamanho=" + firebaseApiKey.Length + "): '" + firebaseApiKey + "'");

        string refreshSalvo = PlayerPrefs.GetString(ChaveRefreshPlayerPrefs(), "");

        if (!string.IsNullOrEmpty(refreshSalvo))
        {
            refreshToken = refreshSalvo;
            yield return RenovarToken();
        }

        if (!autenticado)
            yield return AutenticarAnonimo();

        if (autenticado)
        {
            PlayerPrefs.SetString(ChaveRefreshPlayerPrefs(), refreshToken);
            PlayerPrefs.Save();
            StartCoroutine(RenovarTokenPeriodicamente());
            Debug.Log("[FirebaseManager] Autenticado no Firebase.");
        }
        else
        {
            Debug.LogError("[FirebaseManager] Falha ao autenticar no Firebase — verifique a Web API Key e se o Anonymous Auth está ativado no console.");
        }
    }

    IEnumerator AutenticarAnonimo()
    {
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + firebaseApiKey;
        string json = "{\"returnSecureToken\":true}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        // DIAGNÓSTICO: sempre loga resultado, código HTTP e corpo bruto da resposta,
        // independente de sucesso ou falha.
        Debug.Log("[FirebaseManager][DEBUG] AutenticarAnonimo -> result=" + req.result
            + " | HTTP=" + req.responseCode
            + " | networkError=" + req.error
            + " | resposta='" + req.downloadHandler.text + "'");

        if (req.result == UnityWebRequest.Result.Success)
        {
            string resposta = req.downloadHandler.text;
            idToken = ExtrairValorString(resposta, "idToken");
            refreshToken = ExtrairValorString(resposta, "refreshToken");
            float.TryParse(ExtrairValorString(resposta, "expiresIn"), out float expiraSeg);
            if (expiraSeg <= 0f) expiraSeg = 3600f;
            tokenExpiraEm = Time.unscaledTime + Mathf.Max(60f, expiraSeg - 60f);
            autenticado = !string.IsNullOrEmpty(idToken);

            // DIAGNÓSTICO: confirma se o parser conseguiu extrair o idToken
            Debug.Log("[FirebaseManager][DEBUG] idToken extraído (tamanho=" + idToken.Length
                + "), refreshToken extraído (tamanho=" + refreshToken.Length
                + "), autenticado=" + autenticado);
        }
        else
        {
            Debug.LogError("[FirebaseManager] Erro no login anônimo: " + req.error + " | " + req.downloadHandler.text);
            autenticado = false;
        }
    }

    IEnumerator RenovarToken()
    {
        string url = "https://securetoken.googleapis.com/v1/token?key=" + firebaseApiKey;
        string form = "grant_type=refresh_token&refresh_token=" + refreshToken;
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(form);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            string resposta = req.downloadHandler.text;
            // securetoken usa snake_case, diferente do endpoint de signUp
            idToken = ExtrairValorString(resposta, "id_token");
            refreshToken = ExtrairValorString(resposta, "refresh_token");
            float.TryParse(ExtrairValorString(resposta, "expires_in"), out float expiraSeg);
            if (expiraSeg <= 0f) expiraSeg = 3600f;
            tokenExpiraEm = Time.unscaledTime + Mathf.Max(60f, expiraSeg - 60f);
            autenticado = !string.IsNullOrEmpty(idToken);
        }
        else
        {
            Debug.LogWarning("[FirebaseManager] Falha ao renovar token: " + req.error);
            autenticado = false;
        }
    }

    IEnumerator RenovarTokenPeriodicamente()
    {
        while (true)
        {
            float espera = Mathf.Max(5f, tokenExpiraEm - Time.unscaledTime);
            yield return new WaitForSecondsRealtime(espera);

            yield return RenovarToken();

            if (!autenticado)
            {
                yield return AutenticarAnonimo();
                if (autenticado)
                {
                    PlayerPrefs.SetString(ChaveRefreshPlayerPrefs(), refreshToken);
                    PlayerPrefs.Save();
                }
            }
        }
    }

    // Chame no início de qualquer coroutine que vá falar com o Firebase.
    IEnumerator EsperarAutenticacao()
    {
        float t = 0f;
        while (!autenticado && t < 10f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // Anexa o token na URL (usa & se já tiver query string, senão ?).
    string ComAuth(string url)
    {
        string separador = url.Contains("?") ? "&" : "?";
        return url + separador + "auth=" + idToken;
    }

    // Exposto pra outros scripts que montam URL própria (ex.: MonitorSalaManager).
    public string ConstruirUrlComAuth(string urlSemAuth) => ComAuth(urlSemAuth);

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
        if (!autenticado) yield return EsperarAutenticacao();

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

        string url = ComAuth(FIREBASE_URL + "/resultados/" + userId + "/fase" + fase + ".json");
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
        if (!autenticado) yield return EsperarAutenticacao();

        string url = ComAuth(FIREBASE_URL + "/resultados.json");
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Erro ao buscar ranking: " + req.error);
            callback(new List<DadosJogador>());
            yield break;
        }

        string resposta = req.downloadHandler.text;
        List<DadosJogador> lista = ParsearRanking(resposta);
        lista.Sort((a, b) => b.pontos.CompareTo(a.pontos));
        callback(lista);
    }



List<DadosJogador> ParsearRanking(string json)
{
    var lista = new List<DadosJogador>();
    if (json == "null" || string.IsNullOrEmpty(json)) return lista;

    // Guarda o melhor resultado de cada jogador (por userId)
    var melhorPorJogador = new Dictionary<string, DadosJogador>();

    // Nível 1: cada userId
    foreach (var (userId, blocoUsuario) in ExtrairObjetosComChave(json))
    {
        // Nível 2: cada fase dentro do userId
        foreach (var (faseChave, blocoFase) in ExtrairObjetosComChave(blocoUsuario))
        {
            if (!blocoFase.Contains("\"nome\"")) continue;

            try
            {
                DadosJogador d = new DadosJogador();
                d.userId = userId;
                d.nome = ExtrairValorString(blocoFase, "nome");
                d.pontos = ExtrairValorInt(blocoFase, "pontos");
                d.acertos = ExtrairValorInt(blocoFase, "acertos");
                d.erros = ExtrairValorInt(blocoFase, "erros");
                d.aproveitamento = ExtrairValorInt(blocoFase, "aproveitamento");
                d.fase = ExtrairValorInt(blocoFase, "fase");

                if (string.IsNullOrEmpty(d.nome)) continue;

                if (!melhorPorJogador.ContainsKey(userId) || d.pontos > melhorPorJogador[userId].pontos)
                    melhorPorJogador[userId] = d;
            }
            catch { }
        }
    }

    lista.AddRange(melhorPorJogador.Values);
    return lista;
}

string ExtrairValorString(string json, string chave)
    {
        // Busca só pela chave + ":", sem exigir a aspas logo em seguida —
        // o Google às vezes retorna JSON "pretty" com espaço/quebra de linha
        // depois dos dois pontos (ex: "idToken": "..." em vez de "idToken":"...").
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return "";
        inicio += busca.Length;

        // Pula espaços, tabs e quebras de linha opcionais antes da aspas de abertura.
        while (inicio < json.Length &&
               (json[inicio] == ' ' || json[inicio] == '\n' || json[inicio] == '\r' || json[inicio] == '\t'))
            inicio++;

        if (inicio >= json.Length || json[inicio] != '"') return "";
        inicio++; // pula a aspas de abertura

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

        // Pula espaços, tabs e quebras de linha opcionais antes do número
        // (mesmo motivo do ExtrairValorString — JSON "pretty" do Google).
        while (inicio < json.Length &&
               (json[inicio] == ' ' || json[inicio] == '\n' || json[inicio] == '\r' || json[inicio] == '\t'))
            inicio++;

        int fim = inicio;
        while (fim < json.Length && (char.IsDigit(json[fim]) || json[fim] == '-')) fim++;
        string valor = json.Substring(inicio, fim - inicio);
        int.TryParse(valor, out int resultado);
        return resultado;
    }

    List<(string chave, string bloco)> ExtrairObjetosComChave(string json)
    {
        var resultado = new List<(string chave, string bloco)>();
        int i = 0;
        while (i < json.Length)
        {
            if (json[i] == '"')
            {
                int inicioChave = i + 1;
                int fimChave = json.IndexOf('"', inicioChave);
                if (fimChave < 0) break;
                string chave = json.Substring(inicioChave, fimChave - inicioChave);

                int j = fimChave + 1;
                while (j < json.Length && json[j] != ':') j++;
                j++;
                while (j < json.Length && json[j] == ' ') j++;

                if (j < json.Length && json[j] == '{')
                {
                    int nivel = 1;
                    int k = j + 1;
                    while (k < json.Length && nivel > 0)
                    {
                        if (json[k] == '{') nivel++;
                        else if (json[k] == '}') nivel--;
                        k++;
                    }
                    string bloco = json.Substring(j, k - j);
                    resultado.Add((chave, bloco));
                    i = k;
                    continue;
                }

                i = fimChave + 1;
            }
            else
            {
                i++;
            }
        }
        return resultado;
    }

    List<string> ExtrairChavesDeObjetos(string json)
    {
        var lista = new List<string>();
        foreach (var par in ExtrairObjetosComChave(json))
            lista.Add(par.chave);
        return lista;
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
        if (!autenticado) yield return EsperarAutenticacao();

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string json = "{"
            + "\"status\":\"aguardando\","
            + "\"faseAtual\":1,"
            + "\"criadoEm\":\"" + timestamp + "\""
            + "}";

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigo + ".json");
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

    public void EntrarNaSala(string codigo, System.Action<bool, string> callback)
    {
        StartCoroutine(EntrarNaSalaCoroutine(codigo.ToUpper(), callback));
    }

    IEnumerator EntrarNaSalaCoroutine(string codigo, System.Action<bool, string> callback)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string urlVerifica = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/status.json");
        UnityWebRequest reqVerifica = UnityWebRequest.Get(urlVerifica);
        yield return reqVerifica.SendWebRequest();

        if (reqVerifica.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Erro de rede: " + reqVerifica.error);
            callback(false, "Erro de rede. Tente novamente.");
            yield break;
        }

        string resposta = reqVerifica.downloadHandler.text.Trim();

        if (resposta == "null" || string.IsNullOrEmpty(resposta))
        {
            callback(false, "Sala não encontrada.\nVerifique o código e tente novamente.");
            yield break;
        }

        string statusAtual = resposta.Replace("\"", "");
        if (statusAtual != "aguardando")
        {
            string motivo = statusAtual == "jogando"
                ? "A partida já começou. Peça pro professor criar uma nova sala."
                : "Essa sala não está mais disponível.";
            callback(false, motivo);
            yield break;
        }

        codigoSalaAtual = codigo;
        string json = "{"
            + "\"nome\":\"" + nomeJogador + "\","
            + "\"pontos\":0,"
            + "\"respondeu\":false,"
            + "\"acertou\":false"
            + "}";

        string urlJogador = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/jogadores/" + userId + ".json");
        UnityWebRequest req = new UnityWebRequest(urlJogador, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            callback(true, "");
        else
            callback(false, "Erro ao registrar na sala. Tente novamente.");
    }

    public void IniciarJogoNaSala(string codigo, System.Action<bool> callback)
    {
        StartCoroutine(IniciarJogoCoroutine(codigo, callback));
    }

    IEnumerator IniciarJogoCoroutine(string codigo, System.Action<bool> callback)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/status.json");
        string json = "\"jogando\"";

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        callback(req.result == UnityWebRequest.Result.Success);
    }

    public void EncerrarSalaNoServidor(string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        StartCoroutine(EncerrarSalaCoroutine(codigo));
    }

    IEnumerator EncerrarSalaCoroutine(string codigo)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/status.json");
        string json = "\"encerrada\"";

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("Sala encerrada: " + codigo);
        else
            Debug.LogError("Erro ao encerrar sala: " + req.error);
    }

    public void IniciarEscutaEncerramento(System.Action onSalaEncerrada)
    {
        PararEscutaEncerramento();
        coroutineEncerramento = StartCoroutine(PollingEncerramentoSala(onSalaEncerrada));
    }

    public void PararEscutaEncerramento()
    {
        if (coroutineEncerramento != null)
        {
            StopCoroutine(coroutineEncerramento);
            coroutineEncerramento = null;
        }
    }

    public void PararTodasEscutasDePartida()
    {
        PararEscutaPoderes();
        PararEscutaEncerramento();
        PararEscutaPerguntas();
    }

    IEnumerator PollingEncerramentoSala(System.Action onSalaEncerrada)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        while (true)
        {
            if (!string.IsNullOrEmpty(codigoSalaAtual))
            {
                string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/status.json");
                UnityWebRequest req = UnityWebRequest.Get(url);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string status = req.downloadHandler.text.Trim().Replace("\"", "");
                    if (status == "encerrada")
                    {
                        onSalaEncerrada?.Invoke();
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(3f);
        }
    }

    public void IniciarEsperaDeJogo(System.Action onJogoIniciado)
    {
        StartCoroutine(PollingStatusSala(onJogoIniciado));
    }

    IEnumerator PollingStatusSala(System.Action onJogoIniciado)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        while (true)
        {
            string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/status.json");
            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string status = req.downloadHandler.text.Trim().Replace("\"", "");

                if (status == "jogando")
                {
                    onJogoIniciado?.Invoke();
                    yield break;
                }
            }

            yield return new WaitForSeconds(3f);
        }
    }

    public void BuscarJogadoresSala(string codigo, System.Action<List<DadosJogador>> callback)
    {
        StartCoroutine(BuscarJogadoresCoroutine(codigo, callback));
    }

    IEnumerator BuscarJogadoresCoroutine(string codigo, System.Action<List<DadosJogador>> callback)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/jogadores.json");
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

        foreach (var (chave, bloco) in ExtrairObjetosComChave(json))
        {
            if (!bloco.Contains("\"nome\"")) continue;

            try
            {
                DadosJogador d = new DadosJogador();
                d.userId = chave;
                d.nome = ExtrairValorString(bloco, "nome");
                d.pontos = ExtrairValorInt(bloco, "pontos");
                d.fase = ExtrairValorInt(bloco, "fase");
                d.respondeu = bloco.Contains("\"respondeu\":true");
                d.acertou = bloco.Contains("\"acertou\":true");
                if (!string.IsNullOrEmpty(d.nome))
                    lista.Add(d);
            }
            catch { }
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
        if (!autenticado) yield return EsperarAutenticacao();

        string json = "{\"pontos\":" + pontos + ",\"fase\":" + fase + "}";
        string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/jogadores/" + userId + ".json");

        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
    }

    public void EnviarPoderParaAdversario(string tipoPoder)
    {
        if (string.IsNullOrEmpty(codigoSalaAtual)) return;
        StartCoroutine(EnviarPoderAutoCoroutine(tipoPoder));
    }

    public void EnviarPoderParaAdversario(string tipoPoder, string targetUserId)
    {
        if (string.IsNullOrEmpty(codigoSalaAtual)) return;
        if (string.IsNullOrEmpty(targetUserId))
        {
            Debug.LogWarning("[FirebaseManager] EnviarPoderParaAdversario chamado sem targetUserId.");
            return;
        }
        StartCoroutine(EnviarPoderCoroutine(tipoPoder, targetUserId));
    }

    IEnumerator EnviarPoderAutoCoroutine(string tipoPoder)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/jogadores.json");
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Erro ao buscar jogadores da sala pra enviar poder: " + req.error);
            yield break;
        }

        string json = req.downloadHandler.text;
        if (json == "null" || string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("Sala sem jogadores registrados ainda.");
            yield break;
        }

        List<string> ids = ExtrairChavesDeObjetos(json);
        string alvo = ids.Find(id => id != userId);

        if (string.IsNullOrEmpty(alvo))
        {
            Debug.LogWarning("Nenhum adversário encontrado na sala para enviar poder.");
            yield break;
        }

        yield return EnviarPoderCoroutine(tipoPoder, alvo);
    }

    IEnumerator EnviarPoderCoroutine(string tipoPoder, string targetUserId)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        long timestamp = System.DateTime.UtcNow.Ticks;

        string json = "{\"tipo\":\"" + tipoPoder + "\","
                    + "\"de\":\"" + userId + "\","
                    + "\"timestamp\":\"" + timestamp + "\"}";

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual
                    + "/jogadores/" + targetUserId
                    + "/poderes/" + timestamp + ".json");

        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log("Poder enviado: " + tipoPoder + " -> " + targetUserId);
        else
            Debug.LogError("Erro ao enviar poder: " + req.error);
    }

    public void IniciarEscutaPoderes(System.Action<string> onPoderRecebido)
    {
        PararEscutaPoderes();
        coroutinePoderes = StartCoroutine(PollingPoderes(onPoderRecebido));
    }

    public void PararEscutaPoderes()
    {
        if (coroutinePoderes != null)
        {
            StopCoroutine(coroutinePoderes);
            coroutinePoderes = null;
        }
    }

    IEnumerator PollingPoderes(System.Action<string> onPoderRecebido)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string ultimoTimestamp = "";
        long inicioEscuta = System.DateTime.UtcNow.Ticks;

        while (true)
        {
            string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual
                        + "/jogadores/" + userId
                        + "/poderes.json?orderBy=%22$key%22&limitToLast=1");

            UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string json = req.downloadHandler.text;
                if (json != "null" && !string.IsNullOrEmpty(json))
                {
                    string timestampStr = ExtrairValorString(json, "timestamp");
                    string tipo = ExtrairValorString(json, "tipo");

                    if (!string.IsNullOrEmpty(tipo) && timestampStr != ultimoTimestamp
                        && long.TryParse(timestampStr, out long timestamp))
                    {
                        ultimoTimestamp = timestampStr;

                        if (timestamp >= inicioEscuta)
                            onPoderRecebido?.Invoke(tipo);
                    }
                }
            }
            else
            {
                Debug.LogError("[FirebaseManager] Falha ao checar poderes: " + req.error
                    + " | HTTP " + req.responseCode);
            }

            yield return new WaitForSeconds(2f);
        }
    }

    public void EnviarProximaPergunta(string codigo, string enunciado, int resposta,
                                   int fase, int index, bool boss, System.Action<bool> callback)
    {
        StartCoroutine(EnviarProximaPerguntaCoroutine(codigo, enunciado, resposta, fase, index, boss, callback));
    }

    IEnumerator EnviarProximaPerguntaCoroutine(string codigo, string enunciado, int resposta,
                                                int fase, int index, bool boss, System.Action<bool> callback)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        long timestamp = System.DateTime.UtcNow.Ticks;
        string enunciadoEscapado = enunciado.Replace("\"", "\\\"");
        string json = "{"
            + "\"enunciado\":\"" + enunciadoEscapado + "\","
            + "\"resposta\":" + resposta + ","
            + "\"fase\":" + fase + ","
            + "\"index\":" + index + ","
            + "\"boss\":" + (boss ? "true" : "false") + ","
            + "\"timestamp\":" + timestamp
            + "}";

        string url = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/perguntaAtual.json");
        UnityWebRequest req = new UnityWebRequest(url, "PUT");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        bool sucesso = req.result == UnityWebRequest.Result.Success;

        if (sucesso)
        {
            string urlFase = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/faseAtual.json");
            UnityWebRequest reqFase = new UnityWebRequest(urlFase, "PUT");
            byte[] bodyFase = System.Text.Encoding.UTF8.GetBytes(fase.ToString());
            reqFase.uploadHandler = new UploadHandlerRaw(bodyFase);
            reqFase.downloadHandler = new DownloadHandlerBuffer();
            reqFase.SetRequestHeader("Content-Type", "application/json");
            yield return reqFase.SendWebRequest();
        }

        callback?.Invoke(sucesso);
    }

    public void ResetarRespostasAlunos(string codigo, List<string> userIds)
    {
        if (userIds == null) return;
        foreach (var uid in userIds)
            StartCoroutine(ResetarRespostaCoroutine(codigo, uid));
    }

    IEnumerator ResetarRespostaCoroutine(string codigo, string uid)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string json = "{\"respondeu\":false,\"acertou\":false}";
        string url = ComAuth(FIREBASE_URL + "/salas/" + codigo + "/jogadores/" + uid + ".json");
        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
    }

    public void AtualizarRespostaTurma(int pontos, int fase, bool acertou)
    {
        if (string.IsNullOrEmpty(codigoSalaAtual)) return;
        StartCoroutine(AtualizarRespostaTurmaCoroutine(pontos, fase, acertou));
    }

    IEnumerator AtualizarRespostaTurmaCoroutine(int pontos, int fase, bool acertou)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        string json = "{"
            + "\"pontos\":" + pontos + ","
            + "\"fase\":" + fase + ","
            + "\"respondeu\":true,"
            + "\"acertou\":" + (acertou ? "true" : "false")
            + "}";
        string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/jogadores/" + userId + ".json");
        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
    }

    public void IniciarEscutaPerguntas(System.Action<string, int, int, int, bool> onNovaPergunta,
                                    System.Action onSalaFinalizada)
    {
        PararEscutaPerguntas();
        coroutinePerguntas = StartCoroutine(PollingPerguntaAtual(onNovaPergunta, onSalaFinalizada));
    }

    public void PararEscutaPerguntas()
    {
        if (coroutinePerguntas != null)
        {
            StopCoroutine(coroutinePerguntas);
            coroutinePerguntas = null;
        }
    }

    IEnumerator PollingPerguntaAtual(System.Action<string, int, int, int, bool> onNovaPergunta,
                                  System.Action onSalaFinalizada)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        int ultimoIndex = -1;

        while (true)
        {
            if (!string.IsNullOrEmpty(codigoSalaAtual))
            {
                string urlStatus = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/status.json");
                UnityWebRequest reqStatus = UnityWebRequest.Get(urlStatus);
                yield return reqStatus.SendWebRequest();

                if (reqStatus.result == UnityWebRequest.Result.Success)
                {
                    string status = reqStatus.downloadHandler.text.Trim().Replace("\"", "");
                    if (status == "encerrada")
                    {
                        onSalaFinalizada?.Invoke();
                        yield break;
                    }
                }

                string url = ComAuth(FIREBASE_URL + "/salas/" + codigoSalaAtual + "/perguntaAtual.json");
                UnityWebRequest req = UnityWebRequest.Get(url);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string json = req.downloadHandler.text;
                    if (json != "null" && !string.IsNullOrEmpty(json))
                    {
                        int index = ExtrairValorInt(json, "index");
                        if (index != ultimoIndex)
                        {
                            ultimoIndex = index;
                            string enunciado = ExtrairValorString(json, "enunciado").Replace("\\\"", "\"");
                            int resposta = ExtrairValorInt(json, "resposta");
                            int fase = ExtrairValorInt(json, "fase");
                            bool boss = json.Contains("\"boss\":true") || json.Contains("\"boss\": true");
                            onNovaPergunta?.Invoke(enunciado, resposta, fase, index, boss);
                        }
                    }
                }
            }

            yield return new WaitForSeconds(1.5f);
        }
    }
}

public class DadosJogador
{
    public string userId;
    public string nome;
    public int pontos;
    public int acertos;
    public int erros;
    public int aproveitamento;
    public int fase;
    public bool respondeu;
    public bool acertou;
}