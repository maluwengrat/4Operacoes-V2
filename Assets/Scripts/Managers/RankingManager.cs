using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RankingManager : MonoBehaviour
{
    public static RankingManager instance;

    [Header("Painel")]
    public GameObject rankingPanel;
    public Button btnFecharRanking;
    public Button btnAbrirRanking;

    [Header("Lista")]
    public Transform contentContainer;
    public GameObject itemRankingPrefab;

    [Header("Texto vazio")]
    public TextMeshProUGUI textoCarregando;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        rankingPanel.SetActive(false);

        if (btnFecharRanking != null)
            btnFecharRanking.onClick.AddListener(FecharRanking);

        if (btnAbrirRanking != null)
            btnAbrirRanking.onClick.AddListener(AbrirRanking);
    }

    public void AbrirRanking()
    {
        rankingPanel.SetActive(true);
        Time.timeScale = 0f;

        // Limpa lista antiga
        foreach (Transform filho in contentContainer)
            Destroy(filho.gameObject);

        if (textoCarregando != null)
        {
            textoCarregando.gameObject.SetActive(true);
            textoCarregando.text = "Carregando ranking...";
        }

        // Busca dados no Firebase
        if (FirebaseManager.instance != null)
            FirebaseManager.instance.BuscarRanking(MostrarRanking);
        else
            MostrarErro("Firebase não conectado.");
    }

    void MostrarRanking(List<DadosJogador> jogadores)
    {
        if (textoCarregando != null)
            textoCarregando.gameObject.SetActive(false);

        if (jogadores.Count == 0)
        {
            if (textoCarregando != null)
            {
                textoCarregando.gameObject.SetActive(true);
                textoCarregando.text = "Nenhum resultado ainda!";
            }
            return;
        }

        for (int i = 0; i < jogadores.Count; i++)
        {
            DadosJogador j = jogadores[i];

            // Cria item na lista
            GameObject item = Instantiate(itemRankingPrefab, contentContainer);
            ItemRanking ir = item.GetComponent<ItemRanking>();

            if (ir != null)
                ir.Preencher(i + 1, j.nome, j.pontos, j.acertos, j.aproveitamento);
        }
    }

    void MostrarErro(string msg)
    {
        if (textoCarregando != null)
        {
            textoCarregando.gameObject.SetActive(true);
            textoCarregando.text = msg;
        }
    }

    public void FecharRanking()
    {
        rankingPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}