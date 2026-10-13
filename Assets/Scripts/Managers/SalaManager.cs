using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SalaManager : MonoBehaviour
{
    public static SalaManager instance;

    // ── Painel principal ──────────────────────────────────────────────
    [Header("Painel Principal")]
    public GameObject salaPanel;
    public Button btnAbrirSala;
    public Button btnVoltarSala;
    public TextMeshProUGUI textoStatusSala;

    // ── Painel de escolha ─────────────────────────────────────────────
    [Header("Painel Escolha")]
    public GameObject painelEscolha;
    public Button btnSouAluno;
    public Button btnSouProfessor;

    // ── Painel senha professor ────────────────────────────────────────
    [Header("Painel Senha Professor")]
    public GameObject painelSenhaProfessor;
    public TMP_InputField inputSenha;
    public Button btnConfirmarSenha;
    public Button btnCancelarSenha;

    // ── Painel professor ──────────────────────────────────────────────
    [Header("Painel Professor")]
    public GameObject painelProfessor;
    public Button btnCriarSala;
    public Button btnIniciarJogo;
    public TextMeshProUGUI textoCodigoGerado;

    // ── Painel aluno ──────────────────────────────────────────────────
    [Header("Painel Aluno")]
    public GameObject painelAluno;
    public TMP_InputField inputCodigoSala;
    public TMP_InputField inputNomeAluno; // obrigatório: digitado ou pré-preenchido da URL
    public Button btnEntrarSala;

    [Header("Painel Espera")]
    public GameObject painelEspera;

    // ── Senha do professor ────────────────────────────────────────────
    // ATENÇÃO: fica dentro do build do jogo, então só serve como barreira simples.
    private const string SENHA_PROFESSOR = "prof123";

    // Mesmas letras e números que o FirebaseManager usa para gerar o código da sala
    private const string CARACTERES_CODIGO = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TAMANHO_CODIGO = 6;
    private const int TAMANHO_MAX_NOME = 20;

    private bool souProfessor = false;

    // ── Histórico de navegação ────────────────────────────────────────
    private Stack<GameObject> historicoPaineis = new Stack<GameObject>();

    void Awake() { instance = this; }

    void Start()
    {
        salaPanel.SetActive(false);
        painelEscolha.SetActive(false);
        painelSenhaProfessor.SetActive(false);
        painelProfessor.SetActive(false);
        painelAluno.SetActive(false);

        if (inputNomeAluno != null)
            inputNomeAluno.characterLimit = TAMANHO_MAX_NOME;

        if (btnAbrirSala != null)
            btnAbrirSala.onClick.AddListener(AbrirPainelSala);

        if (btnIniciarJogo != null)
            btnIniciarJogo.onClick.AddListener(IniciarJogoTurma);

        if (btnVoltarSala != null)
            btnVoltarSala.onClick.AddListener(VoltarPainelAnterior);

        if (btnSouAluno != null)
            btnSouAluno.onClick.AddListener(EscolherAluno);

        if (btnSouProfessor != null)
            btnSouProfessor.onClick.AddListener(EscolherProfessor);

        if (btnConfirmarSenha != null)
            btnConfirmarSenha.onClick.AddListener(VerificarSenha);

        if (btnCancelarSenha != null)
            btnCancelarSenha.onClick.AddListener(VoltarPainelAnterior);

        if (btnCriarSala != null)
            btnCriarSala.onClick.AddListener(CriarSalaClicked);

        if (btnEntrarSala != null)
            btnEntrarSala.onClick.AddListener(EntrarSalaClicked);
    }

    // ─────────────────────────────────────────────────────────────────
    // Navegação
    // ─────────────────────────────────────────────────────────────────

    public void AbrirPainelSala()
    {
        historicoPaineis.Clear();
        salaPanel.SetActive(true);
        IrParaPainel(painelEscolha);
    }

    void IrParaPainel(GameObject painelDestino)
    {
        GameObject painelAtivo = ObterPainelAtivo();
        if (painelAtivo != null)
            historicoPaineis.Push(painelAtivo);

        DesativarTodosPaineis();
        painelDestino.SetActive(true);

        if (textoStatusSala != null) textoStatusSala.text = "";
    }

    void VoltarPainelAnterior()
    {
        if (historicoPaineis.Count == 0)
        {
            salaPanel.SetActive(false);
            DesativarTodosPaineis();
            souProfessor = false;
            if (textoCodigoGerado != null) textoCodigoGerado.text = "";
            if (inputCodigoSala != null) inputCodigoSala.text = "";
            if (GameManager.instance != null)
                GameManager.instance.AbrirModoJogo();
            return;
        }

        GameObject painelAnterior = historicoPaineis.Pop();
        DesativarTodosPaineis();
        painelAnterior.SetActive(true);

        if (textoStatusSala != null) textoStatusSala.text = "";
        if (inputSenha != null) inputSenha.text = "";
    }

    GameObject ObterPainelAtivo()
    {
        if (painelEscolha.activeSelf) return painelEscolha;
        if (painelAluno.activeSelf) return painelAluno;
        if (painelSenhaProfessor.activeSelf) return painelSenhaProfessor;
        if (painelProfessor.activeSelf) return painelProfessor;
        return null;
    }

    void DesativarTodosPaineis()
    {
        painelEscolha.SetActive(false);
        painelAluno.SetActive(false);
        painelSenhaProfessor.SetActive(false);
        painelProfessor.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // Escolha de perfil
    // ─────────────────────────────────────────────────────────────────

    // Chamado pelo TutorialManager quando o aluno pula ou termina o tutorial do Turma
    public void ContinuarParaPainelAluno()
    {
        if (inputNomeAluno != null && FirebaseManager.instance != null)
        {
            string nomeAtual = FirebaseManager.instance.nomeJogador;
            inputNomeAluno.text = (nomeAtual == "Jogador") ? "" : nomeAtual;
        }
        IrParaPainel(painelAluno);
    }

    void EscolherAluno()
    {
        if (TutorialManager.instance != null)
            TutorialManager.instance.PerguntarTutorialTurma();
        else
            ContinuarParaPainelAluno();
    }

    void EscolherProfessor()
    {
        if (inputSenha != null)
        {
            inputSenha.text = "";
            inputSenha.contentType = TMP_InputField.ContentType.Password;
        }
        IrParaPainel(painelSenhaProfessor);
    }

    void VerificarSenha()
    {
        if (inputSenha == null) return;

        if (inputSenha.text == SENHA_PROFESSOR)
        {
            souProfessor = true;
            IrParaPainel(painelProfessor);
            if (textoStatusSala != null)
                textoStatusSala.text = "Acesso liberado! Crie uma sala para a turma.";
        }
        else
        {
            if (textoStatusSala != null)
                textoStatusSala.text = "Senha incorreta. Tente novamente.";
            inputSenha.text = "";
        }
    }

    void CriarSalaClicked()
    {
        if (textoStatusSala != null) textoStatusSala.text = "Criando sala...";
        if (textoCodigoGerado != null) textoCodigoGerado.text = "";

        FirebaseManager.instance.CriarSala((codigo) =>
        {
            if (codigo != null)
            {
                if (textoCodigoGerado != null)
                    textoCodigoGerado.text = "Código da sala:\n" + codigo;
                if (textoStatusSala != null)
                    textoStatusSala.text = "Compartilhe o código com a turma!";

                if (painelProfessor != null)
                    painelProfessor.SetActive(false);

                if (MonitorSalaManager.instance != null)
                    MonitorSalaManager.instance.AbrirMonitor(codigo);
            }
            else
            {
                if (textoStatusSala != null)
                    textoStatusSala.text = "Erro ao criar sala. Tente novamente.";
            }
        });
    }

    void IniciarJogoTurma()
    {
        if (string.IsNullOrEmpty(FirebaseManager.instance.codigoSalaAtual))
        {
            if (textoStatusSala != null)
                textoStatusSala.text = "Crie uma sala primeiro!";
            return;
        }

        if (textoStatusSala != null)
            textoStatusSala.text = "Iniciando jogo...";

        FirebaseManager.instance.IniciarJogoNaSala(
            FirebaseManager.instance.codigoSalaAtual, (sucesso) =>
            {
                if (sucesso)
                {
                    salaPanel.SetActive(false);
                    DesativarTodosPaineis();
                    GameManager.instance.IniciarJogo();
                }
                else
                {
                    if (textoStatusSala != null)
                        textoStatusSala.text = "Erro ao iniciar. Tente novamente.";
                }
            });
    }

    // ─────────────────────────────────────────────────────────────────
    // Entrar na sala (aluno): nome e código são obrigatórios
    // ─────────────────────────────────────────────────────────────────

    bool CodigoTemFormatoValido(string codigo)
    {
        if (codigo.Length != TAMANHO_CODIGO) return false;
        foreach (char c in codigo)
            if (CARACTERES_CODIGO.IndexOf(c) < 0) return false;
        return true;
    }

    // Tira aspas, barras e caracteres de controle (quebrariam o JSON enviado ao banco)
    string LimparNome(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in nome)
        {
            if (c == '"' || c == '\\' || char.IsControl(c)) continue;
            sb.Append(c);
        }
        string r = sb.ToString().Trim();
        return r.Length > TAMANHO_MAX_NOME ? r.Substring(0, TAMANHO_MAX_NOME).Trim() : r;
    }

    void EntrarSalaClicked()
    {
        // 1) Nome obrigatório
        string nome = inputNomeAluno != null ? LimparNome(inputNomeAluno.text) : "";
        if (string.IsNullOrEmpty(nome))
        {
            if (textoStatusSala != null)
                textoStatusSala.text = "Digite seu nome para entrar.";
            return;
        }

        // 2) Código obrigatório e no formato certo (6 letras/números)
        string codigo = inputCodigoSala != null
            ? inputCodigoSala.text.Replace(" ", "").Trim().ToUpper()
            : "";

        if (string.IsNullOrEmpty(codigo))
        {
            if (textoStatusSala != null)
                textoStatusSala.text = "Digite o código da sala.";
            return;
        }

        if (!CodigoTemFormatoValido(codigo))
        {
            if (textoStatusSala != null)
                textoStatusSala.text = "Código inválido. Ele tem 6 letras ou números.";
            return;
        }

        if (textoStatusSala != null) textoStatusSala.text = "Entrando na sala...";

        FirebaseManager.instance.nomeJogador = nome;

        // 3) O servidor confirma se a sala existe ("Sala não encontrada" se não existir)
        FirebaseManager.instance.EntrarNaSala(codigo, (sucesso, mensagemErro) =>
        {
            if (sucesso)
            {
                souProfessor = false;
                painelAluno.SetActive(false);

                // Recarregou a página no meio da partida: volta direto para o jogo
                if (FirebaseManager.instance.reentradaPendente)
                {
                    salaPanel.SetActive(false);
                    DesativarTodosPaineis();
                    GameManager.modoAtual = GameManager.ModoJogo.Turma;
                    GameManager.instance.IniciarJogo();
                    return;
                }

                if (SalaEsperaManager.instance != null)
                    SalaEsperaManager.instance.AbrirEspera(codigo);
                else
                    Debug.LogError("SalaEsperaManager não encontrado!");
            }
            else
            {
                if (textoStatusSala != null)
                    textoStatusSala.text = mensagemErro;
            }
        });
    }
}