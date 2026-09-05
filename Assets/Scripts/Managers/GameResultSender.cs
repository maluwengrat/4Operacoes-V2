using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Ponte entre o C# e o JavaScript (GamePlugin.jslib).
/// Coloque este script em um GameObject chamado "GameResultSender" na cena.
/// </summary>
public class GameResultSender : MonoBehaviour
{
    public static GameResultSender instance;

    [DllImport("__Internal")]
    private static extern void EnviarResultadoFase(string json);

    [DllImport("__Internal")]
    private static extern void EnviarQuestao(string json);

    [DllImport("__Internal")]
    private static extern void EnviarRelatorio(string json);

    void Awake()
    {
        instance = this;
    }

    private string partidaId;
    private string inicioPartidaIso;
    private Dictionary<int, int> tentativasPorFase = new Dictionary<int, int>();

    public void IniciarNovaPartida()
    {
        partidaId = System.Guid.NewGuid().ToString();
        inicioPartidaIso = System.DateTime.UtcNow.ToString("o");
        tentativasPorFase.Clear();
    }

    private string GetTipoOperacao(int fase)
    {
        switch (fase)
        {
            case 1: return "Adicao";
            case 2: return "Subtracao";
            case 3: return "Divisao";
            case 4: return "Multiplicacao";
            default: return "Desconhecido";
        }
    }

    /// <summary>
    /// Envia os dados da fase para a plataforma via fetch no JavaScript.
    /// (rota antiga — mantida como estava, sem alterações)
    /// </summary>
    public void Enviar(int fase, int pontuacao, int acertos, int erros,
                       int aproveitamento, int tempoTotal,
                       string operacoesErradasJson, bool concluiuFase)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string tipoOperacao = GetTipoOperacao(fase);
        string json = $"{{" +
            $"\"fase\":{fase}," +
            $"\"tipo_operacao\":\"{tipoOperacao}\"," +
            $"\"pontuacao\":{pontuacao}," +
            $"\"acertos\":{acertos}," +
            $"\"erros\":{erros}," +
            $"\"aproveitamento\":{aproveitamento}," +
            $"\"tempo_total\":{tempoTotal}," +
            $"\"operacoes_erradas\":{operacoesErradasJson}," +
            $"\"concluiu_fase\":{(concluiuFase ? "true" : "false")}" +
        $"}}";

        Debug.Log("[GameResultSender] Enviando: " + json);
        EnviarResultadoFase(json);
#else
        Debug.Log("[GameResultSender] (editor) Envio ignorado fora do WebGL.");
#endif
    }

    public void IncrementarTentativa(int fase)
    {
        if (!tentativasPorFase.ContainsKey(fase)) tentativasPorFase[fase] = 1;
        tentativasPorFase[fase]++;
    }

    private int GetTentativa(int fase)
    {
        return tentativasPorFase.TryGetValue(fase, out int t) ? t : 1;
    }

    /// <summary>Usado pelo GameManager pra montar o relatório final (planilha nova).</summary>
    public int GetTentativaFinal(int fase) => GetTentativa(fase);

    /// <summary>
    /// Envia os dados de uma questão respondida.
    /// (rota já existente, formato inalterado — alimenta a planilha antiga)
    /// </summary>
    public void EnviarQuestaoAtual(int fase, string operacao, int numero,
        string conta, string respostaCorreta, string respostaAluno,
        bool acertou, float tempoSegundos)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int tentativa = GetTentativa(fase);
        string contaEscapada = conta.Replace("\"", "\\\"");
        string json = $"{{" +
            $"\"partida_id\":\"{partidaId}\"," +
            $"\"fase\":{fase}," +
            $"\"operacao\":\"{operacao}\"," +
            $"\"tentativa\":{tentativa}," +
            $"\"numero\":{numero}," +
            $"\"conta\":\"{contaEscapada}\"," +
            $"\"resposta_correta\":\"{respostaCorreta}\"," +
            $"\"resposta_aluno\":\"{respostaAluno}\"," +
            $"\"acertou\":{(acertou ? "true" : "false")}," +
            $"\"tempo_segundos\":{tempoSegundos.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}" +
        $"}}";
        Debug.Log("[GameResultSender] Enviando questao: " + json);
        EnviarQuestao(json);
#else
        Debug.Log("[GameResultSender] (editor) Envio de questao ignorado fora do WebGL.");
#endif
    }

    /// <summary>
    /// Chamado quando o jogo termina de verdade (vitória final).
    /// Envia o relatório completo já calculado — pontuação, %, tentativas por fase,
    /// totais e tempo de duração do jogo. Alimenta a planilha nova.
    /// </summary>
    public void EnviarRelatorioFinal(
        int pontuacaoFase1, float percentFase1, int tentativasFase1,
        int pontuacaoFase2, float percentFase2, int tentativasFase2,
        int pontuacaoFase3, float percentFase3, int tentativasFase3,
        int pontuacaoFase4, float percentFase4, int tentativasFase4,
        int pontuacaoTotal, float percentTotalPontos, float percentTotalFases)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        float tempoDuracaoJogo = 0f;
        if (!string.IsNullOrEmpty(inicioPartidaIso))
        {
            var inicio = System.DateTime.Parse(inicioPartidaIso, null, System.Globalization.DateTimeStyles.RoundtripKind);
            tempoDuracaoJogo = (float)(System.DateTime.UtcNow - inicio).TotalSeconds;
        }

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string json = $"{{" +
            $"\"partida_id\":\"{partidaId}\"," +
            $"\"pontuacao_fase_1\":{pontuacaoFase1},\"porcentagem_fase_1\":{percentFase1.ToString("0.0000", ci)},\"tentativas_fase_1\":{tentativasFase1}," +
            $"\"pontuacao_fase_2\":{pontuacaoFase2},\"porcentagem_fase_2\":{percentFase2.ToString("0.0000", ci)},\"tentativas_fase_2\":{tentativasFase2}," +
            $"\"pontuacao_fase_3\":{pontuacaoFase3},\"porcentagem_fase_3\":{percentFase3.ToString("0.0000", ci)},\"tentativas_fase_3\":{tentativasFase3}," +
            $"\"pontuacao_fase_4\":{pontuacaoFase4},\"porcentagem_fase_4\":{percentFase4.ToString("0.0000", ci)},\"tentativas_fase_4\":{tentativasFase4}," +
            $"\"pontuacao_total\":{pontuacaoTotal}," +
            $"\"porcentagem_total_pontos\":{percentTotalPontos.ToString("0.0000", ci)}," +
            $"\"tempo_duracao_jogo\":{tempoDuracaoJogo.ToString("0.0", ci)}," +
            $"\"porcentagem_total_fases\":{percentTotalFases.ToString("0.0000", ci)}" +
        $"}}";

        Debug.Log("[GameResultSender] Enviando relatorio final: " + json);
        EnviarRelatorio(json);
#else
        Debug.Log("[GameResultSender] (editor) Envio de relatorio final ignorado fora do WebGL.");
#endif
    }
}