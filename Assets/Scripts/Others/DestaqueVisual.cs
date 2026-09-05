using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class DestaqueAnimacao : MonoBehaviour
{
    private CanvasGroup grupo;
    private float timer = 0f;

    void Awake()
    {
        grupo = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        grupo.alpha = 0.3f + Mathf.Abs(Mathf.Sin(timer * 3f)) * 0.7f;
    }
}