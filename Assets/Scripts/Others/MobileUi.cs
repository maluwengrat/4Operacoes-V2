using UnityEngine;

public class MobileUI : MonoBehaviour
{
    public static MobileUI instance;

    [Header("Forçar controles mobile mesmo em desktop (testes)")]
    public bool forcarMobile = false;

    private bool isMobile;
    private PlayerController player;

    private Texture2D texBotao;
    private Texture2D texBotaoPress;
    private Texture2D texBotaoAtira;
    private Texture2D texBotaoAtiraPress;

    private bool pressEsq = false;
    private bool pressDir = false;
    private bool pressAtira = false;

    private int touchIdEsq = -1;
    private int touchIdDir = -1;
    private int touchIdAtira = -1;

    void Awake()
    {
        instance = this;
        // No WebGL, Application.isMobilePlatform sempre retorna false.
        // A detecção real é feita via JavaScript (SetMobileFromJS).
        // Aqui mantemos apenas o forcarMobile para testes no editor.
        isMobile = forcarMobile;
    }

    void Start()
    {
        CriarTexturas();
        player = FindObjectOfType<PlayerController>();
    }

    // Chamado pelo JavaScript via SendMessage quando o navegador detecta celular/tablet
    public void SetMobileFromJS(string valor)
    {
        if (valor == "1")
        {
            isMobile = true;
        }
    }

    void Update()
    {
        if (!isMobile) return;
        if (player == null) player = FindObjectOfType<PlayerController>();
        if (GameManager.instance != null && !GameManager.instance.JogoRodando()) return;

        ProcessarTouch();
    }

    void ProcessarTouch()
    {
        bool novoEsq = false;
        bool novoDir = false;
        bool novoAtira = false;

        Rect rEsq, rDir, rAtira;
        CalcularRects(out rEsq, out rDir, out rAtira);

        for (int ti = 0; ti < Input.touchCount; ti++)
        {
            Touch touch = Input.GetTouch(ti);
            // Converte Y para espaço do OnGUI (topo = 0)
            Vector2 pos = new Vector2(touch.position.x,
                                      Screen.height - touch.position.y);

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

        // Movimento contínuo — só notifica o player quando muda
        if (pressEsq != novoEsq) { pressEsq = novoEsq; player?.PressionarEsquerda(novoEsq); }
        if (pressDir != novoDir) { pressDir = novoDir; player?.PressionarDireita(novoDir); }
        pressAtira = novoAtira;
    }

    // ── Layout ────────────────────────────────────────────────────────
    //  [ ◀ ]   [ ▶ ]                          [ 🔥 ]
    //  canto inferior esquerdo            canto inferior direito

    void CalcularRects(out Rect rEsq, out Rect rDir, out Rect rAtira)
    {
        float W = Screen.width;
        float H = Screen.height;
        float btnSize = Mathf.Clamp(Mathf.Min(W, H) * 0.22f, 70f, 140f);
        float margem = btnSize * 0.3f;
        float baseY = H - btnSize - margem; // distância do topo (OnGUI: Y cresce ↓)

        // Esquerda e direita — canto inferior esquerdo
        rEsq = new Rect(margem, baseY, btnSize, btnSize);
        rDir = new Rect(margem + btnSize + margem, baseY, btnSize, btnSize);

        // Atirar — canto inferior direito
        rAtira = new Rect(W - btnSize - margem, baseY, btnSize, btnSize);
    }

    // ── Desenho ───────────────────────────────────────────────────────

    void OnGUI()
    {
        bool mostrar = isMobile
                       && GameManager.instance != null
                       && GameManager.instance.JogoRodando();
        if (!mostrar) return;

        Rect rEsq, rDir, rAtira;
        CalcularRects(out rEsq, out rDir, out rAtira);

        GUI.DrawTexture(rEsq, pressEsq ? texBotaoPress : texBotao);
        GUI.DrawTexture(rDir, pressDir ? texBotaoPress : texBotao);
        GUI.DrawTexture(rAtira, pressAtira ? texBotaoAtiraPress : texBotaoAtira);

        DesenharSimbolo(rEsq, "◀", 36);
        DesenharSimbolo(rDir, "▶", 36);
        DesenharSimbolo(rAtira, "🔥", 32);
    }

    void DesenharSimbolo(Rect r, string simbolo, int fontSize)
    {
        GUIStyle s = new GUIStyle
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        s.normal.textColor = Color.white;

        GUIStyle sombra = new GUIStyle(s);
        sombra.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), simbolo, sombra);
        GUI.Label(r, simbolo, s);
    }

    // ── Texturas ──────────────────────────────────────────────────────

    void CriarTexturas()
    {
        Color corBotao = new Color(0.976f, 0.451f, 0.086f, 0.55f);
        Color corBotaoPress = new Color(0.976f, 0.451f, 0.086f, 0.85f);
        Color corAtira = new Color(0.15f, 0.60f, 1.00f, 0.60f);
        Color corAtiraPress = new Color(0.15f, 0.60f, 1.00f, 0.90f);

        texBotao = CriarTexRounded(80, corBotao, new Color(1f, 1f, 1f, 0.15f));
        texBotaoPress = CriarTexRounded(80, corBotaoPress, new Color(1f, 1f, 1f, 0.30f));
        texBotaoAtira = CriarTexRounded(80, corAtira, new Color(1f, 1f, 1f, 0.15f));
        texBotaoAtiraPress = CriarTexRounded(80, corAtiraPress, new Color(1f, 1f, 1f, 0.30f));
    }

    Texture2D CriarTexRounded(int size, Color cor, Color bordaCor)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float meio = size * 0.5f;
        float raio = size * 0.42f;
        float borda = size * 0.06f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(meio, meio));
                Color c;
                if (dist < raio - borda) c = cor;
                else if (dist < raio) c = Color.Lerp(cor, bordaCor, (dist - (raio - borda)) / borda);
                else c = Color.clear;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        Destroy(texBotao);
        Destroy(texBotaoPress);
        Destroy(texBotaoAtira);
        Destroy(texBotaoAtiraPress);
    }
}