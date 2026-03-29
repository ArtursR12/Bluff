using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Text;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    private static readonly Color L_Dark  = new Color(0.03f,  0.045f, 0.09f, 1f);
    private static readonly Color L_Gold  = new Color(0.83f,  0.685f, 0.215f, 1f);
    private static readonly Color L_Red   = new Color(0.45f,  0.055f, 0.055f, 1f);
    private static readonly Color L_Green = new Color(0.055f, 0.34f,  0.10f,  1f);
    private static readonly Color L_Blue  = new Color(0.08f,  0.135f, 0.45f,  1f);
    private static readonly Color L_Muted = new Color(0.42f,  0.52f,  0.60f,  1f);
    private static readonly Color L_Field = new Color(0.08f,  0.12f,  0.22f,  1f);

    private GameObject _lobbyPanel;
    private TMP_InputField _nameInput;
    private TMP_InputField _roomCodeInput;
    private TextMeshProUGUI _statusText;
    private Button _createButton;
    private Button _joinButton;

    private Canvas _canvas;
    private TextMeshProUGUI _playerCountText;
    private TextMeshProUGUI _playerListText;
    private GameObject _waitingBg;
    private GameObject _settingsRow;
    private TextMeshProUGUI _lobbyMuteLabel;

    private static readonly Color[] PlayerColors =
    {
        new Color(0.30f, 0.75f, 1.00f), // 0 — sky blue
        new Color(1.00f, 0.55f, 0.20f), // 1 — orange
        new Color(0.55f, 1.00f, 0.45f), // 2 — lime
        new Color(1.00f, 0.38f, 0.65f), // 3 — pink
        new Color(0.75f, 0.55f, 1.00f), // 4 — purple
        new Color(1.00f, 0.92f, 0.30f), // 5 — yellow
    };
    private TextMeshProUGUI _timerLabel;
    private TextMeshProUGUI _deckLabel;
    private int _timerChoice   = 1;  // 0=15s  1=30s  2=60s
    private int _deckChoice    = 0;  // 0=Auto 1=36   2=52
    private int _maxPlayers    = 6;  // 2–6
    private TextMeshProUGUI _maxPlayersLabel;
    private int _botCount = 2;  // 1–3 bots for Practice mode
    private TextMeshProUGUI _botCountLabel;
    private TextMeshProUGUI _waitingRoomCodeText;
    private static readonly float[] TimerValues    = { 15f,  30f,    60f   };
    private static readonly string[] TimerLabels   = { "15s", "30s", "60s" };
    private static readonly string[] DeckLabels    = { "Auto", "36 cards", "52 cards" };
    private static readonly int[]    MaxPlayerOpts = { 2, 3, 4, 5, 6 };
    private Button _startButton;
    private GameObject _countdownOverlay;
    private TextMeshProUGUI _countdownText;
    private GameObject _rulesOverlay;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        BuildLobbyUI();

        // Restore last-used name and room code
        string savedName = PlayerPrefs.GetString("bluff_player_name", "");
        if (!string.IsNullOrEmpty(savedName)) _nameInput.text = savedName;
        string savedCode = PlayerPrefs.GetString("bluff_room_code", "");
        if (!string.IsNullOrEmpty(savedCode)) _roomCodeInput.text = savedCode;

        // Show lifetime stats as initial status hint
        int ltGames = PlayerPrefs.GetInt("bluff_games", 0);
        if (ltGames > 0 && _statusText != null)
        {
            int ltWins   = PlayerPrefs.GetInt("bluff_wins",   0);
            int ltLosses = PlayerPrefs.GetInt("bluff_losses", 0);
            int streak   = PlayerPrefs.GetInt("bluff_streak", 0);
            int ltPiles  = PlayerPrefs.GetInt("bluff_total_piles", 0);
            string streakStr = streak >= 2 ? $"  🔥{streak}" : "";
            string pilesStr  = ltPiles > 0 ? $"  ·  {ltPiles} piles" : "";
            _statusText.text = $"Record:  {ltWins}W / {ltLosses}L  ({ltGames} games){streakStr}{pilesStr}";

            // Show last game result from history
            string history = PlayerPrefs.GetString("bluff_history", "");
            if (!string.IsNullOrEmpty(history))
            {
                string firstEntry = history.Split('\n')[0];
                string[] parts = firstEntry.Split('|');
                if (parts.Length >= 3)
                {
                    string res    = parts[0] == "W" ? "WIN" : "LOSS";
                    string rounds = parts[1];
                    string vs     = parts[2];
                    _statusText.text += $"\nLast: {res} vs {vs}  ({rounds} rounds)";
                }
            }

            _statusText.color = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.75f);
        }

        NetworkedGameManager.OnGameStarted       += Hide;
        NetworkedGameManager.OnPlayerCountChanged += OnPlayerCountChanged;
        NetworkedGameManager.OnPlayerListChanged  += OnPlayerListChanged;
        NetworkedGameManager.OnCountdownTick      += ShowCountdown;
        NetworkedGameManager.OnConnectionLost     += OnConnectionLostInLobby;
        NetworkedGameManager.OnJoinRejected       += OnJoinRejected;
    }

    void OnDestroy()
    {
        NetworkedGameManager.OnGameStarted       -= Hide;
        NetworkedGameManager.OnPlayerCountChanged -= OnPlayerCountChanged;
        NetworkedGameManager.OnPlayerListChanged  -= OnPlayerListChanged;
        NetworkedGameManager.OnCountdownTick      -= ShowCountdown;
        NetworkedGameManager.OnConnectionLost     -= OnConnectionLostInLobby;
        NetworkedGameManager.OnJoinRejected       -= OnJoinRejected;
    }

    private void OnConnectionLostInLobby()
    {
        // Only handle when the lobby is actually visible — during gameplay
        // UIManager owns the connection-lost overlay instead.
        if (_lobbyPanel == null || !_lobbyPanel.activeSelf) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnJoinRejected(string reason)
    {
        if (_statusText != null)
            _statusText.text = reason;
        // Re-enable buttons so the user can try a different room
        if (_joinButton != null) _joinButton.interactable = true;
        if (_createButton != null) _createButton.interactable = true;
    }

    private void BuildLobbyUI()
    {
        _lobbyPanel = new GameObject("LobbyPanel");
        _lobbyPanel.transform.SetParent(_canvas.transform, false);
        _lobbyPanel.transform.SetAsLastSibling();

        RectTransform rect = _lobbyPanel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        _lobbyPanel.AddComponent<Image>().color = L_Dark;

        // Decorative corner suit symbols
        AddSuitDecor(_lobbyPanel, "♠", new Vector2(0.02f, 0.88f), new Vector2(0.15f, 0.98f));
        AddSuitDecor(_lobbyPanel, "♥", new Vector2(0.85f, 0.88f), new Vector2(0.98f, 0.98f), new Color(0.7f, 0.15f, 0.15f, 0.35f));
        AddSuitDecor(_lobbyPanel, "♦", new Vector2(0.02f, 0.02f), new Vector2(0.15f, 0.12f), new Color(0.7f, 0.15f, 0.15f, 0.35f));
        AddSuitDecor(_lobbyPanel, "♣", new Vector2(0.85f, 0.02f), new Vector2(0.98f, 0.12f));

        // Title — BLUFF in gold
        TextMeshProUGUI titleTmp = CreateText(_lobbyPanel, "BLUFF", 56,
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f),
            TextAlignmentOptions.Center, L_Gold);
        titleTmp.fontStyle = FontStyles.Bold;

        // Subtitle
        CreateText(_lobbyPanel, "THE CARD GAME", 13,
            new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.83f),
            TextAlignmentOptions.Center, new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.55f));

        // Gold separator
        AddGoldLine(_lobbyPanel, 0.745f);

        // Your Name label
        CreateText(_lobbyPanel, "YOUR NAME", 11,
            new Vector2(0.1f, 0.670f), new Vector2(0.9f, 0.715f),
            TextAlignmentOptions.Left, L_Muted);

        _nameInput = CreateInputField(_lobbyPanel, "Enter your name...",
            new Vector2(0.1f, 0.590f), new Vector2(0.9f, 0.665f));
        _nameInput.characterLimit = 20;

        // Room Code label
        CreateText(_lobbyPanel, "ROOM CODE", 11,
            new Vector2(0.1f, 0.500f), new Vector2(0.9f, 0.545f),
            TextAlignmentOptions.Left, L_Muted);

        _roomCodeInput = CreateInputField(_lobbyPanel, "Enter room code...",
            new Vector2(0.1f, 0.420f), new Vector2(0.9f, 0.495f));
        _roomCodeInput.characterLimit = 8;
        // Auto-uppercase the room code as the user types
        _roomCodeInput.onValueChanged.AddListener(v =>
        {
            string upper = v.ToUpper();
            if (v != upper) _roomCodeInput.text = upper;
        });

        AddGoldLine(_lobbyPanel, 0.39f);

        // Create Room button
        _createButton = CreateButton(_lobbyPanel, "Create Room", L_Green,
            new Vector2(0.1f, 0.280f), new Vector2(0.9f, 0.375f), OnCreateClicked);

        // Join Room button
        _joinButton = CreateButton(_lobbyPanel, "Join Room", L_Blue,
            new Vector2(0.1f, 0.155f), new Vector2(0.9f, 0.250f), OnJoinClicked);

        // Status text
        _statusText = CreateText(_lobbyPanel, "", 13,
            new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.12f),
            TextAlignmentOptions.Center, new Color(1f, 0.80f, 0.25f));

        _lobbyPanel.transform.SetAsLastSibling();

        // Player count — shown once inside a room (overlaps subtitle area)
        _playerCountText = CreateText(_lobbyPanel, "", 14,
            new Vector2(0.1f, 0.71f), new Vector2(0.9f, 0.77f),
            TextAlignmentOptions.Center, new Color(0.35f, 1f, 0.45f));

        // Waiting-room panel — dark overlay that covers the form inputs once inside a room
        _waitingBg = new GameObject("WaitingBg");
        GameObject waitingBg = _waitingBg;
        waitingBg.transform.SetParent(_lobbyPanel.transform, false);
        RectTransform wbr = waitingBg.AddComponent<RectTransform>();
        wbr.anchorMin = new Vector2(0.08f, 0.38f);
        wbr.anchorMax = new Vector2(0.92f, 0.77f);
        wbr.offsetMin = wbr.offsetMax = Vector2.zero;
        waitingBg.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.08f, 0.15f, 0.92f);

        // Room code display + tap-to-copy (top strip of waitingBg)
        _waitingRoomCodeText = CreateText(waitingBg, "", 13,
            new Vector2(0.04f, 0.88f), new Vector2(0.86f, 1f),
            TextAlignmentOptions.MidlineLeft, new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.9f));

        // Copy icon button (right side of room code)
        CreateButton(waitingBg, "📋", new Color(0.15f, 0.22f, 0.35f),
            new Vector2(0.86f, 0.88f), new Vector2(1f, 1f),
            OnWaitingRoomCopyClicked);

        // Player name list — middle of waitingBg
        _playerListText = CreateText(waitingBg, "", 13,
            new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.88f),
            TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f));
        _playerListText.overflowMode = TMPro.TextOverflowModes.Ellipsis;

        // Thin gold separator
        GameObject sep = new GameObject("Sep");
        sep.transform.SetParent(waitingBg.transform, false);
        RectTransform sr = sep.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.03f, 0.27f); sr.anchorMax = new Vector2(0.97f, 0.27f);
        sr.offsetMin = new Vector2(0, 0); sr.offsetMax = new Vector2(0, 1);
        sep.AddComponent<UnityEngine.UI.Image>().color = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.30f);

        // Settings row — bottom 27% of waitingBg (host-only, hidden for clients)
        _settingsRow = new GameObject("SettingsRow");
        _settingsRow.transform.SetParent(waitingBg.transform, false);
        RectTransform srRect = _settingsRow.AddComponent<RectTransform>();
        srRect.anchorMin = Vector2.zero; srRect.anchorMax = new Vector2(1f, 0.27f);
        srRect.offsetMin = new Vector2(4, 4); srRect.offsetMax = new Vector2(-4, -4);

        // Timer toggle — left third
        CreateSettingButton(_settingsRow, "TIMER", TimerLabels[_timerChoice],
            new Vector2(0f, 0f), new Vector2(0.31f, 1f),
            out _timerLabel, OnTimerCycleClicked);

        // Deck toggle — middle third
        CreateSettingButton(_settingsRow, "DECK", DeckLabels[_deckChoice],
            new Vector2(0.345f, 0f), new Vector2(0.655f, 1f),
            out _deckLabel, OnDeckCycleClicked);

        // Max players toggle — right third
        CreateSettingButton(_settingsRow, "PLAYERS", _maxPlayers.ToString(),
            new Vector2(0.69f, 0f), new Vector2(1f, 1f),
            out _maxPlayersLabel, OnMaxPlayersCycleClicked);

        _settingsRow.SetActive(false);
        _waitingBg.SetActive(false);

        // Start Game button (host, >= 2 players)
        _startButton = CreateButton(_lobbyPanel, "▶  Start Game", new Color(0.55f, 0.35f, 0.04f),
            new Vector2(0.1f, 0.280f), new Vector2(0.9f, 0.375f), OnStartGameClicked);
        _startButton.transform.parent.gameObject.SetActive(false);

        // Mute toggle — bottom-right corner
        {
            GameObject muteFrame = new GameObject("MuteFrame");
            muteFrame.transform.SetParent(_lobbyPanel.transform, false);
            RectTransform mfr = muteFrame.AddComponent<RectTransform>();
            mfr.anchorMin = new Vector2(0.75f, 0.005f);
            mfr.anchorMax = new Vector2(0.98f, 0.038f);
            mfr.offsetMin = mfr.offsetMax = Vector2.zero;
            muteFrame.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.80f);
            Button muteBtn = muteFrame.AddComponent<Button>();
            muteBtn.onClick.AddListener(OnLobbyMuteToggled);
            GameObject muteLblGo = new GameObject("Lbl");
            muteLblGo.transform.SetParent(muteFrame.transform, false);
            RectTransform mlr = muteLblGo.AddComponent<RectTransform>();
            mlr.anchorMin = Vector2.zero; mlr.anchorMax = Vector2.one;
            mlr.offsetMin = mlr.offsetMax = Vector2.zero;
            _lobbyMuteLabel = muteLblGo.AddComponent<TextMeshProUGUI>();
            _lobbyMuteLabel.text      = AudioManager.IsMuted ? "🔇  Muted" : "🔊  Sound";
            _lobbyMuteLabel.fontSize  = 10;
            _lobbyMuteLabel.alignment = TextAlignmentOptions.Center;
            _lobbyMuteLabel.color     = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.70f);
        }

        // "Practice vs Bots" button — left portion of centre bottom bar
        {
            GameObject practiceFrame = new GameObject("PracticeFrame");
            practiceFrame.transform.SetParent(_lobbyPanel.transform, false);
            RectTransform pfr = practiceFrame.AddComponent<RectTransform>();
            pfr.anchorMin = new Vector2(0.24f, 0.005f);
            pfr.anchorMax = new Vector2(0.59f, 0.038f);
            pfr.offsetMin = pfr.offsetMax = Vector2.zero;
            practiceFrame.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.80f);
            Button practiceBtn = practiceFrame.AddComponent<Button>();
            practiceBtn.onClick.AddListener(OnPracticeClicked);
            GameObject practiceLbl = new GameObject("Lbl");
            practiceLbl.transform.SetParent(practiceFrame.transform, false);
            RectTransform plr = practiceLbl.AddComponent<RectTransform>();
            plr.anchorMin = Vector2.zero; plr.anchorMax = Vector2.one;
            plr.offsetMin = plr.offsetMax = Vector2.zero;
            TextMeshProUGUI pTmp = practiceLbl.AddComponent<TextMeshProUGUI>();
            pTmp.text      = "🤖  Practice vs Bots";
            pTmp.fontSize  = 9;
            pTmp.alignment = TextAlignmentOptions.Center;
            pTmp.color     = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.55f);
        }

        // Bot count toggle — right portion of centre bottom bar
        {
            GameObject botCountFrame = new GameObject("BotCountFrame");
            botCountFrame.transform.SetParent(_lobbyPanel.transform, false);
            RectTransform bcr = botCountFrame.AddComponent<RectTransform>();
            bcr.anchorMin = new Vector2(0.60f, 0.005f);
            bcr.anchorMax = new Vector2(0.72f, 0.038f);
            bcr.offsetMin = bcr.offsetMax = Vector2.zero;
            botCountFrame.AddComponent<Image>().color = new Color(0.10f, 0.16f, 0.28f, 0.85f);
            Button botCountBtn = botCountFrame.AddComponent<Button>();
            botCountBtn.onClick.AddListener(OnBotCountToggle);
            GameObject botCountLblGo = new GameObject("Lbl");
            botCountLblGo.transform.SetParent(botCountFrame.transform, false);
            RectTransform bclr = botCountLblGo.AddComponent<RectTransform>();
            bclr.anchorMin = Vector2.zero; bclr.anchorMax = Vector2.one;
            bclr.offsetMin = bclr.offsetMax = Vector2.zero;
            _botCountLabel = botCountLblGo.AddComponent<TextMeshProUGUI>();
            _botCountLabel.text      = "2 Bots ↻";
            _botCountLabel.fontSize  = 8;
            _botCountLabel.alignment = TextAlignmentOptions.Center;
            _botCountLabel.color     = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.75f);
        }

        // "?" rules button — bottom left corner
        {
            GameObject rulesFrame = new GameObject("RulesFrame");
            rulesFrame.transform.SetParent(_lobbyPanel.transform, false);
            RectTransform rfr = rulesFrame.AddComponent<RectTransform>();
            rfr.anchorMin = new Vector2(0.02f, 0.005f);
            rfr.anchorMax = new Vector2(0.22f, 0.038f);
            rfr.offsetMin = rfr.offsetMax = Vector2.zero;
            rulesFrame.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.25f, 0.80f);
            Button rulesBtn = rulesFrame.AddComponent<Button>();
            rulesBtn.onClick.AddListener(OnRulesButtonClicked);
            GameObject rulesLbl = new GameObject("Lbl");
            rulesLbl.transform.SetParent(rulesFrame.transform, false);
            RectTransform rlr = rulesLbl.AddComponent<RectTransform>();
            rlr.anchorMin = Vector2.zero; rlr.anchorMax = Vector2.one;
            rlr.offsetMin = rlr.offsetMax = Vector2.zero;
            TextMeshProUGUI rTmp = rulesLbl.AddComponent<TextMeshProUGUI>();
            rTmp.text      = "❓  How to play";
            rTmp.fontSize  = 9;
            rTmp.alignment = TextAlignmentOptions.Center;
            rTmp.color     = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.65f);
        }

        // Rules overlay
        _rulesOverlay = new GameObject("RulesOverlay");
        _rulesOverlay.transform.SetParent(_lobbyPanel.transform, false);
        {
            RectTransform ror = _rulesOverlay.AddComponent<RectTransform>();
            ror.anchorMin = Vector2.zero; ror.anchorMax = Vector2.one;
            ror.offsetMin = ror.offsetMax = Vector2.zero;

            Canvas rc = _rulesOverlay.AddComponent<Canvas>();
            rc.overrideSorting = true; rc.sortingOrder = 80;
            _rulesOverlay.AddComponent<GraphicRaycaster>();
            _rulesOverlay.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.10f, 0.97f);

            // Dismiss on tap
            Button dismissBtn = _rulesOverlay.AddComponent<Button>();
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(() => _rulesOverlay.SetActive(false));

            CreateText(_rulesOverlay, "HOW TO PLAY", 28,
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.97f),
                TextAlignmentOptions.Center, L_Gold).fontStyle = FontStyles.Bold;

            // Gold lines
            AddGoldLine(_rulesOverlay, 0.865f);
            AddGoldLine(_rulesOverlay, 0.07f);

            string rules =
                "<b>On your turn — no active bet:</b>\n" +
                "  Play 1–4 cards and declare any rank (you can lie!)\n\n" +
                "<b>On your turn — active bet exists:</b>\n" +
                "  <color=#55ff88>Believe</color> — reveal a pile card. Rank matches → discard. Wrong → you take pile.\n" +
                "  <color=#ff5555>Bluff!</color>  — challenge the bet. Bettor lied → they take pile. Honest → you take pile.\n" +
                "  <color=#5599ff>Bet</color>      — play more cards at the same declared rank.\n\n" +
                "<b>Winning:</b>\n" +
                "  Empty your hand first! The last player still holding cards loses.\n\n" +
                "<color=#aabbcc>Short deck (36 cards) is used for 3 or fewer players.</color>";

            TextMeshProUGUI rulesTmp = CreateText(_rulesOverlay, rules, 13,
                new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.86f),
                TextAlignmentOptions.TopLeft, new Color(0.88f, 0.90f, 0.95f));
            rulesTmp.richText = true;

            CreateText(_rulesOverlay, "Tap anywhere to close", 11,
                new Vector2(0.1f, 0.025f), new Vector2(0.9f, 0.07f),
                TextAlignmentOptions.Center, new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.45f));
        }
        _rulesOverlay.SetActive(false);

        // Countdown overlay
        _countdownOverlay = new GameObject("CountdownOverlay");
        _countdownOverlay.transform.SetParent(_lobbyPanel.transform, false);
        RectTransform coRect = _countdownOverlay.AddComponent<RectTransform>();
        coRect.anchorMin = Vector2.zero; coRect.anchorMax = Vector2.one;
        coRect.offsetMin = coRect.offsetMax = Vector2.zero;
        Canvas coCanvas = _countdownOverlay.AddComponent<Canvas>();
        coCanvas.overrideSorting = true; coCanvas.sortingOrder = 50;
        _countdownOverlay.AddComponent<GraphicRaycaster>();
        _countdownOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.80f);

        CreateText(_countdownOverlay, "Game starting...", 20,
            new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.70f),
            TextAlignmentOptions.Center, new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.85f));

        _countdownText = CreateText(_countdownOverlay, "3", 96,
            new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.58f),
            TextAlignmentOptions.Center, L_Gold);

        _countdownOverlay.SetActive(false);
    }

    private void AddSuitDecor(GameObject parent, string suit, Vector2 aMin, Vector2 aMax,
        Color? color = null)
    {
        GameObject go = new GameObject("Suit_" + suit);
        go.transform.SetParent(parent.transform, false);
        RectTransform r = go.AddComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = r.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = suit;
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color ?? new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.30f);
        tmp.raycastTarget = false;
    }

    private void AddGoldLine(GameObject parent, float anchorY)
    {
        GameObject go = new GameObject("GoldLine");
        go.transform.SetParent(parent.transform, false);
        RectTransform r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.08f, anchorY);
        r.anchorMax = new Vector2(0.92f, anchorY);
        r.offsetMin = new Vector2(0, 0);
        r.offsetMax = new Vector2(0, 1);
        go.AddComponent<Image>().color = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.35f);
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
        img.color = L_Field;

        // Focus bottom-line — dim gold normally, bright gold when field is active
        GameObject focusLine = new GameObject("FocusLine");
        focusLine.transform.SetParent(go.transform, false);
        RectTransform flr = focusLine.AddComponent<RectTransform>();
        flr.anchorMin = new Vector2(0f, 0f); flr.anchorMax = new Vector2(1f, 0f);
        flr.offsetMin = new Vector2(0, 0);   flr.offsetMax = new Vector2(0, 3);
        Image focusLineImg = focusLine.AddComponent<Image>();
        focusLineImg.color = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.22f);

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
        input.placeholder    = phComp;

        input.richText     = false;
        input.readOnly     = false;
        input.interactable = true;

        // Visual focus feedback
        input.onSelect.AddListener(_ =>
        {
            img.color         = new Color(L_Field.r * 1.55f, L_Field.g * 1.55f, L_Field.b * 1.55f);
            focusLineImg.color = L_Gold;
        });
        input.onDeselect.AddListener(_ =>
        {
            img.color         = L_Field;
            focusLineImg.color = new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.22f);
        });

        return input;
    }

    private Button CreateButton(GameObject parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        // Gold border frame
        GameObject frame = new GameObject("Frame_" + label);
        frame.transform.SetParent(parent.transform, false);
        RectTransform frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = anchorMin; frameRect.anchorMax = anchorMax;
        frameRect.offsetMin = new Vector2(0, 3); frameRect.offsetMax = new Vector2(0, -3);
        frame.AddComponent<Image>().color = L_Gold;

        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(frame.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);

        go.AddComponent<Image>().color = color;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        // Top highlight
        GameObject hl = new GameObject("Highlight");
        hl.transform.SetParent(go.transform, false);
        RectTransform hlr = hl.AddComponent<RectTransform>();
        hlr.anchorMin = new Vector2(0f, 0.72f); hlr.anchorMax = Vector2.one;
        hlr.offsetMin = hlr.offsetMax = Vector2.zero;
        hl.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    private bool ValidateInputs(string playerName, string roomCode, bool roomRequired)
    {
        if (string.IsNullOrEmpty(playerName))      { _statusText.text = "Please enter your name!"; return false; }
        if (playerName.Length > 20)                { _statusText.text = "Name must be 20 characters or fewer!"; return false; }
        if (roomRequired && string.IsNullOrEmpty(roomCode)) { _statusText.text = "Please enter a room code!"; return false; }
        if (!string.IsNullOrEmpty(roomCode) && !IsAlphanumeric(roomCode)) { _statusText.text = "Room code must be letters and numbers only!"; return false; }
        return true;
    }

    private static bool IsAlphanumeric(string s)
    {
        foreach (char c in s)
            if (!char.IsLetterOrDigit(c)) return false;
        return true;
    }

    private async void OnCreateClicked()
    {
        string playerName = _nameInput.text.Trim();
        string roomCode = _roomCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(roomCode))
        {
            roomCode = GenerateRoomCode();
            _roomCodeInput.text = roomCode;
        }

        if (!ValidateInputs(playerName, roomCode, false)) return;

        PlayerPrefs.SetString("bluff_player_name", playerName);
        PlayerPrefs.SetString("bluff_room_code", roomCode);
        PlayerPrefs.Save();

        SetButtonsInteractable(false);
        _statusText.color = new Color(0.83f, 0.685f, 0.215f); // gold
        _statusText.text  = $"Creating room {roomCode}...";

        try
        {
            await NetworkManager.Instance.CreateRoom(roomCode, playerName, _maxPlayers);
            _statusText.text = $"Room {roomCode} created! Waiting for players...";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreateRoom failed: {e.Message}");
            _statusText.text  = "Failed to create room. Try again.";
            _statusText.color = new Color(1f, 0.35f, 0.35f);
            SetButtonsInteractable(true);
        }
    }

    private async void OnJoinClicked()
    {
        string playerName = _nameInput.text.Trim();
        string roomCode = _roomCodeInput.text.Trim().ToUpper();

        if (!ValidateInputs(playerName, roomCode, true)) return;

        PlayerPrefs.SetString("bluff_player_name", playerName);
        PlayerPrefs.SetString("bluff_room_code", roomCode);
        PlayerPrefs.Save();

        SetButtonsInteractable(false);
        _statusText.color = new Color(0.83f, 0.685f, 0.215f); // gold
        _statusText.text  = $"Joining room {roomCode}...";

        try
        {
            await NetworkManager.Instance.JoinRoom(roomCode, playerName);
            _statusText.text = $"Joined room {roomCode}!";
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JoinRoom failed: {e.Message}");
            bool notFound = e.Message.Contains("GameNotFound") || e.Message.Contains("InvalidGameVersion");
            _statusText.text  = notFound ? "Room not found. Check the code and try again." : "Connection failed. Try again.";
            _statusText.color = new Color(1f, 0.35f, 0.35f);
            SetButtonsInteractable(true);
        }
    }

    private void OnBotCountToggle()
    {
        _botCount = (_botCount % 3) + 1; // 1 → 2 → 3 → 1
        if (_botCountLabel != null)
            _botCountLabel.text = _botCount == 1 ? "1 Bot  ↻" : $"{_botCount} Bots ↻";
    }

    private void OnPracticeClicked()
    {
        if (GameManager.Instance == null)
        {
            if (_statusText != null)
            {
                _statusText.text  = "GameManager not available.";
                _statusText.color = new Color(1f, 0.35f, 0.35f);
            }
            return;
        }
        string playerName = _nameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName)) playerName = "Player";
        PlayerPrefs.SetString("bluff_player_name", playerName);
        PlayerPrefs.Save();
        Hide();
        var names = new System.Collections.Generic.List<string> { playerName };
        for (int i = 1; i <= _botCount; i++) names.Add($"Bot {i}");
        GameManager.Instance.StartGame(names);
        UIManager.Instance?.ShowGameUI();
        UIManager.Instance?.RefreshUI(GameManager.Instance.GetState(), "0");
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

    private void OnPlayerCountChanged(int count)
    {
        int cap = NetworkManager.Instance?.MaxPlayers ?? _maxPlayers;
        _playerCountText.text = $"Players in lobby:  {count} / {cap}";
        _startButton.transform.parent.gameObject.SetActive(
            NetworkManager.Instance != null && NetworkManager.Instance.IsHost && count >= 2);
    }

    private void OnPlayerListChanged(string[] names)
    {
        if (_playerListText == null) return;
        if (names == null || names.Length == 0)
        {
            _waitingBg?.SetActive(false);
            _playerListText.text = "";
            return;
        }

        _waitingBg?.SetActive(true);
        _settingsRow?.SetActive(NetworkManager.Instance != null && NetworkManager.Instance.IsHost);

        // Show room code
        if (_waitingRoomCodeText != null)
        {
            string code = NetworkedGameManager.Instance?.Runner?.SessionInfo.Name ?? "";
            _waitingRoomCodeText.text = string.IsNullOrEmpty(code) ? "" : $"Room: <b>{code}</b>  (tap 📋)";
        }

        var sb = new System.Text.StringBuilder();
        string[] suits = { "♠", "♥", "♦", "♣", "♣", "♠" };
        for (int i = 0; i < names.Length; i++)
        {
            Color c = PlayerColors[i % PlayerColors.Length];
            string hex = UnityEngine.ColorUtility.ToHtmlStringRGB(c);
            sb.Append($"  <color=#{hex}>{suits[i % suits.Length]}  {names[i]}</color>");
            if (i < names.Length - 1) sb.AppendLine();
        }

        // Non-hosts: show current game settings (with safe fallbacks for pre-sync state)
        var ngm = NetworkedGameManager.Instance;
        if (ngm != null && !(NetworkManager.Instance != null && NetworkManager.Instance.IsHost))
        {
            string[] deckStrs = { "Auto", "36 cards", "52 cards" };
            int dIdx  = Mathf.Clamp(ngm.NetworkedDeckChoice, 0, 2);
            int maxP  = ngm.NetworkedMaxPlayers > 0 ? ngm.NetworkedMaxPlayers : 6;
            int timer = ngm.TurnTimeout > 0 ? (int)ngm.TurnTimeout : 30;
            sb.AppendLine();
            sb.Append($"\n  <color=#8899aa>⚙  Timer: <b>{timer}s</b>" +
                      $"  ·  Deck: <b>{deckStrs[dIdx]}</b>" +
                      $"  ·  Max: <b>{maxP}</b></color>");
        }

        _playerListText.text = sb.ToString();
    }

    private void OnTimerCycleClicked()
    {
        _timerChoice = (_timerChoice + 1) % TimerLabels.Length;
        if (_timerLabel != null) _timerLabel.text = TimerLabels[_timerChoice];
        NetworkedGameManager.Instance?.SetLobbySettings(TimerValues[_timerChoice], _deckChoice);
    }

    private void OnDeckCycleClicked()
    {
        _deckChoice = (_deckChoice + 1) % DeckLabels.Length;
        if (_deckLabel != null) _deckLabel.text = DeckLabels[_deckChoice];
        NetworkedGameManager.Instance?.SetLobbySettings(TimerValues[_timerChoice], _deckChoice);
    }

    private void OnWaitingRoomCopyClicked()
    {
        string code = NetworkedGameManager.Instance?.Runner?.SessionInfo.Name ?? "";
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            if (_statusText != null) _statusText.text = $"Room code {code} copied!";
        }
    }

    private void OnLobbyMuteToggled()
    {
        AudioManager.ToggleMute();
        if (_lobbyMuteLabel != null)
            _lobbyMuteLabel.text = AudioManager.IsMuted ? "🔇  Muted" : "🔊  Sound";
    }

    private void OnMaxPlayersCycleClicked()
    {
        int idx = System.Array.IndexOf(MaxPlayerOpts, _maxPlayers);
        _maxPlayers = MaxPlayerOpts[(idx + 1) % MaxPlayerOpts.Length];
        if (_maxPlayersLabel != null) _maxPlayersLabel.text = _maxPlayers.ToString();
        // Max-players is baked into session creation; also update NetworkedGameManager cap
        NetworkedGameManager.Instance?.SetMaxPlayers(_maxPlayers);
    }

    private void CreateSettingButton(GameObject parent, string category, string initialValue,
        Vector2 anchorMin, Vector2 anchorMax,
        out TextMeshProUGUI valueLabel, UnityEngine.Events.UnityAction onClick)
    {
        GameObject frame = new GameObject("Setting_" + category);
        frame.transform.SetParent(parent.transform, false);
        RectTransform fr = frame.AddComponent<RectTransform>();
        fr.anchorMin = anchorMin; fr.anchorMax = anchorMax;
        fr.offsetMin = new Vector2(2, 0); fr.offsetMax = new Vector2(-2, 0);
        frame.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.9f);

        Button btn = frame.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        // Category label (top 40%)
        TextMeshProUGUI cat = CreateText(frame, category, 8,
            new Vector2(0f, 0.55f), Vector2.one,
            TextAlignmentOptions.Center, new Color(L_Gold.r, L_Gold.g, L_Gold.b, 0.70f));
        cat.fontStyle = FontStyles.Bold;

        // Value label (bottom 55%)
        valueLabel = CreateText(frame, initialValue, 12,
            new Vector2(0f, 0f), new Vector2(1f, 0.58f),
            TextAlignmentOptions.Center, Color.white);
        valueLabel.fontStyle = FontStyles.Bold;
    }

    private void OnRulesButtonClicked()
    {
        if (_rulesOverlay != null) _rulesOverlay.SetActive(true);
    }

    private void OnStartGameClicked()
    {
        _startButton.interactable = false;
        // Apply latest settings before starting (in case host changed them after creation)
        NetworkedGameManager.Instance?.SetLobbySettings(TimerValues[_timerChoice], _deckChoice);
        NetworkedGameManager.Instance?.RequestStartGame();
    }

    private void ShowCountdown(int secondsLeft)
    {
        if (secondsLeft == 0)
        {
            _countdownOverlay.SetActive(false);
        }
        else
        {
            _countdownOverlay.SetActive(true);
            _countdownText.text = secondsLeft.ToString();
        }
    }
}