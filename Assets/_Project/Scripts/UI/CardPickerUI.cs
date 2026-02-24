using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bluff.Core;
using System;
using System.Collections.Generic;

public class CardPickerUI : MonoBehaviour
{
    public static CardPickerUI Instance { get; private set; }

    private GameObject _panel;
    private Action<int> _onCardPicked;
    private string _mode; // "believe" or "bluff"

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildBasePanel();
        Hide();
    }

    private void BuildBasePanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        _panel = new GameObject("CardPickerPanel");
        _panel.transform.SetParent(canvas.transform, false);

        RectTransform rect = _panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = _panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.85f);
    }

    public void Show(List<Card> lastBetCards, string mode, Action<int> onCardPicked)
    {
        _onCardPicked = onCardPicked;
        _mode = mode;

        // Clear old content
        foreach (Transform child in _panel.transform)
            Destroy(child.gameObject);

        // Title
        string title = mode == "believe"
            ? "Pick a card to check (you believe!)"
            : "Pick a card to reveal (you think its a bluff!)";

        CreateTitle(title);
        CreateCards(lastBetCards);
        CreateCancelButton();

        _panel.SetActive(true);
    }

    private void CreateTitle(string text)
    {
        GameObject go = new GameObject("Title");
        go.transform.SetParent(_panel.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.75f);
        rect.anchorMax = new Vector2(0.95f, 0.88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
    }

    private void CreateCards(List<Card> cards)
    {
        float cardWidth = 0.18f;
        float spacing = 0.22f;
        float totalWidth = cards.Count * spacing;
        float startX = 0.5f - totalWidth / 2f + 0.05f;

        for (int i = 0; i < cards.Count; i++)
        {
            int index = i;
            float x = startX + i * spacing;

            GameObject go = new GameObject($"Card_{i}");
            go.transform.SetParent(_panel.transform, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(x, 0.5f);
            rect.anchorMax = new Vector2(x + cardWidth, 0.72f);
            rect.offsetMin = new Vector2(3, 3);
            rect.offsetMax = new Vector2(-3, -3);

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.6f, 1f);

            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OnCardPicked(index));

            GameObject label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            RectTransform labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = "?";
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }
    }

    private void CreateCancelButton()
    {
        GameObject go = new GameObject("CancelButton");
        go.transform.SetParent(_panel.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.25f, 0.35f);
        rect.anchorMax = new Vector2(0.75f, 0.43f);
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

    private void OnCardPicked(int index)
    {
        Hide();
        _onCardPicked?.Invoke(index);
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }
}