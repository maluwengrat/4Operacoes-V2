using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FeedbackManager — versão Canvas UI (sem OnGUI, compatível com WebGL).
///
/// SETUP NO INSPECTOR:
///   1. Crie um Canvas filho do seu Canvas principal (ou um Canvas próprio
///      com sortingOrder alto, ex: 100).
///   2. Dentro dele crie um Panel com:
///        - Image (cor escura semi-transparente, bordas arredondadas via sprite)
///        - Barra lateral colorida (Image estreita à esquerda)
///        - TextMeshProUGUI centralizado
///   3. Arraste cada referência nos campos abaixo.
///   4. Desative o Panel no Inspector — o script liga/desliga via código.
///
/// ALTERNATIVA RÁPIDA (sem montar na mão):
///   Deixe todos os campos nulos. O script cria o Canvas e os elementos
///   automaticamente em tempo de execução.
/// </summary>
public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager instance;

    [Header("Referências (opcional — deixe nulo para criar automaticamente)")]
    [Tooltip("O painel raiz do feedback (será ativado/desativado).")]
    public GameObject painelRaiz;

    [Tooltip("A barra colorida lateral (Image à esquerda do painel).")]
    public Image barraLateral;

    [Tooltip("O texto principal da mensagem.")]
    public TextMeshProUGUI textoMensagem;

    // ── Configurações visuais ──────────────────────────────────────
    [Header("Visual")]
    [Tooltip("Largura do painel em pixels UI.")]
    public float largura = 480f;
    [Tooltip("Altura do painel em pixels UI.")]
    public float altura = 80f;
    [Tooltip("Tamanho da fonte.")]
    public int fontSize = 30;
    [Tooltip("Posição vertical: 0 = baixo, 1 = cima.")]
    [Range(0f, 1f)] public float posicaoY = 0.72f;

    // ── Estado interno ─────────────────────────────────────────────
    float _timer;
    bool _ativo;
    Color _cor;

    CanvasGroup _cg;   // para fade com alpha

    // ── Mensagens aleatórias ───────────────────────────────────────
    readonly string[] _acertos = {
        "Excelente!", "Muito bem!", "Correto!",
        "Perfeito!", "Boa resposta!", "Continue assim!", "Arrasou!"
    };
    readonly string[] _erros = {
        "Quase lá! Tente novamente.", "Não desista, você consegue!",
        "Revise o cálculo com calma.", "Concentre-se e tente de novo!",
        "Todo erro é aprendizado!", "Você está chegando lá!", "Mais atenção!"
    };

    // ══════════════════════════════════════════════════════════════
    void Awake()
    {
        instance = this;
        if (painelRaiz == null) CriarUIAutomaticamente();
        else ObterReferencias();

        painelRaiz.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ══════════════════════════════════════════════════════════════

    public void Esconder()
    {
        _ativo = false;
        _timer = 0f;
        if (painelRaiz) painelRaiz.SetActive(false);
    }

    public void MostrarMensagem(string msg, Color cor)
    {
        if (!painelRaiz) return;
        _cor = cor;
        _timer = 2f;
        _ativo = true;
        Exibir(msg, cor);
    }

    public void MostrarErro(string pergunta, int respostaCorreta)
    {
        if (GameManager.instance != null && GameManager.instance.IsPanelAtivo()) return;
        string msg = _erros[Random.Range(0, _erros.Length)];
        MostrarMensagem(msg, new Color(1f, 0.27f, 0.27f));
        _timer = 2.5f;
    }

    public void MostrarAcerto()
    {
        if (GameManager.instance != null && GameManager.instance.IsPanelAtivo()) return;
        string msg = _acertos[Random.Range(0, _acertos.Length)];
        MostrarMensagem(msg, new Color(0.20f, 0.90f, 0.60f));
        _timer = 1.2f;
    }

    // ══════════════════════════════════════════════════════════════
    // UPDATE — fade de saída
    // ══════════════════════════════════════════════════════════════
    void Update()
    {
        if (!_ativo) return;

        // Esconde se um painel de UI estiver aberto
        if (GameManager.instance != null && GameManager.instance.IsPanelAtivo())
        {
            Esconder(); return;
        }

        _timer -= Time.unscaledDeltaTime;

        // Fade out nos últimos 0.4s
        if (_cg != null)
            _cg.alpha = _timer < 0.4f ? Mathf.Clamp01(_timer / 0.4f) : 1f;

        if (_timer <= 0f) Esconder();
    }

    // ══════════════════════════════════════════════════════════════
    // INTERNOS
    // ══════════════════════════════════════════════════════════════

    void Exibir(string msg, Color cor)
    {
        painelRaiz.SetActive(true);
        if (_cg != null) _cg.alpha = 1f;
        if (textoMensagem) textoMensagem.text = msg;
        if (barraLateral) barraLateral.color = cor;
    }

    // Pega referências de um painel já montado no Inspector
    void ObterReferencias()
    {
        _cg = painelRaiz.GetComponent<CanvasGroup>();
        if (_cg == null) _cg = painelRaiz.AddComponent<CanvasGroup>();

        if (textoMensagem == null)
            textoMensagem = painelRaiz.GetComponentInChildren<TextMeshProUGUI>();
        if (barraLateral == null)
        {
            var imgs = painelRaiz.GetComponentsInChildren<Image>();
            if (imgs.Length > 1) barraLateral = imgs[1]; // primeira = fundo, segunda = barra
        }
    }

    // Cria o Canvas e o painel em tempo de execução (modo automático)
    void CriarUIAutomaticamente()
    {
        // ── Canvas ────────────────────────────────────────────────
        var canvasGO = new GameObject("FeedbackCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;   // na frente de tudo
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
            UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // ── Painel raiz ───────────────────────────────────────────
        painelRaiz = new GameObject("FeedbackPanel");
        painelRaiz.transform.SetParent(canvasGO.transform, false);

        _cg = painelRaiz.AddComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;

        var rtPainel = painelRaiz.AddComponent<RectTransform>();
        rtPainel.anchorMin = new Vector2(0.5f, posicaoY);
        rtPainel.anchorMax = new Vector2(0.5f, posicaoY);
        rtPainel.pivot = new Vector2(0.5f, 0.5f);
        rtPainel.sizeDelta = new Vector2(largura, altura);
        rtPainel.anchoredPosition = Vector2.zero;

        // ── Fundo escuro ──────────────────────────────────────────
        var fundo = new GameObject("Fundo");
        fundo.transform.SetParent(painelRaiz.transform, false);
        var rtFundo = fundo.AddComponent<RectTransform>();
        rtFundo.anchorMin = Vector2.zero; rtFundo.anchorMax = Vector2.one;
        rtFundo.offsetMin = rtFundo.offsetMax = Vector2.zero;
        var imgFundo = fundo.AddComponent<Image>();
        imgFundo.color = new Color(0.059f, 0.090f, 0.165f, 0.93f);
        imgFundo.raycastTarget = false;

        // ── Barra lateral colorida (6px à esquerda) ───────────────
        var barra = new GameObject("Barra");
        barra.transform.SetParent(painelRaiz.transform, false);
        var rtBarra = barra.AddComponent<RectTransform>();
        rtBarra.anchorMin = Vector2.zero;
        rtBarra.anchorMax = new Vector2(0f, 1f);
        rtBarra.offsetMin = Vector2.zero;
        rtBarra.offsetMax = Vector2.zero;
        rtBarra.sizeDelta = new Vector2(6f, 0f);   // largura fixa, altura esticada
        rtBarra.anchorMin = new Vector2(0f, 0f);
        rtBarra.anchorMax = new Vector2(0f, 1f);
        rtBarra.pivot = new Vector2(0f, 0.5f);
        rtBarra.anchoredPosition = Vector2.zero;
        rtBarra.sizeDelta = new Vector2(6f, 0f);

        barraLateral = barra.AddComponent<Image>();
        barraLateral.color = Color.green;
        barraLateral.raycastTarget = false;

        // ── Texto ─────────────────────────────────────────────────
        var textoGO = new GameObject("Texto");
        textoGO.transform.SetParent(painelRaiz.transform, false);
        var rtTexto = textoGO.AddComponent<RectTransform>();
        rtTexto.anchorMin = Vector2.zero; rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = new Vector2(14f, 0f);  // espaço para a barra lateral
        rtTexto.offsetMax = Vector2.zero;

        textoMensagem = textoGO.AddComponent<TextMeshProUGUI>();
        textoMensagem.text = "";
        textoMensagem.fontSize = fontSize;
        textoMensagem.fontStyle = FontStyles.Bold;
        textoMensagem.alignment = TextAlignmentOptions.MidlineLeft;
        textoMensagem.color = Color.white;
        textoMensagem.raycastTarget = false;
    }
}