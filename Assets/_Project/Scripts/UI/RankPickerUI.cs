using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bluff.Core;
using System;
using System.Collections.Generic;

public class RankPickerUI : MonoBehaviour
{
    private GameObject _panel;
    private Action<Rank> _onRankSelected;

    private readonly List<Rank> _ranks = new List<Rank>
    {
        Rank.Two, Rank.Three, Rank.Four, Rank.Five,
        Rank.Six, Rank.Seven, Rank.Eight, Rank.Nine,
        Rank.Ten, Rank.Jack, Rank.Queen, Rank.King, Rank.Ace
    };

    public static RankPickerUI Instance { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildPanel();
        Hide();
    }

    private void BuildPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        _panel = new GameObject("RankPickerPanel");
        _panel.transform.SetAsLastSibling(); // Always on top
        _panel.transform.SetParent(canvas.transform, false);

        // Dark overlay background
        RectTransform panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image bg = _panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.85f);

        // Title
        GameObject title = new GameObject("Title");
        title.transform.SetParent(_panel.transform, false);
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.75f);
        titleRect.anchorMax = new Vector2(0.9f, 0.88f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Declare a rank";
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Rank buttons grid
        int cols = 4;
        float btnW = 0.2f;
        float btnH = 0.08f;
        float startX = 0.1f;
        float startY = 0.65f;
        float padX = 0.22f;
        float padY = 0.1f;

        for (int i = 0; i < _ranks.Count; i++)
        {
            Rank rank = _ranks[i];
            int col = i % cols;
            int row = i / cols;

            float x = startX + col * padX;
            float y = startY - row * padY;

            CreateRankButton(rank, new Vector2(x, y - btnH),
                new Vector2(x + btnW, y));
        }

        // Cancel button
        CreateCancelButton();
    }

    private void CreateRankButton(Rank rank, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject("Rank_" + rank);
        go.transform.SetParent(_panel.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(3, 3);
        rect.offsetMax = new Vector2(-3, -3);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.35f, 0.55f, 1f);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => OnRankSelected(rank));

        GameObject label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = rank.ToString();
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    private void CreateCancelButton()
    {
        GameObject go = new GameObject("CancelButton");
        go.transform.SetParent(_panel.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.25f, 0.1f);
        rect.anchorMax = new Vector2(0.75f, 0.18f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.5f, 0.1f, 0.1f, 1f);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Hide);

        GameObject label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = "Cancel";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    private void OnRankSelected(Rank rank)
    {
        Hide();
        _onRankSelected?.Invoke(rank);
    }

    public void Show(Action<Rank> onRankSelected)
    {
        _onRankSelected = onRankSelected;
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }
}