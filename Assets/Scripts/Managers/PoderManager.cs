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
    Acelerador,
    EscudoDuplo,
    CongelarRival,
    DicaRelampago
}

public class PoderManager : MonoBehaviour
{
    public static PoderManager instance;

    [Header("UI Poderes")]
    public GameObject painelPoderes;
    public Button[] btnPoderes;
    public TextMeshProUGUI[] txtCooldownPoderes;
    public Image[] imgPoderes;

    // ── Estado interno ────────────────────────────────────────────────
    private int streakAtual = 0;
    private int poderAtual = -1;
    private bool escudoAtivo = false;

    private float[] cooldowns = { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    private float[] cooldownsMax = { 120f, 180f, 90f, 90f, 150f, 120f, 60f };
    private int[] streakNecessario = { 7, 15, 5, 5, 10, 12, 8 };

    private string[] nomesPoderes = {
        "Inversão", "Tempestade", "Cronômetro",
        "Acelerador", "Escudo Duplo", "Congelar", "Dica"
    };

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

        if (isBoss && streakAtual >= streakNecessario[(int)TipoPoder.Tempestade])
        {
            CarregarPoder((int)TipoPoder.Tempestade); // ← só carrega, não ativa!
            return;
        }

        if (tempoResposta < 3f && cooldowns[(int)TipoPoder.CronometraMaldito] <= 0f && poderAtual == -1)
        {
            CarregarPoder((int)TipoPoder.CronometraMaldito);
            return;
        }

        for (int i = 0; i < streakNecessario.Length; i++)
        {
            if (streakAtual >= streakNecessario[i] && cooldowns[i] <= 0f && poderAtual == -1)
            {
                CarregarPoder(i); // ← só carrega, não ativa!
                break;
            }
        }
    }

    public void RegistrarErro()
    {
        streakAtual = 0;

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

        switch ((TipoPoder)index)
        {
            case TipoPoder.InversaoResposta: AtivarInversaoResposta(); break;
            case TipoPoder.Tempestade: AtivarTempestade(); break;
            case TipoPoder.CronometraMaldito: AtivarCronometraMaldito(); break;
            case TipoPoder.Acelerador: AtivarAcelerador(); break;
            case TipoPoder.EscudoDuplo: AtivarEscudoDuplo(); break;
            case TipoPoder.CongelarRival: AtivarCongelarRival(); break;
            case TipoPoder.DicaRelampago: AtivarDicaRelampago(); break;
        }

        cooldowns[index] = cooldownsMax[index];
        poderAtual = -1;
        AtualizarVisualPoderes();
    }

    // ─────────────────────────────────────────────────────────────────
    // Implementação dos poderes
    // ─────────────────────────────────────────────────────────────────

    void AtivarInversaoResposta()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            FirebaseManager.instance.EnviarPoderParaAdversario("inversao");
            MostrarNotificacao("Inversão enviada ao adversário!");
        }
        else
        {
            StartCoroutine(EmbaralharInimigos(15f));
            MostrarNotificacao("Inversão ativada! Números embaralhados!");
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

    void AtivarTempestade()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            FirebaseManager.instance.EnviarPoderParaAdversario("tempestade");
            MostrarNotificacao("Tempestade enviada ao adversário!");
        }
        else
        {
            StartCoroutine(SpawnInimigosExtras(2));
            MostrarNotificacao("Tempestade! Inimigos extras spawnados!");
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

    void AtivarCronometraMaldito()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            FirebaseManager.instance.EnviarPoderParaAdversario("cronometro");
            MostrarNotificacao("Cronômetro enviado ao adversário!");
        }
        else
        {
            if (GameManager.instance != null)
                GameManager.instance.ReducirTimer(-8f);
            MostrarNotificacao("Cronômetro! +8s no seu timer!");
        }
    }

    void AtivarAcelerador()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            FirebaseManager.instance.EnviarPoderParaAdversario("acelerador");
            MostrarNotificacao("Acelerador enviado ao adversário!");
        }
        else
        {
            StartCoroutine(CongelarInimigos(5f));
            MostrarNotificacao("Acelerador! Inimigos congelados por 5s!");
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
        escudoAtivo = true;
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.AtivarEscudo(60f);
        MostrarNotificacao("Escudo Duplo ativado!");
    }

    void AtivarCongelarRival()
    {
        if (GameManager.modoAtual == GameManager.ModoJogo.Turma)
        {
            FirebaseManager.instance.EnviarPoderParaAdversario("congelar");
            MostrarNotificacao("Congelar enviado ao adversário!");
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
                StartCoroutine(CongelarInimigos(4f));
                MostrarNotificacao("⚠ Adversário usou Congelar!");
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

    void MostrarNotificacao(string msg)
    {
        if (FeedbackManager.instance != null)
            FeedbackManager.instance.MostrarMensagem(msg, new Color(1f, 0.8f, 0f));
    }
}