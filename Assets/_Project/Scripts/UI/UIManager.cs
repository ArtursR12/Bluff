using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bluff.Core;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private GameObject _topPanel;
    private GameObject _middlePanel;
    private GameObject _bottomPanel;

    private TextMeshProUGUI _opponentsText;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _currentBetText;
    private TextMeshProUGUI _pileText;
    private TextMeshProUGUI _discardText;
    private TextMeshProUGUI _handText;

    private Button _believeButton;
    private Button _bluffButton;
    private Button _rebetButton;

    private Canvas _canvas;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        BuildUI();
    }

    private GameObject CreatePanel(string name, float topAnchor,
        float bottomAnchor, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(_canvas.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, bottomAnchor);
        rect.anchorMax = new Vector2(1, topAnchor);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.AddComponent<Image>();
        image.color = color;

        return panel;
    }

    private TextMeshProUGUI CreateText(GameObject parent, string defaultText,
        int fontSize, Vector2 anchorMin, Vector2 anchorMax,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(10, 5);
        rect.offsetMax = new Vector2(-10, -5);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return tmp;
    }

    private Button CreateButton(GameObject parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Button_" + label);
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(5, 5);
        rect.offsetMax = new Vector2(-5, -5);

        Image img = go.AddComponent<Image>();
        img.color = color;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private void BuildTopPanel()
    {
        _topPanel = CreatePanel("TopPanel", 1f, 0.75f,
            new Color(0.15f, 0.15f, 0.25f, 1f));

        _opponentsText = CreateText(_topPanel, "Opponents loading...", 16,
            Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
    }

    private void BuildMiddlePanel()
    {
        _middlePanel = CreatePanel("MiddlePanel", 0.75f, 0.35f,
            new Color(0.1f, 0.2f, 0.1f, 1f));

        _statusText = CreateText(_middlePanel, "Waiting for game...", 18,
            new Vector2(0, 0.7f), Vector2.one, TextAlignmentOptions.Center);

        _currentBetText = CreateText(_middlePanel, "No active bet", 16,
            new Vector2(0, 0.4f), new Vector2(1, 0.7f),
            TextAlignmentOptions.Center);

        _pileText = CreateText(_middlePanel, "Pile: 0", 16,
            new Vector2(0, 0.2f), new Vector2(0.5f, 0.4f),
            TextAlignmentOptions.Center);

        _discardText = CreateText(_middlePanel, "Discard: 0", 16,
            new Vector2(0.5f, 0.2f), new Vector2(1f, 0.4f),
            TextAlignmentOptions.Center);
    }

    private void BuildBottomPanel()
    {
        _bottomPanel = CreatePanel("BottomPanel", 0.35f, 0f,
            new Color(0.2f, 0.1f, 0.1f, 1f));

        _handText = CreateText(_bottomPanel, "Your hand", 14,
            new Vector2(0, 0.5f), Vector2.one,
            TextAlignmentOptions.TopLeft);

        _believeButton = CreateButton(_bottomPanel, "Believe",
            new Color(0.2f, 0.6f, 0.2f),
            new Vector2(0f, 0.05f), new Vector2(0.33f, 0.45f),
            OnBelieveClicked);

        _bluffButton = CreateButton(_bottomPanel, "Bluff!",
            new Color(0.7f, 0.2f, 0.2f),
            new Vector2(0.34f, 0.05f), new Vector2(0.66f, 0.45f),
            OnBluffClicked);

        _rebetButton = CreateButton(_bottomPanel, "Rebet",
            new Color(0.2f, 0.3f, 0.7f),
            new Vector2(0.67f, 0.05f), new Vector2(1f, 0.45f),
            OnRebetClicked);
    }

    private void BuildUI()
    {
        BuildTopPanel();
        BuildMiddlePanel();
        BuildBottomPanel();
    }

    public void RefreshUI(GameState state, string localPlayerId)
    {
        Player localPlayer = state.Players.Find(p => p.Id == localPlayerId);
        if (localPlayer == null) return;

        string opponents = "";
        foreach (Player p in state.Players)
            if (p.Id != localPlayerId)
                opponents += $"{p.Name}: {p.CardCount} cards\n";
        _opponentsText.text = opponents;

        bool isMyTurn = state.CurrentPlayer.Id == localPlayerId;
        _statusText.text = isMyTurn ? "YOUR TURN" : $"{state.CurrentPlayer.Name}'s turn";

        if (state.LastBetCards.Count > 0)
            _currentBetText.text = $"{state.LastBetPlayer.Name} bet " +
                $"{state.LastBetCards.Count}x {state.LastDeclaredRank}";
        else
            _currentBetText.text = "No active bet - start a new round!";

        _pileText.text = $"Pile: {state.Pile.Count}";
        _discardText.text = $"Discard: {state.Discard.Count}";

        string hand = "Your cards:\n";
        for (int i = 0; i < localPlayer.Hand.Count; i++)
            hand += $"[{i}] {localPlayer.Hand[i]}\n";
        _handText.text = hand;

        _believeButton.interactable = isMyTurn && state.LastBetCards.Count > 0;
        _bluffButton.interactable = isMyTurn && state.LastBetCards.Count > 0;
        _rebetButton.interactable = isMyTurn;
    }

    public void ShowGameOver(string loserName)
    {
        _statusText.text = $"{loserName} LOSES!";
        _believeButton.interactable = false;
        _bluffButton.interactable = false;
        _rebetButton.interactable = false;
    }

    private void OnBelieveClicked()
    {
        Debug.Log("Believe clicked!");
    }

    private void OnBluffClicked()
    {
        Debug.Log("Bluff clicked!");
    }

    private void OnRebetClicked()
    {
        Debug.Log("Rebet clicked!");
    }
}