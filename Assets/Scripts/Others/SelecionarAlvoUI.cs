using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────
// Tela de seleção de alvo pra envio de poderes no modo Turma.
//
// COMO ENCAIXAR NO SEU PROJETO:
// 1. Crie um Panel na Canvas (pode chamar "PainelSelecionarAlvo") e
//    deixe ele desativado por padrão (SetActive(false) na cena).
// 2. Dentro dele, um objeto vazio com um Vertical/Grid Layout Group
//    — esse é o "containerListaAlunos".
// 3. Crie UM prefab de botão (nome + TMP_Text) — esse é o
//    "prefabBotaoAluno". Ele será instanciado 1x por aluno da sala.
// 4. Arraste este script pro Panel, preencha os campos no Inspector.
// 5. No botão de cada poder, no OnClick() do Inspector, escolha
//    SelecionarAlvoUI > AbrirSeletorDeAlvo (string) — NÃO a versão
//    com 2 parâmetros, essa não aparece na lista do Button. Digite
//    o nome do poder (ex.: "inversao") no campo de texto que aparece
//    logo abaixo do método.
// ─────────────────────────────────────────────────────────────────────
public class SelecionarAlvoUI : MonoBehaviour
{
    [Header("Referências de UI")]
    public GameObject painelSelecao;      // o Panel inteiro (pra abrir/fechar)
    public Transform containerListaAlunos; // onde os botões entram
    public GameObject prefabBotaoAluno;    // prefab com um TMP_Text dentro

    [Header("Opcional")]
    public TMP_Text textoStatus; // pra mostrar "Carregando..." ou "Sala vazia"

    string tipoPoderPendente;
    System.Action onPoderEnviadoCallback;
    readonly List<GameObject> botoesInstanciados = new List<GameObject>();

    void Awake()
    {
        if (painelSelecao != null)
            painelSelecao.SetActive(false);
    }

    // Versão com 1 parâmetro só — é essa que dá pra plugar direto no
    // OnClick() de um Button pelo Inspector (UnityEvent só chama métodos
    // com 0 ou exatamente 1 argumento).
    public void AbrirSeletorDeAlvo(string tipoPoder)
    {
        AbrirSeletorDeAlvo(tipoPoder, null);
    }

    // Chame isso quando o jogador apertar o botão do poder.
    // Em vez de mandar na hora, abre a lista de alvos primeiro.
    // onEnviado (opcional) dispara DEPOIS que o jogador escolhe o alvo e
    // o poder é de fato enviado — use pra mostrar notificação, tocar som, etc.
    // Essa versão com callback só pode ser chamada por código (C#),
    // não aparece no OnClick() do Inspector por causa dos 2 parâmetros.
    public void AbrirSeletorDeAlvo(string tipoPoder, System.Action onEnviado)
    {
        Debug.Log("[SelecionarAlvoUI] AbrirSeletorDeAlvo chamado com tipoPoder=" + tipoPoder
            + " | painelSelecao é null? " + (painelSelecao == null));

        tipoPoderPendente = tipoPoder;
        onPoderEnviadoCallback = onEnviado;

        if (painelSelecao != null)
            painelSelecao.SetActive(true);

        LimparBotoes();

        if (textoStatus != null)
            textoStatus.text = "Carregando jogadores...";

        string codigo = FirebaseManager.instance.codigoSalaAtual;
        FirebaseManager.instance.BuscarJogadoresSala(codigo, OnJogadoresRecebidos);
    }

    void OnJogadoresRecebidos(List<DadosJogador> jogadores)
    {
        LimparBotoes();

        string meuId = FirebaseManager.instance.userId;

        // Remove o próprio jogador da lista — não faz sentido mirar
        // em si mesmo.
        var alvosDisponiveis = jogadores.FindAll(j => j.userId != meuId);

        if (alvosDisponiveis.Count == 0)
        {
            if (textoStatus != null)
                textoStatus.text = "Nenhum outro jogador na sala ainda.";
            return;
        }

        if (textoStatus != null)
            textoStatus.text = "Escolha um alvo:";

        foreach (DadosJogador jogador in alvosDisponiveis)
        {
            GameObject botaoObj = Instantiate(prefabBotaoAluno, containerListaAlunos);
            botoesInstanciados.Add(botaoObj);

            TMP_Text label = botaoObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = jogador.nome;

            Button botao = botaoObj.GetComponent<Button>();
            if (botao != null)
            {
                // Captura local pra não pegar a variável errada no loop.
                string targetUserId = jogador.userId;
                botao.onClick.AddListener(() => EscolherAlvo(targetUserId));
            }
        }
    }

    void EscolherAlvo(string targetUserId)
    {
        if (!string.IsNullOrEmpty(tipoPoderPendente))
        {
            FirebaseManager.instance.EnviarPoderParaAdversario(tipoPoderPendente, targetUserId);
            onPoderEnviadoCallback?.Invoke();
        }

        FecharSeletor();
    }

    public void FecharSeletor()
    {
        tipoPoderPendente = null;
        onPoderEnviadoCallback = null;
        LimparBotoes();

        if (painelSelecao != null)
            painelSelecao.SetActive(false);
    }

    void LimparBotoes()
    {
        foreach (GameObject botao in botoesInstanciados)
            Destroy(botao);
        botoesInstanciados.Clear();
    }
}