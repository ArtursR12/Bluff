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
    private readonly Dictionary<Rank, GameObject> _rankFrames = new Dictionary<Rank, GameObject>();
    private TextMeshProUGUI _titleText;

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

        Canvas panelCanvas = _panel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 100;
        _panel.AddComponent<GraphicRaycaster>();

        Image bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.04f, 0.09f, 0.95f);

        // Title
        GameObject title = new GameObject("Title");
        title.transform.SetParent(_panel.transform, false);
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.75f);
        titleRect.anchorMax = new Vector2(0.9f, 0.88f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        _titleText = title.AddComponent<TextMeshProUGUI>();
        _titleText.text = "Declare a Rank";
        _titleText.fontSize = 22;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color = new Color(0.83f, 0.685f, 0.215f, 1f);
        _titleText.fontStyle = FontStyles.Bold;

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
        // Gold border frame
        GameObject frame = new GameObject("Frame_Rank_" + rank);
        _rankFrames[rank] = frame;
        frame.transform.SetParent(_panel.transform, false);
        RectTransform frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = anchorMin;
        frameRect.anchorMax = anchorMax;
        frameRect.offsetMin = new Vector2(2, 2);
        frameRect.offsetMax = new Vector2(-2, -2);
        frame.AddComponent<Image>().color = new Color(0.83f, 0.685f, 0.215f, 1f);

        GameObject go = new GameObject("Rank_" + rank);
        go.transform.SetParent(frame.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2);
        rect.offsetMax = new Vector2(-2, -2);

        go.AddComponent<Image>().color = new Color(0.08f, 0.135f, 0.45f, 1f);

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
        tmp.text      = RankShort(rank);
        tmp.fontSize  = 17;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;
    }

    private void CreateCancelButton()
    {
        // Gold border frame
        GameObject frame = new GameObject("Frame_Cancel");
        frame.transform.SetParent(_panel.transform, false);
        RectTransform frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.25f, 0.1f);
        frameRect.anchorMax = new Vector2(0.75f, 0.18f);
        frameRect.offsetMin = new Vector2(2, 2);
        frameRect.offsetMax = new Vector2(-2, -2);
        frame.AddComponent<Image>().color = new Color(0.83f, 0.685f, 0.215f, 1f);

        GameObject go = new GameObject("CancelButton");
        go.transform.SetParent(frame.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2);
        rect.offsetMax = new Vector2(-2, -2);

        go.AddComponent<Image>().color = new Color(0.45f, 0.055f, 0.055f, 1f);

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
        tmp.text      = "✕  Cancel";
        tmp.fontSize  = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;
    }

    private void OnRankSelected(Rank rank)
    {
        Hide();
        _onRankSelected?.Invoke(rank);
    }

    public void Show(Action<Rank> onRankSelected, bool shortDeck = false, int cardCount = 0)
    {
        _onRankSelected = onRankSelected;

        // Update title to show how many cards are being declared on
        if (_titleText != null)
            _titleText.text = cardCount > 0
                ? $"Declare a rank for  <b>{cardCount}</b>  card{(cardCount == 1 ? "" : "s")}"
                : "Declare a Rank";

        // Grey out ranks not in the deck
        foreach (var kvp in _rankFrames)
        {
            bool available = !shortDeck || (int)kvp.Key >= (int)Rank.Six;
            var img  = kvp.Value.GetComponent<Image>();
            var btn  = kvp.Value.GetComponentInChildren<Button>();
            if (img != null)
                img.color = available ? new Color(0.83f, 0.685f, 0.215f, 1f) : new Color(0.3f, 0.3f, 0.3f, 0.5f);
            if (btn != null)
                btn.interactable = available;
        }

        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    private static string RankShort(Rank rank) => rank switch
    {
        Rank.Two   => "2",  Rank.Three => "3",  Rank.Four  => "4",
        Rank.Five  => "5",  Rank.Six   => "6",  Rank.Seven => "7",
        Rank.Eight => "8",  Rank.Nine  => "9",  Rank.Ten   => "10",
        Rank.Jack  => "J",  Rank.Queen => "Q",  Rank.King  => "K",
        Rank.Ace   => "A",  _          => rank.ToString()
    };

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }
}