using UnityEngine;

public class CriarNave : MonoBehaviour
{
    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CriarSpriteNave();
        sr.sortingOrder = 10;
        // ⭐ CRÍTICO: Garantir que a nave apareça NA FRENTE do fundo
        sr.sortingLayerName = "Default"; // Ou crie uma layer "GameObjects"
    }

    Sprite CriarSpriteNave()
    {
        int w = 48, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, Color.clear);

        Color corNave = new Color(0.976f, 0.451f, 0.086f);
        Color corEscuro = new Color(0.6f, 0.25f, 0.04f);

        for (int x = 16; x < 32; x++)
            for (int y = 8; y < 52; y++)
                tex.SetPixel(x, y, corNave);

        for (int y = 52; y < 64; y++)
        {
            int largura = (64 - y);
            int inicio = w / 2 - largura / 2;
            int fim = w / 2 + largura / 2;
            for (int x = inicio; x < fim; x++)
                tex.SetPixel(x, y, corNave);
        }

        for (int y = 8; y < 32; y++)
        {
            int larguraAsa = (32 - y) / 2;
            for (int x = 16 - larguraAsa; x < 16; x++)
                tex.SetPixel(x, y, corNave);
        }

        for (int y = 8; y < 32; y++)
        {
            int larguraAsa = (32 - y) / 2;
            for (int x = 32; x < 32 + larguraAsa; x++)
                tex.SetPixel(x, y, corNave);
        }

        for (int x = 20; x < 28; x++)
            for (int y = 20; y < 36; y++)
                tex.SetPixel(x, y, corEscuro);

        for (int x = 18; x < 30; x++)
            for (int y = 4; y < 10; y++)
                tex.SetPixel(x, y, corEscuro);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 48f);
    }
}