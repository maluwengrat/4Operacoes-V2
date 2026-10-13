using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────
// Tela de seleção de alvo pra envio de poderes no modo Turma.
//
// COMO ENCAIXAR NO SEU PROJETO:
// 1. Crie um Panel na Canvas (pode chamar "PainelSelecionarAlvo") e
//    deixe ele desativado por padrão (caixa desmarcada no Inspector).
// 2. Dentro dele, um objeto vazio com um Vertical/Grid Layout Group
//    — esse é o "containerListaAlunos".
// 3. Crie UM prefab de botão (nome + TMP_Text) — esse é o
//    "prefabBotaoAluno". Ele será instanciado 1x por aluno da sala.
// 4. Arraste este script pro Panel (ou outro objeto sempre ativo) e
//    preencha os campos no Inspector.
// 5. Arraste o botão "Cancelar" do painel para o campo "Btn Cancelar".
//    (O script também tenta achar sozinho um botão chamado Cancelar/
//    Fechar/Voltar dentro do painel, e liga o clique por código.)
// ─────────────────────────────────────────────────────────────────────
public class SelecionarAlvoUI : MonoBehaviour
{
    [Header("Referências de UI")]
    public GameObject painelSelecao;       // o Panel inteiro (pra abrir/fechar)
    public Transform containerListaAlunos; // onde os botões entram
    public GameObject prefabBotaoAluno;    // prefab com um TMP_Text dentro

    [Header("Cancelar")]
    public Button btnCancelar;             // botão que fecha o painel sem enviar nada

    [Header("Opcional")]
    public TMP_Text textoStatus; // pra mostrar "Carregando..." ou "Sala vazia"

    string tipoPoderPendente;
    System.Action onPoderEnviadoCallback;
    System.Action onCanceladoCallback;
    int buscaAtual = 0;     // identifica a busca de jogadores em andamento
    bool abrindo = false;   // true enquanto o painel está sendo ativado por AbrirSeletorDeAlvo
    readonly List<GameObject> botoesInstanciados = new List<GameObject>();

    void Awake()
    {
        // Se este script está no próprio painel (que começa desativado), o Awake só
        // roda na PRIMEIRA ativação, ou seja, dentro do SetActive(true) de
        // AbrirSeletorDeAlvo. Desativar o painel aqui o fecharia no mesmo instante.
        if (painelSelecao != null && !abrindo)
            painelSelecao.SetActive(false);

        ConfigurarBotaoCancelar();
    }

    void ConfigurarBotaoCancelar()
    {
        // Se não foi arrastado no Inspector, procura um botão pelo nome
        if (btnCancelar == null && painelSelecao != null)
        {
            foreach (Button b in painelSelecao.GetComponentsInChildren<Button>(true))
            {
                string n = b.name.ToLower();
                if (n.Contains("cancel") || n.Contains("fechar") || n.Contains("voltar"))
                {
                    btnCancelar = b;
                    break;
                }
            }
        }

        if (btnCancelar != null)
        {
            btnCancelar.onClick.RemoveListener(AoClicarCancelar);
            btnCancelar.onClick.AddListener(AoClicarCancelar);
        }
        else
        {
            Debug.LogWarning("[SelecionarAlvoUI] Botão Cancelar não encontrado. Arraste o botão no campo 'Btn Cancelar'.");
        }
    }

    void AoClicarCancelar()
    {
        Debug.Log("[SelecionarAlvoUI] Cancelar clicado.");
        FecharSeletor();
    }

    // Versão com 1 parâmetro só — dá pra plugar direto no OnClick() de um Button
    // pelo Inspector (UnityEvent só chama métodos com 0 ou 1 argumento).
    public void AbrirSeletorDeAlvo(string tipoPoder)
    {
        AbrirSeletorDeAlvo(tipoPoder, null, null);
    }

    public void AbrirSeletorDeAlvo(string tipoPoder, System.Action onEnviado)
    {
        AbrirSeletorDeAlvo(tipoPoder, onEnviado, null);
    }

    // Abre a lista de alvos.
    //  onEnviado   → dispara DEPOIS que o jogador escolhe o alvo e o poder é enviado
    //  onCancelado → dispara se o painel fechar SEM envio (botão Cancelar, erro, etc.)
    public void AbrirSeletorDeAlvo(string tipoPoder, System.Action onEnviado, System.Action onCancelado)
    {
        Debug.Log("[SelecionarAlvoUI] AbrirSeletorDeAlvo chamado com tipoPoder=" + tipoPoder
            + " | painelSelecao é null? " + (painelSelecao == null));

        if (FirebaseManager.instance == null || painelSelecao == null)
        {
            Debug.LogWarning("[SelecionarAlvoUI] FirebaseManager ou painelSelecao não disponível.");
            onCancelado?.Invoke();
            return;
        }

        tipoPoderPendente = tipoPoder;
        onPoderEnviadoCallback = onEnviado;
        onCanceladoCallback = onCancelado;

        abrindo = true;
        painelSelecao.SetActive(true);
        abrindo = false;

        LimparBotoes();

        if (textoStatus != null)
            textoStatus.text = "Carregando jogadores...";

        // Identifica esta busca: se o painel fechar antes da resposta chegar, ela é ignorada
        int minhaBusca = ++buscaAtual;
        string codigo = FirebaseManager.instance.codigoSalaAtual;
        FirebaseManager.instance.BuscarJogadoresSala(codigo, lista =>
        {
            if (minhaBusca != buscaAtual) return;
            OnJogadoresRecebidos(lista);
        });
    }

    void OnJogadoresRecebidos(List<DadosJogador> jogadores)
    {
        LimparBotoes();

        if (FirebaseManager.instance == null) return;
        string meuId = FirebaseManager.instance.userId;

        // Remove o próprio jogador da lista — não faz sentido mirar em si mesmo.
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
        if (string.IsNullOrEmpty(tipoPoderPendente))
        {
            FecharSeletor();
            return;
        }

        string tipo = tipoPoderPendente;
        System.Action enviado = onPoderEnviadoCallback;

        onCanceladoCallback = null; // enviou: não conta como cancelamento

        FirebaseManager.instance.EnviarPoderParaAdversario(tipo, targetUserId);
        FecharSeletor();
        enviado?.Invoke();
    }

    // Fecha o painel. Se fechar sem ter enviado o poder, avisa quem abriu (onCancelado).
    public void FecharSeletor()
    {
        buscaAtual++; // qualquer busca de jogadores pendente passa a ser ignorada

        System.Action cancelado = onCanceladoCallback;
        tipoPoderPendente = null;
        onPoderEnviadoCallback = null;
        onCanceladoCallback = null;

        LimparBotoes();

        if (painelSelecao != null)
            painelSelecao.SetActive(false);

        cancelado?.Invoke();
    }

    void LimparBotoes()
    {
        foreach (GameObject botao in botoesInstanciados)
            if (botao != null) Destroy(botao);
        botoesInstanciados.Clear();
    }
}