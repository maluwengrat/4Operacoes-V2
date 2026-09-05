using UnityEngine;

public class MobileUI : MonoBehaviour
{
    public static MobileUI instance;

    [Header("Teste no Editor")]
    [Tooltip("Marque para simular o modo mobile ao dar Play dentro do Editor da Unity.")]
    [SerializeField] private bool testarComoMobileNoEditor = false;

    [Header("Tamanho físico dos botões")]
    [Tooltip("Tamanho desejado do botão em centímetros físicos na tela do celular.")]
    [Range(1.0f, 3.0f)]
    [SerializeField] private float tamanhoBotaoCm = 2.4f;

    [Tooltip("Limites de segurança (% da altura da tela) para caso o DPI seja inválido.")]
    [SerializeField] private float tamanhoMinimoPercentual = 0.10f;
    [SerializeField] private float tamanhoMaximoPercentual = 0.32f;

    [Header("Posição dos botões")]
    [Tooltip("Distância dos botões até a lateral da tela, como % da altura da tela.")]
    [Range(0.02f, 0.15f)]
    [SerializeField] private float margemLateralPercentual = 0.09f;

    [Tooltip("Distância dos botões até o fundo da tela, como % da altura da tela.")]
    [Range(0.02f, 0.12f)]
    [SerializeField] private float margemInferiorPercentual = 0.06f;

    [Tooltip("Espaço entre os botões esquerda e direita, como % do tamanho do botão.")]
    [Range(0.10f, 0.80f)]
    [SerializeField] private float espacoEntreEsqDirPercentual = 0.45f;

    [Header("Referência da barra de poderes")]
    [Tooltip("Arraste aqui o RectTransform do painel de poderes (painelPoderes do PoderManager). Usado para centralizar o botão de atirar entre a barra e a borda direita da tela.")]
    [SerializeField] private RectTransform refBarraPoderes;

    // ── Estado interno ────────────────────────────────────────────────
    private bool isMobile;
    private PlayerController player;

    private float devicePixelRatio = 1f;

    private bool pressEsq = false;
    private bool pressDir = false;
    private bool pressAtira = false;
    public bool PressionandoEsquerda => pressEsq;   
    public bool PressionandoDireita => pressDir;

    private int touchIdEsq = -1;
    private int touchIdDir = -1;
    private int touchIdAtira = -1;

    void Awake()
    {
        instance = this;
        isMobile = false;

#if UNITY_EDITOR
        if (testarComoMobileNoEditor) isMobile = true;
#endif
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
    }

    // Chamado pelo JavaScript via SendMessage
    public void SetMobileFromJS(string valor)
    {
        if (valor == "1") isMobile = true;
    }

    // Chamado pelo JavaScript via SendMessage com window.devicePixelRatio
    public void SetDevicePixelRatioFromJS(string valor)
    {
        if (float.TryParse(valor,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float dpr) && dpr > 0f)
        {
            devicePixelRatio = dpr;
        }
    }

    void Update()
    {
        if (!isMobile) return;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (GameManager.instance != null && !GameManager.instance.JogoRodando()) return;
        ProcessarTouch();
    }

    void ProcessarTouch()
    {
        bool novoEsq = false;
        bool novoDir = false;
        bool novoAtira = false;

        CalcularRects(out Rect rEsq, out Rect rDir, out Rect rAtira);

        for (int ti = 0; ti < Input.touchCount; ti++)
        {
            Touch touch = Input.GetTouch(ti);
            Vector2 pos = new Vector2(touch.position.x, Screen.height - touch.position.y);

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.fingerId == touchIdEsq) touchIdEsq = -1;
                if (touch.fingerId == touchIdDir) touchIdDir = -1;
                if (touch.fingerId == touchIdAtira) touchIdAtira = -1;
                continue;
            }

            if (rEsq.Contains(pos))
            {
                novoEsq = true;
                if (touchIdEsq == -1 && touch.phase == TouchPhase.Began)
                    touchIdEsq = touch.fingerId;
            }
            if (rDir.Contains(pos))
            {
                novoDir = true;
                if (touchIdDir == -1 && touch.phase == TouchPhase.Began)
                    touchIdDir = touch.fingerId;
            }
            if (rAtira.Contains(pos))
            {
                novoAtira = true;
                if (touchIdAtira == -1 && touch.phase == TouchPhase.Began)
                {
                    touchIdAtira = touch.fingerId;
                    player?.PressionarAtira();
                }
            }
        }

        if (pressEsq != novoEsq) { pressEsq = novoEsq; player?.PressionarEsquerda(novoEsq); }
        if (pressDir != novoDir) { pressDir = novoDir; player?.PressionarDireita(novoDir); }
        pressAtira = novoAtira;
    }

    void CalcularRects(out Rect rEsq, out Rect rDir, out Rect rAtira)
    {
        float W = Screen.width;
        float H = Screen.height;

        // Usa a menor dimensão para ser consistente em portrait e landscape
        float base_ = Mathf.Min(W, H);

        // Tamanho do botão = 20% da menor dimensão da tela
        // Ajuste este valor conforme preferir (0.18 = menor, 0.25 = maior)
        float btnSize = base_ * 0.12f;

        float margem = btnSize * 0.3f;
        float margemLateral = W * 0.04f;
        float margemInferior = H * 0.05f;
        float baseY = H - btnSize - margemInferior;

        rEsq = new Rect(margemLateral, baseY, btnSize, btnSize);
        rDir = new Rect(margemLateral + btnSize + margem, baseY, btnSize, btnSize);

        float xAtira;
        if (refBarraPoderes != null)
        {
            // borda direita do painel de poderes, em coordenadas de tela
            // (mesmo espaço do GL.LoadPixelMatrix usado no desenho — origem embaixo-esquerda, Y pra cima)
            Vector3[] cantos = new Vector3[4];
            refBarraPoderes.GetWorldCorners(cantos);
            float bordaDireitaBarra = Mathf.Max(cantos[2].x, cantos[3].x);

            // centraliza o botão no meio do espaço entre a barra e a margem direita da tela
            float centro = (bordaDireitaBarra + W) * 0.5f;
            xAtira = centro - btnSize * 0.5f;
        }
        else
        {
            // fallback: comportamento antigo, caso a referência não seja atribuída no Inspector
            xAtira = W - btnSize - margemLateral;
        }

        rAtira = new Rect(xAtira, baseY, btnSize, btnSize);
    }
    // ── Desenho ───────────────────────────────────────────────────────

    void OnGUI()
    {
        bool mostrar = isMobile
                    && GameManager.instance != null
                    && GameManager.instance.JogoRodando();
        if (!mostrar) return;

        CalcularRects(out Rect rEsq, out Rect rDir, out Rect rAtira);

        DesenharBotaoSeta(rEsq, pressEsq, esquerda: true);
        DesenharBotaoSeta(rDir, pressDir, esquerda: false);
        DesenharBotaoAtira(rAtira, pressAtira);
    }

    void DesenharBotaoSeta(Rect r, bool pressionado, bool esquerda)
    {
        Color corFundo = pressionado
            ? new Color(1f, 0.6f, 0.1f, 0.55f)
            : new Color(0.15f, 0.15f, 0.15f, 0.28f);
        Color corBorda = pressionado
            ? new Color(1f, 0.85f, 0.3f, 0.75f)
            : new Color(1f, 0.6f, 0.1f, 0.5f);

        DesenharCirculo(r, corFundo, corBorda, espessuraBorda: 3f);

        float cx = r.x + r.width * 0.5f;
        float cy = r.y + r.height * 0.5f;
        float size = r.width * 0.32f;

        Color corSeta = pressionado
            ? new Color(1f, 1f, 1f, 0.9f)
            : new Color(1f, 0.75f, 0.2f, 0.7f);

        Vector2 ponta = new Vector2(cx + (esquerda ? -size : size), cy);
        Vector2 topoT = new Vector2(cx + (esquerda ? size * 0.5f : -size * 0.5f), cy - size * 0.85f);
        Vector2 baseT = new Vector2(cx + (esquerda ? size * 0.5f : -size * 0.5f), cy + size * 0.85f);
        DesenharTriangulo(ponta, topoT, baseT, corSeta);

        float barraX = esquerda
            ? cx + size * 0.55f - size * 0.12f
            : cx - size * 0.55f + size * 0.12f;
        Rect barra = new Rect(barraX - size * 0.1f, cy - size * 0.8f, size * 0.22f, size * 1.6f);
        DesenharRetanguloSolido(barra, corSeta);
    }

    void DesenharBotaoAtira(Rect r, bool pressionado)
    {
        Color corFundo = pressionado
            ? new Color(1f, 0.2f, 0.1f, 0.55f)
            : new Color(0.15f, 0.05f, 0.05f, 0.28f);
        Color corBorda = pressionado
            ? new Color(1f, 0.6f, 0.2f, 0.75f)
            : new Color(1f, 0.25f, 0.1f, 0.5f);

        DesenharCirculo(r, corFundo, corBorda, espessuraBorda: 3f);

        float cx = r.x + r.width * 0.5f;
        float cy = r.y + r.height * 0.5f;
        float size = r.width * 0.28f;

        Color corIcone = pressionado
            ? new Color(1f, 1f, 1f, 0.9f)
            : new Color(1f, 0.5f, 0.2f, 0.7f);

        Rect corpo = new Rect(cx - size * 0.18f, cy - size * 0.9f, size * 0.36f, size * 1.4f);
        DesenharRetanguloSolido(corpo, corIcone);

        Vector2 ptPonta = new Vector2(cx, cy - size * 0.9f - size * 0.6f);
        Vector2 ptEsq = new Vector2(cx - size * 0.18f, cy - size * 0.9f);
        Vector2 ptDir = new Vector2(cx + size * 0.18f, cy - size * 0.9f);
        DesenharTriangulo(ptPonta, ptEsq, ptDir, corIcone);

        Vector2 asaEsqTopo = new Vector2(cx - size * 0.18f, cy + size * 0.1f);
        Vector2 asaEsqBase = new Vector2(cx - size * 0.18f, cy + size * 0.5f);
        Vector2 asaEsqPont = new Vector2(cx - size * 0.6f, cy + size * 0.5f);
        DesenharTriangulo(asaEsqTopo, asaEsqBase, asaEsqPont, corIcone);

        Vector2 asaDirTopo = new Vector2(cx + size * 0.18f, cy + size * 0.1f);
        Vector2 asaDirBase = new Vector2(cx + size * 0.18f, cy + size * 0.5f);
        Vector2 asaDirPont = new Vector2(cx + size * 0.6f, cy + size * 0.5f);
        DesenharTriangulo(asaDirTopo, asaDirBase, asaDirPont, corIcone);

        Color corChama = pressionado
            ? new Color(1f, 0.9f, 0.1f, 0.9f)
            : new Color(1f, 0.55f, 0.05f, 0.7f);

        Vector2 chamaBase1 = new Vector2(cx - size * 0.18f, cy + size * 0.5f);
        Vector2 chamaBase2 = new Vector2(cx + size * 0.18f, cy + size * 0.5f);
        Vector2 chamaPonta = new Vector2(cx, cy + size * 1.05f);
        DesenharTriangulo(chamaBase1, chamaBase2, chamaPonta, corChama);
    }

    // ── Primitivas GL ────────────────────────────────────────────────

    void DesenharCirculo(Rect r, Color corFundo, Color corBorda, float espessuraBorda)
    {
        int res = 64;
        float cx = r.x + r.width * 0.5f;
        float cy = r.y + r.height * 0.5f;
        float rad = r.width * 0.5f;
        DesenharDiscGL(cx, cy, rad, corBorda, res);
        DesenharDiscGL(cx, cy, rad - espessuraBorda, corFundo, res);
    }

    void DesenharDiscGL(float cx, float cy, float raio, Color cor, int segmentos)
    {
        if (Event.current.type != EventType.Repaint) return;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
        ObterMaterialGL().SetPass(0);
        GL.Begin(GL.TRIANGLES);
        GL.Color(cor);
        for (int i = 0; i < segmentos; i++)
        {
            float a1 = Mathf.PI * 2f * i / segmentos;
            float a2 = Mathf.PI * 2f * (i + 1) / segmentos;
            GL.Vertex3(cx, cy, 0);
            GL.Vertex3(cx + Mathf.Cos(a1) * raio, cy + Mathf.Sin(a1) * raio, 0);
            GL.Vertex3(cx + Mathf.Cos(a2) * raio, cy + Mathf.Sin(a2) * raio, 0);
        }
        GL.End();
        GL.PopMatrix();
    }

    void DesenharTriangulo(Vector2 p1, Vector2 p2, Vector2 p3, Color cor)
    {
        if (Event.current.type != EventType.Repaint) return;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
        ObterMaterialGL().SetPass(0);
        GL.Begin(GL.TRIANGLES);
        GL.Color(cor);
        GL.Vertex3(p1.x, p1.y, 0);
        GL.Vertex3(p2.x, p2.y, 0);
        GL.Vertex3(p3.x, p3.y, 0);
        GL.End();
        GL.PopMatrix();
    }

    void DesenharRetanguloSolido(Rect r, Color cor)
    {
        if (Event.current.type != EventType.Repaint) return;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
        ObterMaterialGL().SetPass(0);
        GL.Begin(GL.QUADS);
        GL.Color(cor);
        GL.Vertex3(r.xMin, r.yMin, 0);
        GL.Vertex3(r.xMax, r.yMin, 0);
        GL.Vertex3(r.xMax, r.yMax, 0);
        GL.Vertex3(r.xMin, r.yMax, 0);
        GL.End();
        GL.PopMatrix();
    }

    private Material _matGL;
    Material ObterMaterialGL()
    {
        if (_matGL == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _matGL = new Material(shader);
            _matGL.hideFlags = HideFlags.HideAndDontSave;
            _matGL.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _matGL.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _matGL.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _matGL.SetInt("_ZWrite", 0);
        }
        return _matGL;
    }

    void OnDestroy()
    {
        if (_matGL != null) Destroy(_matGL);
    }
}