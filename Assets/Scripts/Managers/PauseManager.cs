using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PauseManager : MonoBehaviour
{
    public static PauseManager instance;

    [Header("UI Pause")]
    public GameObject pausePanel;
    public Button btnPause;
    public Button btnResumir;
    public Button btnMenuPrincipal;  // ← novo
    public Button btnRankingPause;   // ← novo

    private bool pausado = false;

    void Awake() { instance = this; }

    void Start()
    {
        pausePanel.SetActive(false);
        btnPause.onClick.AddListener(TogglePause);
        btnResumir.onClick.AddListener(Resumir);

        // Novos botões
        if (btnMenuPrincipal != null)
            btnMenuPrincipal.onClick.AddListener(VoltarMenuPrincipal);

        if (btnRankingPause != null)
            btnRankingPause.onClick.AddListener(AbrirRankingPause);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) &&
            GameManager.instance != null &&
            GameManager.instance.JogoRodando())
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (!GameManager.instance.JogoRodando() && !pausado) return;
        if (pausado) Resumir();
        else Pausar();
    }

    void Pausar()
    {
        pausado = true;
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
    }

    void Resumir()
    {
        pausado = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }

    void VoltarMenuPrincipal()
    {
        // Reseta o estado do jogo e volta ao menu
        pausado = false;
        Time.timeScale = 1f;
        pausePanel.SetActive(false);

        // Destroi inimigos que estiverem na tela
        var inimigos = GameObject.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        foreach (var e in inimigos) Destroy(e.gameObject);

        // Volta ao menu principal via GameManager
        if (GameManager.instance != null)
            GameManager.instance.VoltarAoMenu();
    }

    void AbrirRankingPause()
    {
        // Abre o ranking sem sair do pause
        if (RankingManager.instance != null)
            RankingManager.instance.AbrirRanking();
    }

    public bool EstaPausado() => pausado;
}