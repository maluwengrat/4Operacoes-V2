using UnityEngine;

public class PowerUpCreator : MonoBehaviour
{
    void Start()
    {
        CriarPowerUp("PowerUpEscudo", new Color(0.3f, 0.8f, 1f), PowerUp.Tipo.Escudo);
        CriarPowerUp("PowerUpTempoLento", new Color(0.8f, 0.4f, 1f), PowerUp.Tipo.TempoLento);
    }

    void CriarPowerUp(string nome, Color cor, PowerUp.Tipo tipo)
    {
        GameObject go = new GameObject(nome);

        // Sprite gerado via código (círculo)
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CriarSpriteCirculo(cor);
        sr.sortingOrder = 5;

        // Collider
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        // Script
        PowerUp pu = go.AddComponent<PowerUp>();
        pu.tipo = tipo;
        pu.duracao = 8f;

        // Desativa — só serve como prefab base
        go.SetActive(false);

        Debug.Log(nome + " criado! Arraste da hierarquia para a pasta Prefabs.");
    }

    Sprite CriarSpriteCirculo(Color cor)
    {
        int tamanho = 64;
        Texture2D tex = new Texture2D(tamanho, tamanho);
        float centro = tamanho / 2f;
        float raio = tamanho / 2f - 2f;

        for (int x = 0; x < tamanho; x++)
        {
            for (int y = 0; y < tamanho; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro));
                if (dist <= raio)
                    tex.SetPixel(x, y, cor);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();

        return Sprite.Create(tex,
            new Rect(0, 0, tamanho, tamanho),
            new Vector2(0.5f, 0.5f), 64f);
    }
}