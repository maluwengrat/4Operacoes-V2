using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 18f;
    private bool jAcertou = false;

    void Start()
    {
        // ⭐ Garantir que a bala apareça na frente
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 5;
        }
    }

    void Update()
    {
        transform.Translate(Vector2.up * speed * Time.deltaTime);
        if (transform.position.y > 7f)
            Destroy(gameObject);
    }

    public bool TentarAcertar()
    {
        if (jAcertou) return false;
        jAcertou = true;
        return true;
    }
}