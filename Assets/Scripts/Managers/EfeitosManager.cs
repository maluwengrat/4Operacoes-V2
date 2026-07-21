using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// EfeitosManager — Efeitos visuais épicos para o Math Shooter.
///
/// SETUP:
///   1. Crie um GameObject vazio chamado "EfeitosManager" e adicione este script.
///
/// CHAMADAS:
///   • Enemy.cs → OnTriggerEnter2D, ANTES de Destroy(gameObject):
///       EfeitosManager.instance.ExplodirInimigo(transform.position);
///
///   • GameManager.cs → CheckAnswer, bloco ACERTO, após MostrarAcerto():
///       EfeitosManager.instance.EfeitoAcerto(Vector3.zero);
///
///   • GameManager.cs → CheckAnswer, bloco ERRO (substitui GameOver direto):
///       StartCoroutine(EfeitosManager.instance.MorteNave(transform da nave, () => GameOver()));
///
///   • GameManager.cs → FaseCompleta() e VitoriaFinal(), no início:
///       EfeitosManager.instance.EfeitoPassarFase();
/// </summary>
public class EfeitosManager : MonoBehaviour
{
    public static EfeitosManager instance;

    // ══ CONFIGURAÇÕES PÚBLICAS ══════════════════════════════════════════

    [Header("Shake de Câmera")]
    public float shakeDuracao = 1.2f;
    public float shakeMagnitude = 0.4f;

    [Header("Flash de Tela")]
    public float duracaoFlash = 0.9f;

    [Header("Explosão — Bolas de fogo")]
    public int qtdBolasFogo = 28;
    public float velBolaMin = 160f;
    public float velBolaMax = 480f;
    public float vidaBola = 3.0f;

    [Header("Explosão — Faíscas longas")]
    public int qtdFaiscas = 45;
    public float velFaiscaMin = 280f;
    public float velFaiscaMax = 850f;
    public float vidaFaisca = 2.5f;
    public float comprFaisca = 40f;

    [Header("Explosão — Onda de choque")]
    public float velOnda = 750f;
    public float vidaOnda = 1.1f;
    public float espessuraOnda = 22f;

    [Header("Efeito Passar de Fase")]
    public int qtdConfetes = 100;

    [Header("Morte da Nave")]
    public float duracaoMorteNave = 3.0f;

    // ══ ESTADO INTERNO ══════════════════════════════════════════════════

    private Camera cam;
    private Vector3 camOrigem;
    private float shakeTimer = 0f;
    private Texture2D texBranca;

    private bool flashAtivo;
    private float flashTimer;
    private Color flashCor;

    private bool flashDuploAtivo;
    private float flashDuploTimer;
    private float flashDuploDuracao;
    private Color flashDuploCor;

    private List<Bola> bolas = new List<Bola>();
    private List<Faisca> faiscas = new List<Faisca>();
    private List<OndaChoque> ondas = new List<OndaChoque>();
    private List<Confete> confetes = new List<Confete>();
    private List<TextoVoo> textos = new List<TextoVoo>();

    // Morte da nave
    private bool morteAtiva = false;
    private float morteTimer = 0f;
    private Vector2 navePosicaoTela;
    private float naveRotacao = 0f;
    private float naveEscala = 1f;
    private Vector2 naveVelocidade;
    private float naveVelRot = 0f;
    private float navePiscarTimer = 0f;
    private bool naveVisivel = true;

    // ══ UNITY ═══════════════════════════════════════════════════════════

    void Awake()
    {
        instance = this;
        texBranca = new Texture2D(1, 1);
        texBranca.SetPixel(0, 0, Color.white);
        texBranca.Apply();
    }

    void Start()
    {
        cam = Camera.main;
        // FIX: Salva camOrigem apenas uma vez aqui no Start, com posição limpa garantida
        if (cam != null) camOrigem = cam.transform.position;
    }

    // ══ API PÚBLICA ══════════════════════════════════════════════════════

    /// <summary>
    /// Explosão épica na posição do inimigo.
    /// Faíscas cobrem a tela + bolas de fogo + onda de choque + flash duplo.
    /// Chame ANTES de Destroy(gameObject) no Enemy.cs.
    /// </summary>
    public void ExplodirInimigo(Vector3 posicaoMundo)
    {
        Vector2 origem = WorldToScreen(posicaoMundo);

        // Flash branco-amarelo + segundo pulso laranja
        AtivarFlash(new Color(1f, 0.88f, 0.3f, 0.82f), duracaoFlash);
        StartCoroutine(FlashComDelay(new Color(1f, 0.4f, 0f, 0.52f), 0.28f, 0.55f));

        // Bolas de fogo em todas as direções
        Color[] coresFogo = {
            new Color(1f,  0.9f, 0.1f),
            new Color(1f,  0.5f, 0f  ),
            new Color(1f,  0.15f,0f  ),
            new Color(1f,  1f,   0.6f),
        };
        for (int i = 0; i < qtdBolasFogo; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float vel = Random.Range(velBolaMin, velBolaMax);
            bolas.Add(new Bola
            {
                pos = origem,
                vel = new Vector2(Mathf.Cos(ang) * vel, Mathf.Sin(ang) * vel),
                vida = vidaBola * Random.Range(0.55f, 1.0f),
                vidaMax = vidaBola,
                raio = Random.Range(16f, 48f),
                cor = coresFogo[Random.Range(0, coresFogo.Length)]
            });
        }

        // Faíscas longas cobrindo toda a tela
        for (int i = 0; i < qtdFaiscas; i++)
        {
            float ang = (float)i / qtdFaiscas * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
            float vel = Random.Range(velFaiscaMin, velFaiscaMax);
            float comp = comprFaisca * (vel / velFaiscaMax) * Random.Range(0.7f, 2.8f);
            float t = Random.Range(0f, 1f);
            Color cor = Color.Lerp(new Color(1f, 1f, 0.8f), new Color(1f, 0.3f, 0f), t);
            faiscas.Add(new Faisca
            {
                pos = origem,
                direcao = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)),
                vel = vel,
                comp = comp,
                vida = vidaFaisca * Random.Range(0.45f, 1.0f),
                vidaMax = vidaFaisca,
                cor = cor
            });
        }

        // Onda de choque
        ondas.Add(new OndaChoque
        {
            centro = origem,
            raio = 0f,
            vel = velOnda,
            vida = vidaOnda,
            vidaMax = vidaOnda,
            cor = new Color(1f, 0.8f, 0.2f)
        });

        ShakeCamera();
    }

    /// <summary>Flash vermelho intenso ao errar.</summary>
    public void FlashErro()
    {
        AtivarFlash(new Color(1f, 0f, 0f, 0.85f), duracaoFlash * 1.4f);
        StartCoroutine(FlashComDelay(new Color(0.8f, 0f, 0f, 0.55f), 0.35f, 0.5f));
    }

    /// <summary>
    /// Abala a câmera.
    /// FIX: Só salva camOrigem quando o shake NÃO está ativo,
    /// evitando sobrescrever com posição deslocada (causa do NaN).
    /// </summary>
    public void ShakeCamera()
    {
        if (cam == null) cam = Camera.main;
        // FIX: Só atualiza camOrigem se o shake já terminou (câmera está em posição limpa)
        if (cam != null && shakeTimer <= 0f)
            camOrigem = cam.transform.position;
        shakeTimer = shakeDuracao;
    }

    /// <summary>
    /// Para o shake imediatamente e restaura a câmera para a posição original.
    /// Use no PlayerController.IniciarMorte() em vez de zerar os campos diretamente.
    /// </summary>
    public void PararShake()
    {
        shakeTimer = 0f;
        if (cam != null) cam.transform.position = camOrigem;
    }

    /// <summary>
    /// Apenas flash verde + faíscas verdes ao acertar. Sem texto.
    /// </summary>
    public void EfeitoAcerto(Vector3 posicaoMundo)
    {
        AtivarFlash(new Color(0.1f, 1f, 0.4f, 0.52f), duracaoFlash);

        // Faíscas verdes/amarelas espalhando do centro da tela
        Vector2 origemAcerto = new Vector2(Screen.width * 0.5f, Screen.height * 0.38f);
        for (int i = 0; i < 28; i++)
        {
            float ang = (float)i / 28 * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
            float vel = Random.Range(200f, 620f);
            float comp = Random.Range(18f, 55f);
            faiscas.Add(new Faisca
            {
                pos = origemAcerto,
                direcao = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)),
                vel = vel,
                comp = comp,
                vida = 2.0f * Random.Range(0.5f, 1f),
                vidaMax = 2.0f,
                cor = Color.Lerp(new Color(0.2f, 1f, 0.4f), new Color(1f, 1f, 0.3f),
                                     Random.Range(0f, 1f))
            });
        }

        // Onda de choque verde
        ondas.Add(new OndaChoque
        {
            centro = origemAcerto,
            raio = 0f,
            vel = 700f,
            vida = 1.2f,
            vidaMax = 1.2f,
            cor = new Color(0.2f, 1f, 0.4f)
        });
    }

    /// <summary>Confetes + ondas + textos épicos ao passar de fase.</summary>
    public void EfeitoPassarFase()
    {
        AtivarFlash(new Color(1f, 0.85f, 0f, 0.90f), 0.85f);
        StartCoroutine(FlashComDelay(new Color(1f, 0.5f, 0f, 0.58f), 0.4f, 0.55f));
        ShakeCamera();
        StartCoroutine(SpawnarOndasFase());
        StartCoroutine(SpawnarConfetes());

        string[] msgs = { "FASE COMPLETA!", "INCRÍVEL!", "AVANÇANDO!" };
        Color[] cores = {
            new Color(1f,  0.9f, 0.1f),
            new Color(0.2f,1f,   0.5f),
            new Color(0.4f,0.8f, 1f  ),
        };
        for (int i = 0; i < msgs.Length; i++)
        {
            int idx = i;
            float delay = i * 0.28f;
            StartCoroutine(AdicionarTextoComDelay(
                msgs[idx],
                new Vector2(Screen.width * 0.5f, Screen.height * (0.26f + idx * 0.10f)),
                cores[idx], 3.0f, delay, 1f + idx * 0.18f));
        }
    }

    /// <summary>
    /// Sequência dramática: nave pisca, explode em câmera lenta, tela escurece.
    /// Passe a Transform da nave e um callback que será chamado ao fim (ex: GameOver()).
    /// No GameManager, use: StartCoroutine(EfeitosManager.instance.MorteNave(naveTransform, GameOver));
    /// </summary>
    public IEnumerator MorteNave(Transform naveTransform, System.Action callbackGameOver)
    {
        morteAtiva = true;
        morteTimer = 0f;

        // Captura posição da nave na tela
        navePosicaoTela = WorldToScreen(naveTransform.position);
        naveRotacao = 0f;
        naveEscala = 1f;
        naveVelocidade = new Vector2(Random.Range(-40f, 40f), Random.Range(60f, 140f));
        naveVelRot = Random.Range(180f, 360f) * (Random.value > 0.5f ? 1f : -1f);
        naveVisivel = true;
        navePiscarTimer = 0f;

        // Esconde a nave real imediatamente
        if (naveTransform != null)
        {
            SpriteRenderer sr = naveTransform.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        // ── Fase 1: pisca e treme (0 ~ 0.8s) ─────────────────────────
        float fase1 = 0.8f;
        float t = 0f;
        while (t < fase1)
        {
            t += Time.unscaledDeltaTime;
            morteTimer = t;
            navePiscarTimer += Time.unscaledDeltaTime;

            // Pisca rápido
            if (navePiscarTimer > 0.07f)
            {
                naveVisivel = !naveVisivel;
                navePiscarTimer = 0f;
            }

            // Shake crescente
            shakeMagnitude = Mathf.Lerp(0.1f, 0.5f, t / fase1);
            ShakeCamera();

            // Pequenas explosõezinhas em volta da nave
            if (Random.value > 0.6f)
            {
                Vector2 offset = new Vector2(
                    Random.Range(-30f, 30f),
                    Random.Range(-30f, 30f));
                SpawnarMiniExplosao(navePosicaoTela + offset, 0.4f);
            }

            yield return null;
        }

        // ── Fase 2: explosão principal (0.8s) ─────────────────────────
        naveVisivel = false;
        shakeMagnitude = shakeMagnitude * 2.5f;
        ShakeCamera();

        // Flash branco total
        AtivarFlash(new Color(1f, 0.9f, 0.8f, 1.0f), 0.5f);

        // Explosão gigante centrada na nave
        ExplodirGigante(navePosicaoTela);

        yield return new WaitForSecondsRealtime(0.5f);

        // ── Fase 3: tela escurece lentamente (1.5s) ───────────────────
        float fase3 = 1.5f;
        t = 0f;
        while (t < fase3)
        {
            t += Time.unscaledDeltaTime;
            // Flash escuro crescente (escurece progressivamente)
            float alpha = Mathf.Lerp(0f, 0.95f, t / fase3);
            AtivarFlash(new Color(0f, 0f, 0f, alpha), 0.12f);
            yield return null;
        }

        // ── Tela totalmente preta, chama callback ─────────────────────
        // Tela preta por 0.4s renovando o flash a cada frame
        float telaPretatimer = 0f;
        while (telaPretatimer < 0.4f)
        {
            telaPretatimer += Time.unscaledDeltaTime;
            AtivarFlash(new Color(0f, 0f, 0f, 1f), 0.1f);
            yield return null;
        }

        // Limpa estado antes de abrir o painel
        morteAtiva = false;
        flashAtivo = false;
        flashDuploAtivo = false;
        // FIX: Para o shake corretamente via PararShake() para restaurar camOrigem limpa
        PararShake();
        shakeMagnitude = 0.4f;

        callbackGameOver?.Invoke();
    }

    // ══ UPDATE ════════════════════════════════════════════════════════════

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Shake
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            if (cam != null)
            {
                if (shakeTimer > 0f)
                {
                    // FIX: Usa shakeDuracao com proteção contra divisão por zero
                    float duracaoRef = shakeDuracao > 0f ? shakeDuracao : 1f;
                    float tNorm = shakeTimer / duracaoRef;
                    float mag = shakeMagnitude * tNorm;
                    cam.transform.position = camOrigem
                        + new Vector3(Random.Range(-mag, mag), Random.Range(-mag, mag), 0f);
                }
                else
                {
                    // FIX: Restaura para camOrigem garantida (sem NaN acumulado)
                    cam.transform.position = camOrigem;
                    shakeTimer = 0f;
                }
            }
        }

        if (flashAtivo) { flashTimer -= dt; if (flashTimer <= 0f) flashAtivo = false; }
        if (flashDuploAtivo) { flashDuploTimer -= dt; if (flashDuploTimer <= 0f) flashDuploAtivo = false; }

        // Bolas de fogo
        for (int i = bolas.Count - 1; i >= 0; i--)
        {
            var b = bolas[i];
            b.pos += b.vel * dt;
            b.vel *= 0.94f;
            b.vida -= dt;
            if (b.vida <= 0f) { bolas.RemoveAt(i); continue; }
            bolas[i] = b;
        }

        // Faíscas
        for (int i = faiscas.Count - 1; i >= 0; i--)
        {
            var f = faiscas[i];
            f.pos += f.direcao * f.vel * dt;
            f.vel *= 0.975f;
            f.vida -= dt;
            if (f.vida <= 0f) { faiscas.RemoveAt(i); continue; }
            faiscas[i] = f;
        }

        // Ondas de choque
        for (int i = ondas.Count - 1; i >= 0; i--)
        {
            var o = ondas[i];
            o.raio += o.vel * dt;
            o.vida -= dt;
            if (o.vida <= 0f) { ondas.RemoveAt(i); continue; }
            ondas[i] = o;
        }

        // Confetes
        for (int i = confetes.Count - 1; i >= 0; i--)
        {
            var c = confetes[i];
            c.pos += c.vel * dt;
            c.vel.y += 500f * dt;
            c.rot += c.velRot * dt;
            c.vida -= dt;
            if (c.vida <= 0f) { confetes.RemoveAt(i); continue; }
            confetes[i] = c;
        }

        // Textos
        for (int i = textos.Count - 1; i >= 0; i--)
        {
            var t = textos[i];
            t.pos.y += t.velY * dt;
            t.vida -= dt;
            if (t.vida <= 0f) { textos.RemoveAt(i); continue; }
            textos[i] = t;
        }

        // Animação da nave na morte (fase 1 — deriva com gravidade)
        if (morteAtiva)
        {
            naveVelocidade.y += 120f * dt;   // gravidade leve
            navePosicaoTela += naveVelocidade * dt;
            naveRotacao += naveVelRot * dt;
        }
    }

    // ══ OnGUI ════════════════════════════════════════════════════════════

    void OnGUI()
    {
        float W = Screen.width;
        float H = Screen.height;

        // Flash duplo
        if (flashDuploAtivo)
        {
            float t = Mathf.Clamp01(flashDuploTimer / flashDuploDuracao);
            float a = Mathf.Sin(t * Mathf.PI) * flashDuploCor.a;
            GUI.color = new Color(flashDuploCor.r, flashDuploCor.g, flashDuploCor.b, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), texBranca);
        }

        // Flash principal
        if (flashAtivo)
        {
            float t = Mathf.Clamp01(flashTimer / duracaoFlash);
            float a;
            // Flash preto (morte da nave): alpha direto sem seno para manter tela cheia
            if (flashCor.r == 0f && flashCor.g == 0f && flashCor.b == 0f)
                a = flashCor.a;
            else
                a = Mathf.Sin(t * Mathf.PI) * flashCor.a;
            GUI.color = new Color(flashCor.r, flashCor.g, flashCor.b, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), texBranca);
        }

        GUI.color = Color.white;

        // Ondas de choque
        foreach (var o in ondas)
        {
            float t = Mathf.Clamp01(o.vida / o.vidaMax);
            float esp = espessuraOnda * t;
            GUI.color = new Color(o.cor.r, o.cor.g, o.cor.b, t * 0.92f);
            DesenharAnel(o.centro.x, o.centro.y, o.raio, esp);
        }

        // Bolas de fogo
        foreach (var b in bolas)
        {
            float t = Mathf.Clamp01(b.vida / b.vidaMax);
            float a = Mathf.Sin(t * Mathf.PI);
            float raio = b.raio * (1f + (1f - t) * 0.7f);
            GUI.color = new Color(b.cor.r, b.cor.g, b.cor.b, a);
            DesenharCirculo(b.pos.x, b.pos.y, raio);
        }

        // Faíscas
        foreach (var f in faiscas)
        {
            float t = Mathf.Clamp01(f.vida / f.vidaMax);
            float larg = Mathf.Max(1f, 5f * t);
            Vector2 cauda = f.pos - f.direcao * f.comp * t;

            GUI.color = new Color(f.cor.r, f.cor.g, f.cor.b, t);
            DesenharLinha(cauda, f.pos, larg);

            GUI.color = new Color(1f, 1f, 1f, t * 0.7f);
            DesenharLinha(cauda, f.pos, larg * 0.3f);
        }

        // Confetes
        foreach (var c in confetes)
        {
            float t = Mathf.Clamp01(c.vida / c.vidaMax);
            Matrix4x4 mat = GUI.matrix;
            GUIUtility.RotateAroundPivot(c.rot, c.pos);
            GUI.color = new Color(c.cor.r, c.cor.g, c.cor.b, t);
            GUI.DrawTexture(
                new Rect(c.pos.x - c.largura / 2f, c.pos.y - c.altura / 2f,
                         c.largura, c.altura), texBranca);
            GUI.matrix = mat;
        }

        // Textos voadores
        foreach (var t in textos)
        {
            float alpha = Mathf.Clamp01(t.vida / t.vidaMax);
            float escala = t.escala + (1f - alpha) * 0.3f;
            int fs = Mathf.RoundToInt(44 * escala);

            GUIStyle sh = new GUIStyle();
            sh.fontSize = fs; sh.fontStyle = FontStyle.Bold;
            sh.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.55f);
            sh.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(t.pos.x - 202f, t.pos.y - 22f, 304f, 52f), t.texto, sh);

            GUIStyle st = new GUIStyle();
            st.fontSize = fs; st.fontStyle = FontStyle.Bold;
            st.normal.textColor = new Color(t.cor.r, t.cor.g, t.cor.b, alpha);
            st.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(t.pos.x - 200f, t.pos.y - 24f, 300f, 52f), t.texto, st);
        }

        // Silhueta da nave durante a morte (fase 1)
        if (morteAtiva && naveVisivel)
        {
            // Desenha um triângulo simples representando a nave
            float s = 22f * naveEscala;
            Matrix4x4 mat = GUI.matrix;
            GUIUtility.RotateAroundPivot(naveRotacao, navePosicaoTela);
            GUI.color = new Color(0.976f, 0.451f, 0.086f, 0.9f);
            // Corpo central
            GUI.DrawTexture(new Rect(navePosicaoTela.x - s * 0.3f,
                                     navePosicaoTela.y - s,
                                     s * 0.6f, s * 2f), texBranca);
            // Asa esquerda
            GUI.DrawTexture(new Rect(navePosicaoTela.x - s,
                                     navePosicaoTela.y,
                                     s * 0.7f, s * 0.5f), texBranca);
            // Asa direita
            GUI.DrawTexture(new Rect(navePosicaoTela.x + s * 0.3f,
                                     navePosicaoTela.y,
                                     s * 0.7f, s * 0.5f), texBranca);
            GUI.matrix = mat;
        }

        GUI.color = Color.white;
    }

    // ══ INTERNOS ═════════════════════════════════════════════════════════

    void SpawnarMiniExplosao(Vector2 origem, float escala)
    {
        Color[] cores = {
            new Color(1f, 0.8f, 0.1f),
            new Color(1f, 0.4f, 0f  ),
            new Color(1f, 1f,   0.5f),
        };
        int qtd = Mathf.RoundToInt(10 * escala);
        for (int i = 0; i < qtd; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float vel = Random.Range(80f, 220f) * escala;
            bolas.Add(new Bola
            {
                pos = origem,
                vel = new Vector2(Mathf.Cos(ang) * vel, Mathf.Sin(ang) * vel),
                vida = 0.8f * Random.Range(0.5f, 1f),
                vidaMax = 0.8f,
                raio = Random.Range(6f, 18f) * escala,
                cor = cores[Random.Range(0, cores.Length)]
            });
        }
        int qtdF = Mathf.RoundToInt(12 * escala);
        for (int i = 0; i < qtdF; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float vel = Random.Range(120f, 380f) * escala;
            faiscas.Add(new Faisca
            {
                pos = origem,
                direcao = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)),
                vel = vel,
                comp = Random.Range(12f, 35f) * escala,
                vida = 0.9f * Random.Range(0.4f, 1f),
                vidaMax = 0.9f,
                cor = Color.Lerp(new Color(1f, 1f, 0.6f), new Color(1f, 0.3f, 0f),
                                     Random.Range(0f, 1f))
            });
        }
    }

    void ExplodirGigante(Vector2 origem)
    {
        // Bolas de fogo grandes
        Color[] coresFogo = {
            new Color(1f, 0.9f, 0.1f),
            new Color(1f, 0.5f, 0f  ),
            new Color(1f, 0.15f,0f  ),
            new Color(1f, 1f,   0.7f),
        };
        for (int i = 0; i < 40; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float vel = Random.Range(120f, 600f);
            bolas.Add(new Bola
            {
                pos = origem,
                vel = new Vector2(Mathf.Cos(ang) * vel, Mathf.Sin(ang) * vel),
                vida = 3.5f * Random.Range(0.5f, 1f),
                vidaMax = 3.5f,
                raio = Random.Range(24f, 72f),
                cor = coresFogo[Random.Range(0, coresFogo.Length)]
            });
        }

        // Faíscas massivas cobrindo toda a tela
        for (int i = 0; i < 60; i++)
        {
            float ang = (float)i / 60 * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
            float vel = Random.Range(400f, 1100f);
            float comp = 50f * (vel / 1100f) * Random.Range(1f, 4f);
            float t = Random.Range(0f, 1f);
            Color cor = Color.Lerp(new Color(1f, 1f, 0.8f), new Color(1f, 0.2f, 0f), t);
            faiscas.Add(new Faisca
            {
                pos = origem,
                direcao = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)),
                vel = vel,
                comp = comp,
                vida = 3.0f * Random.Range(0.5f, 1f),
                vidaMax = 3.0f,
                cor = cor
            });
        }

        // 3 ondas de choque consecutivas
        for (int i = 0; i < 3; i++)
        {
            ondas.Add(new OndaChoque
            {
                centro = origem,
                raio = i * 40f,
                vel = velOnda * (1f - i * 0.2f),
                vida = 2.0f,
                vidaMax = 2.0f,
                cor = i == 0 ? new Color(1f, 0.9f, 0.3f)
                        : i == 1 ? new Color(1f, 0.5f, 0.1f)
                                 : new Color(1f, 0.2f, 0.2f)
            });
        }
    }

    // ══ CORROTINAS ═══════════════════════════════════════════════════════

    IEnumerator FlashComDelay(Color cor, float delay, float duracao)
    {
        yield return new WaitForSecondsRealtime(delay);
        flashDuploCor = cor;
        flashDuploTimer = duracao;
        flashDuploDuracao = duracao;
        flashDuploAtivo = true;
    }

    IEnumerator SpawnarOndasFase()
    {
        Color[] coresOnda = {
            new Color(1f,   0.9f, 0.1f),
            new Color(0.3f, 0.9f, 1f  ),
            new Color(1f,   0.3f, 0.8f),
            new Color(0.3f, 1f,   0.4f),
        };
        for (int i = 0; i < 4; i++)
        {
            ondas.Add(new OndaChoque
            {
                centro = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                raio = 0f,
                vel = velOnda * 0.65f,
                vida = 1.8f,
                vidaMax = 1.8f,
                cor = coresOnda[i % coresOnda.Length]
            });
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }

    IEnumerator SpawnarConfetes()
    {
        Color[] coresC = {
            new Color(1f,  0.9f, 0.1f),
            new Color(0.2f,0.8f, 1f  ),
            new Color(1f,  0.3f, 0.7f),
            new Color(0.3f,1f,   0.4f),
            new Color(1f,  0.5f, 0.1f),
            new Color(0.8f,0.3f, 1f  ),
        };
        for (int i = 0; i < qtdConfetes; i++)
        {
            confetes.Add(new Confete
            {
                pos = new Vector2(Random.Range(0f, Screen.width), -20f),
                vel = new Vector2(Random.Range(-220f, 220f), Random.Range(-950f, -280f)),
                rot = Random.Range(0f, 360f),
                velRot = Random.Range(-400f, 400f),
                vida = Random.Range(2.0f, 3.8f),
                vidaMax = 3.8f,
                largura = Random.Range(8f, 22f),
                altura = Random.Range(4f, 12f),
                cor = coresC[Random.Range(0, coresC.Length)]
            });
            if (i % 8 == 0) yield return new WaitForSecondsRealtime(0.03f);
        }
    }

    IEnumerator AdicionarTextoComDelay(string msg, Vector2 pos, Color cor,
                                        float vida, float delay, float escala)
    {
        yield return new WaitForSecondsRealtime(delay);
        textos.Add(new TextoVoo
        {
            pos = pos,
            texto = msg,
            vida = vida,
            vidaMax = vida,
            cor = cor,
            velY = -30f,
            escala = escala
        });
    }

    // ══ HELPERS DE DESENHO ════════════════════════════════════════════════

    void AtivarFlash(Color cor, float duracao)
    {
        flashCor = cor;
        flashTimer = duracao;
        flashAtivo = true;
    }

    void DesenharCirculo(float cx, float cy, float raio)
    {
        int segs = 22;
        float step = (raio * 2f) / segs;
        for (int i = 0; i < segs; i++)
        {
            float y = -raio + i * step;
            float larg = 2f * Mathf.Sqrt(Mathf.Max(0f, raio * raio - y * y));
            GUI.DrawTexture(new Rect(cx - larg / 2f, cy + y, larg, step + 1f), texBranca);
        }
    }

    void DesenharAnel(float cx, float cy, float raio, float espessura)
    {
        int segs = 72;
        for (int i = 0; i < segs; i++)
        {
            float t = (float)i / segs * Mathf.PI * 2f;
            float x = cx + Mathf.Cos(t) * raio;
            float y = cy + Mathf.Sin(t) * raio;
            GUI.DrawTexture(
                new Rect(x - espessura / 2f, y - espessura / 2f, espessura, espessura),
                texBranca);
        }
    }

    void DesenharLinha(Vector2 a, Vector2 b, float largura)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.5f) return;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 centro = (a + b) * 0.5f;
        Matrix4x4 mat = GUI.matrix;
        GUIUtility.RotateAroundPivot(ang, centro);
        GUI.DrawTexture(
            new Rect(centro.x - len / 2f, centro.y - largura / 2f, len, largura),
            texBranca);
        GUI.matrix = mat;
    }

    Vector2 WorldToScreen(Vector3 worldPos)
    {
        if (cam == null) cam = Camera.main;
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    // ══ STRUCTS ═══════════════════════════════════════════════════════════

    struct Bola { public Vector2 pos, vel; public float vida, vidaMax, raio; public Color cor; }
    struct Faisca { public Vector2 pos, direcao; public float vel, comp, vida, vidaMax; public Color cor; }
    struct OndaChoque { public Vector2 centro; public float raio, vel, vida, vidaMax; public Color cor; }
    struct Confete { public Vector2 pos, vel; public float rot, velRot, vida, vidaMax, largura, altura; public Color cor; }
    struct TextoVoo { public Vector2 pos; public string texto; public float vida, vidaMax, velY, escala; public Color cor; }
}