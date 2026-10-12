using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Faz os TMP_InputField funcionarem no celular (WebGL).
// Não precisa colocar na cena: ele se cria sozinho.
public class CampoTextoWebAtivador : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int CampoTextoWeb_EhMobile();
    [DllImport("__Internal")] private static extern void CampoTextoWeb_Iniciar(string goName);
    [DllImport("__Internal")] private static extern void CampoTextoWeb_Abrir(string valor, string placeholder, int maxLen, int senha);
    [DllImport("__Internal")] private static extern void CampoTextoWeb_Fechar();

    private TMP_InputField campoAtual;
    private bool ativo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        var go = new GameObject("CampoTextoWebAtivador");
        DontDestroyOnLoad(go);
        go.AddComponent<CampoTextoWebAtivador>();
    }

    void Start()
    {
        ativo = CampoTextoWeb_EhMobile() == 1;
        if (!ativo) return;

        // Deixa o navegador receber as teclas do <input> HTML
        WebGLInput.captureAllKeyboardInput = false;
        CampoTextoWeb_Iniciar(gameObject.name);
    }

    void Update()
    {
        if (!ativo || EventSystem.current == null) return;

        // O campo foi desativado/destruído (painel fechou)
        if (campoAtual != null && !campoAtual.gameObject.activeInHierarchy)
        {
            campoAtual = null;
            CampoTextoWeb_Fechar();
        }

        GameObject sel = EventSystem.current.currentSelectedGameObject;
        TMP_InputField campo = sel != null ? sel.GetComponent<TMP_InputField>() : null;

        if (campo == campoAtual) return;

        if (campo == null)
        {
            campoAtual = null;
            CampoTextoWeb_Fechar();
            return;
        }

        campoAtual = campo;
        int senha = campo.contentType == TMP_InputField.ContentType.Password ? 1 : 0;
        string placeholder = campo.placeholder is TMP_Text p ? p.text : "";
        CampoTextoWeb_Abrir(campo.text, placeholder, campo.characterLimit, senha);
    }

    // Chamado pelo JavaScript a cada letra digitada
    public void OnTextoWeb(string valor)
    {
        if (campoAtual == null) return;
        if (campoAtual.characterLimit > 0 && valor.Length > campoAtual.characterLimit)
            valor = valor.Substring(0, campoAtual.characterLimit);
        campoAtual.text = valor;
    }

    // Chamado pelo JavaScript quando o aluno aperta Enter/OK ou sai do campo
    public void OnFecharWeb(string _)
    {
        campoAtual = null;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
#endif
}