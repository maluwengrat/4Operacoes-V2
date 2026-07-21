using UnityEngine;

public class PowerUp : MonoBehaviour
{
    public enum Tipo { Escudo, TempoLento }
    public Tipo tipo;
    public float duracao = 8f;
    private float speed = 2f;
    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CriarSpriteCirculo();
        sr.color = tipo == Tipo.Escudo ? new Color(0.3f, 0.8f, 1f) : new Color(0.8f, 0.4f, 1f);
        sr.sortingOrder = 5;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;
    }

    Sprite CriarSpriteCirculo()
    {
        int tamanho = 64;
        Texture2D tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        float centro = tamanho / 2f;
        float raio = tamanho / 2f - 2f;

        for (int x = 0; x < tamanho; x++)
            for (int y = 0; y < tamanho; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro));
                tex.SetPixel(x, y, dist <= raio ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tamanho, tamanho), new Vector2(0.5f, 0.5f), 64f);
    }

    void Update()
    {
        transform.Translate(Vector2.down * speed * Time.deltaTime);
        if (transform.position.y < -6f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        switch (tipo)
        {
            case Tipo.Escudo:
                player.AtivarEscudo(duracao);
                FeedbackManager.instance.MostrarMensagem("ESCUDO ATIVO!", new Color(0.3f, 0.8f, 1f));
                break;
            case Tipo.TempoLento:
                GameManager.instance.AtivarTempoLento(duracao);
                FeedbackManager.instance.MostrarMensagem("TEMPO LENTO!", new Color(0.8f, 0.4f, 1f));
                break;
        }
        if (SoundManager.instance != null)
            SoundManager.instance.TocarPowerUp();  // ← adicione antes do Destroy
        Destroy(gameObject);
    }
}