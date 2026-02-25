using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    private GameObject _lobbyPanel;
    private TMP_InputField _nameInput;
    private TMP_InputField _roomCodeInput;
    private TextMeshProUGUI _statusText;
    private Button _createButton;
    private Button _joinButton;

    private Canvas _canvas;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        BuildLobbyUI();
    }

    private void BuildLobbyUI()
    {
        // Full screen panel
        _lobbyPanel = new GameObject("LobbyPanel");
        _lobbyPanel.transform.SetParent(_canvas.transform, false);
        _lobbyPanel.transform.SetAsLastSibling();

        RectTransform rect = _lobbyPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = _lobbyPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.15f, 1f);

        // Title
        CreateText(_lobbyPanel, "BLUFF", 48,
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f),
            TextAlignmentOptions.Center, Color.white);

        CreateText(_lobbyPanel, "The Card Game", 20,
            new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.83f),
            TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));

        // Name input
        CreateText(_lobbyPanel, "Your Name", 16,
            new Vector2(0.1f, 0.63f), new Vector2(0.9f, 0.70f),
            TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.8f));

        _nameInput = CreateInputField(_lobbyPanel, "Enter your name...",
            new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.63f));

        // Room code input
        CreateText(_lobbyPanel, "Room Code", 16,
            new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.51f),
            TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.8f));

        _roomCodeInput = CreateInputField(_lobbyPanel, "Enter room code...",
            new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.44f));

        // Create button
        _createButton = CreateButton(_lobbyPanel, "Create Room",
            new Color(0.15f, 0.5f, 0.15f),
            new Vector2(0.1f, 0.22f), new Vector2(0.9f, 0.32f),
            OnCreateClicked);

        // Join button
        _joinButton = CreateButton(_lobbyPanel, "Join Room",
            new Color(0.15f, 0.25f, 0.6f),
            new Vector2(0.1f, 0.10f), new Vector2(0.9f, 0.20f),
            OnJoinClicked);

        // Status text
        _statusText = CreateText(_lobbyPanel, "", 15,
            new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.10f),
            TextAlignmentOptions.Center, new Color(1f, 0.8f, 0.2f));

        _lobbyPanel.transform.SetAsLastSibling();
    }

    private TextMeshProUGUI CreateText(GameObject parent, string text,
        int fontSize, Vector2 anchorMin, Vector2 anchorMax,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject("Text_" + text.Substring(0,
            Mathf.Min(8, text.Length)));
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;

        return tmp;
    }

    private TMP_InputField CreateInputField(GameObject parent,
        string placeholder, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject("InputField");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(0, 2);
        rect.offsetMax = new Vector2(0, -2);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.3f, 1f);

        TMP_InputField input = go.AddComponent<TMP_InputField>();

        // Text component
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);
        TextMeshProUGUI textComp = textGo.AddComponent<TextMeshProUGUI>();
        textComp.fontSize = 18;
        textComp.color = Color.white;

        // Placeholder
        GameObject phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(go.transform, false);
        RectTransform phRect = phGo.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(10, 5);
        phRect.offsetMax = new Vector2(-10, -5);
        TextMeshProUGUI phComp = phGo.AddComponent<TextMeshProUGUI>();
        phComp.text = placeholder;
        phComp.fontSize = 18;
        phComp.color = new Color(0.5f, 0.5f, 0.5f);
        phComp.fontStyle = FontStyles.Italic;

        input.textComponent = textComp;
        input.placeholder = phComp;

        input.richText = false;
        input.readOnly = false;
        input.interactable = true;

        return input;
    }

    private Button CreateButton(GameObject parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(0, 3);
        rect.offsetMax = new Vector2(0, -3);

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
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }

    private async void OnCreateClicked()
    {
        string playerName = _nameInput.text.Trim();
        string roomCode = _roomCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(playerName))
        {
            _statusText.text = "Please enter your name!";
            return;
        }

        if (string.IsNullOrEmpty(roomCode))
        {
            // Generate random room code
            roomCode = GenerateRoomCode();
            _roomCodeInput.text = roomCode;
        }

        SetButtonsInteractable(false);
        _statusText.text = $"Creating room {roomCode}...";

        await NetworkManager.Instance.CreateRoom(roomCode, playerName);

        _statusText.text = $"Room {roomCode} created! Waiting for players...";
        // Hide lobby and show waiting room later
    }

    private async void OnJoinClicked()
    {
        string playerName = _nameInput.text.Trim();
        string roomCode = _roomCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(playerName))
        {
            _statusText.text = "Please enter your name!";
            return;
        }

        if (string.IsNullOrEmpty(roomCode))
        {
            _statusText.text = "Please enter a room code!";
            return;
        }

        SetButtonsInteractable(false);
        _statusText.text = $"Joining room {roomCode}...";

        await NetworkManager.Instance.JoinRoom(roomCode, playerName);

        _statusText.text = $"Joined room {roomCode}!";
    }

    private void SetButtonsInteractable(bool value)
    {
        _createButton.interactable = value;
        _joinButton.interactable = value;
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        System.Random rng = new System.Random();
        char[] code = new char[5];
        for (int i = 0; i < 5; i++)
            code[i] = chars[rng.Next(chars.Length)];
        return new string(code);
    }

    public void Hide() => _lobbyPanel.SetActive(false);
    public void Show() => _lobbyPanel.SetActive(true);
}