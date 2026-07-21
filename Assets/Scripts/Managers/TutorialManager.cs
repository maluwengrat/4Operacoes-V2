using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager instance;

    [Header("Painel")]
    public GameObject tutorialPanel;

    [Header("Slide")]
    public TextMeshProUGUI tituloSlide;
    public TextMeshProUGUI textoSlide;
    public Image imagemSlide;
    public TextMeshProUGUI textoPagina;

    [Header("Botões")]
    public Button btnProximo;
    public Button btnAnterior;
    public Button btnPular;

    [Header("Sprites dos slides")]
    public Sprite[] spritesSlides; // arraste as imagens no Inspector

    // ── Conteúdo dos slides ───────────────────────────────────────────
    private string[] titulos = {
        "BEM-VINDO AO MATH SHOOTER!",
        "COMO JOGAR",
        "RESPONDA RÁPIDO!",
        "CUIDADO COM O TEMPO!",
        "PRONTO PARA JOGAR?"
    };

    private string[] textos = {
        "Neste jogo você vai resolver operações matemáticas para destruir os inimigos!\n\nUse seu conhecimento para vencer!",
        "Inimigos com números aparecem na tela.\nO jogador deve utilizar as setas <  > para se mover e utilizar a tecla space para atirar (PC) ou o botão atirar (mobile).", 
        "Atire no inimigo com a RESPOSTA CORRETA para eliminá-lo!",
        "Cada operação tem um tempo limite.\nQuanto mais rápido você responder,\nmais pontos você ganha!\n\nAcertos consecutivos dão bônus de pontos.",
        "Se o tempo acabar ou você atirar no número ERRADO,\nvocê deve começar a fase novamente,\nse suas 3 vidas acabarem voce perde o jogo!",
        "Existem 4 fases:\nFase 1: Adição\nFase 2: Subtração\nFase 3: Divisão\nFase 4: Multiplicação\n\nBoa sorte!"
    };

    private int slideAtual = 0;
    private bool tutorialEmAndamento = false;

    void Awake() { instance = this; }

    void Start()
    {
        tutorialPanel.SetActive(false);

        btnProximo.onClick.AddListener(ProximoSlide);
        btnAnterior.onClick.AddListener(SlideAnterior);
        btnPular.onClick.AddListener(PularTutorial);
    }

    // ─────────────────────────────────────────────────────────────────
    // Abrir / Fechar
    // ─────────────────────────────────────────────────────────────────

    public void AbrirTutorial()
    {
        slideAtual = 0;
        tutorialPanel.SetActive(true);
        tutorialEmAndamento = true;
        Time.timeScale = 0f;
        MostrarSlide(slideAtual);
    }

    void FecharTutorial()
    {
        tutorialPanel.SetActive(false);
        tutorialEmAndamento = false;
        Time.timeScale = 1f;
    }

    // ─────────────────────────────────────────────────────────────────
    // Navegação
    // ─────────────────────────────────────────────────────────────────

    void ProximoSlide()
    {
        if (slideAtual >= titulos.Length - 1)
        {
            // Último slide — inicia o jogo guiado
            FecharTutorial();
            IniciarJogoGuiado();
            return;
        }
        slideAtual++;
        MostrarSlide(slideAtual);
    }

    void SlideAnterior()
    {
        if (slideAtual <= 0) return;
        slideAtual--;
        MostrarSlide(slideAtual);
    }

    void PularTutorial()
    {
        FecharTutorial();
        GameManager.instance.IniciarJogo();
    }

    // ─────────────────────────────────────────────────────────────────
    // Exibir slide
    // ─────────────────────────────────────────────────────────────────

    void MostrarSlide(int index)
    {
        tituloSlide.text = titulos[index];
        textoSlide.text = textos[index];
        textoPagina.text = (index + 1) + " / " + titulos.Length;

        // Imagem do slide (se tiver sprite configurado)
        if (imagemSlide != null && spritesSlides != null && index < spritesSlides.Length)
        {
            imagemSlide.sprite = spritesSlides[index];
            imagemSlide.gameObject.SetActive(spritesSlides[index] != null);
        }

        // Botão anterior só aparece a partir do slide 2
        btnAnterior.gameObject.SetActive(index > 0);

        // Último slide muda o texto do botão para "JOGAR!"
        TextMeshProUGUI txtBtnProximo = btnProximo.GetComponentInChildren<TextMeshProUGUI>();
        if (txtBtnProximo != null)
            txtBtnProximo.text = (index == titulos.Length - 1) ? "JOGAR! ►" : "PRÓXIMO ►";
    }

    // ─────────────────────────────────────────────────────────────────
    // Modo guiado — primeira onda com dicas
    // ─────────────────────────────────────────────────────────────────

    public bool modoGuiado = false;
    public GameObject painelDica;
    public TextMeshProUGUI textoDica;

    void IniciarJogoGuiado()
    {
        modoGuiado = true;
        GameManager.instance.IniciarJogo();

        // Mostra primeira dica após 1 segundo
        Invoke(nameof(MostrarDicaInicial), 1f);
    }

    void MostrarDicaInicial()
    {
        MostrarDica("Veja a operacao no topo!\nAtire no inimigo com a resposta correta!");
        Invoke(nameof(EsconderDica), 4f);
    }

    public void MostrarDica(string mensagem)
    {
        if (painelDica == null || textoDica == null) return;
        textoDica.text = mensagem;
        painelDica.SetActive(true);
    }

    public void EsconderDica()
    {
        if (painelDica != null)
            painelDica.SetActive(false);

        // Após primeira onda, desativa modo guiado
        modoGuiado = false;
    }
}