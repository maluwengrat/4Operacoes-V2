using TMPro;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────
// CampoTextoWebBridgeAtivo
//
// IMPORTANTE: o GameObject que tiver este script precisa se chamar
// EXATAMENTE "CampoTextoWebBridgeAtivo" na Hierarchy — é por esse nome
// que o SendMessage do JavaScript (no .jslib) encontra este objeto.
//
// SETUP:
//   1. Crie um GameObject vazio chamado "CampoTextoWebBridgeAtivo"
//      (pode ser na mesma cena do GameManager, não precisa de UI).
//   2. Adicione este script nele.
//   3. Pronto — os CampoTextoWebAtivador de cada campo se registram
//      aqui sozinhos quando o campo é selecionado.
// ─────────────────────────────────────────────────────────────────────
public class CampoTextoWebBridgeAtivo : MonoBehaviour
{
    public static CampoTextoWebBridgeAtivo instance;

    private TMP_InputField campoAtivo;

    void Awake()
    {
        instance = this;
    }

    public void RegistrarCampoAtivo(TMP_InputField campo)
    {
        campoAtivo = campo;
    }

    // Chamado pelo JavaScript a cada tecla digitada no <input> HTML oculto
    public void OnTextoAlteradoWeb(string novoTexto)
    {
        if (campoAtivo == null) return;

        campoAtivo.text = novoTexto;
        campoAtivo.caretPosition = novoTexto.Length;
    }

    // Chamado pelo JavaScript quando o <input> HTML oculto perde o foco
    // (usuário tocou fora, ou apertou Enter/OK no teclado)
    public void OnPerdeuFocoWeb(string _)
    {
        if (campoAtivo != null)
            campoAtivo.DeactivateInputField();

        campoAtivo = null;
    }
}
