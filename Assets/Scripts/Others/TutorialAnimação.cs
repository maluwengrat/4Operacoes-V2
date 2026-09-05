using UnityEngine;

/// <summary>
/// TutorialBalao — balão de fala + moldura retangular de destaque para o tutorial,
/// desenhado 100% via OnGUI + GL (mesmo estilo do MobileUI.cs), sem UI do Editor.
///
/// SETUP:
///   1. Crie um GameObject vazio chamado "TutorialBalao" e adicione este script.
///
/// USO (dentro do TutorialManager.cs):
///   TutorialBalao.instance.Mostrar("texto aqui", refNave);   // balão com seta + moldura em volta do alvo
///   TutorialBalao.instance.Mostrar("texto aqui", null);      // balão centralizado, sem seta/moldura
///   TutorialBalao.instance.Esconder();
///   TutorialBalao.instance.MostrarDicaRapida("texto", 5f);   // dica que some sozinha
///
/// INTEGRAÇÃO SUGERIDA no TutorialManager.cs (substitui o uso de
/// painelInstrucao/destaqueVisual, que passam a ser opcionais):
///
///   void MostrarInstrucao(string texto, RectTransform alvo)
///   {
///       TutorialBalao.instance.Mostrar(texto, alvo);
///   }
///
///   void FecharTudo()
///   {
///       TutorialBalao.instance.Esconder();
///       if (overlayBloqueio != null) overlayBloqueio.SetActive(false);
///       if (panelModoTutorial != null) panelModoTutorial.SetActive(false);
///   }
///
///   public void MostrarDicaRapida(string mensagem, float duracao)
///   {
///       TutorialBalao.instance.MostrarDicaRapida(mensagem, duracao);
///   }
/// </summary>
public class TutorialBalao : MonoBehaviour
{
    public static TutorialBalao instance;

    [Header("Aparência do Balão")]
    public Color corFundoBalao = new Color(0.08f, 0.08f, 0.10f, 0.92f);
    public Color corBordaBalao = new Color(1f, 0.75f, 0.2f, 0.95f);
    public Color corTexto = new Color(1f, 0.95f, 0.85f, 1f);

    [Header("Aparência do Destaque (moldura pulsante em volta do alvo)")]
    public Color corDestaque = new Color(1f, 0.85f, 0.3f, 0.9f);
    [Tooltip("Quantos pixels a moldura fica afastada da borda do retângulo do alvo.")]
    public float expandirDestaqueBase = 8f;
    [Tooltip("Espessura base da linha da moldura.")]
    public float espessuraDestaqueBase = 4f;

    [Header("Dimensões do Balão")]
    [Range(240, 720)] public int larguraBalao = 480;
    public int paddingBalao = 24;
    public int tamanhoFonte = 30;

    [Header("Pausa")]
    [Tooltip("Arraste aqui o painel de Pause. Enquanto ele estiver ativo, o balão se esconde (OnGUI desenha por cima de tudo, inclusive Canvas).")]
    public GameObject painelPausa;

    // ── Estado ───────────────────────────────────────────────────────
    private bool ativo = false;
    private string texto = "";
    private RectTransform alvo;
    private Vector2 offsetAlvo = Vector2.zero;

    private float fadeAtual = 0f;
    private const float velocidadeFade = 6f;
    private float relogio = 0f; // independe de Time.timeScale (tutorial usa timeScale = 0)

    private GUIStyle estiloTexto;
    private Material _matGL;
    private Coroutine corrotinaAutoEsconder;

    // ── Unity ────────────────────────────────────────────────────────

    void Awake() => instance = this;

    void Update()
    {
        relogio += Time.unscaledDeltaTime;
        float alvoFade = ativo ? 1f : 0f;
        fadeAtual = Mathf.MoveTowards(fadeAtual, alvoFade, velocidadeFade * Time.unscaledDeltaTime);
    }

    void OnDestroy()
    {
        if (_matGL != null) Destroy(_matGL);
    }

    // ── API pública ──────────────────────────────────────────────────

    /// <summary>
    /// Mostra o balão com o texto dado. Se alvo != null, desenha seta + moldura retangular apontando pra ele.
    /// "offset" desloca o retângulo/ponto do destaque em pixels de tela (X: direita+, Y: baixo+),
    /// útil quando o pivot do RectTransform do alvo não bate exatamente com o que aparece visualmente.
    /// </summary>
    public void Mostrar(string novoTexto, RectTransform novoAlvo, Vector2 offset = default)
    {
        texto = novoTexto;
        alvo = novoAlvo;
        offsetAlvo = offset;
        ativo = true;
        PararAutoEsconder();
    }

    public void Esconder()
    {
        ativo = false;
        PararAutoEsconder();
    }

    /// <summary>Dica rápida, sem bloquear o jogo, que some sozinha após "duracao" segundos.</summary>
    public void MostrarDicaRapida(string mensagem, float duracao)
    {
        texto = mensagem;
        alvo = null;
        offsetAlvo = Vector2.zero;
        ativo = true;
        PararAutoEsconder();
        corrotinaAutoEsconder = StartCoroutine(EsconderApos(duracao));
    }

    public bool EstaAtivo() => ativo;

    void PararAutoEsconder()
    {
        if (corrotinaAutoEsconder != null)
        {
            StopCoroutine(corrotinaAutoEsconder);
            corrotinaAutoEsconder = null;
        }
    }

    System.Collections.IEnumerator EsconderApos(float segundos)
    {
        float t = 0f;
        while (t < segundos) { t += Time.unscaledDeltaTime; yield return null; }
        ativo = false;
        corrotinaAutoEsconder = null;
    }

    // ── Desenho ──────────────────────────────────────────────────────

    void OnGUI()
    {
        if (painelPausa != null && painelPausa.activeInHierarchy) return;
        if (fadeAtual <= 0.001f) return;
        if (estiloTexto == null) CriarEstiloTexto();

        Vector2? pontoAlvo = ObterPontoAlvoTela();
        Rect? retanguloAlvo = ObterRetanguloAlvoTela();

        if (retanguloAlvo.HasValue)
            DesenharDestaquePulsante(retanguloAlvo.Value);

        DesenharBalao(pontoAlvo);
    }

    // RectTransform.position (canvas Screen Space - Overlay) tem origem embaixo-esquerda, Y pra cima.
    // O pixel matrix do OnGUI/GL usado aqui tem origem em cima-esquerda, Y pra baixo — por isso o flip.
    Vector2? ObterPontoAlvoTela()
    {
        if (alvo == null) return null;
        Vector3 p = alvo.position;
        return new Vector2(p.x, Screen.height - p.y) + offsetAlvo;
    }

    // Mesma lógica de flip do ObterPontoAlvoTela, mas pegando os 4 cantos do RectTransform
    // (em coordenadas de mundo do Canvas) pra montar o retângulo completo em coordenadas de tela.
    Rect? ObterRetanguloAlvoTela()
    {
        if (alvo == null) return null;

        Vector3[] cantos = new Vector3[4];
        alvo.GetWorldCorners(cantos); // 0=inferior-esq, 1=superior-esq, 2=superior-dir, 3=inferior-dir (mundo)

        float xMin = Mathf.Min(cantos[0].x, cantos[1].x) + offsetAlvo.x;
        float xMax = Mathf.Max(cantos[2].x, cantos[3].x) + offsetAlvo.x;
        float yMin = Screen.height - Mathf.Max(cantos[1].y, cantos[2].y) + offsetAlvo.y;
        float yMax = Screen.height - Mathf.Min(cantos[0].y, cantos[3].y) + offsetAlvo.y;

        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    void CriarEstiloTexto()
    {
        estiloTexto = new GUIStyle(GUI.skin.label)
        {
            fontSize = tamanhoFonte,
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            fontStyle = FontStyle.Bold
        };
        estiloTexto.normal.textColor = corTexto;
    }

    void DesenharBalao(Vector2? pontoAlvo)
    {
        float larguraTexto = larguraBalao - paddingBalao * 2;
        float alturaTexto = estiloTexto.CalcHeight(new GUIContent(texto), larguraTexto);
        float alturaBalao = alturaTexto + paddingBalao * 2;

        float cx = Screen.width * 0.5f;
        float cy;
        bool seteAcima; // true = balão fica ACIMA do alvo (a seta aponta pra baixo)

        if (pontoAlvo.HasValue)
        {
            float margem = 60f;
            bool cabeEmCima = (pontoAlvo.Value.y - alturaBalao - margem) > 20f;
            cy = cabeEmCima ? pontoAlvo.Value.y - alturaBalao - margem : pontoAlvo.Value.y + margem;
            seteAcima = cabeEmCima;
        }
        else
        {
            cy = Screen.height * 0.22f;
            seteAcima = true;
        }

        Rect balaoRect = new Rect(cx - larguraBalao * 0.5f, cy, larguraBalao, alturaBalao);

        Color corFundo = corFundoBalao; corFundo.a *= fadeAtual;
        Color corBorda = corBordaBalao; corBorda.a *= fadeAtual;

        DesenharRetanguloComBorda(balaoRect, corFundo, corBorda, 3f);

        if (pontoAlvo.HasValue)
            DesenharSetaBalao(balaoRect, pontoAlvo.Value, seteAcima, corFundo, corBorda);

        Color corTextoFade = corTexto; corTextoFade.a *= fadeAtual;
        estiloTexto.normal.textColor = corTextoFade;
        GUI.Label(new Rect(balaoRect.x + paddingBalao, balaoRect.y + paddingBalao,
                            balaoRect.width - paddingBalao * 2, balaoRect.height - paddingBalao * 2),
                   texto, estiloTexto);
    }

    void DesenharSetaBalao(Rect balaoRect, Vector2 alvoPos, bool seteAcima, Color corFundo, Color corBorda)
    {
        float tam = 22f;
        float tipX = Mathf.Clamp(alvoPos.x, balaoRect.x + tam * 1.5f, balaoRect.xMax - tam * 1.5f);

        Vector2 base1, base2, ponta;
        if (seteAcima)
        {
            base1 = new Vector2(tipX - tam, balaoRect.yMax);
            base2 = new Vector2(tipX + tam, balaoRect.yMax);
            ponta = new Vector2(tipX, balaoRect.yMax + tam * 1.4f);
        }
        else
        {
            base1 = new Vector2(tipX - tam, balaoRect.yMin);
            base2 = new Vector2(tipX + tam, balaoRect.yMin);
            ponta = new Vector2(tipX, balaoRect.yMin - tam * 1.4f);
        }

        // borda (triângulo maior) + preenchimento (triângulo levemente encolhido por dentro)
        DesenharTriangulo(base1, base2, ponta, corBorda);
        Vector2 encolherBase = (base2 - base1).normalized * 4f;
        Vector2 encolherPonta = (ponta - Vector2.Lerp(base1, base2, 0.5f)).normalized * 5f;
        DesenharTriangulo(base1 + encolherBase, base2 - encolherBase, ponta - encolherPonta, corFundo);
    }

    // Desenha uma moldura (só o contorno, sem preencher o meio) pulsando em volta do retângulo do alvo.
    void DesenharDestaquePulsante(Rect alvoRect)
    {
        float pulso = (Mathf.Sin(relogio * 3.2f) + 1f) * 0.5f; // 0..1
        float espessura = espessuraDestaqueBase + pulso * 3f;
        float expandir = expandirDestaqueBase + pulso * 4f; // a moldura "respira" um pouco pra fora do retângulo

        Color cor = corDestaque;
        cor.a *= fadeAtual * (0.55f + pulso * 0.45f);

        Rect r = new Rect(alvoRect.x - expandir, alvoRect.y - expandir,
                           alvoRect.width + expandir * 2, alvoRect.height + expandir * 2);

        DesenharBordaRetangulo(r, cor, espessura);
    }

    // ── Primitivas GL (mesmo padrão do MobileUI.cs) ────────────────────

    // Contorno (4 retângulos finos: topo, base, esquerda, direita), sem preencher o meio.
    void DesenharBordaRetangulo(Rect r, Color cor, float espessura)
    {
        DesenharRetanguloSolido(new Rect(r.x, r.y, r.width, espessura), cor); // topo
        DesenharRetanguloSolido(new Rect(r.x, r.yMax - espessura, r.width, espessura), cor); // base
        DesenharRetanguloSolido(new Rect(r.x, r.y, espessura, r.height), cor); // esquerda
        DesenharRetanguloSolido(new Rect(r.xMax - espessura, r.y, espessura, r.height), cor); // direita
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

    void DesenharRetanguloComBorda(Rect r, Color corFundo, Color corBorda, float espessura)
    {
        DesenharRetanguloSolido(new Rect(r.x - espessura, r.y - espessura, r.width + espessura * 2, r.height + espessura * 2), corBorda);
        DesenharRetanguloSolido(r, corFundo);
    }

    Material ObterMaterialGL()
    {
        if (_matGL == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _matGL = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _matGL.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _matGL.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _matGL.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _matGL.SetInt("_ZWrite", 0);
        }
        return _matGL;
    }
}