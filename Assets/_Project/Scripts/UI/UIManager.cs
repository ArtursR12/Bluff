using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bluff.Core;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Canvas _canvas;

    // Panels
    private GameObject _topPanel;
    private GameObject _middlePanel;
    private GameObject _bottomPanel;

    // Top panel
    private TextMeshProUGUI _opponentsText;

    // Middle panel
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _currentBetText;
    private TextMeshProUGUI _pileText;
    private TextMeshProUGUI _discardText;

    // Bottom panel
    private GameObject _handContainer;
    private Button _believeButton;
    private Button _bluffButton;
    private Button _rebetButton;
    private Button _confirmBetButton;
    private TextMeshProUGUI _selectionInfoText;

    // Card selection state
    private List<CardView> _handCardViews = new List<CardView>();
    private List<int> _selectedCardIndices = new List<int>();

    // Local player id
    private string _localPlayerId = "0";

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

    // ── PANEL BUILDERS ──────────────────────────────────────

    private GameObject CreatePanel(string name, float bottomAnchor,
        float topAnchor, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(_canvas.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, bottomAnchor);
        rect.anchorMax = new Vector2(1, topAnchor);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = panel.AddComponent<Image>();
        img.color = color;

        return panel;
    }

    private TextMeshProUGUI CreateText(GameObject parent, string text,
        int fontSize, Vector2 anchorMin, Vector2 anchorMax,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(8, 4);
        rect.offsetMax = new Vector2(-8, -4);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return tmp;
    }

    private Button CreateButton(GameObject parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(4, 4);
        rect.offsetMax = new Vector2(-4, -4);

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
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private void BuildUI()
    {
        BuildTopPanel();
        BuildMiddlePanel();
        BuildBottomPanel();

        _topPanel.SetActive(false);
        _middlePanel.SetActive(false);
        _bottomPanel.SetActive(false);
    }

    public void ShowGameUI()
    {
        _topPanel.SetActive(true);
        _middlePanel.SetActive(true);
        _bottomPanel.SetActive(true);
    }

    private void BuildTopPanel()
    {
        _topPanel = CreatePanel("TopPanel", 0.78f, 1f,
            new Color(0.12f, 0.12f, 0.22f, 1f));

        _opponentsText = CreateText(_topPanel, "Waiting for players...", 15,
            Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
    }

    private void BuildMiddlePanel()
    {
        _middlePanel = CreatePanel("MiddlePanel", 0.38f, 0.78f,
            new Color(0.08f, 0.18f, 0.08f, 1f));

        _statusText = CreateText(_middlePanel, "Starting...", 20,
            new Vector2(0, 0.72f), Vector2.one, TextAlignmentOptions.Center);

        _currentBetText = CreateText(_middlePanel, "No active bet", 15,
            new Vector2(0, 0.42f), new Vector2(1, 0.72f),
            TextAlignmentOptions.Center);

        _pileText = CreateText(_middlePanel, "Pile: 0", 15,
            new Vector2(0, 0.18f), new Vector2(0.5f, 0.42f),
            TextAlignmentOptions.Center);

        _discardText = CreateText(_middlePanel, "Discard: 0", 15,
            new Vector2(0.5f, 0.18f), new Vector2(1f, 0.42f),
            TextAlignmentOptions.Center);
    }

    private void BuildBottomPanel()
    {
        _bottomPanel = CreatePanel("BottomPanel", 0f, 0.38f,
            new Color(0.18f, 0.08f, 0.08f, 1f));

        // Hand scroll container
        _handContainer = new GameObject("HandContainer");
        _handContainer.transform.SetParent(_bottomPanel.transform, false);
        RectTransform handRect = _handContainer.AddComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0, 0.35f);
        handRect.anchorMax = new Vector2(1, 1f);
        handRect.offsetMin = new Vector2(5, 5);
        handRect.offsetMax = new Vector2(-5, -5);

        // Selection info text
        _selectionInfoText = CreateText(_bottomPanel, "Select cards to bet",
            13, new Vector2(0, 0.38f), new Vector2(1, 0.48f),
            TextAlignmentOptions.Center);

        // Action buttons row
        _believeButton = CreateButton(_bottomPanel, "Believe",
            new Color(0.15f, 0.55f, 0.15f),
            new Vector2(0f, 0.02f), new Vector2(0.32f, 0.36f),
            OnBelieveClicked);

        _bluffButton = CreateButton(_bottomPanel, "Bluff!",
            new Color(0.65f, 0.15f, 0.15f),
            new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.36f),
            OnBluffClicked);

        _rebetButton = CreateButton(_bottomPanel, "Bet",
            new Color(0.15f, 0.25f, 0.65f),
            new Vector2(0.68f, 0.02f), new Vector2(1f, 0.36f),
            OnBetClicked);

        // Confirm bet button (hidden by default)
        _confirmBetButton = CreateButton(_bottomPanel, "Confirm Bet",
            new Color(0.8f, 0.5f, 0f),
            new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.36f),
            OnConfirmBetClicked);
        _confirmBetButton.gameObject.SetActive(false);
    }

    // ── CARD HAND DISPLAY ────────────────────────────────────

    private void BuildHandCards(List<Card> hand)
    {
        foreach (Transform child in _handContainer.transform)
            Destroy(child.gameObject);
        _handCardViews.Clear();

        if (hand.Count == 0) return;

        RectTransform containerRect = _handContainer.GetComponent<RectTransform>();
        float containerWidth = 390f;
        float containerHeight = 160f;

        float cardWidth = 48f;
        float cardHeight = 70f;

        int count = hand.Count;

        // How much each card overlaps
        float totalWidth = containerWidth * 0.9f;
        float spacing = Mathf.Min(cardWidth + 4f, totalWidth / count);

        float startX = -(spacing * (count - 1)) / 2f;

        for (int i = 0; i < count; i++)
        {
            int index = i;

            GameObject cardGo = new GameObject($"Card_{i}");
            cardGo.transform.SetParent(_handContainer.transform, false);

            RectTransform rect = cardGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            // Fan arc - cards curve slightly
            float t = count > 1 ? (float)i / (count - 1) : 0.5f;
            float xPos = startX + i * spacing;

            // Arc effect
            float arcHeight = count > 4 ? 15f : 5f;
            float normalizedT = (t - 0.5f) * 2f;
            float yPos = -arcHeight * (normalizedT * normalizedT) + 10f;

            // Slight rotation for fan effect
            float maxRotation = Mathf.Min(25f, count * 1.2f);
            float rotation = Mathf.Lerp(-maxRotation, maxRotation, t);

            rect.anchoredPosition = new Vector2(xPos, yPos);
            rect.localRotation = Quaternion.Euler(0, 0, -rotation);

            Image img = cardGo.AddComponent<Image>();
            img.color = Color.white;

            CardView cardView = cardGo.AddComponent<CardView>();
            cardView.Setup(hand[i], index);
            cardView.OnCardClicked += OnHandCardClicked;

            _handCardViews.Add(cardView);
        }
    }

    private void OnHandCardClicked(int index)
    {
        GameState state = NetworkedGameManager.Instance != null
            ? NetworkedGameManager.Instance.GetLocalState()
            : GameManager.Instance.GetState();

        if (state == null || state.Players.Count == 0) return;

        Player localPlayer = state.Players.Find(p => p.Id == _localPlayerId);
        if (localPlayer == null) return;
        if (index >= localPlayer.Hand.Count) return;
        if (state.CurrentPlayer.Id != _localPlayerId) return;

        if (_selectedCardIndices.Contains(index))
        {
            _selectedCardIndices.Remove(index);
            _handCardViews[index].SetSelected(false);
            // Move card back down
            RectTransform rect = _handCardViews[index].GetComponent<RectTransform>();
            rect.anchoredPosition -= new Vector2(0, 20f);
        }
        else
        {
            if (_selectedCardIndices.Count >= 4) return;
            _selectedCardIndices.Add(index);
            _handCardViews[index].SetSelected(true);
            // Pop card up
            RectTransform rect = _handCardViews[index].GetComponent<RectTransform>();
            rect.anchoredPosition += new Vector2(0, 20f);
        }

        UpdateSelectionInfo();
    }

    private void UpdateSelectionInfo()
    {
        int count = _selectedCardIndices.Count;
        if (count == 0)
        {
            _selectionInfoText.text = "Select 1-4 cards to bet";
            _confirmBetButton.gameObject.SetActive(false);
            _rebetButton.gameObject.SetActive(true);
        }
        else
        {
            _selectionInfoText.text = $"{count} card(s) selected - pick rank to declare";
            _confirmBetButton.gameObject.SetActive(false);
            _rebetButton.gameObject.SetActive(true);
        }
    }

    // ── BUTTON HANDLERS ──────────────────────────────────────

    private void OnBelieveClicked()
    {
        GameState state = GameManager.Instance.GetState();
        CardPickerUI.Instance.Show(state.LastBetCards, "believe", (cardIndex) =>
        {
            GameManager.Instance.ResolveBelieve(cardIndex);
            RefreshUI(GameManager.Instance.GetState(), _localPlayerId);
        });
    }

    private void OnBluffClicked()
    {
        GameState state = GameManager.Instance.GetState();
        CardPickerUI.Instance.Show(state.LastBetCards, "bluff", (cardIndex) =>
        {
            GameManager.Instance.ResolveBluff(cardIndex);
            RefreshUI(GameManager.Instance.GetState(), _localPlayerId);
        });
    }

    private void OnBetClicked()
    {
        if (_selectedCardIndices.Count == 0)
        {
            _selectionInfoText.text = "Select at least 1 card first!";
            return;
        }

        if (RankPickerUI.Instance == null)
        {
            Debug.LogError("RankPickerUI.Instance is null!");
            return;
        }

        Debug.Log("Opening rank picker...");
        RankPickerUI.Instance.Show((rank) =>
        {
            Debug.Log($"Rank selected: {rank}");
            GameState state = GameManager.Instance.GetState();
            Player localPlayer = state.Players.Find(p => p.Id == _localPlayerId);

            List<Bluff.Core.Card> selectedCards = new List<Bluff.Core.Card>();
            foreach (int idx in _selectedCardIndices)
                selectedCards.Add(localPlayer.Hand[idx]);

            bool success = GameManager.Instance.TryPlaceBet(selectedCards, rank);
            if (success)
            {
                _selectedCardIndices.Clear();
                RefreshUI(GameManager.Instance.GetState(), _localPlayerId);
            }
        });
    }

    private void OnConfirmBetClicked()
    {
        // Reserved for future use
    }

    // ── REFRESH ──────────────────────────────────────────────

    public void RefreshUI(GameState state, string localPlayerId)
    {
        _localPlayerId = localPlayerId;
        Player localPlayer = state.Players.Find(p => p.Id == localPlayerId);
        if (localPlayer == null) return;

        // Opponents
        string opponents = "";
        foreach (Player p in state.Players)
            if (p.Id != localPlayerId)
                opponents += $"{p.Name}: {p.CardCount} cards    ";
        _opponentsText.text = opponents.TrimEnd();

        // Status
        bool isMyTurn = state.CurrentPlayer.Id == localPlayerId;
        _statusText.text = isMyTurn ? "YOUR TURN" : $"{state.CurrentPlayer.Name}'s turn";
        _statusText.color = isMyTurn ? Color.green : Color.white;

        // Bet info
        if (state.LastBetCards.Count > 0)
            _currentBetText.text = $"{state.LastBetPlayer.Name} bet " +
                $"{state.LastBetCards.Count}x {state.LastDeclaredRank}";
        else
            _currentBetText.text = "No active bet - start a new round!";

        // Pile and discard
        _pileText.text = $"Pile: {state.Pile.Count}";
        _discardText.text = $"Discard: {state.Discard.Count}";

        // Hand cards
        BuildHandCards(localPlayer.Hand);
        _selectedCardIndices.Clear();
        UpdateSelectionInfo();

        // Button states
        bool hasBet = state.LastBetCards.Count > 0;
        bool canChallenge = hasBet && state.LastBetPlayer.Id != localPlayerId;

        _believeButton.interactable = isMyTurn && canChallenge;
        _bluffButton.interactable = isMyTurn && canChallenge;
        _rebetButton.interactable = isMyTurn;

        if (!isMyTurn)
            _selectionInfoText.text = $"Waiting for {state.CurrentPlayer.Name}...";
    }

    public void ShowGameOver(string loserName)
    {
        _statusText.text = $"{loserName} LOSES!";
        _statusText.color = Color.red;
        _believeButton.interactable = false;
        _bluffButton.interactable = false;
        _rebetButton.interactable = false;
    }
}