using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gera ícones pixel art para os poderes via código.
/// Adicione este script a um GameObject vazio e ele criará os sprites automaticamente.
/// </summary>
public class PoderIcones : MonoBehaviour
{
    [Header("Botões dos poderes")]
    public Button[] btnPoderes; // arraste os botões diretamente

    void Start()
    {
        Sprite[] icones = new Sprite[]
        {
        CriarIconeInversao(),
        CriarIconeTempestade(),
        CriarIconeCronometro(),
        CriarIconeAcelerador(),
        CriarIconeEscudo(),
        CriarIconeCongelar(),
        CriarIconeDica()
        };

        for (int i = 0; i < btnPoderes.Length && i < icones.Length; i++)
        {
            if (btnPoderes[i] != null)
            {
                Image img = btnPoderes[i].GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = icones[i];
                    img.color = Color.white;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helper — criar sprite a partir de pixels
    // ─────────────────────────────────────────────────────────────────

    Sprite CriarSprite(Color[] pixels, int tamanho = 16)
    {
        Texture2D tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // pixel art — sem suavização
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tamanho, tamanho), new Vector2(0.5f, 0.5f), tamanho);
    }

    Color[] Canvas(int tamanho = 16)
    {
        Color[] pixels = new Color[tamanho * tamanho];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        return pixels;
    }

    void Pixel(Color[] pixels, int x, int y, Color cor, int tamanho = 16)
    {
        if (x < 0 || x >= tamanho || y < 0 || y >= tamanho) return;
        pixels[y * tamanho + x] = cor;
    }

    void Rect(Color[] pixels, int x, int y, int w, int h, Color cor, int tamanho = 16)
    {
        for (int px = x; px < x + w; px++)
            for (int py = y; py < y + h; py++)
                Pixel(pixels, px, py, cor, tamanho);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 0 — Inversão (setas trocadas ↔)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeInversao()
    {
        Color[] p = Canvas();
        Color c = new Color(1f, 0.3f, 0.3f); // vermelho

        // Seta esquerda →
        Rect(p, 1, 9, 6, 2, c);
        Pixel(p, 3, 11, c); Pixel(p, 3, 8, c);
        Pixel(p, 2, 12, c); Pixel(p, 2, 7, c);

        // Seta direita ←
        Rect(p, 9, 5, 6, 2, c);
        Pixel(p, 12, 4, c); Pixel(p, 12, 7, c);
        Pixel(p, 13, 3, c); Pixel(p, 13, 8, c);

        // Ponto de troca
        Rect(p, 6, 6, 4, 4, new Color(1f, 0.8f, 0f));

        return CriarSprite(p);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 1 — Tempestade (nuvem com raio)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeTempestade()
    {
        Color[] p = Canvas();
        Color nuvem = new Color(0.7f, 0.7f, 1f);
        Color raio = new Color(1f, 0.9f, 0f);

        // Nuvem
        Rect(p, 2, 10, 12, 3, nuvem);
        Rect(p, 4, 12, 8, 2, nuvem);
        Rect(p, 3, 11, 3, 3, nuvem);
        Rect(p, 8, 11, 4, 3, nuvem);

        // Raio ⚡
        Pixel(p, 8, 9, raio); Pixel(p, 7, 8, raio);
        Pixel(p, 6, 7, raio); Pixel(p, 7, 6, raio);
        Pixel(p, 8, 5, raio); Pixel(p, 7, 4, raio);
        Pixel(p, 6, 3, raio);
        Rect(p, 6, 6, 3, 1, raio);

        return CriarSprite(p);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 2 — Cronômetro (relógio com X)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeCronometro()
    {
        Color[] p = Canvas();
        Color borda = new Color(0.9f, 0.4f, 0.1f);
        Color fundo = new Color(0.2f, 0.1f, 0.05f);
        Color ponteiro = Color.white;
        Color menos = new Color(1f, 0.2f, 0.2f);

        // Círculo do relógio
        Rect(p, 3, 4, 10, 8, borda);
        Rect(p, 2, 6, 12, 4, borda);
        Rect(p, 4, 5, 8, 6, fundo);
        Rect(p, 3, 6, 10, 4, fundo);

        // Ponteiros
        Pixel(p, 8, 8, ponteiro);
        Pixel(p, 8, 7, ponteiro);
        Pixel(p, 8, 6, ponteiro);
        Pixel(p, 9, 8, ponteiro);
        Pixel(p, 10, 8, ponteiro);

        // Centro
        Pixel(p, 8, 8, borda);

        // Topo do relógio
        Rect(p, 7, 12, 2, 2, borda);
        Rect(p, 6, 13, 4, 1, borda);

        // Sinal de menos (vermelho) — indica redução
        Rect(p, 5, 2, 6, 2, menos);

        return CriarSprite(p);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 3 — Acelerador (raios duplos →→)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeAcelerador()
    {
        Color[] p = Canvas();
        Color c1 = new Color(1f, 0.7f, 0f);
        Color c2 = new Color(1f, 0.4f, 0f);

        // Raio 1 (maior)
        int[] rx1 = { 2, 3, 4, 5, 6, 7, 8 };
        int[] ry1 = { 8, 8, 8, 7, 7, 7, 6 };
        for (int i = 0; i < rx1.Length; i++) Pixel(p, rx1[i], ry1[i], c1);
        int[] rx1b = { 6, 7, 8, 9, 10, 11, 12 };
        int[] ry1b = { 9, 9, 9, 8, 8, 8, 7 };
        for (int i = 0; i < rx1b.Length; i++) Pixel(p, rx1b[i], ry1b[i], c1);

        // Setas
        Pixel(p, 13, 6, c1); Pixel(p, 14, 7, c1); Pixel(p, 13, 8, c1);
        Pixel(p, 14, 9, c1); Pixel(p, 13, 10, c1);

        // Raio 2 (menor, laranja)
        Rect(p, 3, 11, 7, 2, c2);
        Pixel(p, 11, 11, c2); Pixel(p, 12, 10, c2); Pixel(p, 11, 9, c2);

        // Partículas de velocidade
        Pixel(p, 1, 5, c1); Pixel(p, 1, 11, c2);
        Pixel(p, 2, 3, c1); Pixel(p, 2, 13, c2);

        return CriarSprite(p);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 4 — Escudo Duplo (dois escudos sobrepostos)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeEscudo()
    {
        Color[] p = Canvas();
        Color c1 = new Color(0.3f, 0.8f, 1f);
        Color c2 = new Color(0.1f, 0.5f, 0.9f);

        // Escudo de trás (menor, mais escuro)
        Rect(p, 5, 8, 8, 5, c2);
        Rect(p, 4, 9, 10, 3, c2);
        Pixel(p, 9, 4, c2); Pixel(p, 9, 5, c2);
        Pixel(p, 9, 6, c2); Pixel(p, 9, 7, c2);

        // Escudo da frente (maior, mais claro)
        Rect(p, 3, 9, 8, 5, c1);
        Rect(p, 2, 10, 10, 3, c1);
        Pixel(p, 7, 5, c1); Pixel(p, 7, 6, c1);
        Pixel(p, 7, 7, c1); Pixel(p, 7, 8, c1);

        // Estrela no centro
        Pixel(p, 7, 11, Color.white);
        Pixel(p, 6, 11, Color.white); Pixel(p, 8, 11, Color.white);
        Pixel(p, 7, 10, Color.white); Pixel(p, 7, 12, Color.white);

        return CriarSprite(p);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 5 — Congelar (cristal de gelo ❄)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeCongelar()
    {
        Color[] p = Canvas();
        Color c = new Color(0.5f, 0.9f, 1f);
        Color brilho = Color.white;

        // Eixo vertical
        Rect(p, 7, 1, 2, 14, c);

        // Eixo horizontal
        Rect(p, 1, 7, 14, 2, c);

        // Diagonais
        for (int i = 0; i < 5; i++)
        {
            Pixel(p, 3 + i, 3 + i, c);
            Pixel(p, 12 - i, 3 + i, c);
            Pixel(p, 3 + i, 12 - i, c);
            Pixel(p, 12 - i, 12 - i, c);
        }

        // Pontas
        Pixel(p, 7, 0, brilho); Pixel(p, 8, 0, brilho);
        Pixel(p, 7, 15, brilho); Pixel(p, 8, 15, brilho);
        Pixel(p, 0, 7, brilho); Pixel(p, 0, 8, brilho);
        Pixel(p, 15, 7, brilho); Pixel(p, 15, 8, brilho);

        // Centro
        Rect(p, 6, 6, 4, 4, brilho);

        return CriarSprite(p);
    }

    // ─────────────────────────────────────────────────────────────────
    // Ícone 6 — Dica Relâmpago (lâmpada com raio)
    // ─────────────────────────────────────────────────────────────────
    Sprite CriarIconeDica()
    {
        Color[] p = Canvas();
        Color lampada = new Color(1f, 0.95f, 0.3f);
        Color base_ = new Color(0.8f, 0.8f, 0.8f);
        Color raio = new Color(1f, 0.6f, 0f);

        // Lâmpada
        Rect(p, 5, 8, 6, 5, lampada);
        Rect(p, 4, 9, 8, 4, lampada);
        Rect(p, 3, 10, 10, 3, lampada);

        // Base da lâmpada
        Rect(p, 6, 6, 4, 3, base_);
        Rect(p, 7, 5, 2, 2, base_);

        // Raio dentro da lâmpada
        Pixel(p, 8, 11, raio); Pixel(p, 7, 10, raio);
        Pixel(p, 8, 9, raio); Pixel(p, 7, 8, raio);
        Rect(p, 6, 9, 3, 1, raio);

        // Brilho
        Pixel(p, 3, 13, Color.white); Pixel(p, 12, 13, Color.white);
        Pixel(p, 2, 11, Color.white); Pixel(p, 13, 11, Color.white);
        Pixel(p, 8, 15, lampada); Pixel(p, 8, 14, lampada);

        return CriarSprite(p);
    }
}