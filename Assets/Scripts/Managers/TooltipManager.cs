using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager instance;

    [Header("UI Tooltip")]
    public GameObject painelTooltip;
    public TextMeshProUGUI txtNome;
    public TextMeshProUGUI txtDescricao;
    public RectTransform rectTooltip;

    [Header("Configuração Visual")]
    public float larguraFixa = 200f;
    public float paddingVertical = 20f;
    public float paddingHorizontal = 16f;

    private Coroutine coroutineTooltip;
    private Image imagemFundo;

    void Awake() { instance = this; }

    void Start()
    {
        if (painelTooltip != null)
        {
            painelTooltip.SetActive(false);

            // Garante Canvas Group sem bloqueio de raycast
            CanvasGroup cg = painelTooltip.GetComponent<CanvasGroup>();
            if (cg == null) cg = painelTooltip.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Pega ou cria o fundo
            imagemFundo = painelTooltip.GetComponent<Image>();
            if (imagemFundo == null)
                imagemFundo = painelTooltip.AddComponent<Image>();

            imagemFundo.color = new Color(0.05f, 0.05f, 0.15f, 0.92f);

            // Remove Content Size Fitter se existir — vamos controlar manualmente
            ContentSizeFitter csf = painelTooltip.GetComponent<ContentSizeFitter>();
            if (csf != null) csf.enabled = false;
        }

        // Configura textos
        if (txtNome != null)
        {
            txtNome.enableWordWrapping = true;
            txtNome.overflowMode = TextOverflowModes.Overflow;
        }
        if (txtDescricao != null)
        {
            txtDescricao.enableWordWrapping = true;
            txtDescricao.overflowMode = TextOverflowModes.Overflow;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Mostrar tooltip
    // ─────────────────────────────────────────────────────────────────

    public void MostrarTooltip(string nome, string descricao, Transform alvo)
    {
        if (painelTooltip == null) return;
        if (coroutineTooltip != null) StopCoroutine(coroutineTooltip);
        coroutineTooltip = StartCoroutine(MostrarComDelay(nome, descricao, alvo));
    }

    IEnumerator MostrarComDelay(string nome, string descricao, Transform alvo)
    {
        yield return new WaitForSeconds(0.3f);

        // Define textos
        if (txtNome != null) txtNome.text = nome;
        if (txtDescricao != null) txtDescricao.text = descricao;

        // Configura largura dos textos
        if (txtNome != null)
        {
            RectTransform rt = txtNome.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(larguraFixa - paddingHorizontal * 2, 0);
        }
        if (txtDescricao != null)
        {
            RectTransform rt = txtDescricao.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(larguraFixa - paddingHorizontal * 2, 0);
        }

        // Ativa para poder medir
        painelTooltip.SetActive(true);
        yield return null;

        // Calcula altura necessária
        float alturaNome = txtNome != null ? txtNome.preferredHeight : 0f;
        float alturaDesc = txtDescricao != null ? txtDescricao.preferredHeight : 0f;
        float alturaTotal = alturaNome + alturaDesc + paddingVertical * 2 + 6f;

        // Define tamanho do painel
        if (rectTooltip != null)
            rectTooltip.sizeDelta = new Vector2(larguraFixa, alturaTotal);

        // Posiciona os textos manualmente
        if (txtNome != null)
        {
            RectTransform rt = txtNome.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -paddingVertical);
            rt.sizeDelta = new Vector2(-paddingHorizontal * 2, alturaNome);
        }
        if (txtDescricao != null)
        {
            RectTransform rt = txtDescricao.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -paddingVertical - alturaNome - 4f);
            rt.sizeDelta = new Vector2(-paddingHorizontal * 2, alturaDesc);
        }

        // Posiciona acima do ícone
        if (alvo != null && rectTooltip != null)
        {
            Vector3 posAcima = alvo.position + new Vector3(0f, alturaTotal + 40f, 0f);
            float x = Mathf.Clamp(posAcima.x, larguraFixa / 2f, Screen.width - larguraFixa / 2f);
            float y = Mathf.Clamp(posAcima.y, alturaTotal / 2f, Screen.height - alturaTotal / 2f);
            rectTooltip.position = new Vector2(x, y);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Esconder tooltip
    // ─────────────────────────────────────────────────────────────────

    public void EsconderTooltip()
    {
        if (coroutineTooltip != null)
        {
            StopCoroutine(coroutineTooltip);
            coroutineTooltip = null;
        }
        if (painelTooltip != null)
            painelTooltip.SetActive(false);
    }
}