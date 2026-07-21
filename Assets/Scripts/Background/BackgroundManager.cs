using UnityEngine;
using UnityEngine.SceneManagement;

public class BackgroundManager : MonoBehaviour
{
    [Header("Referência ao PixelBackground")]
    public PixelBackground pixelBg;

    public static BackgroundManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (pixelBg == null)
                pixelBg = GetComponent<PixelBackground>();

            if (pixelBg == null)
                pixelBg = gameObject.AddComponent<PixelBackground>();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (pixelBg == null)
            Debug.LogError("PixelBackground NÃO encontrado! Verifique a referência.");
        else
        {
            Debug.Log($"PixelBackground encontrado: {pixelBg.gameObject.name}");
            Debug.Log($"Tipo atual: {pixelBg.backgroundType}");
            Debug.Log($"Posição Z: {pixelBg.transform.position.z}");
        }
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryFindPixelBackground();
        if (GameManager.instance != null && GameManager.instance.JogoRodando())
            SetStage(GameManager.instance.GetFaseAtual());
        else
            SetMainMenu();
    }

    void TryFindPixelBackground()
    {
        if (pixelBg == null)
            pixelBg = FindObjectOfType<PixelBackground>();

        if (pixelBg == null)
            Debug.LogWarning("BackgroundManager: nenhum PixelBackground encontrado na cena!");
    }

    public void ChangeBackground(PixelBackground.BackgroundType type)
    {
        if (pixelBg == null)
        {
            TryFindPixelBackground();
            if (pixelBg == null) return;
        }
        pixelBg.backgroundType = type;
    }

    // ── Métodos públicos ────────────────────────────────────

    public void SetMainMenu()
    {
        ChangeBackground(PixelBackground.BackgroundType.MainMenu);
    }

    public void SetHowToPlay()
    {
        ChangeBackground(PixelBackground.BackgroundType.Overlay);
    }

    public void SetNextLevel()
    {
        ChangeBackground(PixelBackground.BackgroundType.Overlay);
    }

    public void SetGameOver()
    {
        ChangeBackground(PixelBackground.BackgroundType.GameOver);
    }

    public void SetPause()
    {
        ChangeBackground(PixelBackground.BackgroundType.Overlay);
    }

    public void SetStage(int stageNumber)
    {
        switch (stageNumber)
        {
            case 1: ChangeBackground(PixelBackground.BackgroundType.Stage1_Space); break;
            case 2: ChangeBackground(PixelBackground.BackgroundType.Stage2_Cave); break;
            case 3: ChangeBackground(PixelBackground.BackgroundType.Stage3_Forest); break;
            case 4: ChangeBackground(PixelBackground.BackgroundType.Stage4_City); break;
            default: Debug.LogWarning($"Fase {stageNumber} não existe!"); break;
        }
    }
}