using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager instance;

    [Header("Painel Raiz")]
    public GameObject panelModoTutorial;

    [Header("Escolha do Tutorial (Modo Solo)")]
    public GameObject painelEscolhaTutorialSolo;
    public Button btnFazerTutorialSolo;
    public Button btnPularTutorialSolo;

    [Header("Escolha do Tutorial (Modo Turma)")]
    public GameObject painelEscolhaTutorialTurma;
    public Button btnFazerTutorialTurma;
    public Button btnPularTutorialTurma;

    private bool tutorialTurmaAtivo = false;

    // ── Overlay de bloqueio ───────────────────────────────────────────
    [Header("Overlay")]
    public GameObject overlayBloqueio;   // painel semitransparente que bloqueia input

    // ── Botão pular ───────────────────────────────────────────────────
    [Header("Botão Pular")]
    public Button btnPularTutorial;

    // ── Referências ───────────────────────────────────────────────────
    [Header("Referências")]
    public RectTransform refNave;         // posição da nave no canvas
    public RectTransform refPergunta;     // posição do texto da pergunta
    public RectTransform refTimer;        // posição do timer
    public RectTransform refVidas;        // posição das vidas
    public RectTransform refPoderes;      // posição do painel de poderes

    [Header("Ajuste fino do destaque (pixels — X: direita+, Y: baixo+)")]
    public Vector2 offsetPergunta = Vector2.zero;
    public Vector2 offsetTimer = Vector2.zero;
    public Vector2 offsetVidas = Vector2.zero;
    public Vector2 offsetPoderes = Vector2.zero;

    // ── Estado ────────────────────────────────────────────────────────
    public bool modoGuiado = false;
    private bool tutorialAtivo = false;
    private int passoAtual = 0;
    private bool aguardandoAcao = false;
    private bool acaoRealizada = false;

    // ─────────────────────────────────────────────────────────────────
    void Awake() { instance = this; }

    void Start()
    {
        if (overlayBloqueio != null) overlayBloqueio.SetActive(false);
        if (painelEscolhaTutorialSolo != null) painelEscolhaTutorialSolo.SetActive(false);

        if (btnPularTutorial != null)
            btnPularTutorial.onClick.AddListener(PularTutorial);

        if (btnFazerTutorialSolo != null)
            btnFazerTutorialSolo.onClick.AddListener(() =>
            {
                painelEscolhaTutorialSolo.SetActive(false);
                AbrirTutorial();
            });

        if (btnPularTutorialSolo != null)
            btnPularTutorialSolo.onClick.AddListener(() =>
            {
                painelEscolhaTutorialSolo.SetActive(false);
                GameManager.instance.IniciarJogo();
            });

        if (painelEscolhaTutorialTurma != null) painelEscolhaTutorialTurma.SetActive(false);

        if (btnFazerTutorialTurma != null)
            btnFazerTutorialTurma.onClick.AddListener(() =>
            {
                painelEscolhaTutorialTurma.SetActive(false);
                AbrirTutorialTurma();
            });

        if (btnPularTutorialTurma != null)
            btnPularTutorialTurma.onClick.AddListener(() =>
            {
                painelEscolhaTutorialTurma.SetActive(false);
                if (SalaManager.instance != null)
                {
                    SalaManager.instance.salaPanel.SetActive(true);
                    SalaManager.instance.ContinuarParaPainelAluno();
                }
            });
    }

    // ─────────────────────────────────────────────────────────────────
    // Entrada pública
    // ─────────────────────────────────────────────────────────────────
    private bool continuarClicado = false;

    public void OnCliqueContinuar()
    {
        continuarClicado = true;
    }

    IEnumerator EsperarToque()
    {
        continuarClicado = false;
        while (!continuarClicado)
        {
            if (Input.anyKeyDown || HouveToqueComecou())
                continuarClicado = true;
            yield return null;
        }
    }

    // Detecta o INÍCIO de um toque (não basta checar touchCount > 0, senão
    // dispararia em todo frame enquanto o dedo estiver na tela).
    bool HouveToqueComecou()
    {
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return false;
    }

    IEnumerator EsperarAcao(AcaoTipo acao)
    {
        acaoRealizada = false;
        aguardandoAcao = (acao == AcaoTipo.Acertar);

        while (!acaoRealizada)
        {
            if (acao == AcaoTipo.Mover)
            {
                bool tecla = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)
                          || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D);
                if (tecla) acaoRealizada = true;
            }
            yield return null;
        }

        aguardandoAcao = false;
    }
    public void AbrirTutorial()
    {
        modoGuiado = true;
        tutorialAtivo = true;
        passoAtual = 0;

        if (panelModoTutorial != null) panelModoTutorial.SetActive(true);

        GameManager.instance.IniciarJogo();
        GameManager.instance.timerPausadoExterno = true;
        Time.timeScale = 0f;
        StartCoroutine(IniciarFluxoTutorial());
    }

    void PularTutorial()
    {
        StopAllCoroutines();
        FecharTudo();
        modoGuiado = false;
        tutorialAtivo = false;
        GameManager.instance.timerPausadoExterno = false;
        Time.timeScale = 1f;

        if (tutorialTurmaAtivo)
        {
            FinalizarTutorialTurma();
        }
    }
    public void PerguntarTutorialSolo()
    {
        if (painelEscolhaTutorialSolo != null)
            painelEscolhaTutorialSolo.SetActive(true);
        else
            AbrirTutorial(); // fallback caso o painel não esteja configurado ainda
    }

    public void PerguntarTutorialTurma()
    {
        if (painelEscolhaTutorialTurma != null)
        {
            if (SalaManager.instance != null) SalaManager.instance.salaPanel.SetActive(false);
            painelEscolhaTutorialTurma.SetActive(true);
        }
        else if (SalaManager.instance != null)
        {
            SalaManager.instance.ContinuarParaPainelAluno();
        }
    }
    public void AbrirTutorialTurma()
    {
        if (SalaManager.instance != null)
            SalaManager.instance.salaPanel.SetActive(false);

        // Roda como uma partida Solo de prática — a sala real só é
        // acessada depois que o tutorial termina.
        GameManager.modoAtual = GameManager.ModoJogo.Solo;

        tutorialTurmaAtivo = true;
        modoGuiado = true;
        tutorialAtivo = true;
        passoAtual = 0;

        if (panelModoTutorial != null) panelModoTutorial.SetActive(true);

        GameManager.instance.IniciarJogo();
        GameManager.instance.timerPausadoExterno = true;
        Time.timeScale = 0f;
        StartCoroutine(IniciarFluxoTutorialTurmaExclusivo());
    }
    void FecharTudo()
    {
        TutorialBalao.instance.Esconder();
        if (overlayBloqueio != null) overlayBloqueio.SetActive(false);
        if (panelModoTutorial != null) panelModoTutorial.SetActive(false);
    }


    // ─────────────────────────────────────────────────────────────────
    // Fluxo do tutorial Solo
    // ─────────────────────────────────────────────────────────────────

    IEnumerator IniciarFluxoTutorial()
    {
        Bloquear(true);
        MostrarInstrucao("Esta é a sua NAVE!\nUse ← → para mover\ne ESPAÇO para atirar.\n\n(toque para continuar)", refNave);
        yield return EsperarToque();

        MostrarInstrucao("Agora MOVA a nave!\nUse as setas ou os botões na tela.", refNave);
        Bloquear(false);
        yield return EsperarAcao(AcaoTipo.Mover);

        Bloquear(true);
        MostrarInstrucao("Veja a OPERAÇÃO no topo!\nVocê precisa resolver e atirar\nno inimigo com a resposta certa.\n\n(toque para continuar)", refPergunta, offsetPergunta);
        yield return EsperarToque();

        MostrarInstrucao("Atire no inimigo com a\nRESPOSTA CORRETA!", null);
        Bloquear(false);
        yield return EsperarAcao(AcaoTipo.Acertar);

        // GameManager.CheckAnswer() chama Invoke(nameof(SpawnWave), 2f) no acerto, e Invoke()
        // é afetado por Time.timeScale — por isso a espera aqui precisa ser MAIOR que 2s,
        // senão o Invoke fica congelado no meio quando a gente zera o timeScale a seguir.
        yield return EsperarSegundos(2.3f);

        Bloquear(true);
        MostrarInstrucao("MUITO BEM! 🎉\nAcertos consecutivos carregam PODERES!\n\n(toque para continuar)", null);
        yield return EsperarToque();

        MostrarInstrucao("Veja o TIMER no canto!\nSe acabar, você perde uma vida.\n\n(toque para continuar)", refTimer, offsetTimer);
        yield return EsperarToque();

        MostrarInstrucao("Você tem 3 VIDAS ❤️❤️❤️\nErrar ou o tempo acabar custa uma vida.\n\n(toque para continuar)", refVidas, offsetVidas);
        yield return EsperarToque();

        MostrarInstrucao("Acerte em sequência para\nCARREGAR PODERES!\n\n(toque para continuar)", refPoderes, offsetPoderes);
        yield return EsperarToque();

        MostrarInstrucao("Você está pronto!\nDeseja continuar para o jogo?\n\n(toque para continuar)", null);
        yield return EsperarToque();

        Bloquear(false);
        FecharTudo();
        tutorialAtivo = false;
        modoGuiado = false;
        GameManager.instance.timerPausadoExterno = false;

        // CORRIGIDO: antes chamava IniciarJogo() direto, mas ao acertar a
        // pergunta de prática o GameManager agenda Invoke(SpawnWave, 2f),
        // que dispara DURANTE o tutorial (enquanto timeScale=1) e deixa 5
        // inimigos "fantasmas" parados em cena. IniciarJogo() não destrói
        // inimigos existentes, então eles se somavam aos 5 da partida real.
        // VoltarAoMenu() cancela invokes pendentes e destrói tudo antes de
        // começar a partida de verdade do zero.
        if (GameManager.instance != null)
            GameManager.instance.VoltarAoMenu();

        GameManager.modoAtual = GameManager.ModoJogo.Solo;
        GameManager.instance.IniciarJogo();
    }

    // ─────────────────────────────────────────────────────────────────
    // Fluxo do tutorial exclusivo da Turma — não repete mover/atirar/
    // timer/vidas (isso já foi visto no tutorial Solo). Só explica o
    // que muda no Turma. Jogo fica travado (Time.timeScale = 0) do
    // início ao fim; o aluno só toca pra avançar os balões.
    // ─────────────────────────────────────────────────────────────────

    IEnumerator IniciarFluxoTutorialTurmaExclusivo()
    {
        Bloquear(true);

        MostrarInstrucao("Bem-vindo ao MODO TURMA! 👨‍🏫\n\nAqui o PROFESSOR controla o\nritmo da partida — início,\npróxima pergunta e próxima fase.\n\n(toque para continuar)", null);
        yield return EsperarToque();

        MostrarInstrucao("A MESMA pergunta aparece pra\nTODOS ao mesmo tempo!\nAtire na resposta certa assim\nque ela surgir na tela.\n\n(toque para continuar)", refPergunta, offsetPergunta);
        yield return EsperarToque();

        MostrarInstrucao("A tela do professor mostra,\nem tempo real, quem acertou,\nquem errou, e o RANKING\nda turma inteira!\n\n(toque para continuar)", null);
        yield return EsperarToque();

        MostrarInstrucao("Acertos em sequência ainda\ncarregam PODERES, igual\nno modo Solo!\n\n(toque para continuar)", refPoderes, offsetPoderes);
        yield return EsperarToque();

        MostrarInstrucao("Mas aqui os poderes se dividem\nem OFENSIVOS e BENÉFICOS.\n\n(toque para continuar)", refPoderes, offsetPoderes);
        yield return EsperarToque();

        MostrarInstrucao("BENÉFICOS (Dica, Escudo,\nTempo Lento) te ajudam,\nassim como no modo Solo.\n\n(toque para continuar)", refPoderes, offsetPoderes);
        yield return EsperarToque();

        MostrarInstrucao("Já os OFENSIVOS (Inversão,\nTempestade, Cronômetro,\nDistorção, Congelar) você usa\nESCOLHENDO um adversário\nda turma para atingir!\n\n(toque para continuar)", refPoderes, offsetPoderes);
        yield return EsperarToque();

        MostrarInstrucao("Treino concluído! 🚀\n\nAgora digite o CÓDIGO da sala\nque o professor passou\npara entrar na turma de verdade.\n\n(toque para continuar)", null);
        yield return EsperarToque();

        Bloquear(false);
        FecharTudo();
        tutorialAtivo = false;
        modoGuiado = false;
        GameManager.instance.timerPausadoExterno = false;

        FinalizarTutorialTurma();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers de fluxo
    // ─────────────────────────────────────────────────────────────────
    void FinalizarTutorialTurma()
    {
        tutorialTurmaAtivo = false;
        modoGuiado = false;
        Time.timeScale = 1f;

        if (GameManager.instance != null)
        {
            GameManager.instance.VoltarAoMenu(); // encerra a partida de prática sem enviar resultado

            // CORRIGIDO: VoltarAoMenu() ativa o menuPrincipalPanel (via
            // MostrarSomente). Sem desligar ele aqui, o menu principal
            // ficava ativo ao mesmo tempo que o salaPanel logo abaixo,
            // sobrepondo os dois painéis na tela.
            if (GameManager.instance.menuPrincipalPanel != null)
                GameManager.instance.menuPrincipalPanel.SetActive(false);
        }

        GameManager.modoAtual = GameManager.ModoJogo.Turma;

        if (SalaManager.instance != null)
        {
            SalaManager.instance.salaPanel.SetActive(true);
            SalaManager.instance.ContinuarParaPainelAluno();
        }
    }

    IEnumerator EsperarSegundos(float segundos)
    {
        float t = 0f;
        while (t < segundos)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    enum AcaoTipo { Mover, Acertar, Errar }

    IEnumerator EsperarAcaoOuTempo(AcaoTipo acao, float tempoMax)
    {
        float t = 0f;
        acaoRealizada = false;

        while (t < tempoMax && !acaoRealizada)
        {
            t += Time.unscaledDeltaTime;

            // Detecta movimento pelo teclado ou botões mobile
            if (acao == AcaoTipo.Mover)
            {
                bool tecla = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)
                          || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D);
                if (tecla) acaoRealizada = true;
            }

            yield return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Chamado pelo GameManager quando o jogador acerta
    // ─────────────────────────────────────────────────────────────────

    public void NotificarAcerto()
    {
        if (aguardandoAcao) acaoRealizada = true;
    }

    // ─────────────────────────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────────────────────────

    void MostrarInstrucao(string texto, RectTransform alvo, Vector2 offset = default)
    {
        TutorialBalao.instance.Mostrar(texto, alvo, offset);
    }

    void Bloquear(bool bloquear)
    {
        Time.timeScale = bloquear ? 0f : 1f;
        if (overlayBloqueio != null)
            overlayBloqueio.SetActive(bloquear);
    }

    // ─────────────────────────────────────────────────────────────────
    // Dica rápida (não bloqueia o jogo)
    // ─────────────────────────────────────────────────────────────────

    public void MostrarDicaRapida(string mensagem, float duracao)
    {
        TutorialBalao.instance.MostrarDicaRapida(mensagem, duracao);
    }

    // Mantido para compatibilidade com código antigo
    public void MostrarDica(string mensagem)
    {
        MostrarDicaRapida(mensagem, 5f);
    }

    public void EsconderDica()
    {
        TutorialBalao.instance.Esconder();
        modoGuiado = false;
    }
}