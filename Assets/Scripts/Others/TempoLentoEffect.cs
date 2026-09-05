using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TempoLentoEffect — cria uma vinheta roxa cobrindo a tela enquanto o
/// power-up de Tempo Lento estiver ativo, com leve pulsação e fade suave
/// de entrada/saída.
///
/// SETUP NO INSPECTOR:
///   Crie um GameObject vazio na cena (ex: "TempoLentoEffect") e arraste
///   este script nele. Não precisa configurar mais nada — o overlay é
///   criado automaticamente em tempo de execução, igual ao FeedbackManager.
/// </summary>
public class TempoLentoEffect : MonoBehaviour
{
    public static TempoLentoEffect instance;

    [Header("Visual")]
    [Tooltip("Cor base da vinheta (combina com a cor do power-up).")]
    public Color corFiltro = new Color(0.55f, 0.15f, 0.85f);

    [Tooltip("Opacidade máxima da vinheta enquanto ativa.")]
    [Range(0f, 1f)] public float alphaMaximo = 0.30f;

    [Tooltip("Velocidade da pulsação (maior = pisca mais rápido).")]
    public float velocidadePulso = 2.2f;

    [Tooltip("Quanto a opacidade varia na pulsação.")]
    [Range(0f, 0.3f)] public float amplitudePulso = 0.07f;

    [Tooltip("Duração do fade de saída, em segundos.")]
    public float fadeOutDuracao = 0.6f;

    // ── Estado interno ─────────────────────────────────────────────
    Image _overlay;
    float _timer;
    bool _ativo;

    void Awake()
    {
        instance = this;
        CriarOverlay();
    }

    // ══════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ══════════════════════════════════════════════════════════════

    /// <summary>Ativa (ou renova) a vinheta pela duração informada.</summary>
    public void Ativar(float duracao)
    {
        _timer = duracao;
        _ativo = true;
        _overlay.gameObject.SetActive(true);
    }

    public void Desativar()
    {
        _ativo = false;
        _overlay.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════
    void Update()
    {
        if (!_ativo) return;

        // unscaledDeltaTime: continua contando certo mesmo com o
        // Time.timeScale reduzido pelo próprio tempo lento
        _timer -= Time.unscaledDeltaTime;

        if (_timer <= 0f)
        {
            Desativar();
            return;
        }

        float fade = _timer < fadeOutDuracao ? _timer / fadeOutDuracao : 1f;
        float pulso = Mathf.Sin(Time.unscaledTime * velocidadePulso) * amplitudePulso;
        float alphaFinal = Mathf.Clamp01(alphaMaximo + pulso) * fade;

        Color c = corFiltro;
        c.a = alphaFinal;
        _overlay.color = c;
    }

    // ══════════════════════════════════════════════════════════════
    // INTERNOS
    // ══════════════════════════════════════════════════════════════

    void CriarOverlay()
    {
        var canvasGO = new GameObject("TempoLentoCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // na frente do jogo, atrás do FeedbackManager (150)
        canvasGO.AddComponent<CanvasScaler>();

        var overlayGO = new GameObject("VinhetaTempoLento");
        overlayGO.transform.SetParent(canvasGO.transform, false);
        var rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _overlay = overlayGO.AddComponent<Image>();
        _overlay.sprite = CriarSpriteVinheta();
        _overlay.raycastTarget = false;

        Color cInicial = corFiltro;
        cInicial.a = 0f;
        _overlay.color = cInicial;

        overlayGO.SetActive(false);
    }

    // Gera uma textura transparente no centro e opaca nas bordas (vinheta)
    Sprite CriarSpriteVinheta()
    {
        int tamanho = 256;
        Texture2D tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        float centro = tamanho / 2f;
        float maxDist = centro * 1.42f; // aprox. diagonal do quadrado

        for (int x = 0; x < tamanho; x++)
        {
            for (int y = 0; y < tamanho; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro));
                float t = Mathf.Clamp01(dist / maxDist);
                float alpha = Mathf.SmoothStep(0f, 1f, t);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tamanho, tamanho), new Vector2(0.5f, 0.5f));
    }
}
