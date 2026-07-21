using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    [Header("Músicas por Fase")]
    public AudioClip musicaFase1;
    public AudioClip musicaFase2;
    public AudioClip musicaFase3;
    public AudioClip musicaFase4;
    public AudioClip musicaBoss;

    [Header("Efeitos Sonoros")]
    public AudioClip somTiro;
    public AudioClip somExplosao;
    public AudioClip somAcerto;
    public AudioClip somErro;
    public AudioClip somPowerUp;
    public AudioClip somFaseCompleta;
    public AudioClip somGameOver;

    [Header("Volumes")]
    [Range(0f, 1f)] public float volumeMusica = 0.25f;
    [Range(0f, 1f)] public float volumeSFX = 1.0f;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource gameOverSource;

    private int faseAtualMusica = 1;

    void Awake()
    {
        instance = this;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = volumeMusica;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.volume = volumeSFX;

        // Toca independente do timeScale
        gameOverSource = gameObject.AddComponent<AudioSource>();
        gameOverSource.playOnAwake = false;
        gameOverSource.volume = volumeSFX;
        gameOverSource.ignoreListenerPause = true;
    }

    void Start()
    {
        TocarMusicaFase(1);
    }

    // ── Músicas ───────────────────────────────────────────────────────

    public void TocarMusicaFase(int fase)
    {
        faseAtualMusica = fase;
        AudioClip clip = fase switch
        {
            1 => musicaFase1,
            2 => musicaFase2,
            3 => musicaFase3,
            4 => musicaFase4,
            _ => musicaFase1
        };
        gameOverSource.Stop();
        TrocarMusica(clip);
    }

    public void TocarMusicaBoss()
    {
        if (musicaBoss == null) return;
        TrocarMusica(musicaBoss);
    }

    public void VoltarMusicaFase()
    {
        gameOverSource.Stop();
        TocarMusicaFase(faseAtualMusica);
    }

    void TrocarMusica(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.clip = clip;
        musicSource.volume = volumeMusica;
        musicSource.Play();
    }

    // ── Efeitos Sonoros ───────────────────────────────────────────────

    public void TocarTiro() => Tocar(somTiro);
    public void TocarExplosao() => Tocar(somExplosao);
    public void TocarAcerto() => Tocar(somAcerto);
    public void TocarPowerUp() => Tocar(somPowerUp);
    public void TocarFaseCompleta() => Tocar(somFaseCompleta);

    // FIX: TocarErro usa gameOverSource para tocar mesmo se timeScale for alterado
    // e para não ser cortado por outros sons no sfxSource
    public void TocarErro()
    {
        if (somErro == null) return;
        gameOverSource.Stop();
        gameOverSource.pitch = 1f;
        gameOverSource.clip = somErro;
        gameOverSource.Play();
    }

    public void TocarGameOver()
    {
        PararMusica();
        gameOverSource.Stop();
        if (somGameOver == null) return;
        gameOverSource.pitch = 1f;
        gameOverSource.clip = somGameOver;
        gameOverSource.Play();
    }

    void Tocar(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, 1f);
    }

    // ── Utilitários ───────────────────────────────────────────────────

    public void PararMusica() => musicSource.Stop();

    public void VolumeMusica(float v)
    {
        volumeMusica = v;
        musicSource.volume = v;
    }

    public void VolumeSFX(float v)
    {
        volumeSFX = v;
        sfxSource.volume = v;
        gameOverSource.volume = v;
    }
}