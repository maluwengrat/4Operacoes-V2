using UnityEngine;
using UnityEngine.UI;

public class SeguirNave : MonoBehaviour
{
    private RectTransform rectTransform;
    private Camera cam;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        cam = Camera.main;
    }

    void Update()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null || cam == null) return;

        // Converte posição do mundo para posição na tela
        Vector2 posNaTela = cam.WorldToScreenPoint(player.transform.position);
        rectTransform.position = posNaTela;
    }
}