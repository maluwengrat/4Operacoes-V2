using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PixelBackground : MonoBehaviour
{
    public enum BackgroundType
    {
        MainMenu,
        Overlay,
        GameOver,
        Stage1_Space,
        Stage2_Cave,
        Stage3_Forest,
        Stage4_City
    }

    [Header("Config")]
    public BackgroundType backgroundType = BackgroundType.MainMenu;
    public int texWidth = 320;
    public int texHeight = 180;
    public float animSpeed = 1f;
    public float worldWidth = 20f;

    private Texture2D _tex;
    private Color32[] _pixels;
    private float _time;
    private SpriteRenderer _spriteRenderer;
    private float _worldHeight;

    private static Color32 C(int r, int g, int b) => new((byte)r, (byte)g, (byte)b, 255);
    private static readonly Color32 BLACK = C(8, 8, 16);
    private static readonly Color32 WHITE = C(240, 240, 240);

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Point;
        _tex.wrapMode = TextureWrapMode.Clamp;
        _tex.anisoLevel = 0;
        _pixels = new Color32[texWidth * texHeight];
        _worldHeight = worldWidth * texHeight / texWidth;
        float ppu = texWidth / worldWidth;
        var sprite = Sprite.Create(
            _tex,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f, 0.5f),
            ppu, 0, SpriteMeshType.FullRect);
        _spriteRenderer.sprite = sprite;
        _spriteRenderer.sortingLayerName = "Background";
        _spriteRenderer.sortingOrder = -10;
        transform.localScale = Vector3.one;
        transform.position = new Vector3(0, 0, 10);
        Fill(BLACK);
        _tex.SetPixels32(_pixels);
        _tex.Apply();
    }

    private void Start()
    {
        Draw();
        _tex.SetPixels32(_pixels);
        _tex.Apply();
    }

    private void Update()
    {
        _time += Time.unscaledDeltaTime * animSpeed;
        Draw();
        if (_tex != null && _pixels != null)
        {
            _tex.SetPixels32(_pixels);
            _tex.Apply();
        }
        if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            transform.position = new Vector3(camPos.x, camPos.y, 10);
        }
    }

    private void Draw()
    {
        Fill(BLACK);
        switch (backgroundType)
        {
            case BackgroundType.MainMenu: DrawMainMenu(); break;
            case BackgroundType.Overlay: DrawOverlay(); break;
            case BackgroundType.GameOver: DrawGameOver(); break;
            case BackgroundType.Stage1_Space: DrawStage1(); break;
            case BackgroundType.Stage2_Cave: DrawStage2(); break;
            case BackgroundType.Stage3_Forest: DrawStage3(); break;
            case BackgroundType.Stage4_City: DrawStage4(); break;
        }
    }

    // ───────────────────── helpers ─────────────────────
    private void SetPixel(int x, int y, Color32 c)
    {
        if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return;
        _pixels[y * texWidth + x] = c;
    }

    private Color32 GetPixel(int x, int y)
    {
        if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) return BLACK;
        return _pixels[y * texWidth + x];
    }

    private void Fill(Color32 c)
    {
        for (int i = 0; i < _pixels.Length; i++) _pixels[i] = c;
    }

    private static float Hash(float x, float y) =>
        Mathf.Abs(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f % 1f);

    private static float Noise(float x, float y)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        float a = Hash(ix, iy), b = Hash(ix + 1, iy);
        float c2 = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c2, d, ux), uy);
    }

    private static float FBM(float x, float y, int oct = 4)
    {
        float v = 0f, amp = 0.5f, freq = 1f, max = 0f;
        for (int i = 0; i < oct; i++)
        {
            v += Noise(x * freq, y * freq) * amp;
            max += amp;
            amp *= 0.5f;
            freq *= 2.1f;
        }
        return v / max;
    }

    private static Color32 Lerp32(Color32 a, Color32 b, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color32(
            (byte)(a.r + (b.r - a.r) * t),
            (byte)(a.g + (b.g - a.g) * t),
            (byte)(a.b + (b.b - a.b) * t), 255);
    }

    private static Color32 Add32(Color32 a, Color32 b, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color32(
            (byte)Mathf.Min(255, a.r + (int)(b.r * t)),
            (byte)Mathf.Min(255, a.g + (int)(b.g * t)),
            (byte)Mathf.Min(255, a.b + (int)(b.b * t)), 255);
    }

    // ═══════════════════════════════════════════════════════
    // MAIN MENU
    // ═══════════════════════════════════════════════════════
    private void DrawMainMenu()
    {
        // ── Paleta ──────────────────────────────────────────────
        Color32 deepSpace = C(3, 4, 14);
        Color32 midSpace = C(8, 10, 28);
        Color32 nebOrange = C(180, 70, 10);
        Color32 nebPurple = C(60, 20, 100);
        Color32 nebBlue = C(15, 30, 90);
        Color32 starW = C(245, 245, 255);
        Color32 starWarm = C(255, 200, 120);
        Color32 starOrange = C(255, 130, 40);
        Color32 starCool = C(160, 200, 255);

        // ── Fundo gradiente radial ───────────────────────────────
        float cx = texWidth * 0.5f, cy = texHeight * 0.45f;
        float maxR = Mathf.Sqrt(cx * cx + cy * cy);
        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float dx = x - cx, dy2 = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy2 * dy2) / maxR;
                SetPixel(x, y, Lerp32(midSpace, deepSpace, r * 0.9f));
            }

        // ── Nebulosa laranja (canto inferior direito) ────────────
        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float nx = x / (float)texWidth;
                float ny = y / (float)texHeight;
                float n1 = FBM(nx * 3f + _time * 0.005f, ny * 2.5f, 4);
                float n2 = FBM(nx * 2f - _time * 0.003f, ny * 3f, 3);
                float maskBR = Mathf.Clamp01((nx - 0.3f) * 2f) * Mathf.Clamp01((1f - ny - 0.1f) * 2f);
                float maskTL = Mathf.Clamp01((1f - nx - 0.2f) * 2f) * Mathf.Clamp01((ny - 0.4f) * 2f);
                Color32 px2 = GetPixel(x, y);
                px2 = Lerp32(px2, nebOrange, n1 * maskBR * 0.55f);
                px2 = Lerp32(px2, nebPurple, n2 * maskTL * 0.45f);
                px2 = Lerp32(px2, nebBlue, n1 * n2 * 0.3f);
                SetPixel(x, y, px2);
            }

        // ── Planeta laranja (canto superior direito) ─────────────
        int pR = 38, pX = (int)(texWidth * 0.82f), pY = (int)(texHeight * 0.72f);
        // Brilho ao redor
        for (int y = pY - pR * 2; y <= pY + pR * 2; y++)
            for (int x = pX - pR * 2; x <= pX + pR * 2; x++)
            {
                float d = Mathf.Sqrt((x - pX) * (x - pX) + (y - pY) * (y - pY));
                if (d > pR && d < pR * 2f)
                {
                    float glow = (1f - (d - pR) / pR) * 0.25f;
                    SetPixel(x, y, Lerp32(GetPixel(x, y), nebOrange, glow));
                }
            }
        // Corpo do planeta
        for (int y = pY - pR; y <= pY + pR; y++)
            for (int x = pX - pR; x <= pX + pR; x++)
            {
                float d = Mathf.Sqrt((x - pX) * (x - pX) + (y - pY) * (y - pY));
                if (d > pR) continue;
                float t = d / pR;
                // Base laranja com variação de noise
                float nx = (x - pX) / (float)pR;
                float ny2 = (y - pY) / (float)pR;
                float n = Noise(nx * 3f + _time * 0.02f, ny2 * 2f);
                Color32 base1 = Lerp32(C(255, 120, 30), C(200, 60, 0), t * 0.7f + n * 0.3f);
                // Listras atmosféricas
                float stripe = Mathf.Sin(ny2 * 8f + n * 2f) * 0.15f;
                base1 = Lerp32(base1, C(255, 160, 60), Mathf.Max(0, stripe));
                base1 = Lerp32(base1, C(140, 40, 0), Mathf.Max(0, -stripe));
                // Sombra lateral esquerda
                float shadow = Mathf.Clamp01((-nx + 0.3f) * 1.5f);
                base1 = Lerp32(base1, C(15, 5, 0), shadow * 0.75f);
                // Highlight direito
                float highlight = Mathf.Clamp01((nx + ny2 * 0.3f - 0.4f) * 2f) * (1f - t * 0.8f);
                base1 = Lerp32(base1, C(255, 210, 120), highlight * 0.35f);
                SetPixel(x, y, base1);
            }
        // Anel do planeta
        int ringY = pY + 6;
        for (int x = pX - (int)(pR * 1.7f); x <= pX + (int)(pR * 1.7f); x++)
        {
            float dx2 = (x - pX) / (float)pR;
            float ringD = Mathf.Abs(dx2);
            if (ringD < 0.55f || ringD > 1.65f) continue;
            float d = Mathf.Sqrt((x - pX) * (x - pX) + (ringY - pY) * (ringY - pY));
            bool insidePlanet = d < pR * 0.95f;
            if (!insidePlanet)
            {
                float fade = 1f - Mathf.Abs(ringD - 1.1f) * 1.8f;
                fade = Mathf.Clamp01(fade) * 0.7f;
                SetPixel(x, ringY, Lerp32(GetPixel(x, ringY), C(200, 100, 30), fade));
                SetPixel(x, ringY + 1, Lerp32(GetPixel(x, ringY + 1), C(180, 80, 20), fade * 0.5f));
                SetPixel(x, ringY - 1, Lerp32(GetPixel(x, ringY - 1), C(160, 70, 15), fade * 0.4f));
            }
        }

        // ── Estrelas pequenas (fundo) ────────────────────────────
        for (int i = 0; i < 600; i++)
        {
            float h1 = Hash(i * 1.31f, 0f), h2 = Hash(i * 1.31f, 1f), h3 = Hash(i * 1.31f, 2f);
            int sx = (int)(h1 * texWidth), sy = (int)(h2 * texHeight);
            float twinkle = Mathf.Sin(_time * (0.4f + h3 * 1.2f) + i * 2.1f) * 0.15f + 0.85f;
            Color32 sc = h3 > 0.93f ? starWarm : h3 > 0.85f ? starOrange : h3 > 0.60f ? starCool : starW;
            SetPixel(sx, sy, Lerp32(GetPixel(sx, sy), sc, twinkle * 0.4f));
        }

        // ── Estrelas médias ──────────────────────────────────────
        for (int i = 0; i < 200; i++)
        {
            float h1 = Hash(i * 2.53f, 5f), h2 = Hash(i * 2.53f, 6f), h3 = Hash(i * 2.53f, 7f);
            int sx = (int)(h1 * texWidth), sy = (int)(h2 * texHeight);
            float twinkle = Mathf.Sin(_time * (0.8f + h3 * 2f) + i * 1.5f) * 0.25f + 0.75f;
            Color32 sc = h3 > 0.87f ? starWarm : h3 > 0.68f ? starOrange : starCool;
            float bright = twinkle * (0.5f + h3 * 0.5f);
            SetPixel(sx, sy, Lerp32(GetPixel(sx, sy), sc, bright));
            if (h3 > 0.75f)
            {
                float cr = twinkle * 0.3f;
                SetPixel(sx + 1, sy, Lerp32(GetPixel(sx + 1, sy), sc, cr));
                SetPixel(sx - 1, sy, Lerp32(GetPixel(sx - 1, sy), sc, cr));
                SetPixel(sx, sy + 1, Lerp32(GetPixel(sx, sy + 1), sc, cr));
                SetPixel(sx, sy - 1, Lerp32(GetPixel(sx, sy - 1), sc, cr));
            }
        }

        // ── Estrelas grandes com brilho em cruz ─────────────────
        for (int i = 0; i < 35; i++)
        {
            float h1 = Hash(i * 6.11f, 10f), h2 = Hash(i * 6.11f, 11f), h3 = Hash(i * 6.11f, 12f);
            int sx = (int)(h1 * texWidth), sy = (int)(h2 * texHeight);
            // Evita área do planeta
            float dpx = (sx - pX) / (float)pR, dpy = (sy - pY) / (float)pR;
            if (dpx * dpx + dpy * dpy < 4f) continue;
            float twinkle = Mathf.Sin(_time * (1.2f + h3 * 3.5f) + i) * 0.35f + 0.65f;
            Color32 sc = h3 > 0.55f ? starWarm : starOrange;
            SetPixel(sx, sy, Lerp32(GetPixel(sx, sy), sc, twinkle));
            for (int r2 = 1; r2 <= 6; r2++)
            {
                float sp = twinkle * (1f - r2 / 7f) * 0.7f;
                SetPixel(sx + r2, sy, Lerp32(GetPixel(sx + r2, sy), sc, sp));
                SetPixel(sx - r2, sy, Lerp32(GetPixel(sx - r2, sy), sc, sp));
                SetPixel(sx, sy + r2, Lerp32(GetPixel(sx, sy + r2), sc, sp));
                SetPixel(sx, sy - r2, Lerp32(GetPixel(sx, sy - r2), sc, sp));
            }
        }

        // ── Cometa ───────────────────────────────────────────────
        {
            float speed = 12f;
            float ct = (_time * speed + 60f) % (texWidth + 160f);
            int cometX = (int)(ct - 80f);
            int cometY = (int)(texHeight * 0.65f + ct * 0.04f);
            int tailLen = 80;
            for (int t2 = 0; t2 < tailLen; t2++)
            {
                float tf = t2 / (float)tailLen;
                float fade = (1f - tf) * (1f - tf);
                int tx = cometX - t2, ty = cometY - (int)(t2 * 0.04f);
                Color32 tc2 = Lerp32(starWarm, Lerp32(nebOrange, deepSpace, tf * 0.9f), tf);
                SetPixel(tx, ty, Lerp32(GetPixel(tx, ty), tc2, fade * 0.9f));
                SetPixel(tx, ty + 1, Lerp32(GetPixel(tx, ty + 1), tc2, fade * 0.4f));
                SetPixel(tx, ty - 1, Lerp32(GetPixel(tx, ty - 1), tc2, fade * 0.3f));
            }
            float headPulse = Mathf.Sin(_time * 8f) * 0.1f + 0.9f;
            SetPixel(cometX, cometY, Lerp32(starWarm, starW, headPulse * 0.7f));
            for (int r2 = 1; r2 <= 5; r2++)
            {
                float gf = (1f - r2 / 6f) * headPulse * 0.55f;
                for (int a2 = 0; a2 < 12; a2++)
                {
                    float ang = a2 / 12f * Mathf.PI * 2f;
                    int hx = cometX + (int)(Mathf.Cos(ang) * r2);
                    int hy = cometY + (int)(Mathf.Sin(ang) * r2);
                    SetPixel(hx, hy, Lerp32(GetPixel(hx, hy), starWarm, gf));
                }
            }
        }

        // ── Scanlines e vinheta ──────────────────────────────────
        for (int y = 0; y < texHeight; y += 3)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), deepSpace, 0.05f));

        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float ex = (x / (float)texWidth - 0.5f) * 2f;
                float ey = (y / (float)texHeight - 0.5f) * 2f;
                float vig = Mathf.Clamp01(ex * ex * 0.4f + ey * ey * 0.5f - 0.2f) * 0.65f;
                if (vig > 0.01f)
                    SetPixel(x, y, Lerp32(GetPixel(x, y), deepSpace, vig));
            }
    }

    // ═══════════════════════════════════════════════════════
    // OVERLAY — Como Jogar / Pause / Próxima Fase
    // ═══════════════════════════════════════════════════════
    private void DrawOverlay()
    {
        Color32 bg = C(4, 6, 18);
        Color32 mid = C(10, 14, 35);
        Color32 line = C(22, 35, 70);
        Color32 bright = C(40, 65, 130);

        // Base com gradiente radial suave
        float cx = texWidth * 0.5f, cy = texHeight * 0.5f;
        float maxR = Mathf.Sqrt(cx * cx + cy * cy);
        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float dx = x - cx, dy2 = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy2 * dy2) / maxR;
                SetPixel(x, y, Lerp32(mid, bg, r * r));
            }

        // Linhas horizontais pulsantes lentas
        for (int y = 0; y < texHeight; y += 18)
            for (int x = 0; x < texWidth; x++)
            {
                float pulse = Mathf.Sin(_time * 0.4f + x * 0.02f) * 0.08f + 0.92f;
                SetPixel(x, y, Lerp32(GetPixel(x, y), line, 0.75f * pulse));
            }

        // Linhas verticais
        for (int x = 0; x < texWidth; x += 28)
            for (int y = 0; y < texHeight; y++)
            {
                float pulse = Mathf.Sin(_time * 0.3f + y * 0.025f) * 0.08f + 0.92f;
                SetPixel(x, y, Lerp32(GetPixel(x, y), line, 0.70f * pulse));
            }

        // Nós nas interseções
        for (int y = 0; y < texHeight; y += 18)
            for (int x = 0; x < texWidth; x += 28)
            {
                float pulse = Mathf.Sin(_time * 1.2f + x * 0.15f + y * 0.2f) * 0.3f + 0.7f;
                SetPixel(x, y, Lerp32(line, bright, pulse));
            }

        // Scanlines
        for (int y = 0; y < texHeight; y += 3)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), bg, 0.08f));

        // Vinheta
        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float ex = (x / (float)texWidth - 0.5f) * 2f;
                float ey = (y / (float)texHeight - 0.5f) * 2f;
                float vig = Mathf.Clamp01(ex * ex * 0.5f + ey * ey * 0.6f - 0.2f) * 0.60f;
                if (vig > 0.01f)
                    SetPixel(x, y, Lerp32(GetPixel(x, y), bg, vig));
            }
    }

    // ═══════════════════════════════════════════════════════
    // GAME OVER
    // ═══════════════════════════════════════════════════════
    private void DrawGameOver()
    {
        Color32 bg1 = C(8, 0, 0);
        Color32 bg2 = C(30, 5, 5);
        Color32 red1 = C(200, 20, 0);
        Color32 red2 = C(255, 80, 40);
        Color32 blood = C(100, 0, 0);

        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(bg1, bg2, y / (float)texHeight));

        int seed = Mathf.FloorToInt(_time * 8f);
        for (int i = 0; i < 6; i++)
        {
            float h = Hash(i + seed, 9.9f);
            int gy = (int)(h * texHeight), gh = (int)(Hash(i + seed, 5.5f) * 8) + 1;
            int gx = (int)((Hash(i + seed, 3.3f) - 0.5f) * 20);
            for (int dy = 0; dy < gh; dy++)
                for (int x = 0; x < texWidth; x++)
                {
                    int sy = gy + dy;
                    if (sy < 0 || sy >= texHeight) continue;
                    SetPixel(x, sy, Lerp32(
                        GetPixel(Mathf.Clamp(x + gx, 0, texWidth - 1), sy), red1, 0.6f));
                }
        }
        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float dx2 = (x / (float)texWidth - 0.5f) * 2f, dy2 = (y / (float)texHeight - 0.5f) * 2f;
                SetPixel(x, y, Lerp32(GetPixel(x, y), blood,
                    Mathf.Clamp01(dx2 * dx2 + dy2 * dy2) * 0.7f));
            }
        for (int y = 0; y < texHeight; y += 2)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), BLACK, 0.25f));

        float ep = Mathf.Sin(_time * 5f) * 0.5f + 0.5f;
        for (int x = 0; x < texWidth; x++)
            for (int h = 0; h < 4; h++)
            {
                SetPixel(x, h, Lerp32(GetPixel(x, h), red2, ep));
                SetPixel(x, texHeight - 1 - h, Lerp32(GetPixel(x, texHeight - 1 - h), red2, ep));
            }
    }

    // ═══════════════════════════════════════════════════════
    // STAGE 1 — CIDADE AO ENTARDECER
    // ═══════════════════════════════════════════════════════
    private void DrawStage1()
    {
        Color32 skyTop = C(20, 15, 50);
        Color32 skyMid = C(220, 90, 40);
        Color32 skyLow = C(255, 160, 60);
        Color32 sunColor = C(255, 230, 100);
        Color32 bldDark = C(18, 14, 30);
        Color32 bldMid = C(28, 22, 45);
        Color32 winOn = C(255, 200, 100);
        Color32 winOff = C(35, 28, 50);
        Color32 groundCol = C(15, 12, 25);
        Color32 roadLine = C(50, 45, 70);
        Color32 neonBlue = C(80, 180, 255);
        Color32 neonRed = C(255, 60, 60);
        int groundY = 22;

        for (int y = groundY; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float ny = (y - groundY) / (float)(texHeight - groundY);
                Color32 sky;
                if (ny < 0.35f) sky = Lerp32(skyLow, skyMid, ny / 0.35f);
                else sky = Lerp32(skyMid, skyTop, (ny - 0.35f) / 0.65f);
                sky = Lerp32(sky, skyLow, Mathf.Exp(-ny * 5f) * (Mathf.Sin(_time * 0.4f) * 0.1f + 0.9f) * 0.25f);
                SetPixel(x, y, sky);
            }

        int sunX = texWidth / 2, sunY = groundY + 8, sunR = 14;
        for (int y = sunY - sunR; y <= sunY + sunR; y++)
            for (int x = sunX - sunR * 2; x <= sunX + sunR * 2; x++)
            {
                float dx2 = (x - sunX) / 2f, dy2 = y - sunY;
                float d = Mathf.Sqrt(dx2 * dx2 + dy2 * dy2);
                if (d < sunR) SetPixel(x, y, Lerp32(skyMid, sunColor, 1f - d / sunR));
                else if (d < sunR * 2.2f)
                    SetPixel(x, y, Lerp32(GetPixel(x, y), sunColor,
                        (1f - (d - sunR) / (sunR * 1.2f)) * 0.18f));
            }

        int[] bwBack = { 18, 14, 22, 16, 20, 13, 24, 15, 18, 17, 21 };
        int[] bhBack = { 38, 52, 30, 60, 44, 58, 36, 50, 55, 34, 50 };
        int bxB = 0;
        for (int i = 0; i < bwBack.Length; i++)
        {
            DrawMenuBuilding(bxB, bwBack[i], bhBack[i], groundY, Lerp32(bldDark, BLACK, 0.4f), winOff, winOn, false);
            bxB += bwBack[i] + 1;
        }
        int[] bwFront = { 22, 17, 28, 19, 24, 16, 30, 18, 22, 20 };
        int[] bhFront = { 55, 72, 42, 80, 60, 76, 48, 65, 70, 52 };
        int bxF = -4;
        for (int i = 0; i < bwFront.Length; i++)
        {
            DrawMenuBuilding(bxF, bwFront[i], bhFront[i], groundY, bldMid, winOff, winOn, true);
            bxF += bwFront[i] + 2;
        }

        for (int y = 0; y < groundY; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float t = y / (float)groundY;
                Color32 g = Lerp32(groundCol, Lerp32(groundCol, skyLow, 0.15f), t);
                g = Lerp32(g, sunColor, Mathf.Exp(-Mathf.Abs(x - sunX) * 0.04f) * t * 0.12f);
                SetPixel(x, y, g);
            }
        for (int x = 0; x < texWidth; x++) SetPixel(x, groundY, roadLine);

        int dashY = groundY / 2;
        for (int x = 0; x < texWidth; x++)
        {
            int xOff = (x + (int)(_time * 60f)) % 20;
            if (xOff < 10) SetPixel(x, dashY, C(200, 180, 80));
        }

        DrawCar((int)((_time * 55f) % (texWidth + 30) - 10), 8, 1, neonBlue, winOn);
        DrawCar((int)(texWidth - (_time * 38f) % (texWidth + 30) + 10), 14, -1, C(60, 200, 120), C(255, 80, 80));
        DrawCar((int)((_time * 30f + 180f) % (texWidth + 30) - 10), 6, 1, neonRed, C(200, 200, 60));

        for (int y = 0; y < texHeight; y += 2)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), BLACK, 0.10f));
    }

    private void DrawMenuBuilding(int startX, int width, int height, int groundY,
        Color32 wall, Color32 winOff, Color32 winOn, bool drawWindows)
    {
        for (int h = 0; h < height; h++)
            for (int dx = 0; dx < width; dx++)
            {
                int x = startX + dx, y = groundY + h;
                if (x < 0 || x >= texWidth || y >= texHeight) continue;
                float edge = Mathf.Abs((dx - width * 0.5f) / (width * 0.5f));
                SetPixel(x, y, Lerp32(wall, Lerp32(wall, BLACK, 0.5f), edge * 0.4f));
            }
        if (!drawWindows) return;
        int winW = 3, winH = 3, gapX = 4, gapY = 4;
        for (int wy = gapY; wy < height - gapY; wy += winH + gapY)
            for (int wx = startX + gapX; wx < startX + width - gapX; wx += winW + gapX)
            {
                float chance = Hash(wx * 3.7f, wy * 6.1f + Mathf.Floor(_time * 0.2f));
                Color32 wc = winOff;
                if (chance > 0.45f)
                {
                    float flk = Mathf.Sin(_time * (2f + chance * 8f) + wx + wy) * 0.1f + 0.9f;
                    wc = Lerp32(winOff, winOn, (chance - 0.45f) * 2f * flk);
                }
                for (int dy = 0; dy < winH; dy++)
                    for (int dx = 0; dx < winW; dx++)
                    {
                        int px2 = wx + dx, py2 = (groundY + wy) + dy;
                        if (px2 >= 0 && px2 < texWidth && py2 < texHeight)
                            SetPixel(px2, py2, wc);
                    }
            }
    }

    private void DrawCar(int cx, int cy, int dir, Color32 bodyColor, Color32 headlightColor)
    {
        Color32 windowC = C(120, 200, 255), tireC = C(20, 20, 20), rimC = C(160, 160, 160), tailC = C(255, 40, 20);
        for (int dy = 0; dy < 5; dy++)
            for (int dx = -8; dx <= 8; dx++)
                SetPixel(cx + dx, cy + dy, Lerp32(bodyColor, BLACK, dy == 4 ? 0.5f : 0f));
        for (int dy = 5; dy < 9; dy++)
        {
            int hw = 5 - (dy - 5), shiftX = dir * 1;
            for (int dx = -hw + shiftX; dx <= hw + shiftX; dx++)
                SetPixel(cx + dx, cy + dy, Lerp32(bodyColor, windowC, 0.3f));
        }
        for (int dy = 5; dy < 9; dy++)
        {
            int hw = 3 - (dy - 5) / 2;
            for (int dx = -hw; dx <= hw; dx++) SetPixel(cx + dx, cy + dy, windowC);
        }
        int fX = cx + dir * 8;
        SetPixel(fX, cy + 2, headlightColor); SetPixel(fX, cy + 3, headlightColor);
        SetPixel(fX + dir, cy + 2, Lerp32(headlightColor, WHITE, 0.5f));
        SetPixel(fX + dir, cy + 3, Lerp32(headlightColor, WHITE, 0.5f));
        int rX = cx - dir * 8;
        SetPixel(rX, cy + 2, tailC); SetPixel(rX, cy + 3, tailC);
        foreach (int wo in new[] { -5, 5 })
        {
            int wx = cx + wo;
            for (int r = -2; r <= 2; r++)
                for (int c2 = -2; c2 <= 2; c2++)
                    if (r * r + c2 * c2 <= 4) SetPixel(wx + c2, cy - 1 + r, tireC);
            SetPixel(wx, cy, rimC);
        }
    }

    // ═══════════════════════════════════════════════════════
    // STAGE 2 — CAVERNA / CRISTAIS
    // ═══════════════════════════════════════════════════════
    private void DrawStage2()
    {
        Color32 bgDeep = C(4, 3, 8);
        Color32 bgMid = C(10, 8, 18);
        Color32 rockDark = C(18, 14, 22);
        Color32 rockMid = C(35, 28, 42);
        Color32 rockLight = C(60, 50, 70);
        Color32 crystalB = C(40, 120, 200);
        Color32 crystalG = C(40, 200, 120);
        Color32 crystalP = C(180, 60, 255);
        Color32 glowB = C(60, 160, 255);
        Color32 glowG = C(60, 255, 160);
        Color32 glowP = C(220, 100, 255);
        Color32 lavaCol = C(220, 80, 10);
        Color32 lavaLight = C(255, 160, 40);
        Color32 dripCol = C(80, 180, 200);
        Color32 mushRed = C(200, 40, 30);
        Color32 mushWhite = C(230, 220, 210);
        Color32 dustCol2 = C(150, 130, 180);

        for (int y = 0; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float nx = x / (float)texWidth, ny = y / (float)texHeight;
                Color32 bg = bgDeep;
                bg = Lerp32(bg, bgMid, Noise(nx * 4f + _time * 0.01f, ny * 3f) * 0.6f);
                bg = Lerp32(bg, rockDark, Noise(nx * 8f, ny * 7f + _time * 0.008f) * 0.3f);
                SetPixel(x, y, bg);
            }

        for (int y = 0; y < texHeight; y++)
        {
            float noiseL = Noise(0.1f, y * 0.12f + _time * 0.005f);
            float noiseR = Noise(0.9f, y * 0.12f - _time * 0.005f);
            int wallL = (int)(18 + noiseL * 22f), wallR = (int)(18 + noiseR * 22f);
            for (int x = 0; x < wallL; x++)
            {
                float t = x / (float)wallL;
                SetPixel(x, y, Lerp32(rockDark, Lerp32(rockMid, rockLight, t), t));
            }
            for (int x = texWidth - wallR; x < texWidth; x++)
            {
                float t = (texWidth - x) / (float)wallR;
                SetPixel(x, y, Lerp32(rockDark, Lerp32(rockMid, rockLight, t), t));
            }
        }

        for (int i = 0; i < 18; i++)
        {
            float h = Hash(i * 3.7f, 0f), h2 = Hash(i * 3.7f, 1f);
            int sx = (int)(h * texWidth), sh = (int)(10 + h2 * 28f), sw = (int)(3 + h2 * 6f);
            for (int dy = 0; dy < sh; dy++)
            {
                int hw = Mathf.Max(1, (int)(sw * (1f - (float)dy / sh)));
                for (int dx = -hw; dx <= hw; dx++)
                    SetPixel(sx + dx, texHeight - 1 - dy,
                        Lerp32(rockDark, rockMid, 1f - Mathf.Abs(dx) / (float)(hw + 1)));
            }
            float dripT = (_time * (0.3f + h * 0.4f) + h2 * 5f) % 1f;
            int dripY = texHeight - 1 - sh - (int)(dripT * 20f);
            if (dripY > 0) SetPixel(sx, dripY, Lerp32(dripCol, bgDeep, dripT));
        }

        for (int i = 0; i < 14; i++)
        {
            float h = Hash(i * 5.1f + 99f, 0f), h2 = Hash(i * 5.1f + 99f, 1f);
            int sx = (int)(h * texWidth), sh = (int)(6 + h2 * 20f), sw = (int)(2 + h2 * 5f);
            for (int dy = 0; dy < sh; dy++)
            {
                int hw = Mathf.Max(1, (int)(sw * (1f - (float)dy / sh)));
                for (int dx = -hw; dx <= hw; dx++)
                    SetPixel(sx + dx, dy, Lerp32(rockDark, rockMid, 1f - Mathf.Abs(dx) / (float)(hw + 1)));
            }
        }

        int[][] crystals2 = {
            new[]{50,40,0},new[]{95,55,1},new[]{160,35,2},
            new[]{210,60,0},new[]{265,42,1},new[]{texWidth-55,50,2},
            new[]{130,30,0},new[]{texWidth-90,38,1}
        };
        foreach (var cr in crystals2)
        {
            int crx = cr[0], cry = cr[1], cType = cr[2];
            Color32 cc = cType == 0 ? crystalB : (cType == 1 ? crystalG : crystalP);
            Color32 cg = cType == 0 ? glowB : (cType == 1 ? glowG : glowP);
            float pulse = Mathf.Sin(_time * (1.5f + cType * 0.5f) + crx * 0.1f) * 0.4f + 0.6f;
            int ch = (int)(12 + Hash(crx, cry) * 16f), cw = (int)(3 + Hash(crx, cry + 1) * 4f);
            for (int dy = 0; dy < ch; dy++)
            {
                float t = dy / (float)ch;
                int hw = Mathf.Max(1, (int)(cw * Mathf.Sin(t * Mathf.PI)));
                for (int dx = -hw; dx <= hw; dx++)
                {
                    float edge = 1f - Mathf.Abs(dx) / (float)(hw + 1);
                    SetPixel(crx + dx, cry + dy,
                        Lerp32(Lerp32(cc, C(220, 240, 255), edge * 0.5f), bgDeep, t * 0.3f));
                }
            }
            for (int dy = -5; dy <= ch + 5; dy++)
                for (int dx = -6; dx <= 6; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + Mathf.Pow(dy - ch / 2f, 2));
                    float fade = Mathf.Clamp01(1f - d / 8f) * pulse * 0.4f;
                    if (fade > 0.05f)
                        SetPixel(crx + dx, cry + dy, Lerp32(GetPixel(crx + dx, cry + dy), cg, fade));
                }
        }

        int lavaY = 12;
        for (int y = 0; y < lavaY; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float wave = Mathf.Sin(x * 0.1f + _time * 1.5f) * 2f + Mathf.Sin(x * 0.05f - _time) * 1.5f;
                float t = (y + wave) / (float)lavaY;
                SetPixel(x, y, Lerp32(lavaCol, lavaLight, Noise(x * 0.1f + _time * 0.5f, y * 0.2f) * (1f - t)));
            }
        for (int y = lavaY; y < lavaY + 12; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float fade = 1f - (y - lavaY) / 12f;
                float flicker = Mathf.Sin(_time * 3f + x * 0.2f) * 0.2f + 0.8f;
                SetPixel(x, y, Lerp32(GetPixel(x, y), lavaCol, fade * flicker * 0.35f));
            }

        int[] mushX2 = { 70, 140, 220, texWidth - 60, texWidth - 120 };
        for (int i = 0; i < mushX2.Length; i++)
        {
            int mx = mushX2[i], my = lavaY + 2;
            float pulse2 = Mathf.Sin(_time * 2f + i * 1.3f) * 0.3f + 0.7f;
            Color32 mc = i % 2 == 0 ? mushRed : C(180, 60, 200);
            for (int dy = 0; dy < 6; dy++) SetPixel(mx, my + dy, mushWhite);
            for (int dy = 0; dy < 5; dy++)
            {
                int hw = 7 - dy;
                for (int dx = -hw; dx <= hw; dx++)
                    SetPixel(mx + dx, my + 6 + dy, Lerp32(mc, mushWhite, (1f - Mathf.Abs(dx) / (float)(hw + 1)) * 0.3f));
            }
            SetPixel(mx - 2, my + 8, mushWhite); SetPixel(mx + 2, my + 8, mushWhite); SetPixel(mx, my + 9, mushWhite);
            for (int dy = -3; dy <= 10; dy++)
                for (int dx = -10; dx <= 10; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float fade = Mathf.Clamp01(1f - d / 10f) * pulse2 * 0.3f;
                    if (fade > 0.05f) SetPixel(mx + dx, my + dy, Lerp32(GetPixel(mx + dx, my + dy), mc, fade));
                }
        }

        for (int i = 0; i < 25; i++)
        {
            float h1 = Hash(i * 2.3f, 6f), h2 = Hash(i * 2.3f, 7f);
            float ft = (_time * (5f + h2 * 8f) + h1 * 10f) % 1f;
            int px2 = (int)(h1 * texWidth + Mathf.Sin(_time + i) * 5f), py2 = (int)(ft * texHeight);
            SetPixel(px2, py2, Lerp32(GetPixel(px2, py2), dustCol2, Mathf.Sin(ft * Mathf.PI) * 0.4f));
        }

        for (int y = 0; y < texHeight; y += 2)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), bgDeep, 0.10f));
    }

    // ═══════════════════════════════════════════════════════
    // STAGE 3 — OCEANO
    // ═══════════════════════════════════════════════════════
    private void DrawStage3()
    {
        Color32 waterDeep = C(0, 15, 50);
        Color32 waterMid = C(0, 40, 90);
        Color32 waterLight = C(10, 80, 130);
        Color32 waterSurf = C(20, 120, 160);
        Color32 foamCol = C(180, 230, 255);
        Color32 sandCol = C(180, 140, 60);
        Color32 sandLight = C(210, 175, 90);
        Color32 coralRed = C(220, 60, 40);
        Color32 coralPink = C(255, 120, 150);
        Color32 coralYellow = C(220, 180, 30);
        Color32 seaweedA = C(10, 100, 40);
        Color32 seaweedB = C(20, 140, 60);
        Color32 fishA = C(255, 140, 0);
        Color32 fishB = C(80, 200, 255);
        Color32 jellyCol = C(200, 100, 255);
        Color32 bubbleCol = C(150, 220, 255);
        int surfaceY = texHeight - 18, sandY = 14;

        for (int y = sandY; y < texHeight; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float ny = (y - sandY) / (float)(texHeight - sandY);
                Color32 wc = Lerp32(waterDeep, waterMid, ny * 0.6f);
                wc = Lerp32(wc, waterLight, Mathf.Max(0, ny - 0.4f) * 1.5f);
                float caus = Noise(x * 0.08f + _time * 0.3f, y * 0.06f - _time * 0.1f);
                float caus2 = Noise(x * 0.05f - _time * 0.2f, y * 0.09f + _time * 0.15f);
                wc = Lerp32(wc, waterSurf, (caus + caus2) * 0.5f * ny * 0.5f);
                SetPixel(x, y, wc);
            }

        for (int x = 0; x < texWidth; x++)
        {
            float wave1 = Mathf.Sin(x * 0.15f + _time * 2f) * 2f, wave2 = Mathf.Sin(x * 0.08f - _time * 1.3f) * 1.5f;
            int waveY = surfaceY + (int)(wave1 + wave2);
            for (int dy2 = 0; dy2 < 4; dy2++)
                SetPixel(x, waveY + dy2, dy2 == 0 ? foamCol : Lerp32(waterSurf, waterMid, dy2 / 4f));
            if (Mathf.Sin(x * 0.15f + _time * 2f) > 0.7f)
                SetPixel(x, waveY - 1, Lerp32(foamCol, waterSurf, 0.4f));
        }

        for (int y = 0; y < sandY; y++)
            for (int x = 0; x < texWidth; x++)
            {
                float t = y / (float)sandY;
                Color32 sc = Lerp32(sandLight, sandCol, t);
                sc = Lerp32(sc, C(150, 115, 45), Noise(x * 0.3f, y * 0.3f) * 0.3f);
                SetPixel(x, y, sc);
            }

        for (int i = 0; i < 18; i++)
        {
            float h = Hash(i * 3.1f, 1f), h2 = Hash(i * 3.1f, 2f);
            int crx = (int)(h * texWidth), ch = (int)(8 + h2 * 20);
            Color32 cc = i % 3 == 0 ? coralRed : (i % 3 == 1 ? coralPink : coralYellow);
            float sway = Mathf.Sin(_time * (0.8f + h * 0.5f) + i) * 1.5f;
            for (int s = 0; s < ch; s++)
                SetPixel(crx + (int)(sway * s / ch), sandY + s, Lerp32(cc, C(150, 30, 20), s / (float)ch));
            for (int b = 0; b < 4; b++)
            {
                float bh2 = Hash(i * 3.1f + b, 5f);
                int bLen = (int)(3 + bh2 * 6), bStart = (int)(ch * (0.4f + b * 0.15f)), bDir = b % 2 == 0 ? 1 : -1;
                for (int s = 0; s < bLen; s++)
                    SetPixel(crx + (int)(sway * bStart / ch) + s * bDir, sandY + bStart + s / 2,
                        Lerp32(cc, foamCol, s / (float)bLen * 0.3f));
            }
        }

        for (int i = 0; i < 12; i++)
        {
            float h = Hash(i * 7.3f, 9f);
            int sx = (int)(h * texWidth), sh2 = (int)(12 + h * 22);
            for (int s = 0; s < sh2; s++)
            {
                float sway = Mathf.Sin(_time * (1f + h * 0.5f) + s * 0.3f + i) * 3f;
                int px2 = sx + (int)sway, py2 = sandY + s;
                Color32 wc2 = Lerp32(seaweedA, seaweedB, Mathf.Sin(s * 0.3f) * 0.5f + 0.5f);
                SetPixel(px2, py2, wc2); SetPixel(px2 + 1, py2, Lerp32(wc2, waterMid, 0.3f));
            }
        }

        for (int i = 0; i < 8; i++)
        {
            float h1 = Hash(i, 0.1f), h2 = Hash(i, 0.2f), h3 = Hash(i, 0.3f);
            float speed = 15f + h3 * 25f;
            float ft = (_time * speed * (i % 2 == 0 ? 1f : -1f) + h1 * texWidth) % (texWidth + 20);
            int fx = i % 2 == 0 ? (int)ft : (int)(texWidth - ft);
            int fy = sandY + 5 + (int)(h2 * (texHeight - sandY - 20)) + (int)(Mathf.Sin(_time * 2f + i * 1.5f) * 3f);
            Color32 fc = i % 2 == 0 ? fishA : fishB; int dir = i % 2 == 0 ? 1 : -1;
            SetPixel(fx, fy, fc); SetPixel(fx + dir, fy, fc); SetPixel(fx - dir, fy, fc);
            SetPixel(fx, fy + 1, fc); SetPixel(fx, fy - 1, fc);
            SetPixel(fx - dir * 2, fy + 1, Lerp32(fc, waterMid, 0.3f));
            SetPixel(fx - dir * 2, fy - 1, Lerp32(fc, waterMid, 0.3f));
            SetPixel(fx + dir, fy, C(10, 10, 10));
        }

        for (int i = 0; i < 4; i++)
        {
            float h1 = Hash(i * 5f, 3f), h2 = Hash(i * 5f, 4f);
            float jt = (_time * 8f + h1 * 20f) % (texHeight - sandY);
            int jx = (int)(h1 * texWidth * 0.8f + texWidth * 0.1f), jy = sandY + 5 + (int)jt;
            float pulse = Mathf.Sin(_time * 3f + i) * 0.3f + 0.7f;
            int jr = (int)(5 + pulse * 2f);
            for (int dy2 = 0; dy2 <= jr; dy2++)
                for (int dx2 = -(jr - dy2 / 2); dx2 <= (jr - dy2 / 2); dx2++)
                    SetPixel(jx + dx2, jy + dy2, Lerp32(jellyCol, bubbleCol, (float)dy2 / jr * pulse));
            for (int t = 0; t < 5; t++)
            {
                int tx = jx - 4 + t * 2;
                for (int ts = 0; ts < 8; ts++)
                {
                    float sway2 = Mathf.Sin(_time * 2f + ts * 0.5f + t) * 2f;
                    SetPixel(tx + (int)sway2, jy - ts, Lerp32(jellyCol, waterMid, ts / 8f));
                }
            }
        }

        for (int i = 0; i < 20; i++)
        {
            float h1 = Hash(i * 1.9f, 7f), h2 = Hash(i * 1.9f, 8f);
            float bt = (_time * (5f + h2 * 10f) + h1 * 10f) % (texHeight - sandY);
            int bx = (int)(h1 * texWidth) + (int)(Mathf.Sin(_time * 1.5f + i) * 2f), by = sandY + 2 + (int)bt;
            SetPixel(bx, by, Lerp32(waterMid, bubbleCol, 0.4f));
        }
    }

    // ═══════════════════════════════════════════════════════
    // STAGE 4 — ENTARDECER ENCANTADO
    // ═══════════════════════════════════════════════════════
    private void DrawStage4()
    {
        Color32 skyTop = C(45, 30, 100);
        Color32 skyMid = C(180, 80, 120);
        Color32 skyHoriz = C(255, 150, 80);
        Color32 skyLow = C(255, 200, 120);
        Color32 cloudA = C(255, 180, 160);
        Color32 cloudB = C(200, 130, 170);
        Color32 cloudSh = C(140, 80, 120);
        Color32 moonCol = C(245, 235, 195);
        Color32 moonDark = C(190, 178, 140);
        Color32 moonGlow = C(255, 240, 200);
        Color32 starCol = C(255, 240, 200);
        Color32 hillBack = C(60, 35, 80);
        Color32 hillMid = C(40, 70, 50);
        Color32 hillFront = C(25, 55, 35);
        Color32 treeTrunk = C(35, 22, 15);
        Color32 treeFol = C(20, 60, 30);
        Color32 treeFolL = C(35, 85, 45);
        Color32 birdCol = C(15, 10, 25);
        int horizonY = 36;

        for (int y = 0; y < texHeight; y++)
        {
            float ny = y / (float)texHeight;
            Color32 sky;
            if (ny < 0.25f) sky = Lerp32(skyLow, skyHoriz, ny / 0.25f);
            else if (ny < 0.55f) sky = Lerp32(skyHoriz, skyMid, (ny - 0.25f) / 0.30f);
            else sky = Lerp32(skyMid, skyTop, (ny - 0.55f) / 0.45f);
            sky = Lerp32(sky, skyHoriz, Mathf.Sin(_time * 0.25f) * 0.04f);
            for (int x = 0; x < texWidth; x++) SetPixel(x, y, sky);
        }

        for (int i = 0; i < 90; i++)
        {
            float h1 = Hash(i * 1.9f, 0f), h2 = Hash(i * 1.9f, 1f), h3 = Hash(i * 1.9f, 2f);
            int sx = (int)(h1 * texWidth), sy = (int)(h2 * texHeight * 0.45f) + (int)(texHeight * 0.55f);
            float twinkle = Mathf.Sin(_time * (1f + h3 * 3f) + i * 2.3f) * 0.4f + 0.6f;
            float vis = (sy - texHeight * 0.55f) / (texHeight * 0.45f);
            SetPixel(sx, sy, Lerp32(GetPixel(sx, sy), starCol, twinkle * vis * 0.85f));
        }

        int moonX = (int)(texWidth * 0.80f), moonY = (int)(texHeight * 0.75f), moonR = 12;
        for (int y = moonY - moonR * 2; y <= moonY + moonR * 2; y++)
            for (int x = moonX - moonR * 2; x <= moonX + moonR * 2; x++)
            {
                float d = Mathf.Sqrt((x - moonX) * (x - moonX) + (y - moonY) * (y - moonY));
                if (d > moonR && d < moonR * 2.2f)
                    SetPixel(x, y, Lerp32(GetPixel(x, y), moonGlow, (1f - (d - moonR) / (moonR * 1.2f)) * 0.22f));
            }
        for (int y = moonY - moonR; y <= moonY + moonR; y++)
            for (int x = moonX - moonR; x <= moonX + moonR; x++)
            {
                float d = Mathf.Sqrt((x - moonX) * (x - moonX) + (y - moonY) * (y - moonY));
                if (d < moonR)
                {
                    float crater = Noise((x - moonX) * 0.3f, (y - moonY) * 0.3f) * 0.4f;
                    float lit = Mathf.Clamp01(1.1f - d / moonR);
                    SetPixel(x, y, Lerp32(moonDark, moonCol, (crater + lit) * 0.8f));
                }
            }
        int biteX = moonX + 7, biteY = moonY + 2, biteR = moonR - 2;
        for (int y = moonY - moonR; y <= moonY + moonR; y++)
            for (int x = moonX - moonR; x <= moonX + moonR; x++)
            {
                float d = Mathf.Sqrt((x - moonX) * (x - moonX) + (y - moonY) * (y - moonY));
                float db = Mathf.Sqrt((x - biteX) * (x - biteX) + (y - biteY) * (y - biteY));
                if (d < moonR && db < biteR) SetPixel(x, y, GetPixel(x - 1, y));
            }

        float cl1x = (_time * 6f) % (texWidth + 80f) - 40f;
        DrawCloud4((int)cl1x, (int)(texHeight * 0.60f), 14, cloudA, cloudSh);
        float cl2x = (_time * 9f + 120f) % (texWidth + 60f) - 30f;
        DrawCloud4((int)cl2x, (int)(texHeight * 0.72f), 9, cloudB, cloudSh);
        float cl3x = texWidth - (_time * 12f + 60f) % (texWidth + 40f);
        DrawCloud4((int)cl3x, (int)(texHeight * 0.65f), 7, cloudA, cloudSh);

        for (int x = 0; x < texWidth; x++)
        {
            float h = Mathf.Sin(x * 0.022f + 0.5f) * 14f + Mathf.Sin(x * 0.011f + 1.2f) * 8f + horizonY + 8f;
            int top = (int)h;
            for (int y = 0; y < top && y < texHeight; y++)
                SetPixel(x, y, Lerp32(C(25, 15, 50), hillBack, y / (float)top * 0.9f));
            SetPixel(x, top, Lerp32(hillBack, cloudA, 0.18f));
        }
        for (int x = 0; x < texWidth; x++)
        {
            float h = Mathf.Sin(x * 0.03f + 2.1f) * 10f + Mathf.Sin(x * 0.017f) * 6f + horizonY * 0.55f + 4f;
            int top = (int)h;
            for (int y = 0; y < top && y < texHeight; y++)
                SetPixel(x, y, Lerp32(C(15, 35, 20), hillMid, y / (float)top * 0.85f));
            SetPixel(x, top, Lerp32(hillMid, treeFolL, 0.25f));
        }

        int[] treeXs = { 18, 42, 75, 108, 148, 185, 218, 258, 285, texWidth - 22 };
        for (int i = 0; i < treeXs.Length; i++)
        {
            int tx = treeXs[i];
            int ty = (int)(Mathf.Sin(tx * 0.03f + 2.1f) * 10f + Mathf.Sin(tx * 0.017f) * 6f + horizonY * 0.55f + 4f);
            int th = (int)(10 + Hash(tx, 0f) * 8f);
            float wobble = Mathf.Sin(_time * 0.6f + i * 0.9f) * 0.8f;
            for (int s = 0; s < (int)(th * 0.4f); s++)
                SetPixel(tx + (int)(wobble * s / th), ty + s, treeTrunk);
            int baseY = ty + (int)(th * 0.35f);
            for (int layer = 0; layer < 3; layer++)
            {
                int layerY = baseY + layer * (int)(th * 0.22f);
                int hw = (int)((th * 0.55f) * (1f - layer * 0.28f));
                for (int dy = 0; dy < (int)(th * 0.3f); dy++)
                    for (int dx = -(hw - dy); dx <= (hw - dy); dx++)
                        SetPixel(tx + dx + (int)(wobble * layer * 0.4f), layerY + dy,
                            Lerp32(treeFol, treeFolL, (1f - Mathf.Abs(dx) / (float)(hw + 1)) * 0.5f));
            }
        }

        for (int x = 0; x < texWidth; x++)
        {
            float h = Mathf.Sin(x * 0.035f + 3.5f) * 8f + Mathf.Sin(x * 0.02f + 0.8f) * 5f + 8f;
            int top = (int)h;
            for (int y = 0; y < top && y < texHeight; y++)
                SetPixel(x, y, Lerp32(BLACK, hillFront, (float)y / top));
            SetPixel(x, top, hillFront);
        }

        for (int i = 0; i < 7; i++)
        {
            float h1 = Hash(i * 2.7f, 5f), h2 = Hash(i * 2.7f, 6f);
            float bt = (_time * (18f + h1 * 14f) + h1 * texWidth) % (texWidth + 20f);
            int bx = (int)bt, by2 = (int)(texHeight * 0.42f + h2 * texHeight * 0.22f + Mathf.Sin(_time * 1.2f + i * 1.5f) * 3f);
            int wingUp = Mathf.Sin(_time * 5f + i * 1.1f) > 0 ? 1 : -1;
            SetPixel(bx, by2, birdCol); SetPixel(bx + 1, by2, birdCol);
            SetPixel(bx - 1, by2 + wingUp, birdCol); SetPixel(bx - 2, by2 + wingUp * 2, birdCol);
            SetPixel(bx + 2, by2 + wingUp, birdCol); SetPixel(bx + 3, by2 + wingUp * 2, birdCol);
        }

        int sunXPos = texWidth / 2;
        for (int x = 0; x < texWidth; x++)
        {
            float dist = Mathf.Abs(x - sunXPos) / (float)texWidth;
            float refl = Mathf.Exp(-dist * dist * 8f) * 0.22f * (Mathf.Sin(_time * 0.3f) * 0.08f + 0.92f);
            for (int y = 0; y < horizonY / 2; y++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), skyHoriz, refl * (1f - y / (float)(horizonY / 2))));
        }

        for (int y = 0; y < texHeight; y += 2)
            for (int x = 0; x < texWidth; x++)
                SetPixel(x, y, Lerp32(GetPixel(x, y), BLACK, 0.07f));
    }

    private void DrawCloud4(int cx, int cy, int sc, Color32 col, Color32 shadow)
    {
        int[] bOffX = { 0, -sc, sc, -(int)(sc * 0.6f), (int)(sc * 0.6f), -(int)(sc * 1.1f), (int)(sc * 1.1f) };
        int[] bOffY = { 0, 0, 0, (int)(sc * 0.4f), (int)(sc * 0.4f), (int)(sc * 0.3f), (int)(sc * 0.3f) };
        int[] bRad = { sc, (int)(sc * 0.7f), (int)(sc * 0.7f), (int)(sc * 0.6f), (int)(sc * 0.6f), (int)(sc * 0.55f), (int)(sc * 0.55f) };
        for (int b = 0; b < bOffX.Length; b++)
            for (int dy = -bRad[b]; dy <= bRad[b]; dy++)
                for (int dx = -bRad[b]; dx <= bRad[b]; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > bRad[b]) continue;
                    float shade = Mathf.Clamp01((dy + bRad[b]) / (float)(bRad[b] * 2f));
                    SetPixel(cx + bOffX[b] + dx, cy + bOffY[b] + dy, Lerp32(col, shadow, shade * 0.55f));
                }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _tex != null)
        {
            Draw();
            _tex.SetPixels32(_pixels);
            _tex.Apply();
        }
    }
}