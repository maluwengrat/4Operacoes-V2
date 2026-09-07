using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TipoPoder
{
    InversaoResposta,
    Tempestade,
    CronometraMaldito,
    Distorcao,
    EscudoDuplo,
    CongelarRival,
    DicaRelampago,
    Escudo,
    TempoLento
}

public class PoderManager : MonoBehaviour
{
    public static PoderManager instance;

    [Header("UI Poderes")]
    public GameObject painelPoderes;
    public Button[] btnPoderes;
    public TextMeshProUGUI[] txtCooldownPoderes;
    public Image[] imgPoderes;

    [Header("Modo Turma")]
    public SelecionarAlvoUI selecionarAlvoUI;

    [Header("Duração dos efeitos (Escudo / Tempo Lento)")]
    public float duracaoEscudo = 8f;
    public float duracaoTempoLento = 8f;

    // ── Estado interno ────────────────────────────────────────────────
    private int streakAtual = 0;
    private int poderAtual = -1;
    private bool escudoAtivo = false;
    private bool[] poderDesbloqueado = new bool[9]; // false por padrão = ainda não conquistado
                                                    // Retorna o índice do poder atualmente carregado (-1 se nenhum)
    public int GetPoderAtual() => poderAtual;

    // Arrays com 9 posições agora (7 originais + Escudo + TempoLento)
    private float[] cooldowns = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    private float[] cooldownsMax = { 120f, 180f, 90f, 90f, 150f, 120f, 60f, 100f, 130f };

    // REBALANCEADO: os valores anteriores (4, 8, 5, 5, 5, 6, 3, 4, 5) faziam
    // quase todo poder carregar em bem menos de uma fase (7 ondas), e o erro
    // só tirava 2 de streak — praticamente sem penalidade real. Agora os
    // thresholds exigem sequências mais próximas de 1 a 2 fases inteiras,
    // e a penalidade de erro foi aumentada (ver RegistrarErro), então
    // carregar um poder volta a ser uma conquista de verdade, não algo
    // que acontece a cada minuto.
    //
    // Isso vale pro modo SOLO, onde uma onda nova nasce a cada poucos
    // segundos — dá pra emendar 8-14 acertos rápido. Ver streakNecessarioTurma
    // logo abaixo pro modo Turma, onde o ritmo é bem mais lento.
    private int[] streakNecessario = { 8, 14, 9, 9, 9, 10, 6, 7, 9 };


    private string[] nomesPoderes = {
        "Inversão", "Tempestade", "Cronômetro",
        "Distorção", "Escudo Duplo", "Congelar", "Dica",
        "Escudo", "Tempo Lento"
    };

    // Evita registrar o polling duas vezes (Start() + chamada explícita
    // do GameManager) na mesma partida.
    private bool escutandoPoderes = false;

    void Awake() { instance = this; }

    void Start()
    {
        if (painelPoderes != null)
            painelPoderes.SetActive(true);

        for (int i = 0; i < btnPoderes.Length; i++)
        {
            int index = i;
            if (btnPoderes[i] != null)
                btnPoderes[i].onClick.AddListener(() => UsarPoder(index));
        }

        AtualizarVisualPoderes();
        IniciarEscutaSeNecessario();
    }

    public void IniciarEscutaSeNecessario()
    {
        if (escutandoPoderes) return;

        if (GameManager.modoAtual == GameManager.ModoJogo.Turma
            && FirebaseManager.instance != null)
        {
            escutandoPoderes = true;
            FirebaseManager.instance.IniciarEscutaPoderes(ReceberPoderDoAdversario);
        }
    }

    // Chame isso ao voltar pro menu, senão numa segunda partida Turma
    // (mesma sessão do jogo) IniciarEscutaSeNecessario() nunca mais faz
    // nada, porque a trava já ficou true pra sempre desde a primeira.
    public void PararEscuta()
    {
        escutandoPoderes = false;
    }

    void Update()
    {
        bool algumCooldownAtivo = false;
        for (int i = 0; i < cooldowns.Length; i++)
        {
            if (cooldowns[i] > 0f)
            {
                cooldowns[i] -= Time.deltaTime;
                algumCooldownAtivo = true;
            }
        }

        // Atualiza visual a cada frame se há cooldown ativo
        if (algumCooldownAtivo || poderAtual != -1)
            AtualizarVisualPoderes();

        // Atualiza textos de cooldown
        for (int i = 0; i < cooldowns.Length; i++)
            AtualizarUIPoderIndex(i);
    }

    // ─────────────────────────────────────────────────────────────────
    // Streak
    // ─────────────────────────────────────────────────────────────────

    public void RegistrarAcerto(bool isBoss, float tempoResposta)
    {
            streakAtual++;
            int[] limiares = streakNecessario;   // agora sempre o mesmo array, independente do mo

            if (isBoss && streakAtual >= limiares[(int)TipoPoder.Tempestade])
        {
            CarregarPoder((int)TipoPoder.Tempestade); // ← só carrega, não ativa!
            return;
        }

        // CORRIGIDO: antes, essa condição ignorava completamente a sequência
        // (streakAtual) — qualquer acerto abaixo de 3s carregava o poder,
        // não importa se era o primeiro acerto da partida ou o vigésimo.
        // Numa fase simples (ex.: adição), responder rápido é trivial, então
        // esse poder aparecia o tempo todo. Agora ele também precisa da
        // sequência mínima, igual aos outros — a resposta rápida só decide
        // qual poder carrega quando os dois critérios já foram atingidos.
        if (tempoResposta < 3f && streakAtual >= limiares[(int)TipoPoder.CronometraMaldito]
            && cooldowns[(int)TipoPoder.CronometraMaldito] <= 0f && poderAtual == -1)
        {
            CarregarPoder((int)TipoPoder.CronometraMaldito);
            return;
        }

        for (int i = 0; i < limiares.Length; i++)
        {
            if (streakAtual >= limiares[i] && cooldowns[i] <= 0f && poderAtual == -1)
            {
                CarregarPoder(i); // ← só carrega, não ativa!
                break;
            }

        }
    }

    public void RegistrarErro()
    {
        // REBALANCEADO: antes tirava só 2 de streak por erro, o que na
        // prática não penalizava quase nada (bastava 1 acerto extra pra
        // "recuperar" o erro). No Solo agora tira 5 — um erro dói de
        // verdade e atrasa o próximo poder.
        //
        // NOVO: no Turma isso ficaria desproporcional, porque os limiares
        // já são bem menores (3-5). Tirar 5 de um streak que só precisa
        // chegar a 3 zeraria o progresso inteiro num único erro — punitivo
        // demais pra sala de aula, onde errar uma conta faz parte do
        // aprendizado. Usa uma penalidade menor lá.
        int penalidade = GameManager.modoAtual == GameManager.ModoJogo.Turma ? 2 : 5;
        streakAtual = Mathf.Max(0, streakAtual - penalidade);

        if (poderAtual >= 0 && poderAtual <= 3)
        {
            MostrarNotificacao("Poder perdido pelo erro!");
            poderAtual = -1;
            AtualizarVisualPoderes();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Carregar poder
    // ─────────────────────────────────────────────────────────────────

    void CarregarPoder(int index)
    {
        if (poderAtual != -1) return;
        poderAtual = index;
        poderDesbloqueado[index] = true;
        MostrarNotificacao("Poder carregado: " + nomesPoderes[index] + "!");
        AtualizarVisualPoderes();
    }

    void AtualizarVisualPoderes()
    {
        for (int i = 0; i < btnPoderes.Length; i++)
        {
            if (btnPoderes[i] == null) continue;

            Image img = btnPoderes[i].GetComponent<Image>();
            CanvasGroup cg = btnPoderes[i].GetComponent<CanvasGroup>();
            if (cg == null) cg = btnPoderes[i].gameObject.AddComponent<CanvasGroup>();

            if (!poderDesbloqueado[i])
            {
                if (img != null) img.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                cg.alpha = 0.6f;
                cg.interactable = false;
                continue;
            }

            if (i == poderAtual)
            {
                // Poder carregado — dourado e pulsando
                if (img != null) img.color = new Color(1f, 0.85f, 0f, 1f);
                cg.alpha = 1f;
                cg.interactable = true;
            }
            else if (cooldowns[i] > 0f)
            {
                // Em cooldown — escurecido e desabilitado
                if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                cg.alpha = 0.5f;
                cg.interactable = false;
            }
            else
            {
                // Disponível mas não carregado — normal
                if (img != null) img.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                cg.alpha = 0.8f;
                cg.interactable = false; // só usa quando carregado
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Usar poder
    // ─────────────────────────────────────────────────────────────────

    public void UsarPoder(int index)
    {
        if (index != poderAtual) return;
        if (cooldowns[index] > 0f) return;

        Debug.Log("[PoderManager] UsarPoder index=" + index
            + " tipo=" + (TipoPoder)index
            + " | GameManager.modoAtual=" + GameManager.modoAtual);

        switch ((TipoPoder)index)
        {
            case TipoPoder.InversaoResposta: AtivarInversaoResposta(); break;
            case TipoPoder.Tempestade: AtivarTempestade(); break;
            case TipoPoder.CronometraMaldito: AtivarCronometraMaldito(); break;
            case TipoPoder.Distorcao: AtivarDistorcao(); break;
            case TipoPoder.EscudoDuplo: AtivarEscudoDuplo(); break;
            case TipoPoder.CongelarRival: AtivarCongelarRival(); break;
            case TipoPoder.DicaRelampago: AtivarDicaRelampago(); break;
            case TipoPoder.Escudo: AtivarEscudoSimples(); break;
            case TipoPoder.TempoLento: AtivarTempoLentoPoder(); break;
        }

        cooldowns[index] = cooldownsMax[index];
        poderAtual = -1;
        AtualizarVisualPoderes();
    }

    // ─────────────────────────────────────────────────────────────────
    // Implementação dos poderes
    //
    // Nos poderes que funcionam em Turma: antes o código chamava
    // FirebaseManager.instance.EnviarPoderParaAdversario(tipo) direto,
    // com 1 argumento só — isso escolhe um alvo sozinho, sem deixar o
    // jogador escolher quem recebe (numa turma de 30, ia mirar em
    // alguém arbitrário). Agora abre o seletor de alvo primeiro; o
    // envio de fato e a notificação só acontecem depois que o jogador
    // escolhe alguém na lista.
    // ─────────────────────────────────────────────────────────────────

    void AtivarInversaoResposta()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            if (selecionarAlvoUI != null)
            {
                selecionarAlvoUI.AbrirSeletorDeAlvo("inversao",
                    () => MostrarNotificacao("Inversão enviada ao adversário!"));
            }
            else
            {
                Debug.LogWarning("[PoderManager] selecionarAlvoUI não configurado no Inspector.");
            }
        }
        else
        {
            StartCoroutine(DestacarRespostaCerta(15f));
            MostrarNotificacao("Inversão ativada! Resposta certa em destaque!");
        }
    }

    IEnumerator EmbaralharInimigos(float duracao)
    {
        var inimigos = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        int respostaCorreta = GameManager.instance.GetCorrectAnswer();

        Dictionary<Enemy, int> numerosOriginais = new Dictionary<Enemy, int>();
        foreach (var e in inimigos)
            numerosOriginais[e] = e.myNumber;

        List<int> numerosErrados = new List<int>();
        foreach (var e in inimigos)
            if (e.myNumber != respostaCorreta)
                numerosErrados.Add(e.myNumber + Random.Range(-3, 4));

        int j = 0;
        foreach (var e in inimigos)
        {
            if (e.myNumber != respostaCorreta && j < numerosErrados.Count)
            {
                e.SetNumber(numerosErrados[j]);
                j++;
            }
        }

        yield return new WaitForSeconds(duracao);

        foreach (var e in inimigos)
            if (e != null && numerosOriginais.ContainsKey(e))
                e.SetNumber(numerosOriginais[e]);
    }

    IEnumerator DestacarRespostaCerta(float duracao)
    {
        var inimigos = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        int respostaCorreta = GameManager.instance.GetCorrectAnswer();

        Enemy alvo = null;
        foreach (var e in inimigos)
            if (e.myNumber == respostaCorreta) { alvo = e; break; }

        if (alvo == null)
        {
            Debug.LogWarning("Inversão: nenhum inimigo com a resposta correta foi encontrado em cena.");
            yield break;
        }

        SpriteRenderer sr = alvo.GetComponentInChildren<SpriteRenderer>();
        TMPro.TextMeshPro label = alvo.numberLabel;

        Color corOriginalSprite = sr != null ? sr.color : Color.white;
        Color corOriginalTexto = label != null ? label.color : Color.white;
        float timer = 0f;

        while (timer < duracao)
        {
            if (alvo == null) yield break;

            float pulso = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
            Color corDestaque = new Color(1f, 0.85f, 0f);

            if (sr != null)
                sr.color = Color.Lerp(corOriginalSprite, corDestaque, pulso * 0.7f);

            if (label != null)
                label.color = Color.Lerp(corOriginalTexto, corDestaque, pulso * 0.85f);

            timer += Time.deltaTime;
            yield return null;
        }

        if (alvo != null)
        {
            if (sr != null) sr.color = corOriginalSprite;
            if (label != null) label.color = corOriginalTexto;
        }
    }
    void AtivarTempestade()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            if (selecionarAlvoUI != null)
            {
                selecionarAlvoUI.AbrirSeletorDeAlvo("tempestade",
                    () => MostrarNotificacao("Tempestade enviada ao adversário!"));
            }
            else
            {
                Debug.LogWarning("[PoderManager] selecionarAlvoUI não configurado no Inspector.");
            }
        }
        else
        {
            StartCoroutine(SpawnInimigosBonus(2));
            MostrarNotificacao("Tempestade! Inimigos com a resposta certa!");
        }
    }

    IEnumerator SpawnInimigosExtras(int quantidade)
    {
        for (int i = 0; i < quantidade; i++)
        {
            float x = Random.Range(-6f, 6f);
            Vector3 pos = new Vector3(x, 5f, 0);
            int numeroErrado = GameManager.instance.GetCorrectAnswer() + Random.Range(1, 8);

            if (GameManager.instance.enemyPrefab != null)
            {
                GameObject go = Instantiate(GameManager.instance.enemyPrefab, pos, Quaternion.identity);
                Enemy e = go.GetComponent<Enemy>();
                if (e != null)
                {
                    e.SetNumber(numeroErrado);
                    e.speed = 0.8f;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    // Versão "benéfica" da Tempestade, usada no Solo/Tutorial: spawna inimigos extras
    // já com a resposta CERTA, aumentando as chances do jogador acertar.
    IEnumerator SpawnInimigosBonus(int quantidade)
    {
        for (int i = 0; i < quantidade; i++)
        {
            float x = Random.Range(-6f, 6f);
            Vector3 pos = new Vector3(x, 5f, 0);
            int numeroCorreto = GameManager.instance.GetCorrectAnswer();

            if (GameManager.instance.enemyPrefab != null)
            {
                GameObject go = Instantiate(GameManager.instance.enemyPrefab, pos, Quaternion.identity);
                Enemy e = go.GetComponent<Enemy>();
                if (e != null)
                {
                    e.SetNumber(numeroCorreto);
                    e.speed = 0.8f;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    void AtivarCronometraMaldito()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            if (selecionarAlvoUI != null)
            {
                selecionarAlvoUI.AbrirSeletorDeAlvo("cronometro",
                    () => MostrarNotificacao("Cronômetro enviado ao adversário!"));
            }
            else
            {
                Debug.LogWarning("[PoderManager] selecionarAlvoUI não configurado no Inspector.");
            }
        }
        else
        {
            if (GameManager.instance != null)
                GameManager.instance.ReducirTimer(-8f);
            MostrarNotificacao("Cronômetro! +8s no seu timer!");
        }
    }

    void AtivarDistorcao()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            if (selecionarAlvoUI != null)
            {
                selecionarAlvoUI.AbrirSeletorDeAlvo("acelerador",
                    () => MostrarNotificacao("Distorção enviada ao adversário!"));
            }
            else
            {
                Debug.LogWarning("[PoderManager] selecionarAlvoUI não configurado no Inspector.");
            }
        }
        else
        {
            StartCoroutine(AcelerarInimigos(5f, 0.5f));
            MostrarNotificacao("Distorção! Inimigos 50% mais lentos por 5s!");
        }
    }

    IEnumerator AcelerarInimigos(float duracao, float multiplicador)
    {
        var inimigos = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        foreach (var e in inimigos) e.speed *= multiplicador;
        yield return new WaitForSeconds(duracao);
        foreach (var e in inimigos)
            if (e != null) e.speed /= multiplicador;
    }

    void AtivarEscudoDuplo()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();

        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            escudoAtivo = true; // bloqueia o PRÓXIMO poder recebido do adversário
            if (player != null) player.AtivarEscudo(60f);
            MostrarNotificacao("Escudo Duplo ativado! Bloqueia o próximo poder do adversário.");
        }
        else
        {
            // No Solo não existe poder de adversário pra bloquear — aqui
            // "duplo" vira dois efeitos de proteção ao mesmo tempo:
            // escudo contra erro + tempo lento nos inimigos.
            if (player != null) player.AtivarEscudo(duracaoEscudo);

            if (GameManager.instance != null)
                GameManager.instance.AtivarTempoLento(duracaoTempoLento);

            if (TempoLentoEffect.instance != null)
                TempoLentoEffect.instance.Ativar(duracaoTempoLento);

            MostrarNotificacao("Escudo Duplo! Escudo + Tempo Lento ativados!");
        }
    }

    void AtivarCongelarRival()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            if (selecionarAlvoUI != null)
            {
                selecionarAlvoUI.AbrirSeletorDeAlvo("congelar",
                    () => MostrarNotificacao("Congelar enviado ao adversário!"));
            }
            else
            {
                Debug.LogWarning("[PoderManager] selecionarAlvoUI não configurado no Inspector.");
            }
        }
        else
        {
            StartCoroutine(CongelarInimigos(4f));
            MostrarNotificacao("Inimigos congelados por 4s!");
        }
    }

    IEnumerator CongelarInimigos(float duracao)
    {
        var inimigos = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        float[] velocidadesOriginais = new float[inimigos.Length];

        for (int i = 0; i < inimigos.Length; i++)
        {
            velocidadesOriginais[i] = inimigos[i].speed;
            inimigos[i].speed = 0f;
        }

        yield return new WaitForSeconds(duracao);

        for (int i = 0; i < inimigos.Length; i++)
            if (inimigos[i] != null)
                inimigos[i].speed = velocidadesOriginais[i];
    }

    void AtivarDicaRelampago()
    {
        StartCoroutine(EsconderInimigosErrados(6f));
        MostrarNotificacao("Dica! 2 inimigos errados removidos!");
    }

    IEnumerator EsconderInimigosErrados(float duracao)
    {
        var inimigos = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        int respostaCorreta = GameManager.instance.GetCorrectAnswer();

        List<Enemy> removidosList = new List<Enemy>();
        int removidos = 0;

        foreach (var e in inimigos)
        {
            if (removidos >= 2) break;
            if (e.myNumber != respostaCorreta)
            {
                e.gameObject.SetActive(false);
                removidosList.Add(e);
                removidos++;
            }
        }

        yield return new WaitForSeconds(duracao);

        foreach (var e in removidosList)
            if (e != null) e.gameObject.SetActive(true);
    }

    // ── Escudo simples e Tempo Lento (ex-coletáveis) ────────────────────

    void AtivarEscudoSimples()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.AtivarEscudo(duracaoEscudo);
        MostrarNotificacao("Escudo ativado!");
    }

    void AtivarTempoLentoPoder()
    {
        if (GameManager.instance != null)
            GameManager.instance.AtivarTempoLento(duracaoTempoLento);

        if (TempoLentoEffect.instance != null)
            TempoLentoEffect.instance.Ativar(duracaoTempoLento);

        MostrarNotificacao("Tempo lento ativado!");
    }

    // ─────────────────────────────────────────────────────────────────
    // Receber poder do adversário (modo Turma)
    // ─────────────────────────────────────────────────────────────────

    public void ReceberPoderDoAdversario(string tipoPoder)
    {
        // Verifica se o Escudo Duplo bloqueia
        if (TentarBloquearPoder()) return;

        switch (tipoPoder)
        {
            case "inversao":
                StartCoroutine(EmbaralharInimigos(15f));
                MostrarNotificacao("⚠ Adversário usou Inversão!");
                break;
            case "tempestade":
                StartCoroutine(SpawnInimigosExtras(2));
                MostrarNotificacao("⚠ Adversário usou Tempestade!");
                break;
            case "cronometro":
                if (GameManager.instance != null)
                    GameManager.instance.ReducirTimer(8f);
                MostrarNotificacao("⚠ Adversário usou Cronômetro! -8s!");
                break;
            case "acelerador":
                StartCoroutine(AcelerarInimigos(20f, 1.4f));
                MostrarNotificacao("⚠ Adversário usou Acelerador!");
                break;
            case "congelar":
                {
                    PlayerController playerAlvo = FindFirstObjectByType<PlayerController>();
                    if (playerAlvo != null) playerAlvo.TravarTiro(4f);
                    MostrarNotificacao("⚠ Adversário travou sua mira por 4s!");
                }
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Escudo Duplo
    // ─────────────────────────────────────────────────────────────────

    public bool TentarBloquearPoder()
    {
        if (escudoAtivo)
        {
            escudoAtivo = false;
            MostrarNotificacao("Escudo Duplo bloqueou o poder!");
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────────────────────────

    void AtualizarUIPoderIndex(int index)
    {
        if (index >= txtCooldownPoderes.Length) return;
        if (txtCooldownPoderes[index] != null)
        {
            txtCooldownPoderes[index].text = cooldowns[index] > 0f
                ? Mathf.CeilToInt(cooldowns[index]) + "s"
                : "";
        }
    }

    public void ResetarPoderes()
    {
        streakAtual = 0;
        poderAtual = -1;
        escudoAtivo = false;

        for (int i = 0; i < cooldowns.Length; i++)
            cooldowns[i] = 0f;

        // Se quiser que os poderes voltem a aparecer "bloqueados/cinza" até
        // serem conquistados de novo na nova partida, resete também:
        for (int i = 0; i < poderDesbloqueado.Length; i++)
            poderDesbloqueado[i] = false;

        AtualizarVisualPoderes();
    }

    void MostrarNotificacao(string msg)
    {
        // No modo Tutorial, quem explica tudo é o TutorialManager via TutorialBalao —
        // evita duplicar com a notificação nativa do FeedbackManager (carregar E usar poder).
        if (GameManager.modoAtual == GameManager.ModoJogo.Tutorial) return;

        if (FeedbackManager.instance != null)
            FeedbackManager.instance.MostrarMensagem(msg, new Color(1f, 0.8f, 0f));
    }
}