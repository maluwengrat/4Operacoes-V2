using UnityEngine;
using TMPro;

public class Enemy : MonoBehaviour
{
    public TextMeshPro numberLabel;
    public int myNumber;
    public float speed = 1.5f;

    private float limitaY = -3.5f;

    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 15;
            sr.sortingLayerName = "Default";
        }

        if (numberLabel != null)
        {
            numberLabel.sortingOrder = 16;
        }
    }

    void Update()
    {
        transform.Translate(Vector2.down * speed * Time.deltaTime);
        if (transform.position.y <= limitaY)
        {
            float novoX = Random.Range(-6f, 6f);
            transform.position = new Vector3(novoX, 4f, 0);
        }
    }

    public void SetNumber(int number)
    {
        myNumber = number;
        if (numberLabel != null)
            numberLabel.text = number.ToString();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Bullet")) return;

        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet == null || !bullet.TentarAcertar()) return;

        Destroy(other.gameObject);

        bool eraCorreto = (myNumber == GameManager.instance.GetCorrectAnswer());

        if (EfeitosManager.instance != null)
            EfeitosManager.instance.ExplodirInimigo(transform.position);

        if (eraCorreto && SoundManager.instance != null)
            SoundManager.instance.TocarExplosao();

        GameManager.instance.CheckAnswer(myNumber);
        Destroy(gameObject);
    }
}