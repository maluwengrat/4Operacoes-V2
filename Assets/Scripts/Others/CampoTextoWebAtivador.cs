using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// ─────────────────────────────────────────────────────────────────────
// CampoTextoWebAtivador
//
// Encaixe este script no MESMO GameObject de cada TMP_InputField que
// precisa funcionar em mobile WebGL: InputCódigoSala, InputNomeAluno,
// InputSenha (do painel do professor).
//
// SETUP:
//   1. Selecione o GameObject do campo (ex.: InputCódigoSala).
//   2. Add Component > CampoTextoWebAtivador.
//   3. O campo "Campo" preenche sozinho (pega o TMP_InputField do
//      próprio objeto). Confirme que preencheu certo.
//   4. Marque "Senha" como true SÓ no InputSenha do professor
//      (os outros ficam false).
//
// Fora de WebGL (Editor, build standalone) este script não faz nada —
// o TMP_InputField funciona normalmente do jeito nativo do Unity.
// ─────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(TMP_InputField))]
public class CampoTextoWebAtivador : MonoBehaviour, ISelectHandler
{
    [Tooltip("Preenchido automaticamente a partir deste GameObject.")]
    public TMP_InputField campo;

    [Tooltip("Marque true apenas para campos de senha (ex.: InputSenha do professor).")]
    public bool senha = false;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void CTW_Mostrar(
        float x, float y, float largura, float altura,
        string placeholder, int senhaFlag, string textoInicial, int maxLength);
#endif

    void Reset()
    {
        campo = GetComponent<TMP_InputField>();
    }

    void Awake()
    {
        if (campo == null) campo = GetComponent<TMP_InputField>();
    }

    public void OnSelect(BaseEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Só ativa o input HTML em mobile — no desktop (teclado físico)
        // o TMP_InputField já funciona normalmente sem esse truque.
        if (MobileUI.instance == null || !MobileUI.instance.IsMobile) return;

        if (CampoTextoWebBridgeAtivo.instance != null)
            CampoTextoWebBridgeAtivo.instance.RegistrarCampoAtivo(campo);

        RectTransform rt = campo.GetComponent<RectTransform>();
        Vector3[] cantos = new Vector3[4];
        rt.GetWorldCorners(cantos); // 0=inferior-esq, 1=superior-esq, 2=superior-dir, 3=inferior-dir

        float xMin = Mathf.Min(cantos[0].x, cantos[1].x);
        float xMax = Mathf.Max(cantos[2].x, cantos[3].x);
        float topoMundo = Mathf.Max(cantos[1].y, cantos[2].y);
        float baseMundo = Mathf.Min(cantos[0].y, cantos[3].y);

        float largura = xMax - xMin;
        float altura = topoMundo - baseMundo;
        float yTopoTela = Screen.height - topoMundo; // flip: Unity Y-pra-cima → tela Y-pra-baixo

        string placeholder = "";
        if (campo.placeholder != null)
        {
            TMP_Text placeholderTmp = campo.placeholder as TMP_Text;
            if (placeholderTmp != null) placeholder = placeholderTmp.text;
        }

        CTW_Mostrar(xMin, yTopoTela, largura, altura,
                    placeholder, senha ? 1 : 0, campo.text, campo.characterLimit);
#endif
    }
}
