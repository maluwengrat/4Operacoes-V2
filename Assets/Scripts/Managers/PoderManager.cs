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
    private bool selecionandoAlvo = false; // painel de escolha de adversário aberto
    private bool[] poderDesbloqueado = new bool[9]; // false por padrão = ainda não conquistado

    // Retorna o índice do poder atualmente carregado (-1 se nenhum)
    public int GetPoderAtual() => poderAtual;

    // Arrays com 9 posições (7 originais + Escudo + TempoLento)
    private float[] cooldowns = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    private float[] cooldownsMax = { 120f, 180f, 90f, 90f, 150f, 120f, 60f, 100f, 130f };

    // Sequência de acertos necessária pra carregar cada poder (rebalanceado:
    // carregar um poder deve ser uma conquista, não algo que acontece a cada minuto).
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
    // IniciarEscutaSeNecessario() nunca mais faz nada.
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
        int[] limiares = streakNecessario;

        if (isBoss && streakAtual >= limiares[(int)TipoPoder.Tempestade])
        {
            CarregarPoder((int)TipoPoder.Tempestade); // só carrega, não ativa!
            return;
        }

        // A resposta rápida só decide qual poder carrega quando a sequência mínima já foi atingida.
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
                CarregarPoder(i); // só carrega, não ativa!
                break;
            }
        }
    }

    public void RegistrarErro()
    {
        // No Solo o erro tira 5 de streak; na Turma tira 2, porque ali o ritmo é bem
        // mais lento e errar uma conta faz parte do aprendizado.
        int penalidade = GameManager.modoAtual == GameManager.ModoJogo.Turma ? 2 : 5;
        streakAtual = Mathf.Max(0, streakAtual - penalidade);

        if (poderAtual >= 0 && poderAtual <= 3)
        {
            MostrarNotificacao("Poder perdido pelo erro!");
            poderAtual = -1;

            // Se o aluno estava escolhendo o alvo desse poder, fecha o painel
            if (selecionandoAlvo && selecionarAlvoUI != null)
                selecionarAlvoUI.FecharSeletor();

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
                // Poder carregado — dourado
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
        if (selecionandoAlvo) return; // já está escolhendo o alvo

        Debug.Log("[PoderManager] UsarPoder index=" + index
            + " tipo=" + (TipoPoder)index
            + " | GameManager.modoAtual=" + GameManager.modoAtual);

        // Cada Ativar... devolve true se o poder foi usado na hora, ou false se o
        // gasto fica pendente (painel de alvo aberto) ou se não deu pra usar.
        bool consumidoAgora = true;

        switch ((TipoPoder)index)
        {
            case TipoPoder.InversaoResposta: consumidoAgora = AtivarInversaoResposta(); break;
            case TipoPoder.Tempestade: consumidoAgora = AtivarTempestade(); break;
            case TipoPoder.CronometraMaldito: consumidoAgora = AtivarCronometraMaldito(); break;
            case TipoPoder.Distorcao: consumidoAgora = AtivarDistorcao(); break;
            case TipoPoder.EscudoDuplo: AtivarEscudoDuplo(); break;
            case TipoPoder.CongelarRival: consumidoAgora = AtivarCongelarRival(); break;
            case TipoPoder.DicaRelampago: AtivarDicaRelampago(); break;
            case TipoPoder.Escudo: AtivarEscudoSimples(); break;
            case TipoPoder.TempoLento: AtivarTempoLentoPoder(); break;
        }

        if (consumidoAgora)
            ConsumirPoder(index);
    }

    // Gasta o poder: inicia o cooldown e descarrega
    void ConsumirPoder(int index)
    {
        selecionandoAlvo = false;
        cooldowns[index] = cooldownsMax[index];
        if (poderAtual == index) poderAtual = -1;
        AtualizarVisualPoderes();
    }

    // ─────────────────────────────────────────────────────────────────
    // Implementação dos poderes
    //
    // Poderes ofensivos na Turma abrem o seletor de alvo. O poder só é gasto
    // quando o jogador escolhe alguém; se cancelar (ou perder o poder por erro),
    // ele continua carregado.
    // ─────────────────────────────────────────────────────────────────

    // Abre o seletor de alvo. Devolve sempre false: o gasto acontece no callback.
    bool AbrirSeletorParaPoder(int index, string tipoRede, string mensagemEnviado)
    {
        if (selecionarAlvoUI == null)
        {
            Debug.LogWarning("[PoderManager] selecionarAlvoUI não configurado no Inspector.");
            return false; // poder continua carregado
        }

        selecionandoAlvo = true;
        selecionarAlvoUI.AbrirSeletorDeAlvo(
            tipoRede,
            () =>
            {
                MostrarNotificacao(mensagemEnviado);
                ConsumirPoder(index);
            },
            () => { selecionandoAlvo = false; });

        return false;
    }

    bool AtivarInversaoResposta()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
            return AbrirSeletorParaPoder((int)TipoPoder.InversaoResposta, "inversao",
                "Inversão enviada ao adversário!");

        StartCoroutine(DestacarRespostaCerta(15f));
        MostrarNotificacao("Inversão ativada! Resposta certa em destaque!");
        return true;
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

    bool AtivarTempestade()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
            return AbrirSeletorParaPoder((int)TipoPoder.Tempestade, "tempestade",
                "Tempestade enviada ao adversário!");

        StartCoroutine(SpawnInimigosBonus(2));
        MostrarNotificacao("Tempestade! Inimigos com a resposta certa!");
        return true;
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

    bool AtivarCronometraMaldito()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
            return AbrirSeletorParaPoder((int)TipoPoder.CronometraMaldito, "cronometro",
                "Cronômetro enviado ao adversário!");

        if (GameManager.instance != null)
            GameManager.instance.ReducirTimer(-8f);
        MostrarNotificacao("Cronômetro! +8s no seu timer!");
        return true;
    }

    bool AtivarDistorcao()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
            return AbrirSeletorParaPoder((int)TipoPoder.Distorcao, "acelerador",
                "Distorção enviada ao adversário!");

        StartCoroutine(AcelerarInimigos(5f, 0.5f));
        MostrarNotificacao("Distorção! Inimigos 50% mais lentos por 5s!");
        return true;
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

    bool AtivarCongelarRival()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
            return AbrirSeletorParaPoder((int)TipoPoder.CongelarRival, "congelar",
                "Congelar enviado ao adversário!");

        StartCoroutine(CongelarInimigos(4f));
        MostrarNotificacao("Inimigos congelados por 4s!");
        return true;
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

        // Se o painel de alvo estava aberto, fecha (e zera o estado de seleção)
        if (selecionandoAlvo && selecionarAlvoUI != null)
            selecionarAlvoUI.FecharSeletor();
        selecionandoAlvo = false;

        for (int i = 0; i < cooldowns.Length; i++)
            cooldowns[i] = 0f;

        // Os poderes voltam a aparecer "bloqueados/cinza" até serem conquistados de novo
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