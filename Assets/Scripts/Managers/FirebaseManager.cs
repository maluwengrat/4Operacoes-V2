using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

// ─────────────────────────────────────────────────────────────────────
// Tipos auxiliares
// ─────────────────────────────────────────────────────────────────────

public class RespostaHttp
{
    public bool sucesso;
    public long codigo;
    public string corpo = "";
    public string erro = "";
    public double t0; // realtime no início da última tentativa
    public double t1; // realtime no fim da última tentativa
}

public class PerguntaTurma
{
    public string enunciado;
    public int resposta;
    public int fase;
    public int index;
    public bool boss;
    public float duracao;
    public long inicioMs; // horário do SERVIDOR (ms) em que o professor enviou a pergunta
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
    public bool respondeu;      // mantido por compatibilidade
    public int respondeuIndex = -1;
    public bool acertou;
}

// ─────────────────────────────────────────────────────────────────────
// FirebaseManager
//
// Estrutura no banco:
//   professores/{uid}                   true   (criado só pelo console do Firebase)
//   salas/{codigo}/professorUid
//   salas/{codigo}/criadoEmMs
//   salas/{codigo}/controle/status      aguardando | jogando | finalizada | encerrada
//   salas/{codigo}/controle/modoManual  true | false
//   salas/{codigo}/controle/pergunta    { enunciado, resposta, fase, index, boss, duracao, inicio }
//   salas/{codigo}/jogadores/{userId}   { nome, ownerUid, pontos, fase, respondeuIndex, acertou, visto }
//
// Contas:
//   - Aluno: login anônimo (automático).
//   - Professor: login + senha (Firebase Auth e-mail/senha). O "login" vira o e-mail
//     login@professor.mathshooter.app. A sessão fica só na memória: ao sair, o jogo
//     volta para a conta anônima.
// ─────────────────────────────────────────────────────────────────────

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager instance;

    private const string FIREBASE_URL = "https://mathshooter-6c0f7-default-rtdb.firebaseio.com";

    // Domínio "de mentirinha" usado para transformar o login do professor em e-mail.
    // Ele não precisa existir. Use o MESMO domínio ao criar as contas no console do Firebase.
    private const string DOMINIO_PROFESSOR = "professor.mathshooter.app";

    [Header("Autenticação (Firebase Anonymous Auth)")]
    [Tooltip("Web API Key do projeto — Configurações do Projeto > Geral, no console do Firebase.")]
    public string firebaseApiKey = "";

    public string userId = "";
    public string nomeJogador = "Jogador";
    public string codigoSalaAtual = "";

    // ── Reentrada na sala (aluno que recarregou a página no meio da partida) ──
    [HideInInspector] public bool reentradaPendente = false;
    [HideInInspector] public int pontosReentrada = 0;
    [HideInInspector] public int respondeuIndexReentrada = -1;

    // ── Estado da autenticação ──────────────────────────────────────────
    private string idToken = "";
    private string refreshToken = "";
    private string authUid = "";          // uid do Firebase Auth (usado nas regras de segurança)
    private float tokenExpiraEm = 0f;     // Time.unscaledTime em que o token expira
    private bool autenticado = false;

    public bool EstaAutenticado => autenticado;

    // ── Sessão de professor ─────────────────────────────────────────────
    public bool ProfessorLogado { get; private set; }
    public string LoginProfessorAtual { get; private set; } = "";

    // Sessão anônima guardada enquanto o professor está logado
    private string anonIdToken = "";
    private string anonRefreshToken = "";
    private string anonUid = "";
    private float anonTokenExpiraEm = 0f;

    private int sessaoProfessorId = 0;       // muda a cada login/logout (cancela logouts pendentes)
    private bool loginEmAndamento = false;
    private int falhasLogin = 0;
    private double bloqueadoAte = 0;         // Time.realtimeSinceStartupAsDouble

    // ── Relógio do servidor ─────────────────────────────────────────────
    private bool relogioSincronizado = false;
    private long serverMsNoSync = 0;
    private double realtimeNoSync = 0;
    private double melhorRtt = 999;
    private double ultimoSyncRealtime = 0;

    public bool RelogioSincronizado => relogioSincronizado;

    // Horário atual do servidor em ms (válido se RelogioSincronizado)
    public long ServerNowMs()
    {
        double decorrido = (Time.realtimeSinceStartupAsDouble - realtimeNoSync) * 1000.0;
        return serverMsNoSync + (long)decorrido;
    }

    // Referências das coroutines de "escuta" que ficam rodando durante uma partida Turma
    private Coroutine coroutinePoderes;
    private Coroutine coroutineEncerramento;
    private Coroutine coroutinePerguntas;
    private Coroutine coroutineHeartbeat;

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
                nomeJogador = SanitizarNome(nome);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[FirebaseManager] Não foi possível ler nome da URL: " + e.Message);
        }
#endif
        Debug.Log("Nome do jogador: " + nomeJogador);
    }

    // Remove aspas, barras e caracteres de controle (quebrariam o JSON montado à mão)
    public static string SanitizarNome(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return "Jogador";
        var sb = new System.Text.StringBuilder();
        foreach (char c in nome)
        {
            if (c == '"' || c == '\\' || char.IsControl(c)) continue;
            sb.Append(c);
        }
        string r = sb.ToString().Trim();
        if (r.Length > 20) r = r.Substring(0, 20);
        return r.Length == 0 ? "Jogador" : r;
    }

    // Escapa texto para colocar dentro de uma string JSON (usado na senha, que pode ter qualquer caractere)
    static string EscaparJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────
    // Autenticação anônima (alunos)
    // ─────────────────────────────────────────────────────────────────

    string ChaveRefreshPlayerPrefs() => "firebaseRefreshToken_" + Application.dataPath.GetHashCode();

    IEnumerator IniciarAutenticacao()
    {
        if (string.IsNullOrEmpty(firebaseApiKey))
        {
            Debug.LogError("[FirebaseManager] firebaseApiKey não configurada no Inspector — nenhuma chamada ao Firebase vai funcionar.");
            yield break;
        }

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
            Debug.Log("[FirebaseManager] Autenticado no Firebase. uid=" + authUid);
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

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string resposta = req.downloadHandler.text;
                idToken = ExtrairValorString(resposta, "idToken");
                refreshToken = ExtrairValorString(resposta, "refreshToken");
                authUid = ExtrairValorString(resposta, "localId");
                float.TryParse(ExtrairValorString(resposta, "expiresIn"), out float expiraSeg);
                if (expiraSeg <= 0f) expiraSeg = 3600f;
                tokenExpiraEm = Time.unscaledTime + Mathf.Max(60f, expiraSeg - 60f);
                autenticado = !string.IsNullOrEmpty(idToken);
            }
            else
            {
                Debug.LogError("[FirebaseManager] Erro no login anônimo: " + req.error + " | " + req.downloadHandler.text);
                autenticado = false;
            }
        }
    }

    IEnumerator RenovarToken()
    {
        string url = "https://securetoken.googleapis.com/v1/token?key=" + firebaseApiKey;
        string form = "grant_type=refresh_token&refresh_token=" + refreshToken;
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(form);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
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
                string uid = ExtrairValorString(resposta, "user_id");
                if (!string.IsNullOrEmpty(uid)) authUid = uid;
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
                // A sessão do professor expirou e não renovou: volta para a conta anônima
                if (ProfessorLogado)
                {
                    Debug.LogWarning("[FirebaseManager] Sessão de professor expirada. Voltando para a conta anônima.");
                    RestaurarSessaoAnonima();
                    yield return RenovarToken();
                }

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
    }

    // Espera (até 10s) a autenticação ficar pronta.
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

    // Mantido por compatibilidade com outros scripts.
    public string ConstruirUrlComAuth(string urlSemAuth) => ComAuth(urlSemAuth);

    // ─────────────────────────────────────────────────────────────────
    // Login do professor (login + senha)
    // ─────────────────────────────────────────────────────────────────

    // Aceita só letras sem acento, números, ponto, hífen e sublinhado (3 a 30 caracteres).
    // Devolve o login normalizado (minúsculo) ou null se for inválido.
    public static string NormalizarLoginProfessor(string login)
    {
        if (string.IsNullOrEmpty(login)) return null;
        string l = login.Trim().ToLowerInvariant();
        if (l.Length < 3 || l.Length > 30) return null;

        foreach (char c in l)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
            if (!ok) return null;
        }

        if (l[0] == '.' || l[l.Length - 1] == '.') return null;
        return l;
    }

    // callback(sucesso, mensagem)
    public void LoginProfessor(string login, string senha, System.Action<bool, string> callback)
    {
        StartCoroutine(LoginProfessorCoroutine(login, senha, callback));
    }

    IEnumerator LoginProfessorCoroutine(string login, string senha, System.Action<bool, string> callback)
    {
        if (loginEmAndamento)
        {
            callback(false, "Aguarde, entrando...");
            yield break;
        }

        loginEmAndamento = true;
        yield return LoginProfessorInterno(login, senha, callback);
        loginEmAndamento = false;
    }

    IEnumerator LoginProfessorInterno(string login, string senha, System.Action<bool, string> callback)
    {
        // Bloqueio temporário depois de várias tentativas erradas (o Firebase também limita por conta)
        double agora = Time.realtimeSinceStartupAsDouble;
        if (agora < bloqueadoAte)
        {
            int resta = Mathf.CeilToInt((float)(bloqueadoAte - agora));
            callback(false, "Muitas tentativas. Aguarde " + resta + "s para tentar de novo.");
            yield break;
        }

        // Já está logado: só renova a sessão (e cancela um logout que estava pendente)
        if (ProfessorLogado)
        {
            sessaoProfessorId++;
            callback(true, "");
            yield break;
        }

        string loginNorm = NormalizarLoginProfessor(login);
        if (loginNorm == null)
        {
            callback(false, "Login inválido. Use letras sem acento, números, ponto, hífen ou sublinhado (3 a 30 caracteres).");
            yield break;
        }

        if (string.IsNullOrEmpty(senha))
        {
            callback(false, "Digite a senha.");
            yield break;
        }

        if (!autenticado) yield return EsperarAutenticacao();
        if (!autenticado)
        {
            callback(false, "Sem conexão com o servidor. Tente novamente.");
            yield break;
        }

        string email = loginNorm + "@" + DOMINIO_PROFESSOR;
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" + firebaseApiKey;
        string corpo = "{\"email\":\"" + email + "\","
                     + "\"password\":\"" + EscaparJson(senha) + "\","
                     + "\"returnSecureToken\":true}";

        bool sucessoHttp = false;
        string resposta = "";
        string erroRede = "";

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(corpo));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            sucessoHttp = req.result == UnityWebRequest.Result.Success;
            resposta = req.downloadHandler.text;
            erroRede = req.error;
        }

        if (!sucessoHttp)
        {
            // Erro de rede (sem resposta do servidor): não conta como senha errada
            if (string.IsNullOrEmpty(resposta))
            {
                callback(false, "Erro de rede. Tente novamente.");
                yield break;
            }

            if (resposta.Contains("TOO_MANY_ATTEMPTS"))
            {
                callback(false, "Muitas tentativas. Tente de novo mais tarde.");
                yield break;
            }

            RegistrarFalhaLogin();
            Debug.LogWarning("[FirebaseManager] Login de professor recusado: " + ExtrairValorString(resposta, "message"));
            callback(false, "Login ou senha incorretos.");
            yield break;
        }

        string novoIdToken = ExtrairValorString(resposta, "idToken");
        string novoRefresh = ExtrairValorString(resposta, "refreshToken");
        string novoUid = ExtrairValorString(resposta, "localId");
        float.TryParse(ExtrairValorString(resposta, "expiresIn"), out float expiraSeg);
        if (expiraSeg <= 0f) expiraSeg = 3600f;

        if (string.IsNullOrEmpty(novoIdToken) || string.IsNullOrEmpty(novoUid))
        {
            callback(false, "Resposta inesperada do servidor. Tente novamente.");
            yield break;
        }

        // Guarda a sessão anônima e passa a usar a do professor (só em memória)
        anonIdToken = idToken;
        anonRefreshToken = refreshToken;
        anonUid = authUid;
        anonTokenExpiraEm = tokenExpiraEm;

        idToken = novoIdToken;
        refreshToken = novoRefresh;
        authUid = novoUid;
        tokenExpiraEm = Time.unscaledTime + Mathf.Max(60f, expiraSeg - 60f);
        autenticado = true;

        // Confere se a conta está na lista de professores (criada só pelo console)
        var rLista = new RespostaHttp();
        yield return Requisitar("GET", "/professores/" + authUid + ".json", null, rLista, 2);

        if (!rLista.sucesso)
        {
            RestaurarSessaoAnonima();
            yield return RenovarToken();
            callback(false, "Não foi possível verificar a permissão. Tente novamente.");
            yield break;
        }

        if (rLista.corpo.Trim() != "true")
        {
            RestaurarSessaoAnonima();
            yield return RenovarToken();
            RegistrarFalhaLogin();
            callback(false, "Esta conta não tem permissão de professor.");
            yield break;
        }

        falhasLogin = 0;
        ProfessorLogado = true;
        LoginProfessorAtual = loginNorm;
        sessaoProfessorId++;
        Debug.Log("[FirebaseManager] Professor autenticado: " + loginNorm);
        callback(true, "");
    }

    void RegistrarFalhaLogin()
    {
        falhasLogin++;
        if (falhasLogin >= 5)
        {
            bloqueadoAte = Time.realtimeSinceStartupAsDouble + 60.0;
            falhasLogin = 0;
        }
    }

    // Volta para a conta anônima (só troca as variáveis; quem chama renova o token)
    void RestaurarSessaoAnonima()
    {
        idToken = anonIdToken;
        refreshToken = anonRefreshToken;
        authUid = anonUid;
        tokenExpiraEm = anonTokenExpiraEm;
        autenticado = !string.IsNullOrEmpty(idToken);

        ProfessorLogado = false;
        LoginProfessorAtual = "";
        sessaoProfessorId++;
    }

    // Sai da conta de professor e volta para a conta anônima
    public void LogoutProfessor()
    {
        if (!ProfessorLogado) return;
        StartCoroutine(LogoutProfessorCoroutine());
    }

    IEnumerator LogoutProfessorCoroutine()
    {
        if (!ProfessorLogado) yield break;

        RestaurarSessaoAnonima();
        yield return RenovarToken();
        if (!autenticado) yield return AutenticarAnonimo();

        Debug.Log("[FirebaseManager] Sessão de professor encerrada.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Requisição única (todas as chamadas ao banco passam por aqui)
    //  - espera a autenticação
    //  - tenta de novo em erro de rede / 5xx / 429
    //  - em 401 por token expirado, renova o token e tenta de novo
    //  - erros de permissão (regras) e 4xx não repetem
    //  - silencioso = true usa print=silent (resposta vazia, bem mais leve)
    // ─────────────────────────────────────────────────────────────────

    IEnumerator Requisitar(string metodo, string caminho, string corpo, RespostaHttp saida,
                           int tentativas = 3, bool silencioso = false)
    {
        if (!autenticado) yield return EsperarAutenticacao();

        for (int tentativa = 1; tentativa <= tentativas; tentativa++)
        {
            string url = FIREBASE_URL + caminho;
            if (silencioso) url += (url.Contains("?") ? "&" : "?") + "print=silent";
            url = ComAuth(url);

            using (UnityWebRequest req = new UnityWebRequest(url, metodo))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (corpo != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(corpo));
                    req.SetRequestHeader("Content-Type", "application/json");
                }

                saida.t0 = Time.realtimeSinceStartupAsDouble;
                yield return req.SendWebRequest();
                saida.t1 = Time.realtimeSinceStartupAsDouble;

                saida.codigo = req.responseCode;
                saida.sucesso = req.result == UnityWebRequest.Result.Success;
                saida.corpo = saida.sucesso ? req.downloadHandler.text : "";
                saida.erro = saida.sucesso ? "" : (req.error + " | " + req.downloadHandler.text);
            }

            if (saida.sucesso) yield break;

            // O Realtime Database responde 401 tanto para token expirado quanto para regra negando
            bool semPermissao = saida.erro.Contains("Permission denied");

            if (saida.codigo == 401 && !semPermissao)
            {
                yield return RenovarToken();
                if (!autenticado && !ProfessorLogado) yield return AutenticarAnonimo();
                continue;
            }

            if (semPermissao || (saida.codigo >= 400 && saida.codigo < 500 && saida.codigo != 429))
                yield break; // erro permanente: repetir não adianta

            if (tentativa < tentativas)
                yield return new WaitForSecondsRealtime(0.5f * tentativa);
        }
    }

    // Guarda uma amostra do relógio do servidor, a partir de uma resposta que
    // devolve um valor {".sv":"timestamp"} já resolvido.
    void SincronizarRelogio(RespostaHttp r, string chave)
    {
        if (r == null || !r.sucesso) return;
        long servidorMs = ExtrairValorLong(r.corpo, chave);
        if (servidorMs <= 0) return;

        double rtt = r.t1 - r.t0;
        double agora = Time.realtimeSinceStartupAsDouble;
        bool velho = !relogioSincronizado || (agora - ultimoSyncRealtime) > 60.0;

        // Só troca a amostra se for mais precisa (menor latência) ou se a atual está velha
        if (!velho && rtt > melhorRtt) return;

        serverMsNoSync = servidorMs;
        realtimeNoSync = (r.t0 + r.t1) * 0.5;
        melhorRtt = rtt;
        ultimoSyncRealtime = agora;

        if (!relogioSincronizado)
            Debug.Log("[FirebaseManager] Relógio do servidor sincronizado (latência " + (rtt * 1000.0).ToString("F0") + " ms).");
        relogioSincronizado = true;
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
        if (!autenticado) yield return EsperarAutenticacao();

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string json = "{"
            + "\"nome\":\"" + SanitizarNome(nomeJogador) + "\","
            + "\"ownerUid\":\"" + authUid + "\","
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

        var r = new RespostaHttp();
        yield return Requisitar("PUT", "/resultados/" + userId + "/fase" + fase + ".json", json, r, 3, true);

        if (r.sucesso)
            Debug.Log("Resultado salvo no Firebase! Fase " + fase);
        else
            Debug.LogError("Erro ao salvar: " + r.erro);
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
        var r = new RespostaHttp();
        yield return Requisitar("GET", "/resultados.json", null, r, 2);

        if (!r.sucesso)
        {
            Debug.LogError("Erro ao buscar ranking: " + r.erro);
            callback(new List<DadosJogador>());
            yield break;
        }

        List<DadosJogador> lista = ParsearRanking(r.corpo);
        lista.Sort((a, b) => b.pontos.CompareTo(a.pontos));
        callback(lista);
    }

    List<DadosJogador> ParsearRanking(string json)
    {
        var lista = new List<DadosJogador>();
        if (json == "null" || string.IsNullOrEmpty(json)) return lista;

        // Guarda o melhor resultado de cada jogador (por userId)
        var melhorPorJogador = new Dictionary<string, DadosJogador>();

        foreach (var (uidJogador, blocoUsuario) in ExtrairObjetosComChave(json))
        {
            foreach (var (faseChave, blocoFase) in ExtrairObjetosComChave(blocoUsuario))
            {
                if (!blocoFase.Contains("\"nome\"")) continue;

                try
                {
                    DadosJogador d = new DadosJogador();
                    d.userId = uidJogador;
                    d.nome = ExtrairValorString(blocoFase, "nome");
                    d.pontos = ExtrairValorInt(blocoFase, "pontos");
                    d.acertos = ExtrairValorInt(blocoFase, "acertos");
                    d.erros = ExtrairValorInt(blocoFase, "erros");
                    d.aproveitamento = ExtrairValorInt(blocoFase, "aproveitamento");
                    d.fase = ExtrairValorInt(blocoFase, "fase");

                    if (string.IsNullOrEmpty(d.nome)) continue;

                    if (!melhorPorJogador.ContainsKey(uidJogador) || d.pontos > melhorPorJogador[uidJogador].pontos)
                        melhorPorJogador[uidJogador] = d;
                }
                catch { }
            }
        }

        lista.AddRange(melhorPorJogador.Values);
        return lista;
    }

    // ─────────────────────────────────────────────────────────────────
    // Parsers simples de JSON
    // ─────────────────────────────────────────────────────────────────

    bool TemChave(string json, string chave) => json.Contains("\"" + chave + "\":");

    string ExtrairValorString(string json, string chave)
    {
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return "";
        inicio += busca.Length;

        while (inicio < json.Length &&
               (json[inicio] == ' ' || json[inicio] == '\n' || json[inicio] == '\r' || json[inicio] == '\t'))
            inicio++;

        if (inicio >= json.Length || json[inicio] != '"') return "";
        inicio++;

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

        while (inicio < json.Length &&
               (json[inicio] == ' ' || json[inicio] == '\n' || json[inicio] == '\r' || json[inicio] == '\t'))
            inicio++;

        int fim = inicio;
        while (fim < json.Length && (char.IsDigit(json[fim]) || json[fim] == '-')) fim++;
        string valor = json.Substring(inicio, fim - inicio);
        int.TryParse(valor, out int resultado);
        return resultado;
    }

    long ExtrairValorLong(string json, string chave)
    {
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return 0;
        inicio += busca.Length;

        while (inicio < json.Length && char.IsWhiteSpace(json[inicio])) inicio++;

        int fim = inicio;
        while (fim < json.Length && (char.IsDigit(json[fim]) || json[fim] == '-')) fim++;
        long.TryParse(json.Substring(inicio, fim - inicio), out long resultado);
        return resultado;
    }

    bool ExtrairValorBool(string json, string chave)
    {
        string busca = "\"" + chave + "\":";
        int inicio = json.IndexOf(busca);
        if (inicio < 0) return false;
        inicio += busca.Length;

        while (inicio < json.Length && char.IsWhiteSpace(json[inicio])) inicio++;

        return inicio + 4 <= json.Length && string.CompareOrdinal(json, inicio, "true", 0, 4) == 0;
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
    // Criar sala (professor) — exige login de professor
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
        StartCoroutine(CriarSalaCoroutine(callback));
    }

    IEnumerator CriarSalaCoroutine(System.Action<string> callback)
    {
        if (!ProfessorLogado)
        {
            Debug.LogError("Criar sala exige login de professor.");
            callback(null);
            yield break;
        }

        if (!autenticado) yield return EsperarAutenticacao();
        if (!autenticado) { callback(null); yield break; }

        // Procura um código que ainda não exista (evita sobrescrever a sala de outro professor)
        string codigo = null;
        for (int i = 0; i < 6; i++)
        {
            string candidato = GerarCodigoSala();
            var rCheca = new RespostaHttp();
            yield return Requisitar("GET", "/salas/" + candidato + "/professorUid.json", null, rCheca, 2);
            if (rCheca.sucesso && rCheca.corpo.Trim() == "null")
            {
                codigo = candidato;
                break;
            }
        }

        if (codigo == null)
        {
            Debug.LogError("Não foi possível gerar um código de sala livre.");
            callback(null);
            yield break;
        }

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        // A sala já nasce em modo manual (padrão seguro)
        string json = "{"
            + "\"professorUid\":\"" + authUid + "\","
            + "\"criadoEm\":\"" + timestamp + "\","
            + "\"criadoEmMs\":{\".sv\":\"timestamp\"},"
            + "\"controle\":{"
                + "\"status\":\"aguardando\","
                + "\"modoManual\":true"
            + "}"
            + "}";

        var r = new RespostaHttp();
        yield return Requisitar("PUT", "/salas/" + codigo + ".json", json, r, 3, true);

        if (r.sucesso)
        {
            codigoSalaAtual = codigo;
            Debug.Log("Sala criada: " + codigo);
            callback(codigo);
        }
        else
        {
            Debug.LogError("Erro ao criar sala: " + r.erro);
            callback(null);
        }
    }

    // O PAINEL DO PROFESSOR chama isso quando a checkbox
    // "inserir pergunta manualmente" muda (e também ao iniciar a partida).
    public void DefinirModoManualSala(string codigo, bool manual)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        StartCoroutine(DefinirModoManualCoroutine(codigo, manual));
    }

    IEnumerator DefinirModoManualCoroutine(string codigo, bool manual)
    {
        var r = new RespostaHttp();
        yield return Requisitar("PUT", "/salas/" + codigo + "/controle/modoManual.json",
                                manual ? "true" : "false", r, 3, true);

        if (!r.sucesso)
            Debug.LogError("Erro ao definir modo manual: " + r.erro);
    }

    // ─────────────────────────────────────────────────────────────────
    // Entrar na sala (aluno) — aceita reentrada se a partida já começou
    // ─────────────────────────────────────────────────────────────────

    public void EntrarNaSala(string codigo, System.Action<bool, string> callback)
    {
        StartCoroutine(EntrarNaSalaCoroutine(codigo.ToUpper(), callback));
    }

    IEnumerator EntrarNaSalaCoroutine(string codigo, System.Action<bool, string> callback)
    {
        reentradaPendente = false;
        pontosReentrada = 0;
        respondeuIndexReentrada = -1;

        if (!autenticado) yield return EsperarAutenticacao();
        if (!autenticado)
        {
            callback(false, "Sem conexão com o servidor. Tente novamente.");
            yield break;
        }

        var rStatus = new RespostaHttp();
        yield return Requisitar("GET", "/salas/" + codigo + "/controle/status.json", null, rStatus, 3);

        if (!rStatus.sucesso)
        {
            Debug.LogError("Erro de rede: " + rStatus.erro);
            callback(false, "Erro de rede. Tente novamente.");
            yield break;
        }

        string resposta = rStatus.corpo.Trim();

        if (resposta == "null" || string.IsNullOrEmpty(resposta))
        {
            callback(false, "Sala não encontrada.\nVerifique o código e tente novamente.");
            yield break;
        }

        string statusAtual = resposta.Replace("\"", "");

        bool reentrada = false;
        int pontosAnteriores = 0;
        int respondeuAnterior = -1;

        if (statusAtual != "aguardando")
        {
            // Partida em andamento: só deixa entrar quem já estava na sala (ex.: recarregou a página)
            if (statusAtual == "jogando")
            {
                var rJog = new RespostaHttp();
                yield return Requisitar("GET", "/salas/" + codigo + "/jogadores/" + userId + ".json", null, rJog, 2);

                if (rJog.sucesso && rJog.corpo.Trim() != "null" && rJog.corpo.Contains("\"nome\""))
                {
                    reentrada = true;
                    pontosAnteriores = ExtrairValorInt(rJog.corpo, "pontos");
                    respondeuAnterior = TemChave(rJog.corpo, "respondeuIndex")
                        ? ExtrairValorInt(rJog.corpo, "respondeuIndex") : -1;
                }
            }

            if (!reentrada)
            {
                string motivo = statusAtual == "jogando"
                    ? "A partida já começou. Peça pro professor criar uma nova sala."
                    : "Essa sala não está mais disponível.";
                callback(false, motivo);
                yield break;
            }
        }

        nomeJogador = SanitizarNome(nomeJogador);

        string metodo;
        string json;
        if (reentrada)
        {
            // Mantém pontos e resposta; só atualiza nome e sinal de vida
            metodo = "PATCH";
            json = "{"
                + "\"nome\":\"" + nomeJogador + "\","
                + "\"visto\":{\".sv\":\"timestamp\"}"
                + "}";
        }
        else
        {
            metodo = "PUT";
            json = "{"
                + "\"nome\":\"" + nomeJogador + "\","
                + "\"ownerUid\":\"" + authUid + "\","
                + "\"pontos\":0,"
                + "\"fase\":1,"
                + "\"respondeuIndex\":-1,"
                + "\"acertou\":false,"
                + "\"visto\":{\".sv\":\"timestamp\"}"
                + "}";
        }

        var r = new RespostaHttp();
        yield return Requisitar(metodo, "/salas/" + codigo + "/jogadores/" + userId + ".json", json, r, 3);

        if (r.sucesso)
        {
            codigoSalaAtual = codigo;
            reentradaPendente = reentrada;
            pontosReentrada = pontosAnteriores;
            respondeuIndexReentrada = respondeuAnterior;

            // A resposta traz o "visto" já resolvido pelo servidor: serve de amostra do relógio
            SincronizarRelogio(r, "visto");
            if (!relogioSincronizado)
                Debug.LogWarning("[FirebaseManager] Não foi possível sincronizar o relógio com o servidor; o timer vai começar na chegada da pergunta.");

            IniciarHeartbeat();
            callback(true, "");
        }
        else
        {
            Debug.LogError("Erro ao registrar na sala: " + r.erro);
            callback(false, "Erro ao registrar na sala. Tente novamente.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Sair da sala + sinal de vida (heartbeat)
    // ─────────────────────────────────────────────────────────────────

    // Aluno saiu: remove ele da sala e para o sinal de vida
    public void SairDaSala()
    {
        string codigo = codigoSalaAtual;
        PararHeartbeat();
        reentradaPendente = false;
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(userId)) return;

        codigoSalaAtual = "";
        StartCoroutine(SairDaSalaCoroutine(codigo, userId));
    }

    IEnumerator SairDaSalaCoroutine(string codigo, string uid)
    {
        var r = new RespostaHttp();
        yield return Requisitar("DELETE", "/salas/" + codigo + "/jogadores/" + uid + ".json", null, r, 2, true);

        if (!r.sucesso)
            Debug.LogWarning("[FirebaseManager] Erro ao sair da sala: " + r.erro);
    }

    void IniciarHeartbeat()
    {
        PararHeartbeat();
        coroutineHeartbeat = StartCoroutine(HeartbeatCoroutine());
    }

    void PararHeartbeat()
    {
        if (coroutineHeartbeat != null)
        {
            StopCoroutine(coroutineHeartbeat);
            coroutineHeartbeat = null;
        }
    }

    // A cada 5s grava o horário do servidor em "visto" (e aproveita a resposta
    // para manter o relógio sincronizado).
    IEnumerator HeartbeatCoroutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(5f);

            if (string.IsNullOrEmpty(codigoSalaAtual) || !autenticado) continue;

            var r = new RespostaHttp();
            yield return Requisitar("PATCH",
                "/salas/" + codigoSalaAtual + "/jogadores/" + userId + ".json",
                "{\"visto\":{\".sv\":\"timestamp\"}}", r, 1);

            SincronizarRelogio(r, "visto");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Status da sala
    // ─────────────────────────────────────────────────────────────────

    // callback: status ("aguardando", "jogando", "finalizada", "encerrada"),
    // "null" se a sala não existe mais, ou null se a consulta falhou.
    public void ConsultarStatusSala(System.Action<string> callback)
    {
        StartCoroutine(ConsultarStatusCoroutine(codigoSalaAtual, callback));
    }

    IEnumerator ConsultarStatusCoroutine(string codigo, System.Action<string> callback)
    {
        if (string.IsNullOrEmpty(codigo)) { callback(null); yield break; }

        var r = new RespostaHttp();
        yield return Requisitar("GET", "/salas/" + codigo + "/controle/status.json", null, r, 1);

        if (!r.sucesso) { callback(null); yield break; }

        string status = r.corpo.Trim().Replace("\"", "");
        callback(string.IsNullOrEmpty(status) ? "null" : status);
    }

    public void IniciarJogoNaSala(string codigo, System.Action<bool> callback)
    {
        StartCoroutine(DefinirStatusCoroutine(codigo, "jogando", callback));
    }

    // Todas as perguntas foram feitas: os alunos mostram o resultado final
    public void FinalizarSalaNoServidor(string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        StartCoroutine(DefinirStatusCoroutine(codigo, "finalizada", null));
    }

    IEnumerator DefinirStatusCoroutine(string codigo, string status, System.Action<bool> callback)
    {
        var r = new RespostaHttp();
        yield return Requisitar("PUT", "/salas/" + codigo + "/controle/status.json",
                                "\"" + status + "\"", r, 3, true);

        if (!r.sucesso)
            Debug.LogError("Erro ao definir status '" + status + "': " + r.erro);

        callback?.Invoke(r.sucesso);
    }

    // Encerra a sala, apaga ela do banco e sai da conta de professor
    public void EncerrarSalaNoServidor(string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return;
        StartCoroutine(EncerrarSalaCoroutine(codigo));
    }

    IEnumerator EncerrarSalaCoroutine(string codigo)
    {
        int sessao = sessaoProfessorId;

        var r = new RespostaHttp();
        yield return Requisitar("PUT", "/salas/" + codigo + "/controle/status.json", "\"encerrada\"", r, 3, true);

        if (r.sucesso)
            Debug.Log("Sala encerrada: " + codigo);
        else
            Debug.LogError("Erro ao encerrar sala: " + r.erro);

        // Dá tempo dos alunos perceberem. Quem não vir "encerrada" vê a sala sumir e sai do mesmo jeito.
        yield return new WaitForSecondsRealtime(5f);

        var rDel = new RespostaHttp();
        yield return Requisitar("DELETE", "/salas/" + codigo + ".json", null, rDel, 2, true);

        if (rDel.sucesso)
            Debug.Log("Sala removida do banco: " + codigo);
        else
            Debug.LogWarning("Não foi possível remover a sala " + codigo + ": " + rDel.erro);

        // Sai da conta de professor, a não ser que ele já tenha entrado de novo ou criado outra sala
        if (ProfessorLogado && sessao == sessaoProfessorId && string.IsNullOrEmpty(codigoSalaAtual))
            yield return LogoutProfessorCoroutine();
    }

    // Mantido por compatibilidade (o GameManager agora usa o polling único de perguntas)
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
        while (true)
        {
            if (!string.IsNullOrEmpty(codigoSalaAtual))
            {
                var r = new RespostaHttp();
                yield return Requisitar("GET", "/salas/" + codigoSalaAtual + "/controle/status.json", null, r, 1);

                if (r.sucesso)
                {
                    string status = r.corpo.Trim().Replace("\"", "");
                    if (status == "encerrada" || status == "null")
                    {
                        onSalaEncerrada?.Invoke();
                        yield break;
                    }
                }
            }

            yield return new WaitForSecondsRealtime(3f);
        }
    }

    public void IniciarEsperaDeJogo(System.Action onJogoIniciado)
    {
        StartCoroutine(PollingStatusSala(onJogoIniciado));
    }

    IEnumerator PollingStatusSala(System.Action onJogoIniciado)
    {
        while (true)
        {
            var r = new RespostaHttp();
            yield return Requisitar("GET", "/salas/" + codigoSalaAtual + "/controle/status.json", null, r, 1);

            if (r.sucesso)
            {
                string status = r.corpo.Trim().Replace("\"", "");
                if (status == "jogando")
                {
                    onJogoIniciado?.Invoke();
                    yield break;
                }
            }

            yield return new WaitForSecondsRealtime(3f);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Jogadores da sala
    // ─────────────────────────────────────────────────────────────────

    // JSON bruto de jogadores (para o painel do professor). callback(null) se falhou.
    public IEnumerator LerJogadoresJson(string codigo, System.Action<string> callback)
    {
        var r = new RespostaHttp();
        yield return Requisitar("GET", "/salas/" + codigo + "/jogadores.json", null, r, 1);
        callback(r.sucesso ? r.corpo : null);
    }

    public void BuscarJogadoresSala(string codigo, System.Action<List<DadosJogador>> callback)
    {
        StartCoroutine(BuscarJogadoresCoroutine(codigo, callback));
    }

    IEnumerator BuscarJogadoresCoroutine(string codigo, System.Action<List<DadosJogador>> callback)
    {
        var r = new RespostaHttp();
        yield return Requisitar("GET", "/salas/" + codigo + "/jogadores.json", null, r, 2);

        var lista = new List<DadosJogador>();

        if (!r.sucesso || r.corpo == "null" || string.IsNullOrEmpty(r.corpo))
        {
            callback(lista);
            yield break;
        }

        foreach (var (chave, bloco) in ExtrairObjetosComChave(r.corpo))
        {
            if (!bloco.Contains("\"nome\"")) continue;

            try
            {
                DadosJogador d = new DadosJogador();
                d.userId = chave;
                d.nome = ExtrairValorString(bloco, "nome");
                d.pontos = ExtrairValorInt(bloco, "pontos");
                d.fase = ExtrairValorInt(bloco, "fase");
                d.respondeuIndex = TemChave(bloco, "respondeuIndex") ? ExtrairValorInt(bloco, "respondeuIndex") : -1;
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
        StartCoroutine(AtualizarPontosCoroutine(codigoSalaAtual, pontos, fase));
    }

    IEnumerator AtualizarPontosCoroutine(string codigo, int pontos, int fase)
    {
        string json = "{\"pontos\":" + pontos + ",\"fase\":" + fase + "}";
        var r = new RespostaHttp();
        yield return Requisitar("PATCH", "/salas/" + codigo + "/jogadores/" + userId + ".json", json, r, 3, true);
    }

    // Aluno respondeu a pergunta de número "perguntaIndex" (o professor compara com o índice atual)
    public void AtualizarRespostaTurma(int pontos, int fase, bool acertou, int perguntaIndex)
    {
        if (string.IsNullOrEmpty(codigoSalaAtual)) return;
        StartCoroutine(AtualizarRespostaTurmaCoroutine(codigoSalaAtual, pontos, fase, acertou, perguntaIndex));
    }

    IEnumerator AtualizarRespostaTurmaCoroutine(string codigo, int pontos, int fase, bool acertou, int perguntaIndex)
    {
        string json = "{"
            + "\"pontos\":" + pontos + ","
            + "\"fase\":" + fase + ","
            + "\"respondeuIndex\":" + perguntaIndex + ","
            + "\"acertou\":" + (acertou ? "true" : "false")
            + "}";

        var r = new RespostaHttp();
        yield return Requisitar("PATCH", "/salas/" + codigo + "/jogadores/" + userId + ".json", json, r, 3, true);

        if (!r.sucesso)
            Debug.LogWarning("[FirebaseManager] Não foi possível registrar a resposta: " + r.erro);
    }

    // ─────────────────────────────────────────────────────────────────
    // Poderes
    // ─────────────────────────────────────────────────────────────────

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
        var r = new RespostaHttp();
        yield return Requisitar("GET", "/salas/" + codigoSalaAtual + "/jogadores.json", null, r, 2);

        if (!r.sucesso)
        {
            Debug.LogError("Erro ao buscar jogadores da sala pra enviar poder: " + r.erro);
            yield break;
        }

        if (r.corpo == "null" || string.IsNullOrEmpty(r.corpo))
        {
            Debug.LogWarning("Sala sem jogadores registrados ainda.");
            yield break;
        }

        List<string> ids = ExtrairChavesDeObjetos(r.corpo);
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
        long timestamp = System.DateTime.UtcNow.Ticks;

        string json = "{\"tipo\":\"" + tipoPoder + "\","
                    + "\"de\":\"" + userId + "\","
                    + "\"timestamp\":\"" + timestamp + "\"}";

        var r = new RespostaHttp();
        yield return Requisitar("PUT",
            "/salas/" + codigoSalaAtual + "/jogadores/" + targetUserId + "/poderes/" + timestamp + ".json",
            json, r, 3, true);

        if (r.sucesso)
            Debug.Log("Poder enviado: " + tipoPoder + " -> " + targetUserId);
        else
            Debug.LogError("Erro ao enviar poder: " + r.erro);
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
        string ultimoTimestamp = "";
        long inicioEscuta = System.DateTime.UtcNow.Ticks;

        while (true)
        {
            if (!string.IsNullOrEmpty(codigoSalaAtual))
            {
                var r = new RespostaHttp();
                yield return Requisitar("GET",
                    "/salas/" + codigoSalaAtual + "/jogadores/" + userId
                    + "/poderes.json?orderBy=%22$key%22&limitToLast=1", null, r, 1);

                if (r.sucesso)
                {
                    string json = r.corpo;
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
                    Debug.LogWarning("[FirebaseManager] Falha ao checar poderes: " + r.erro);
                }
            }

            yield return new WaitForSecondsRealtime(2f);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Perguntas da turma
    // ─────────────────────────────────────────────────────────────────

    // Professor: publica a pergunta. "inicio" é gravado com o relógio do servidor,
    // e todos os celulares calculam o tempo restante a partir dele.
    public void EnviarProximaPergunta(string codigo, string enunciado, int resposta,
                                      int fase, int index, bool boss, float duracao,
                                      System.Action<bool> callback)
    {
        StartCoroutine(EnviarProximaPerguntaCoroutine(codigo, enunciado, resposta, fase, index, boss, duracao, callback));
    }

    IEnumerator EnviarProximaPerguntaCoroutine(string codigo, string enunciado, int resposta,
                                               int fase, int index, bool boss, float duracao,
                                               System.Action<bool> callback)
    {
        string json = "{"
            + "\"enunciado\":\"" + EscaparJson(enunciado) + "\","
            + "\"resposta\":" + resposta + ","
            + "\"fase\":" + fase + ","
            + "\"index\":" + index + ","
            + "\"boss\":" + (boss ? "true" : "false") + ","
            + "\"duracao\":" + Mathf.RoundToInt(duracao) + ","
            + "\"inicio\":{\".sv\":\"timestamp\"}"
            + "}";

        var r = new RespostaHttp();
        yield return Requisitar("PUT", "/salas/" + codigo + "/controle/pergunta.json", json, r, 3, true);

        if (!r.sucesso)
            Debug.LogError("Erro ao enviar pergunta: " + r.erro);

        callback?.Invoke(r.sucesso);
    }

    public void IniciarEscutaPerguntas(System.Action<PerguntaTurma> onNovaPergunta,
                                       System.Action onSalaEncerrada,
                                       System.Action onPartidaFinalizada)
    {
        PararEscutaPerguntas();
        coroutinePerguntas = StartCoroutine(PollingControle(onNovaPergunta, onSalaEncerrada, onPartidaFinalizada));
    }

    public void PararEscutaPerguntas()
    {
        if (coroutinePerguntas != null)
        {
            StopCoroutine(coroutinePerguntas);
            coroutinePerguntas = null;
        }
    }

    // UMA única requisição por ciclo: lê status + modoManual + pergunta de "controle".
    IEnumerator PollingControle(System.Action<PerguntaTurma> onNovaPergunta,
                                System.Action onSalaEncerrada,
                                System.Action onPartidaFinalizada)
    {
        int ultimoIndex = -1;

        while (true)
        {
            if (!string.IsNullOrEmpty(codigoSalaAtual))
            {
                var r = new RespostaHttp();
                yield return Requisitar("GET", "/salas/" + codigoSalaAtual + "/controle.json", null, r, 1);

                if (r.sucesso)
                {
                    string json = r.corpo.Trim();

                    // Sala apagada pelo professor
                    if (json == "null" || string.IsNullOrEmpty(json))
                    {
                        onSalaEncerrada?.Invoke();
                        yield break;
                    }

                    string status = ExtrairValorString(json, "status");
                    if (status == "encerrada")
                    {
                        onSalaEncerrada?.Invoke();
                        yield break;
                    }

                    // Checkbox do professor
                    if (TemChave(json, "modoManual") && GameManager.instance != null)
                        GameManager.instance.DefinirModoManualTurma(ExtrairValorBool(json, "modoManual"));

                    // Pergunta atual (só dispara se for uma pergunta nova)
                    if (TemChave(json, "enunciado") && TemChave(json, "index"))
                    {
                        int index = ExtrairValorInt(json, "index");
                        if (index != ultimoIndex)
                        {
                            ultimoIndex = index;

                            float duracao = ExtrairValorInt(json, "duracao");
                            var p = new PerguntaTurma
                            {
                                enunciado = ExtrairValorString(json, "enunciado").Replace("\\\"", "\""),
                                resposta = ExtrairValorInt(json, "resposta"),
                                fase = ExtrairValorInt(json, "fase"),
                                index = index,
                                boss = ExtrairValorBool(json, "boss"),
                                duracao = duracao > 0f ? duracao : 30f,
                                inicioMs = ExtrairValorLong(json, "inicio")
                            };
                            onNovaPergunta?.Invoke(p);
                        }
                    }

                    if (status == "finalizada")
                    {
                        onPartidaFinalizada?.Invoke();
                        yield break;
                    }
                }
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }
}