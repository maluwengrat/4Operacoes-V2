using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 4f;
    public GameObject bulletPrefab;
    public Transform firePoint;

    private bool escudoAtivo = false;
    private GameObject escudoVisual;

    private bool btnEsquerdoPressionado = false;
    private bool btnDireitoPressionado = false;
    private bool btnAtirouAgora = false;

    private float limiteEsq;
    private float limiteDirMin;

    private float cooldownTiro = 0.22f;
    private float timerTiro = 0f;

    private bool morrendo = false;
    private float morteTimer = 0f;
    private bool gameOverDisparado = false;

    private enum FaseMorte { Zoom, Queda, Explosao }
    private FaseMorte faseMorte = FaseMorte.Zoom;

    private const float duracaoZoom = 0.6f;
    private const float duracaoQueda = 1.15f;
    private const float duracaoExplosao = 1.2f;

    private Vector3 posicaoZoomAlvo = new Vector3(0f, 0f, 0f);
    private float escalaZoomAlvo = 2.5f;
    private Vector3 posicaoInicialMorte;
    private float quedaVelAtual = 0f;

    void Start()
    {
        Camera cam = Camera.main;
        limiteEsq = cam.ViewportToWorldPoint(new Vector3(0.04f, 0, cam.nearClipPlane)).x;
        limiteDirMin = cam.ViewportToWorldPoint(new Vector3(0.96f, 0, cam.nearClipPlane)).x;
        CriarEscudoVisual();

        // ← adicione isto:
        transform.position = new Vector3(0f, -3.5f, 0f);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 20;
            sr.sortingLayerName = "Default";
        }
    }

    public void ResetarPlayer()
    {
        CancelInvoke();
        StopAllCoroutines();
        morrendo = false;
        morteTimer = 0f;
        gameOverDisparado = false;
        faseMorte = FaseMorte.Zoom;
        quedaVelAtual = 0f;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) { sr.enabled = true; sr.color = Color.white; }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
        escudoAtivo = false;
        if (escudoVisual != null) escudoVisual.SetActive(false);
        timerTiro = 0f;
        btnEsquerdoPressionado = false;
        btnDireitoPressionado = false;
        btnAtirouAgora = false;
    }

    void Update()
    {
        if (morrendo) { AtualizarAnimacaoMorte(); return; }
        if (GameManager.instance != null && !GameManager.instance.JogoRodando()) return;
        MoverNave();
        GerentiarTiro();
    }

    public void IniciarMorte()
    {
        if (morrendo) return;
        morrendo = true;
        morteTimer = 0f;
        gameOverDisparado = false;
        faseMorte = FaseMorte.Zoom;
        quedaVelAtual = 0f;
        posicaoInicialMorte = transform.position;
        UsarEscudo();
        if (EfeitosManager.instance != null) EfeitosManager.instance.PararShake();
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        if (SoundManager.instance != null)
        {
            SoundManager.instance.PararMusica();
            SoundManager.instance.TocarErro();
        }
    }

    void AtualizarAnimacaoMorte()
    {
        morteTimer += Time.deltaTime;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        if (faseMorte == FaseMorte.Zoom)
        {
            float progresso = Mathf.Clamp01(morteTimer / duracaoZoom);
            float t = Mathf.SmoothStep(0f, 1f, progresso);
            transform.position = Vector3.Lerp(posicaoInicialMorte, posicaoZoomAlvo, t);
            float escala = Mathf.Lerp(1f, escalaZoomAlvo, t);
            transform.localScale = new Vector3(escala, escala, 1f);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, t);
            if (progresso > 0.85f && sr != null) sr.enabled = Mathf.Sin(morteTimer * 18f) > 0f;
            if (morteTimer >= duracaoZoom)
            {
                morteTimer = 0f;
                faseMorte = FaseMorte.Queda;
                transform.position = posicaoZoomAlvo;
                transform.localScale = new Vector3(escalaZoomAlvo, escalaZoomAlvo, 1f);
                transform.rotation = Quaternion.identity;
                if (sr != null) sr.enabled = true;
                if (EfeitosManager.instance != null)
                {
                    EfeitosManager.instance.shakeDuracao = 0.3f;
                    EfeitosManager.instance.shakeMagnitude = 0.15f;
                    EfeitosManager.instance.ShakeCamera();
                }
            }
        }
        else if (faseMorte == FaseMorte.Queda)
        {
            float progresso = morteTimer / duracaoQueda;
            quedaVelAtual = Mathf.Lerp(0.2f, 4f, progresso * progresso);
            transform.Translate(Vector2.down * quedaVelAtual * Time.deltaTime, Space.World);
            float rotacao = Mathf.Sin(morteTimer * 3f) * Mathf.Lerp(8f, 25f, progresso);
            transform.rotation = Quaternion.Euler(0f, 0f, rotacao);
            transform.localScale = new Vector3(escalaZoomAlvo, escalaZoomAlvo, 1f);
            if (progresso > 0.82f && sr != null) sr.enabled = Mathf.Sin(morteTimer * 20f) > 0f;
            if (morteTimer >= duracaoQueda)
            {
                morteTimer = 0f;
                faseMorte = FaseMorte.Explosao;
                if (EfeitosManager.instance != null)
                {
                    EfeitosManager.instance.shakeDuracao = 0.8f;
                    EfeitosManager.instance.shakeMagnitude = 0.35f;
                    EfeitosManager.instance.ExplodirInimigo(transform.position);
                }
                if (SoundManager.instance != null) SoundManager.instance.TocarExplosao();
                if (sr != null) sr.enabled = false;
                transform.localScale = Vector3.zero;
            }
        }
        else if (faseMorte == FaseMorte.Explosao)
        {
            if (morteTimer >= duracaoExplosao && !gameOverDisparado)
            {
                gameOverDisparado = true;
                if (GameManager.instance != null) GameManager.instance.ExecutarGameOver();
            }
        }
    }

    void MoverNave()
    {
        float direcao = 0f;
        float speedAtual = speed;

        // acelerômetro no mobile — velocidade maior para responder ao tilt
        if (SystemInfo.supportsAccelerometer &&
            (Application.isMobilePlatform ||
             SystemInfo.deviceType == DeviceType.Handheld))
        {
            float tilt = Input.acceleration.x;
            if (Mathf.Abs(tilt) > 0.08f)
                direcao += tilt * 2.5f;
            speedAtual = speed * 1.8f; // mobile precisa de mais velocidade
        }

        // New Input System
        var teclado = UnityEngine.InputSystem.Keyboard.current;
        if (teclado != null)
        {
            if (teclado.leftArrowKey.isPressed || teclado.aKey.isPressed) direcao -= 1f;
            if (teclado.rightArrowKey.isPressed || teclado.dKey.isPressed) direcao += 1f;
        }

        // Fallback clássico
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) direcao -= 1f;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) direcao += 1f;

        if (btnEsquerdoPressionado) direcao -= 1f;
        if (btnDireitoPressionado) direcao += 1f;

        direcao = Mathf.Clamp(direcao, -1f, 1f);

        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x + direcao * speedAtual * Time.deltaTime, limiteEsq, limiteDirMin);

        Camera cam = Camera.main;
        float limiteInf = cam.ViewportToWorldPoint(new Vector3(0, 0.02f, 0)).y;
        float limiteSup = cam.ViewportToWorldPoint(new Vector3(0, 0.25f, 0)).y;
        p.y = Mathf.Clamp(p.y, limiteInf, limiteSup);

        transform.position = p;
    }
    void GerentiarTiro()
    {
        timerTiro -= Time.deltaTime;

        bool atirarTeclado = false;

        // ── New Input System ──
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            atirarTeclado = UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                            UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame;
        }

        // ── Fallback Input clássico (garante WebGL) ──
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow))
            atirarTeclado = true;

        bool atirarBotao = btnAtirouAgora;
        btnAtirouAgora = false;

        if ((atirarTeclado || atirarBotao) && timerTiro <= 0f)
        {
            Atirar();
            timerTiro = cooldownTiro;
        }
    }

    void Atirar()
    {
        if (bulletPrefab == null || firePoint == null) return;
        Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        if (SoundManager.instance != null) SoundManager.instance.TocarTiro();
    }

    public void PressionarEsquerda(bool pressionado) => btnEsquerdoPressionado = pressionado;
    public void PressionarDireita(bool pressionado) => btnDireitoPressionado = pressionado;
    public void PressionarAtira() => btnAtirouAgora = true;

    public void AtivarEscudo(float duracao)
    {
        CancelInvoke(nameof(DesativarEscudo));
        escudoAtivo = true;
        AtualizarVisualEscudo(true);
        if (duracao > 0f) Invoke(nameof(DesativarEscudo), duracao);
    }

    public void UsarEscudo()
    {
        CancelInvoke(nameof(DesativarEscudo));
        escudoAtivo = false;
        AtualizarVisualEscudo(false);
    }

    void DesativarEscudo()
    {
        escudoAtivo = false;
        AtualizarVisualEscudo(false);
    }

    public bool TemEscudo() => escudoAtivo;

    void CriarEscudoVisual()
    {
        escudoVisual = new GameObject("EscudoVisual");
        escudoVisual.transform.SetParent(transform, false);
        escudoVisual.transform.localPosition = Vector3.zero;
        SpriteRenderer sr = escudoVisual.AddComponent<SpriteRenderer>();
        sr.sprite = CriarSpriteCirculo();
        sr.color = new Color(0.3f, 0.8f, 1f, 0.35f);
        sr.sortingOrder = 10;
        escudoVisual.transform.localScale = Vector3.one * 2.2f;
        escudoVisual.SetActive(false);
    }

    void AtualizarVisualEscudo(bool ativo)
    {
        if (escudoVisual != null) escudoVisual.SetActive(ativo);
    }

    Sprite CriarSpriteCirculo()
    {
        int tamanho = 64;
        float centro = tamanho / 2f;
        float raio = tamanho / 2f - 2f;
        Texture2D tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        for (int x = 0; x < tamanho; x++)
            for (int y = 0; y < tamanho; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro));
                if (dist > raio) tex.SetPixel(x, y, Color.clear);
                else if (dist > raio - 4f) tex.SetPixel(x, y, new Color(0.3f, 0.8f, 1f, 1f));
                else tex.SetPixel(x, y, new Color(0.3f, 0.8f, 1f, 0.15f));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tamanho, tamanho), new Vector2(0.5f, 0.5f), 64f);
    }
}