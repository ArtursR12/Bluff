using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Bluff.Core;
using System.Collections.Generic;
using System.Text;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Canvas _canvas;

    // Panels
    private GameObject _topPanel;
    private GameObject _middlePanel;
    private GameObject _bottomPanel;

    // Top panel
    private GameObject _opponentFansContainer;
    private TextMeshProUGUI _roomCodeText;
    private TextMeshProUGUI _muteButtonLabel;

    // Middle panel
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _currentBetText;
    private GameObject _pileVisualContainer;
    private TextMeshProUGUI _pileCountText;
    private TextMeshProUGUI _discardText;
    private TextMeshProUGUI _actionLogText;

    // Middle panel
    private Image _statusBg;
    private TextMeshProUGUI _betRankBig;

    // Action log
    private readonly Queue<string> _actionLog = new Queue<string>();
    private readonly List<string> _fullHistory = new List<string>();
    private GameObject _historyOverlay;
    private TextMeshProUGUI _historyText;
    private int _lastBetPlayerIndex = -1;
    private int _lastBetCount      = -1;

    // Bottom panel
    private GameObject _handContainer;
    private Button _believeButton;
    private Button _bluffButton;
    private Button _rebetButton;
    private TextMeshProUGUI _rebetButtonLabel;
    private TextMeshProUGUI _selectionInfoText;
    private TextMeshProUGUI _localPlayerInfoText;

    // Card selection state
    private List<CardView> _handCardViews = new List<CardView>();
    private List<int> _selectedCardIndices = new List<int>();

    // Overlays
    private GameObject _gameOverOverlay;
    private TextMeshProUGUI _gameOverText;
    private TextMeshProUGUI _gameOverWinnersText;
    private Button _playAgainButton;
    private GameObject _playAgainFrame;
    private TextMeshProUGUI _waitingForHostText;

    // Restart countdown overlay (used during Play Again)
    private GameObject _gameCountdownOverlay;
    private TextMeshProUGUI _gameCountdownText;

    // Turn glow border on bottom panel
    private Image _turnGlowStrip;

    // Turn timer progress bar
    private Image _timerBarFill;

    // Connection lost overlay (above everything)
    private GameObject _connectionLostOverlay;

    // Turn pulse
    private Coroutine _turnPulseRoutine;
    private bool _wasMyTurn;

    // Flag: suppress timer bar during play-again countdown
    private bool _isCountingDown;

    // 10-second turn warning — fires once per turn
    private bool _warned10s;
    private string _lastCurrentPlayerForWarning;

    // Track when local player goes empty-handed
    private bool _warnedHandEmpty;

    // Round counter (increments each time a pile is resolved)
    private int _roundNumber;

    // Smooth count tweens for pile / discard
    private int _displayedPileCount;
    private int _displayedDiscardCount;
    private Coroutine _pileTweenRoutine;
    private Coroutine _discardTweenRoutine;

    // Pile shake
    private Coroutine _pileShakeRoutine;
    private int _lastBuiltPileCount = -1;

    // Offline bot auto-play
    private Coroutine _botTurnRoutine;

    // Initial deal animation flag
    private bool _pendingDealAnimation;

    // Local player id
    private string _localPlayerId = "0";

    // Active game manager — set once at game start, never checked per-action
    private IGameManager _gameManager;

    // Per-game stats (reset on game start)
    private struct PlayerStats { public int PilesTaken; public int BluffsCaught; public int BadChallenges; }
    private readonly Dictionary<string, PlayerStats> _stats = new Dictionary<string, PlayerStats>();
    private TextMeshProUGUI _statsText;
    private TextMeshProUGUI _lifetimeText;

    // Disconnect grace countdown banner
    private GameObject _disconnectBanner;
    private TextMeshProUGUI _disconnectBannerText;
    private Button _endGraceButton;
    private Coroutine _disconnectGraceRoutine;

    // Android back-button double-press state
#if UNITY_ANDROID
    private float _lastBackPress = -99f;
#endif

    // Spectator reaction buttons (built lazily, replacing action buttons)
    private bool _spectatorButtonsBuilt;
    private GameObject _spectatorReactionContainer;

    // ── PALETTE ─────────────────────────────────────────────────
    private static readonly Color P_Dark   = new Color(0.035f, 0.05f,  0.10f,  1f);
    private static readonly Color P_Felt   = new Color(0.05f,  0.135f, 0.065f, 1f);
    private static readonly Color P_Gold   = new Color(0.83f,  0.685f, 0.215f, 1f);
    private static readonly Color P_GoldBg = new Color(0.83f,  0.685f, 0.215f, 0.13f);
    private static readonly Color P_Red    = new Color(0.45f,  0.055f, 0.055f, 1f);
    private static readonly Color P_Green  = new Color(0.055f, 0.34f,  0.10f,  1f);
    private static readonly Color P_Blue   = new Color(0.08f,  0.135f, 0.45f,  1f);
    private static readonly Color P_Muted  = new Color(0.42f,  0.52f,  0.60f,  1f);
    private static readonly Color P_Pane   = new Color(0.09f,  0.14f,  0.24f,  0.65f);

    // Per-player colors (index 0–5) for multi-player identification
    private static readonly Color[] P_PlayerColors =
    {
        new Color(0.30f, 0.75f, 1.00f, 1f),  // 0 — sky blue
        new Color(1.00f, 0.55f, 0.20f, 1f),  // 1 — orange
        new Color(0.55f, 1.00f, 0.45f, 1f),  // 2 — lime
        new Color(1.00f, 0.38f, 0.65f, 1f),  // 3 — pink
        new Color(0.75f, 0.55f, 1.00f, 1f),  // 4 — purple
        new Color(1.00f, 0.92f, 0.30f, 1f),  // 5 — yellow
    };

    private static Color GetPlayerColor(int playerIndex)
        => P_PlayerColors[Mathf.Clamp(playerIndex, 0, P_PlayerColors.Length - 1)];

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _canvas = FindFirstObjectByType<Canvas>();
        BuildUI();
        NetworkedGameManager.OnGameStarted    += ShowGameUI;
        NetworkedGameManager.OnStateRefresh   += RefreshUI;
        NetworkedGameManager.OnGameOver       += ShowGameOver;
        NetworkedGameManager.OnCardRevealed   += OnCardRevealedHandler;
        NetworkedGameManager.OnGameResetting  += OnGameResettingHandler;
        NetworkedGameManager.OnCountdownTick  += OnGameCountdownTick;
        NetworkedGameManager.OnConnectionLost  += OnConnectionLostHandler;
        NetworkedGameManager.OnTurnTimedOut    += OnTurnTimedOutHandler;
        NetworkedGameManager.OnDisconnectGrace    += OnDisconnectGraceHandler;
        NetworkedGameManager.OnSpectatorReaction  += OnSpectatorReactionHandler;
        NetworkedGameManager.OnPlayerReconnected  += OnPlayerReconnectedHandler;

        if (GuessingScreenUI.Instance == null)
            new GameObject("GuessingScreenUI").AddComponent<GuessingScreenUI>();
    }

    void OnDestroy()
    {
        NetworkedGameManager.OnGameStarted   -= ShowGameUI;
        NetworkedGameManager.OnStateRefresh  -= RefreshUI;
        NetworkedGameManager.OnGameOver      -= ShowGameOver;
        NetworkedGameManager.OnCardRevealed  -= OnCardRevealedHandler;
        NetworkedGameManager.OnGameResetting -= OnGameResettingHandler;
        NetworkedGameManager.OnCountdownTick -= OnGameCountdownTick;
        NetworkedGameManager.OnConnectionLost  -= OnConnectionLostHandler;
        NetworkedGameManager.OnTurnTimedOut    -= OnTurnTimedOutHandler;
        NetworkedGameManager.OnDisconnectGrace   -= OnDisconnectGraceHandler;
        NetworkedGameManager.OnSpectatorReaction -= OnSpectatorReactionHandler;
        NetworkedGameManager.OnPlayerReconnected -= OnPlayerReconnectedHandler;
    }

    void Update()
    {
        if (_timerBarFill == null || NetworkedGameManager.Instance == null) return;
        if (!_topPanel.activeSelf) return; // only during active game

        // Freeze bar during play-again countdown or after game ends (old timer may still tick)
        if (_isCountingDown || NetworkedGameManager.Instance.GameOver)
        {
            _timerBarFill.fillAmount = 0f;
            _timerBarFill.color = P_Muted;
            return;
        }

        float remaining = NetworkedGameManager.Instance.GetTurnTimeRemaining();
        float timeout   = NetworkedGameManager.Instance.TurnTimeout;
        float ratio     = Mathf.Clamp01(remaining / (timeout > 0f ? timeout : 30f));
        _timerBarFill.fillAmount = ratio;

        // Gold → orange → red as time runs out
        Color timerColor;
        if (ratio > 0.4f)
            timerColor = P_Gold;
        else if (ratio > 0.15f)
            timerColor = Color.Lerp(new Color(1f, 0.38f, 0.05f), P_Gold, (ratio - 0.15f) / 0.25f);
        else
            timerColor = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.38f, 0.05f), ratio / 0.15f);

        _timerBarFill.color = timerColor;

        // 10-second warning toast — once per turn, only when it's our turn
        var ngm = NetworkedGameManager.Instance;
        string curId = ngm.GetLocalState()?.CurrentPlayer?.Id ?? "";
        if (curId != _lastCurrentPlayerForWarning)
        {
            _lastCurrentPlayerForWarning = curId;
            _warned10s = false;
        }
        if (!_warned10s && remaining > 0f && remaining <= 10f && curId == _localPlayerId)
        {
            _warned10s = true;
            ShowToast("⏰  10 seconds left!", new Color(1f, 0.38f, 0.05f));
        }

#if UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_gameOverOverlay != null && _gameOverOverlay.activeSelf)
            {
                OnDisconnectClicked();
            }
            else if (_topPanel.activeSelf)
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastBackPress < 2f)
                    OnDisconnectClicked();
                else
                {
                    _lastBackPress = now;
                    ShowToast("Press back again to leave game", P_Muted);
                }
            }
        }
#endif
    }

    private void OnTurnTimedOutHandler(string playerName)
    {
        ShowToast($"⏱  {playerName} ran out of time", new Color(1f, 0.55f, 0.15f));
    }

    private void OnSpectatorReactionHandler(string playerName, string emoji)
    {
        ShowToast($"{emoji}  {playerName}", new Color(1f, 0.85f, 0.30f));
    }

    private void OnDisconnectGraceHandler(string playerName, int seconds)
    {
        if (_disconnectGraceRoutine != null) StopCoroutine(_disconnectGraceRoutine);
        _disconnectGraceRoutine = StartCoroutine(DisconnectGraceRoutine(playerName, seconds));
    }

    private void OnPlayerReconnectedHandler(string playerName)
    {
        // Cancel the grace period banner
        if (_disconnectGraceRoutine != null)
        {
            StopCoroutine(_disconnectGraceRoutine);
            _disconnectGraceRoutine = null;
        }
        if (_disconnectBanner != null) _disconnectBanner.SetActive(false);
        ShowToast($"{playerName} reconnected!", new Color(0.35f, 1f, 0.45f));
        AddActionLog($"<color=#55ff88>{playerName}</color> reconnected");
    }

    private void EnsureDisconnectBanner()
    {
        if (_disconnectBanner != null) return;

        _disconnectBanner = new GameObject("DisconnectBanner");
        _disconnectBanner.transform.SetParent(_canvas.transform, false);

        Canvas dc = _disconnectBanner.AddComponent<Canvas>();
        dc.overrideSorting = true;
        dc.sortingOrder    = 160;
        _disconnectBanner.AddComponent<GraphicRaycaster>();

        RectTransform dr = _disconnectBanner.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0f, 0.89f);
        dr.anchorMax = new Vector2(1f, 0.98f);
        dr.offsetMin = dr.offsetMax = Vector2.zero;

        _disconnectBanner.AddComponent<Image>().color = new Color(0.50f, 0.08f, 0.04f, 0.93f);

        AddHorizontalStrip(_disconnectBanner, atBottom: false, new Color(1f, 0.35f, 0.10f), 2f);
        AddHorizontalStrip(_disconnectBanner, atBottom: true,  new Color(1f, 0.35f, 0.10f), 2f);

        // Text — left 72%
        GameObject textGo = new GameObject("Txt");
        textGo.transform.SetParent(_disconnectBanner.transform, false);
        RectTransform tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = new Vector2(0.72f, 1f);
        tr.offsetMin = new Vector2(8, 2);
        tr.offsetMax = new Vector2(-4, -2);

        _disconnectBannerText = textGo.AddComponent<TextMeshProUGUI>();
        _disconnectBannerText.fontSize  = 12;
        _disconnectBannerText.alignment = TextAlignmentOptions.MidlineLeft;
        _disconnectBannerText.color     = Color.white;
        _disconnectBannerText.fontStyle = FontStyles.Bold;

        // "End now" button — right 27%
        _endGraceButton = CreateButton(_disconnectBanner, "End Now",
            new Color(0.55f, 0.08f, 0.08f),
            new Vector2(0.73f, 0.08f), new Vector2(0.98f, 0.92f),
            OnEndGraceClicked);
        var btnLbl = _endGraceButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnLbl != null) btnLbl.fontSize = 11;
    }

    private void OnEndGraceClicked()
    {
        NetworkedGameManager.Instance?.RPC_ForceEndGrace();
    }

    private System.Collections.IEnumerator DisconnectGraceRoutine(string playerName, int seconds)
    {
        EnsureDisconnectBanner();
        _disconnectBanner.SetActive(true);

        for (int s = seconds; s >= 0; s--)
        {
            if (s == 0)
                _disconnectBannerText.text = $"📶  {playerName} disconnected  —  ending game...";
            else if (s > 8)
                _disconnectBannerText.text = $"📶  {playerName} disconnected  —  can rejoin  ({s}s)";
            else
                _disconnectBannerText.text = $"📶  {playerName} disconnected  —  ending in  {s}s";
            if (s == 0) break;
            yield return new WaitForSeconds(1f);
        }

        yield return new WaitForSeconds(2f);
        _disconnectBanner.SetActive(false);
        _disconnectGraceRoutine = null;
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
        // Gold border frame
        GameObject frame = new GameObject("Frame_" + label);
        frame.transform.SetParent(parent.transform, false);
        RectTransform frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = anchorMin;
        frameRect.anchorMax = anchorMax;
        frameRect.offsetMin = new Vector2(4, 4);
        frameRect.offsetMax = new Vector2(-4, -4);
        frame.AddComponent<Image>().color = P_Gold;

        // Button inset inside frame
        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(frame.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2);
        rect.offsetMax = new Vector2(-2, -2);

        go.AddComponent<Image>().color = color;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        // Top-quarter highlight (glass effect)
        GameObject hl = new GameObject("Highlight");
        hl.transform.SetParent(go.transform, false);
        RectTransform hlr = hl.AddComponent<RectTransform>();
        hlr.anchorMin = new Vector2(0f, 0.72f);
        hlr.anchorMax = Vector2.one;
        hlr.offsetMin = hlr.offsetMax = Vector2.zero;
        hl.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

        // Label
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 17;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    private void BuildUI()
    {
        BuildTopPanel();
        BuildMiddlePanel();
        BuildBottomPanel();
        BuildGameOverOverlay();
        BuildGameCountdownOverlay();
        BuildConnectionLostOverlay();

        _topPanel.SetActive(false);
        _middlePanel.SetActive(false);
        _bottomPanel.SetActive(false);
    }

    public void ShowGameUI()
    {
        _gameManager = NetworkedGameManager.Instance != null
            ? (IGameManager)NetworkedGameManager.Instance
            : GameManager.Instance;
        _actionLog.Clear();
        _fullHistory.Clear();
        _actionLogText.text = "";
        if (_historyOverlay != null) _historyOverlay.SetActive(false);
        _lastBetPlayerIndex = -1;
        _lastBetCount       = -1;
        _roomCodeText.text  = "";
        _wasMyTurn          = false;
        _pendingDealAnimation = true;
        _isCountingDown = false;
        _displayedPileCount = 0;
        _displayedDiscardCount = 0;
        _lastBuiltPileCount = -1;
        _stats.Clear();
        _warned10s = false;
        _lastCurrentPlayerForWarning = "";
        _warnedHandEmpty = false;
        _roundNumber = 0;
        if (_disconnectGraceRoutine != null) { StopCoroutine(_disconnectGraceRoutine); _disconnectGraceRoutine = null; }
        if (_disconnectBanner != null) _disconnectBanner.SetActive(false);
        if (_botTurnRoutine != null) { StopCoroutine(_botTurnRoutine); _botTurnRoutine = null; }
        // Reset spectator reaction buttons
        if (_spectatorReactionContainer != null)
        {
            Destroy(_spectatorReactionContainer);
            _spectatorReactionContainer = null;
        }
        _spectatorButtonsBuilt = false;
        // Ensure original action buttons are visible for a new game
        if (_believeButton != null) _believeButton.transform.parent.gameObject.SetActive(true);
        if (_bluffButton   != null) _bluffButton.transform.parent.gameObject.SetActive(true);
        if (_rebetButton   != null) _rebetButton.transform.parent.gameObject.SetActive(true);
        if (_selectionInfoText != null) _selectionInfoText.color = P_Muted;
        _topPanel.SetActive(true);
        _middlePanel.SetActive(true);
        _bottomPanel.SetActive(true);

        // Seed the action log with a start marker so it's never blank at round 0
        string startLabel = NetworkedGameManager.Instance != null
            ? (NetworkedGameManager.Instance.IsShortDeck ? "36-card deck" : "52-card deck")
            : "offline";
        AddActionLog($"<color=#445544>── game started  ({startLabel}) ──────────</color>");
    }

    private void AddHorizontalStrip(GameObject parent, bool atBottom, Color color, float px = 2f)
    {
        GameObject strip = new GameObject(atBottom ? "StripBottom" : "StripTop");
        strip.transform.SetParent(parent.transform, false);
        RectTransform r = strip.AddComponent<RectTransform>();
        if (atBottom) { r.anchorMin = new Vector2(0,0); r.anchorMax = new Vector2(1,0); r.offsetMin = new Vector2(0,0); r.offsetMax = new Vector2(0,px); }
        else          { r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1); r.offsetMin = new Vector2(0,-px); r.offsetMax = new Vector2(0,0); }
        strip.AddComponent<Image>().color = color;
    }

    private void BuildTopPanel()
    {
        float safeT = 1f - SafeArea.Top;
        _topPanel = CreatePanel("TopPanel", safeT - 0.24f, safeT, P_Dark);
        AddHorizontalStrip(_topPanel, atBottom: true, P_Gold, 2f);

        _opponentFansContainer = new GameObject("OpponentFans");
        _opponentFansContainer.transform.SetParent(_topPanel.transform, false);
        RectTransform fanRect = _opponentFansContainer.AddComponent<RectTransform>();
        fanRect.anchorMin = new Vector2(0f, 0f);
        fanRect.anchorMax = new Vector2(0.80f, 1f);
        fanRect.offsetMin = Vector2.zero;
        fanRect.offsetMax = Vector2.zero;

        _roomCodeText = CreateText(_topPanel, "", 10,
            new Vector2(0.81f, 0.40f), new Vector2(1f, 1f),
            TextAlignmentOptions.MidlineRight);
        _roomCodeText.color = P_Gold;

        // Transparent tap target so tapping the room code copies it
        GameObject rcHit = new GameObject("RoomCodeHit");
        rcHit.transform.SetParent(_topPanel.transform, false);
        RectTransform rchr = rcHit.AddComponent<RectTransform>();
        rchr.anchorMin = new Vector2(0.81f, 0.38f);
        rchr.anchorMax = Vector2.one;
        rchr.offsetMin = rchr.offsetMax = Vector2.zero;
        rcHit.AddComponent<Image>().color = Color.clear;
        Button rcBtn = rcHit.AddComponent<Button>();
        rcBtn.transition = Selectable.Transition.None;
        rcBtn.onClick.AddListener(OnRoomCodeTapped);

        // Mute button — small, bottom-right corner of top panel
        GameObject muteBtnGo = new GameObject("MuteBtn");
        muteBtnGo.transform.SetParent(_topPanel.transform, false);
        RectTransform mbr = muteBtnGo.AddComponent<RectTransform>();
        mbr.anchorMin = new Vector2(0.82f, 0f);
        mbr.anchorMax = new Vector2(1.00f, 0.36f);
        mbr.offsetMin = new Vector2(2, 2);
        mbr.offsetMax = new Vector2(-2, -2);
        muteBtnGo.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.18f, 0.70f);
        Button muteBtn = muteBtnGo.AddComponent<Button>();
        muteBtn.transition = Selectable.Transition.None;
        muteBtn.onClick.AddListener(OnMuteToggled);

        GameObject muteLbl = new GameObject("Lbl");
        muteLbl.transform.SetParent(muteBtnGo.transform, false);
        RectTransform mlr = muteLbl.AddComponent<RectTransform>();
        mlr.anchorMin = Vector2.zero; mlr.anchorMax = Vector2.one;
        mlr.offsetMin = mlr.offsetMax = Vector2.zero;
        _muteButtonLabel = muteLbl.AddComponent<TextMeshProUGUI>();
        _muteButtonLabel.text      = AudioManager.IsMuted ? "🔇" : "🔊";
        _muteButtonLabel.fontSize  = 13;
        _muteButtonLabel.alignment = TextAlignmentOptions.Center;
        _muteButtonLabel.color     = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.75f);
    }

    private void OnMuteToggled()
    {
        AudioManager.ToggleMute();
        if (_muteButtonLabel != null)
            _muteButtonLabel.text = AudioManager.IsMuted ? "🔇" : "🔊";
    }

    private static readonly string[] SpectatorEmojis = { "🔥", "😮", "😂", "👏", "👀" };
    private static readonly Color[]  SpectatorColors =
    {
        new Color(0.70f, 0.20f, 0.05f),
        new Color(0.20f, 0.18f, 0.55f),
        new Color(0.55f, 0.40f, 0.04f),
        new Color(0.08f, 0.35f, 0.12f),
        new Color(0.45f, 0.10f, 0.40f),
    };

    private void BuildSpectatorReactionButtons()
    {
        if (_spectatorButtonsBuilt) return;
        _spectatorButtonsBuilt = true;

        // Hide the original action buttons
        _believeButton.transform.parent.gameObject.SetActive(false);
        _bluffButton.transform.parent.gameObject.SetActive(false);
        _rebetButton.transform.parent.gameObject.SetActive(false);

        // Container so we can destroy it cleanly on game reset
        _spectatorReactionContainer = new GameObject("SpectatorReactions");
        _spectatorReactionContainer.transform.SetParent(_bottomPanel.transform, false);
        RectTransform cr = _spectatorReactionContainer.AddComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = cr.offsetMax = Vector2.zero;

        // Add 5 emoji buttons evenly spaced across the button zone
        float slotW = 1f / SpectatorEmojis.Length;
        float gap   = 0.006f;
        for (int i = 0; i < SpectatorEmojis.Length; i++)
        {
            int idx  = i;
            float x0 = i * slotW + gap;
            float x1 = x0 + slotW - gap * 2f;
            Button btn = CreateButton(_spectatorReactionContainer, SpectatorEmojis[i], SpectatorColors[i],
                new Vector2(x0, 0.01f), new Vector2(x1, 0.27f),
                () => SendSpectatorReaction(SpectatorEmojis[idx]));
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.fontSize = 22;
        }
    }

    private void SendSpectatorReaction(string emoji)
    {
        var ngm = NetworkedGameManager.Instance;
        if (ngm == null) return;
        string name = PlayerPrefs.GetString("bluff_player_name", "Spectator");
        ngm.RPC_SpectatorReaction(emoji, name);
    }

    private void BuildMiddlePanel()
    {
        float safeB = SafeArea.Bottom;
        float safeT = 1f - SafeArea.Top;
        float usable = safeT - safeB;
        float midBottom = safeB + 0.30f * usable;
        float midTop    = safeT - 0.24f * usable;
        _middlePanel = CreatePanel("MiddlePanel", midBottom, midTop, P_Felt);

        // ── STATUS BAR (top 14%) ──────────────────────────────
        GameObject statusCont = new GameObject("StatusCont");
        statusCont.transform.SetParent(_middlePanel.transform, false);
        RectTransform scr = statusCont.AddComponent<RectTransform>();
        scr.anchorMin = new Vector2(0f, 0.86f); scr.anchorMax = Vector2.one;
        scr.offsetMin = scr.offsetMax = Vector2.zero;
        _statusBg = statusCont.AddComponent<Image>();
        _statusBg.color = P_Pane;

        // Timer bar — sits at very bottom of the status container (3 px tall)
        GameObject timerBg = new GameObject("TimerBg");
        timerBg.transform.SetParent(statusCont.transform, false);
        RectTransform tbgr = timerBg.AddComponent<RectTransform>();
        tbgr.anchorMin = new Vector2(0f, 0f); tbgr.anchorMax = new Vector2(1f, 0f);
        tbgr.offsetMin = new Vector2(0f, 0f); tbgr.offsetMax = new Vector2(0f, 3f);
        timerBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

        GameObject timerFill = new GameObject("TimerFill");
        timerFill.transform.SetParent(timerBg.transform, false);
        RectTransform tfr = timerFill.AddComponent<RectTransform>();
        tfr.anchorMin = Vector2.zero; tfr.anchorMax = Vector2.one;
        tfr.offsetMin = tfr.offsetMax = Vector2.zero;
        _timerBarFill = timerFill.AddComponent<Image>();
        _timerBarFill.type        = Image.Type.Filled;
        _timerBarFill.fillMethod  = Image.FillMethod.Horizontal;
        _timerBarFill.fillOrigin  = 0;
        _timerBarFill.color       = P_Gold;
        _timerBarFill.fillAmount  = 1f;

        AddHorizontalStrip(statusCont, atBottom: true,
            new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.4f), 1f);

        _statusText = CreateText(statusCont, "Starting...", 19,
            Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
        _statusText.fontStyle = FontStyles.Bold;

        // ── BET WIDGET (62–86%) ───────────────────────────────
        GameObject betBox = new GameObject("BetBox");
        betBox.transform.SetParent(_middlePanel.transform, false);
        RectTransform bbr = betBox.AddComponent<RectTransform>();
        bbr.anchorMin = new Vector2(0.04f, 0.62f); bbr.anchorMax = new Vector2(0.96f, 0.86f);
        bbr.offsetMin = bbr.offsetMax = Vector2.zero;
        betBox.AddComponent<Image>().color = new Color(0.04f, 0.10f, 0.04f, 0.85f);

        // Left gold accent bar
        GameObject accentBar = new GameObject("AccentBar");
        accentBar.transform.SetParent(betBox.transform, false);
        RectTransform abr = accentBar.AddComponent<RectTransform>();
        abr.anchorMin = Vector2.zero; abr.anchorMax = new Vector2(0f, 1f);
        abr.offsetMin = new Vector2(0, 0); abr.offsetMax = new Vector2(4, 0);
        accentBar.AddComponent<Image>().color = P_Gold;

        _currentBetText = CreateText(betBox, "No active bet", 13,
            new Vector2(0.04f, 0f), new Vector2(0.72f, 1f), TextAlignmentOptions.MidlineLeft);
        _currentBetText.color = P_Muted;

        // Large declared rank — right side of bet box (semi-transparent gold)
        _betRankBig = CreateText(betBox, "", 42,
            new Vector2(0.70f, 0f), Vector2.one, TextAlignmentOptions.MidlineRight);
        _betRankBig.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.22f);
        _betRankBig.fontStyle = FontStyles.Bold;

        // ── TABLE AREA (22–62%) ───────────────────────────────
        // Pile section — left 52%
        GameObject pileSection = new GameObject("PileSection");
        pileSection.transform.SetParent(_middlePanel.transform, false);
        RectTransform psr = pileSection.AddComponent<RectTransform>();
        psr.anchorMin = new Vector2(0.03f, 0.22f); psr.anchorMax = new Vector2(0.55f, 0.62f);
        psr.offsetMin = psr.offsetMax = Vector2.zero;

        // "PILE" label
        TextMeshProUGUI pileLabel = CreateText(pileSection, "PILE", 9,
            new Vector2(0f, 0.82f), Vector2.one, TextAlignmentOptions.Center);
        pileLabel.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.55f);
        pileLabel.fontStyle = FontStyles.Bold;

        _pileVisualContainer = new GameObject("PileVisual");
        _pileVisualContainer.transform.SetParent(pileSection.transform, false);
        RectTransform pvRect = _pileVisualContainer.AddComponent<RectTransform>();
        pvRect.anchorMin = new Vector2(0.10f, 0.12f); pvRect.anchorMax = new Vector2(0.90f, 0.82f);
        pvRect.offsetMin = pvRect.offsetMax = Vector2.zero;

        _pileCountText = CreateText(pileSection, "0", 18,
            new Vector2(0f, 0f), new Vector2(1f, 0.16f), TextAlignmentOptions.Center);
        _pileCountText.color = P_Gold;
        _pileCountText.fontStyle = FontStyles.Bold;

        // Discard section — right 43%
        GameObject discardSection = new GameObject("DiscardSection");
        discardSection.transform.SetParent(_middlePanel.transform, false);
        RectTransform dsr = discardSection.AddComponent<RectTransform>();
        dsr.anchorMin = new Vector2(0.56f, 0.22f); dsr.anchorMax = new Vector2(0.97f, 0.62f);
        dsr.offsetMin = dsr.offsetMax = Vector2.zero;
        discardSection.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.22f);

        TextMeshProUGUI discardLabel = CreateText(discardSection, "DISCARD", 9,
            new Vector2(0f, 0.80f), Vector2.one, TextAlignmentOptions.Center);
        discardLabel.color = new Color(P_Muted.r, P_Muted.g, P_Muted.b, 0.55f);
        discardLabel.fontStyle = FontStyles.Bold;

        // Discard ghost card placeholder (thin muted card outline)
        GameObject discardGhost = new GameObject("DiscardGhost");
        discardGhost.transform.SetParent(discardSection.transform, false);
        RectTransform dgr = discardGhost.AddComponent<RectTransform>();
        dgr.anchorMin = new Vector2(0.15f, 0.18f); dgr.anchorMax = new Vector2(0.85f, 0.78f);
        dgr.offsetMin = dgr.offsetMax = Vector2.zero;
        discardGhost.AddComponent<Image>().color = new Color(P_Muted.r, P_Muted.g, P_Muted.b, 0.06f);

        _discardText = CreateText(discardSection, "0", 22,
            new Vector2(0f, 0.36f), new Vector2(1f, 0.82f), TextAlignmentOptions.Center);
        _discardText.color = new Color(P_Muted.r, P_Muted.g, P_Muted.b, 0.85f);
        _discardText.fontStyle = FontStyles.Bold;

        // ── ACTION LOG (bottom 22%) ───────────────────────────
        GameObject logBox = new GameObject("LogBox");
        logBox.transform.SetParent(_middlePanel.transform, false);
        RectTransform lbr = logBox.AddComponent<RectTransform>();
        lbr.anchorMin = new Vector2(0f, 0f); lbr.anchorMax = new Vector2(1f, 0.22f);
        lbr.offsetMin = lbr.offsetMax = Vector2.zero;
        logBox.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);
        AddHorizontalStrip(logBox, atBottom: false,
            new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.20f), 1f);

        _actionLogText = CreateText(logBox, "", 10,
            new Vector2(0.02f, 0f), new Vector2(0.98f, 1f),
            TextAlignmentOptions.BottomLeft);
        _actionLogText.color = new Color(0.60f, 0.80f, 0.60f, 1f);
        _actionLogText.overflowMode = TextOverflowModes.Truncate;

        // Tap the log box to open the full game history overlay
        Button logTapBtn = logBox.AddComponent<Button>();
        logTapBtn.transition = Selectable.Transition.None;
        logTapBtn.onClick.AddListener(ToggleHistoryOverlay);

        TextMeshProUGUI expandHint = CreateText(logBox, "▲ history", 8,
            new Vector2(0.72f, 0.82f), new Vector2(0.99f, 1f),
            TextAlignmentOptions.MidlineRight);
        expandHint.color = new Color(0.38f, 0.50f, 0.55f, 0.50f);
        expandHint.raycastTarget = false;
    }

    private void BuildBottomPanel()
    {
        float safeB2  = SafeArea.Bottom;
        float safeT2  = 1f - SafeArea.Top;
        float usable2 = safeT2 - safeB2;
        float botTop  = safeB2 + 0.30f * usable2;
        _bottomPanel = CreatePanel("BottomPanel", safeB2, botTop, new Color(0.03f, 0.04f, 0.09f, 1f));

        // Turn-glow top border (replaces static gold strip — pulses when YOUR TURN)
        GameObject glowStrip = new GameObject("TurnGlow");
        glowStrip.transform.SetParent(_bottomPanel.transform, false);
        RectTransform gsr = glowStrip.AddComponent<RectTransform>();
        gsr.anchorMin = new Vector2(0f, 1f); gsr.anchorMax = new Vector2(1f, 1f);
        gsr.offsetMin = new Vector2(0f, -3f); gsr.offsetMax = new Vector2(0f, 0f);
        _turnGlowStrip = glowStrip.AddComponent<Image>();
        _turnGlowStrip.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.35f);

        // Hand — top 66% of panel
        _handContainer = new GameObject("HandContainer");
        _handContainer.transform.SetParent(_bottomPanel.transform, false);
        RectTransform handRect = _handContainer.AddComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0f, 0.32f);
        handRect.anchorMax = new Vector2(1f, 1f);
        handRect.offsetMin = new Vector2(4, 4);
        handRect.offsetMax = new Vector2(-4, -4);

        // Selection info strip
        _selectionInfoText = CreateText(_bottomPanel, "Tap cards to select",
            11, new Vector2(0f, 0.27f), new Vector2(1f, 0.34f),
            TextAlignmentOptions.Center);
        _selectionInfoText.color = P_Muted;

        // Buttons — bottom 27%, split 3 ways
        _believeButton = CreateButton(_bottomPanel, "Believe", P_Green,
            new Vector2(0.005f, 0.01f), new Vector2(0.328f, 0.27f), OnBelieveClicked);

        _bluffButton = CreateButton(_bottomPanel, "Bluff!", P_Red,
            new Vector2(0.338f, 0.01f), new Vector2(0.661f, 0.27f), OnBluffClicked);

        _rebetButton = CreateButton(_bottomPanel, "Bet", P_Blue,
            new Vector2(0.672f, 0.01f), new Vector2(0.995f, 0.27f), OnBetClicked);
        _rebetButtonLabel = _rebetButton.GetComponentInChildren<TextMeshProUGUI>();

        // Local player info strip — top edge of panel, semi-transparent over cards
        _localPlayerInfoText = CreateText(_bottomPanel, "", 10,
            new Vector2(0.02f, 0.90f), new Vector2(0.98f, 1.00f),
            TextAlignmentOptions.Center);
        _localPlayerInfoText.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.50f);
    }

    private void BuildGameOverOverlay()
    {
        _gameOverOverlay = new GameObject("GameOverOverlay");
        _gameOverOverlay.transform.SetParent(_canvas.transform, false);

        RectTransform rect = _gameOverOverlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Canvas oc = _gameOverOverlay.AddComponent<Canvas>();
        oc.overrideSorting = true; oc.sortingOrder = 200;
        _gameOverOverlay.AddComponent<GraphicRaycaster>();

        _gameOverOverlay.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.94f);

        // Decorative top and bottom gold lines
        AddHorizontalStrip(_gameOverOverlay, atBottom: false, P_Gold, 3f);
        AddHorizontalStrip(_gameOverOverlay, atBottom: true,  P_Gold, 3f);

        // "GAME OVER" in gold
        TextMeshProUGUI title = CreateText(_gameOverOverlay, "GAME OVER", 48,
            new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f),
            TextAlignmentOptions.Center);
        title.color     = P_Gold;
        title.fontStyle = FontStyles.Bold;

        // Loser line — red
        _gameOverText = CreateText(_gameOverOverlay, "", 24,
            new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.84f),
            TextAlignmentOptions.Center);
        _gameOverText.color = new Color(1f, 0.30f, 0.30f, 1f);

        // Winners line — green
        _gameOverWinnersText = CreateText(_gameOverOverlay, "", 17,
            new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.72f),
            TextAlignmentOptions.Center);
        _gameOverWinnersText.color = new Color(0.35f, 1f, 0.45f, 1f);

        // Stats divider — thin horizontal line at 61.5% height
        {
            GameObject divGo = new GameObject("StatsDivider");
            divGo.transform.SetParent(_gameOverOverlay.transform, false);
            RectTransform divR = divGo.AddComponent<RectTransform>();
            divR.anchorMin = new Vector2(0.05f, 0.615f);
            divR.anchorMax = new Vector2(0.95f, 0.615f);
            divR.offsetMin = new Vector2(0, -1f);
            divR.offsetMax = new Vector2(0,  1f);
            divGo.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.6f, 0.4f);
        }

        // Per-player stats area
        _statsText = CreateText(_gameOverOverlay, "", 15,
            new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.61f),
            TextAlignmentOptions.TopLeft);
        _statsText.color = new Color(0.85f, 0.88f, 0.95f, 1f);

        // Lifetime record line (above buttons)
        _lifetimeText = CreateText(_gameOverOverlay, "", 11,
            new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.22f),
            TextAlignmentOptions.Center);
        _lifetimeText.color = new Color(0.55f, 0.65f, 0.75f, 1f);

        // Play Again (host only)
        _playAgainButton = CreateButton(_gameOverOverlay, "Play Again", P_Green,
            new Vector2(0.05f, 0.03f), new Vector2(0.46f, 0.17f), OnPlayAgainClicked);
        _playAgainFrame = _playAgainButton.transform.parent.gameObject;
        _playAgainFrame.SetActive(false);

        // "Waiting for host" text — shown for non-host clients in same slot as Play Again button
        _waitingForHostText = CreateText(_gameOverOverlay, "Waiting for host to restart...", 11,
            new Vector2(0.05f, 0.03f), new Vector2(0.46f, 0.17f),
            TextAlignmentOptions.Center);
        _waitingForHostText.color = new Color(0.50f, 0.65f, 0.50f, 0.70f);
        _waitingForHostText.gameObject.SetActive(false);

        // Disconnect
        CreateButton(_gameOverOverlay, "Disconnect", P_Red,
            new Vector2(0.54f, 0.03f), new Vector2(0.95f, 0.17f), OnDisconnectClicked);

        _gameOverOverlay.SetActive(false);
    }

    private void OnPlayAgainClicked()
    {
        _playAgainButton.interactable = false;

        // Offline restart: reinitialise GameManager with the same player names
        if (NetworkedGameManager.Instance == null && GameManager.Instance != null)
        {
            GameState prevState = GameManager.Instance.GetState();
            var names = prevState.Players.ConvertAll(p => p.Name);
            _gameOverOverlay.SetActive(false);
            GameManager.Instance.StartGame(names);
            ShowGameUI(); // resets all counters, clears logs, re-enables buttons
            RefreshUI(GameManager.Instance.GetState(), _localPlayerId);
            return;
        }

        NetworkedGameManager.Instance?.RequestPlayAgain();
    }

    private void OnGameResettingHandler()
    {
        _isCountingDown = true;
        _gameOverOverlay.SetActive(false);
        if (_waitingForHostText != null) _waitingForHostText.gameObject.SetActive(false);
        _actionLog.Clear();
        _fullHistory.Clear();
        _actionLogText.text = "";
        if (_historyOverlay != null) _historyOverlay.SetActive(false);
        _lastBetPlayerIndex = -1;
        _lastBetCount       = -1;
        if (_timerBarFill != null) { _timerBarFill.fillAmount = 0f; _timerBarFill.color = P_Muted; }
    }

    private void OnDisconnectClicked()
    {
        NetworkManager.Instance.Disconnect();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── GAME COUNTDOWN (play again) ──────────────────────────

    private void BuildGameCountdownOverlay()
    {
        _gameCountdownOverlay = new GameObject("GameCountdown");
        _gameCountdownOverlay.transform.SetParent(_canvas.transform, false);

        RectTransform r = _gameCountdownOverlay.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        Canvas c = _gameCountdownOverlay.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 190;
        _gameCountdownOverlay.AddComponent<GraphicRaycaster>();
        _gameCountdownOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        CreateText(_gameCountdownOverlay, "New round starting...", 22,
            new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.70f),
            TextAlignmentOptions.Center);

        _gameCountdownText = CreateText(_gameCountdownOverlay, "3", 96,
            new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.58f),
            TextAlignmentOptions.Center);

        _gameCountdownOverlay.SetActive(false);
    }

    private void OnGameCountdownTick(int seconds)
    {
        // Only handle during active game (play again); lobby handles initial countdown
        if (!_topPanel.activeSelf) return;
        if (seconds == 0)
        {
            _gameCountdownOverlay.SetActive(false);
        }
        else
        {
            _gameCountdownText.text = seconds.ToString();
            _gameCountdownOverlay.SetActive(true);
        }
    }

    // ── TURN PULSE ───────────────────────────────────────────

    private void UpdateTurnPulse(bool isMyTurn)
    {
        if (isMyTurn && !_wasMyTurn)
        {
            if (_turnPulseRoutine != null) StopCoroutine(_turnPulseRoutine);
            _turnPulseRoutine = StartCoroutine(TurnPulseRoutine());
            StartCoroutine(YourTurnFlashRoutine());
        }
        else if (!isMyTurn && _wasMyTurn)
        {
            if (_turnPulseRoutine != null) StopCoroutine(_turnPulseRoutine);
            _turnPulseRoutine = null;
            _statusText.transform.localScale = Vector3.one;
            if (_turnGlowStrip != null)
                _turnGlowStrip.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.35f);
        }
        _wasMyTurn = isMyTurn;
    }

    private System.Collections.IEnumerator TurnPulseRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * 2.2f;
            float pulse = (Mathf.Sin(t * Mathf.PI) + 1f) * 0.5f; // 0..1
            // Scale status text slightly
            float s = 1f + 0.06f * pulse;
            _statusText.transform.localScale = new Vector3(s, s, 1f);
            // Pulse glow strip alpha from 0.5 to 1.0
            if (_turnGlowStrip != null)
                _turnGlowStrip.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.55f + 0.45f * pulse);
            yield return null;
        }
    }

    private System.Collections.IEnumerator YourTurnFlashRoutine()
    {
        GameObject go = new GameObject("YourTurnFlash");
        go.transform.SetParent(_canvas.transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 170;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.10f, 0.44f);
        rt.anchorMax = new Vector2(0.90f, 0.58f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = "YOUR TURN";
        tmp.fontSize  = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0f);

        // Scale + fade in
        float e = 0f;
        while (e < 0.22f)
        {
            e += Time.deltaTime;
            float a = e / 0.22f;
            tmp.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, a);
            go.transform.localScale = new Vector3(0.8f + 0.2f * a, 0.8f + 0.2f * a, 1f);
            yield return null;
        }
        yield return new WaitForSeconds(0.65f);
        // Fade out
        e = 0f;
        while (e < 0.35f)
        {
            e += Time.deltaTime;
            float a = 1f - e / 0.35f;
            tmp.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, a);
            yield return null;
        }
        Destroy(go);
    }

    // ── ROOM CODE CLIPBOARD ──────────────────────────────────

    private void OnRoomCodeTapped()
    {
        string code = NetworkedGameManager.Instance?.Runner?.SessionInfo.Name ?? "";
        if (string.IsNullOrEmpty(code)) return;
        GUIUtility.systemCopyBuffer = code;
        ShowToast($"Room code {code} copied!", P_Gold);
    }

    // ── COUNT TWEEN ──────────────────────────────────────────

    private void TweenPileCount(int target)
    {
        if (_displayedPileCount == target) return;
        if (_pileTweenRoutine != null) StopCoroutine(_pileTweenRoutine);
        int from = _displayedPileCount;
        _pileTweenRoutine = StartCoroutine(CountTween(from, target,
            _pileCountText, v => _displayedPileCount = v));
    }

    private void TweenDiscardCount(int target)
    {
        if (_displayedDiscardCount == target) return;
        if (_discardTweenRoutine != null) StopCoroutine(_discardTweenRoutine);
        int from = _displayedDiscardCount;
        _discardTweenRoutine = StartCoroutine(CountTween(from, target,
            _discardText, v => _displayedDiscardCount = v));
    }

    private System.Collections.IEnumerator CountTween(int from, int to,
        TextMeshProUGUI label, System.Action<int> update)
    {
        float duration = Mathf.Clamp(Mathf.Abs(to - from) * 0.035f, 0.15f, 0.55f);
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            int val  = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            if (label != null) label.text = val.ToString();
            update(val);
            yield return null;
        }
        if (label != null) label.text = to.ToString();
        update(to);
    }

    // ── PILE SHAKE ───────────────────────────────────────────

    private void TriggerPileShake()
    {
        if (_pileShakeRoutine != null) StopCoroutine(_pileShakeRoutine);
        _pileShakeRoutine = StartCoroutine(PileShakeRoutine());
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    private System.Collections.IEnumerator PileShakeRoutine()
    {
        RectTransform rt = _pileVisualContainer.GetComponent<RectTransform>();
        float[] angles = { -10f, 12f, -9f, 10f, -6f, 5f, 0f };
        foreach (float angle in angles)
        {
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return new WaitForSeconds(0.04f);
        }
        rt.localRotation = Quaternion.identity;
        _pileShakeRoutine = null;
    }

    // ── TOAST NOTIFICATIONS ──────────────────────────────────

    private int _activeToasts;

    private void ShowToast(string message, Color? accent = null)
    {
        StartCoroutine(ToastRoutine(message, accent ?? Color.white));
    }

    private System.Collections.IEnumerator ToastRoutine(string message, Color textColor)
    {
        int slot = _activeToasts++;

        GameObject go = new GameObject("Toast");
        go.transform.SetParent(_canvas.transform, false);

        Canvas tc = go.AddComponent<Canvas>();
        tc.overrideSorting = true;
        tc.sortingOrder = 175;
        go.AddComponent<GraphicRaycaster>();

        float yTop = 0.96f - slot * 0.08f;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.30f, yTop - 0.07f);
        r.anchorMax = new Vector2(0.98f, yTop);
        r.offsetMin = r.offsetMax = Vector2.zero;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.16f, 0f);

        GameObject textGo = new GameObject("Msg");
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(10, 3); tr.offsetMax = new Vector2(-10, -3);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = message;
        tmp.fontSize  = 13;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color     = new Color(textColor.r, textColor.g, textColor.b, 0f);

        // Fade in
        float e = 0f;
        while (e < 0.2f)
        {
            e += Time.deltaTime;
            float a = e / 0.2f;
            bg.color  = new Color(0.08f, 0.10f, 0.16f, 0.88f * a);
            tmp.color = new Color(textColor.r, textColor.g, textColor.b, a);
            yield return null;
        }

        yield return new WaitForSeconds(2.2f);

        // Fade out
        e = 0f;
        while (e < 0.3f)
        {
            e += Time.deltaTime;
            float a = 1f - e / 0.3f;
            bg.color  = new Color(0.08f, 0.10f, 0.16f, 0.88f * a);
            tmp.color = new Color(textColor.r, textColor.g, textColor.b, a);
            yield return null;
        }

        _activeToasts--;
        Destroy(go);
    }

    // ── CARD DEAL ANIMATION ──────────────────────────────────

    private System.Collections.IEnumerator DealHandAnimation()
    {
        List<CardView> views = new List<CardView>(_handCardViews);
        foreach (CardView cv in views)
            cv.transform.localScale = Vector3.zero;

        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] == null) continue;
            StartCoroutine(ScaleInCard(views[i].transform));
            yield return new WaitForSeconds(0.05f);
        }
    }

    private System.Collections.IEnumerator ScaleInCard(Transform t)
    {
        float e = 0f;
        while (e < 0.18f)
        {
            if (t == null) yield break;
            e += Time.deltaTime;
            float s = Mathf.SmoothStep(0f, 1f, e / 0.18f);
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }

    // ── OPPONENT FANS ────────────────────────────────────────

    private void BuildOpponentFans(List<Player> players, string localId, string currentPlayerId,
        string lastBetPlayerId = "")
    {
        foreach (Transform child in _opponentFansContainer.transform)
            Destroy(child.gameObject);

        var opponents = players.FindAll(p => p.Id != localId);
        if (opponents.Count == 0) return;

        float slotW = 1f / opponents.Count;

        for (int i = 0; i < opponents.Count; i++)
        {
            Player opp = opponents[i];
            bool isCurrentPlayer = opp.Id == currentPlayerId;
            bool isLastBetter   = !string.IsNullOrEmpty(lastBetPlayerId) && opp.Id == lastBetPlayerId;
            float x0 = i * slotW;

            // Slot container
            GameObject slot = new GameObject($"OppSlot_{i}");
            slot.transform.SetParent(_opponentFansContainer.transform, false);
            RectTransform slotRect = slot.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(x0, 0f);
            slotRect.anchorMax = new Vector2(x0 + slotW, 1f);
            slotRect.offsetMin = new Vector2(3, 2);
            slotRect.offsetMax = new Vector2(-3, -2);

            // Slot background — gold tint when it's this opponent's turn
            Image slotBg = slot.AddComponent<Image>();
            slotBg.color = isCurrentPlayer
                ? new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.12f)
                : new Color(0f, 0f, 0f, 0.12f);

            // Gold left border if it's their turn
            if (isCurrentPlayer)
            {
                GameObject turnBar = new GameObject("TurnBar");
                turnBar.transform.SetParent(slot.transform, false);
                RectTransform tbr = turnBar.AddComponent<RectTransform>();
                tbr.anchorMin = Vector2.zero; tbr.anchorMax = new Vector2(0f, 1f);
                tbr.offsetMin = new Vector2(0, 2); tbr.offsetMax = new Vector2(3, -2);
                turnBar.AddComponent<Image>().color = P_Gold;
            }

            // Small "BET" tag if this player placed the last bet
            if (isLastBetter && !isCurrentPlayer)
            {
                GameObject betTag = new GameObject("BetTag");
                betTag.transform.SetParent(slot.transform, false);
                RectTransform betTagRect = betTag.AddComponent<RectTransform>();
                betTagRect.anchorMin = new Vector2(0f, 0.78f); betTagRect.anchorMax = new Vector2(0.45f, 1f);
                betTagRect.offsetMin = new Vector2(2, 1); betTagRect.offsetMax = new Vector2(-2, -1);
                betTag.AddComponent<Image>().color = new Color(0.6f, 0.15f, 0.05f, 0.75f);
                GameObject betTxt = new GameObject("T");
                betTxt.transform.SetParent(betTag.transform, false);
                RectTransform bttr = betTxt.AddComponent<RectTransform>();
                bttr.anchorMin = Vector2.zero; bttr.anchorMax = Vector2.one;
                bttr.offsetMin = bttr.offsetMax = Vector2.zero;
                TextMeshProUGUI btmp = betTxt.AddComponent<TextMeshProUGUI>();
                btmp.text = "BET"; btmp.fontSize = 7; btmp.color = Color.white;
                btmp.alignment = TextAlignmentOptions.Center;
                btmp.fontStyle = FontStyles.Bold;
            }

            // Name label — top 22%
            GameObject nameGo = new GameObject("Name");
            nameGo.transform.SetParent(slot.transform, false);
            RectTransform nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.78f);
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(2, 0);
            nameRect.offsetMax = new Vector2(-2, -1);
            int oppIdx = int.TryParse(opp.Id, out int parsedIdx) ? parsedIdx : i;
            Color oppColor = GetPlayerColor(oppIdx);

            TextMeshProUGUI nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text          = opp.Name;
            nameTmp.fontSize      = isCurrentPlayer ? 12 : 10;
            nameTmp.color         = isCurrentPlayer ? P_Gold : new Color(oppColor.r, oppColor.g, oppColor.b, 0.9f);
            nameTmp.fontStyle     = isCurrentPlayer ? FontStyles.Bold : FontStyles.Normal;
            nameTmp.alignment     = TextAlignmentOptions.Center;
            nameTmp.overflowMode  = TextOverflowModes.Ellipsis;

            // Card count badge — small pill top-right corner
            GameObject badgeGo = new GameObject("CardCount");
            badgeGo.transform.SetParent(slot.transform, false);
            RectTransform badgeRect = badgeGo.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.60f, 0.62f);
            badgeRect.anchorMax = new Vector2(0.98f, 0.79f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            // Pill background — red when 1–2 cards (danger), grey when out, green otherwise
            bool lowCards = opp.CardCount > 0 && opp.CardCount <= 2;
            badgeGo.AddComponent<Image>().color = opp.CardCount == 0
                ? new Color(0.2f, 0.2f, 0.2f, 0.6f)
                : lowCards
                    ? new Color(0.45f, 0.12f, 0.05f, 0.85f)
                    : new Color(0.06f, 0.18f, 0.06f, 0.8f);
            // Count text
            GameObject bTxt = new GameObject("T");
            bTxt.transform.SetParent(badgeGo.transform, false);
            RectTransform btr = bTxt.AddComponent<RectTransform>();
            btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one;
            btr.offsetMin = btr.offsetMax = Vector2.zero;
            TextMeshProUGUI badgeTmp = bTxt.AddComponent<TextMeshProUGUI>();
            badgeTmp.text      = opp.CardCount == 0 ? "OUT" : opp.CardCount.ToString();
            badgeTmp.fontSize  = opp.CardCount == 0 ? 8 : 12;
            badgeTmp.color     = opp.CardCount == 0
                ? new Color(0.5f, 0.5f, 0.5f)
                : lowCards
                    ? new Color(1f, 0.55f, 0.30f, 1f)
                    : new Color(0.55f, 1f, 0.55f, 1f);
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;

            // Mini card fan — bottom 62%
            BuildMiniFan(slot, opp.CardCount);
        }
    }

    private void BuildMiniFan(GameObject parent, int count)
    {
        if (count == 0)
        {
            // Show a dimmed "no cards" indicator
            GameObject noneGo = new GameObject("NoCards");
            noneGo.transform.SetParent(parent.transform, false);
            RectTransform nr = noneGo.AddComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.1f, 0.05f);
            nr.anchorMax = new Vector2(0.9f, 0.62f);
            nr.offsetMin = nr.offsetMax = Vector2.zero;
            TextMeshProUGUI nTmp = noneGo.AddComponent<TextMeshProUGUI>();
            nTmp.text      = "✓";
            nTmp.fontSize  = 22;
            nTmp.color     = new Color(0.35f, 0.80f, 0.45f, 0.55f);
            nTmp.alignment = TextAlignmentOptions.Center;
            nTmp.raycastTarget = false;
            return;
        }

        int displayCount = Mathf.Min(count, 10);
        float cardW   = 26f;
        float cardH   = 38f;
        // Span-based: total spread capped at ~70% of slot width via anchor; use overlap
        float maxSpread = 52f; // max total spread px
        float overlap = displayCount <= 1 ? 0f
            : Mathf.Clamp(maxSpread / (displayCount - 1), 6f, 22f);

        // Cards container in lower 68% of slot
        GameObject fanContainer = new GameObject("Fan");
        fanContainer.transform.SetParent(parent.transform, false);
        RectTransform fcRect = fanContainer.AddComponent<RectTransform>();
        fcRect.anchorMin = new Vector2(0f, 0f);
        fcRect.anchorMax = new Vector2(1f, 0.65f);
        fcRect.offsetMin = Vector2.zero;
        fcRect.offsetMax = Vector2.zero;

        float totalSpread = displayCount <= 1 ? 0f : (displayCount - 1) * overlap;

        for (int i = 0; i < displayCount; i++)
        {
            float t      = displayCount > 1 ? (float)i / (displayCount - 1) : 0.5f;
            float maxRot = Mathf.Min(20f, displayCount * 1.6f);
            float rot    = Mathf.Lerp(-maxRot, maxRot, t);
            float xOff   = -totalSpread * 0.5f + i * overlap;
            // Arc: centre cards slightly elevated
            float nt = (t - 0.5f) * 2f;
            float arcH = Mathf.Min(6f, displayCount * 0.5f);
            float yOff = arcH * (1f - nt * nt) + 3f;

            GameObject cardGo = new GameObject($"MiniCard_{i}");
            cardGo.transform.SetParent(fanContainer.transform, false);
            cardGo.transform.SetSiblingIndex(i); // back-to-front order

            RectTransform r = cardGo.AddComponent<RectTransform>();
            r.sizeDelta        = new Vector2(cardW, cardH);
            r.anchorMin        = new Vector2(0.5f, 0f);
            r.anchorMax        = new Vector2(0.5f, 0f);
            r.pivot            = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(xOff, yOff);
            r.localRotation    = Quaternion.Euler(0, 0, -rot);

            cardGo.AddComponent<Image>();
            CardView cv = cardGo.AddComponent<CardView>();
            cv.Setup(new Card(Suit.Spades, Rank.Ace), i, faceDown: true);
        }

        if (count > displayCount)
        {
            GameObject moreGo = new GameObject("More");
            moreGo.transform.SetParent(parent.transform, false);
            RectTransform mr = moreGo.AddComponent<RectTransform>();
            mr.anchorMin = new Vector2(0.55f, 0.58f);
            mr.anchorMax = new Vector2(1f,   0.76f);
            mr.offsetMin = mr.offsetMax = Vector2.zero;
            TextMeshProUGUI mTmp = moreGo.AddComponent<TextMeshProUGUI>();
            mTmp.text      = $"+{count - displayCount}";
            mTmp.fontSize  = 10;
            mTmp.color     = new Color(0.85f, 0.75f, 0.3f, 1f);
            mTmp.alignment = TextAlignmentOptions.BottomRight;
            mTmp.fontStyle = FontStyles.Bold;
        }
    }

    // ── PILE VISUAL ──────────────────────────────────────────

    private void BuildPileVisual(int pileCount)
    {
        if (pileCount == _lastBuiltPileCount) return;
        _lastBuiltPileCount = pileCount;
        foreach (Transform child in _pileVisualContainer.transform)
            Destroy(child.gameObject);

        if (pileCount == 0)
        {
            // Dashed ghost placeholder
            GameObject empty = new GameObject("EmptyPile");
            empty.transform.SetParent(_pileVisualContainer.transform, false);
            RectTransform er = empty.AddComponent<RectTransform>();
            er.anchorMin = new Vector2(0.15f, 0.08f); er.anchorMax = new Vector2(0.85f, 0.92f);
            er.offsetMin = er.offsetMax = Vector2.zero;
            Image eImg = empty.AddComponent<Image>();
            eImg.color = new Color(0f, 0.12f, 0.04f, 0.55f);

            // Inner dashed-border effect using two nested thin panels
            for (int side = 0; side < 4; side++)
            {
                GameObject line = new GameObject($"Border_{side}");
                line.transform.SetParent(empty.transform, false);
                RectTransform lr = line.AddComponent<RectTransform>();
                Vector2 aMin, aMax, offMin, offMax;
                switch (side)
                {
                    case 0: aMin=new Vector2(0,1); aMax=Vector2.one; offMin=new Vector2(4,-2); offMax=new Vector2(-4,0); break;
                    case 1: aMin=Vector2.zero; aMax=new Vector2(1,0); offMin=new Vector2(4,0); offMax=new Vector2(-4,2); break;
                    case 2: aMin=Vector2.zero; aMax=new Vector2(0,1); offMin=new Vector2(0,4); offMax=new Vector2(2,-4); break;
                    default: aMin=new Vector2(1,0); aMax=Vector2.one; offMin=new Vector2(-2,4); offMax=new Vector2(0,-4); break;
                }
                lr.anchorMin=aMin; lr.anchorMax=aMax; lr.offsetMin=offMin; lr.offsetMax=offMax;
                line.AddComponent<Image>().color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.22f);
            }
            return;
        }

        // Stack of face-down cards with realistic spread + tilt
        int show = Mathf.Min(pileCount, 8);
        float[] tilts = { 0f, -5f, 3.5f, -2f, 6f, -3.5f, 2f, -6f };

        for (int i = 0; i < show; i++)
        {
            // Older cards offset slightly (buried under new ones)
            float xSpread = tilts[i % tilts.Length] * 0.6f;
            float ySpread = i * 1.2f;

            GameObject cardGo = new GameObject($"PileCard_{i}");
            cardGo.transform.SetParent(_pileVisualContainer.transform, false);

            RectTransform r = cardGo.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0.08f, 0.06f);
            r.anchorMax = new Vector2(0.92f, 0.94f);
            r.offsetMin = new Vector2(xSpread, -ySpread);
            r.offsetMax = new Vector2(xSpread, -ySpread);
            r.localRotation = Quaternion.Euler(0f, 0f, tilts[i % tilts.Length]);

            cardGo.AddComponent<Image>();
            CardView cv = cardGo.AddComponent<CardView>();
            cv.Setup(new Card(Suit.Spades, Rank.Ace), i, faceDown: true);
        }
    }

    // ── ACTION LOG ───────────────────────────────────────────

    private void AddActionLog(string message)
    {
        _actionLog.Enqueue(message);
        while (_actionLog.Count > 4) _actionLog.Dequeue();
        _actionLogText.text = string.Join("\n", _actionLog);

        _fullHistory.Add(message);
        if (_historyText != null)
            UpdateHistoryText();
    }

    private void UpdateHistoryText()
    {
        if (_fullHistory.Count == 0)
        {
            _historyText.text = "<color=#445566>No rounds played yet...</color>";
            return;
        }
        // Show last 30 entries in reverse (newest at top)
        int start = Mathf.Max(0, _fullHistory.Count - 30);
        var slice = _fullHistory.GetRange(start, _fullHistory.Count - start);
        slice.Reverse();
        _historyText.text = string.Join("\n", slice);
    }

    private void ToggleHistoryOverlay()
    {
        if (_historyOverlay == null)
            BuildHistoryOverlay();
        bool show = !_historyOverlay.activeSelf;
        _historyOverlay.SetActive(show);
        if (show) UpdateHistoryText();
    }

    private void CopyGameLogToClipboard()
    {
        if (_fullHistory.Count == 0)
        {
            ShowToast("Nothing to copy yet", P_Muted);
            return;
        }
        var sb = new StringBuilder();
        foreach (string entry in _fullHistory)
            sb.AppendLine(StripRichText(entry));
        GUIUtility.systemCopyBuffer = sb.ToString().Trim();
        ShowToast("Game log copied!", P_Gold);
    }

    // Strip TMPro rich-text tags, leaving plain text suitable for clipboard export.
    private static string StripRichText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var result = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) result.Append(c);
        }
        return result.ToString();
    }

    private void BuildHistoryOverlay()
    {
        _historyOverlay = new GameObject("HistoryOverlay");
        _historyOverlay.transform.SetParent(_canvas.transform, false);
        RectTransform hor = _historyOverlay.AddComponent<RectTransform>();
        hor.anchorMin = new Vector2(0f, 0.22f);
        hor.anchorMax = new Vector2(1f, 0.78f);
        hor.offsetMin = hor.offsetMax = Vector2.zero;

        Canvas hc = _historyOverlay.AddComponent<Canvas>();
        hc.overrideSorting = true;
        hc.sortingOrder = 115;
        _historyOverlay.AddComponent<GraphicRaycaster>();
        _historyOverlay.AddComponent<Image>().color = new Color(0.02f, 0.05f, 0.12f, 0.96f);

        Button dismissBtn = _historyOverlay.AddComponent<Button>();
        dismissBtn.transition = Selectable.Transition.None;
        dismissBtn.onClick.AddListener(() => _historyOverlay.SetActive(false));

        // Title bar
        TextMeshProUGUI title = CreateText(_historyOverlay, "GAME HISTORY", 13,
            new Vector2(0.04f, 0.88f), new Vector2(0.88f, 1f),
            TextAlignmentOptions.MidlineLeft);
        title.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.85f);
        title.fontStyle = FontStyles.Bold;
        title.raycastTarget = false;

        TextMeshProUGUI closeHint = CreateText(_historyOverlay, "✕ tap to close", 9,
            new Vector2(0.60f, 0.88f), new Vector2(0.98f, 1f),
            TextAlignmentOptions.MidlineRight);
        closeHint.color = new Color(0.45f, 0.55f, 0.60f, 0.70f);
        closeHint.raycastTarget = false;

        // "Copy" button — stops propagation so the dismiss button underneath doesn't fire
        GameObject copyGo = new GameObject("CopyBtn");
        copyGo.transform.SetParent(_historyOverlay.transform, false);
        RectTransform cbr = copyGo.AddComponent<RectTransform>();
        cbr.anchorMin = new Vector2(0.04f, 0.88f); cbr.anchorMax = new Vector2(0.32f, 1f);
        cbr.offsetMin = new Vector2(0, 2); cbr.offsetMax = new Vector2(0, -2);
        copyGo.AddComponent<Image>().color = new Color(0.10f, 0.18f, 0.28f, 0.85f);
        Button copyBtn = copyGo.AddComponent<Button>();
        copyBtn.transition = Selectable.Transition.None;
        copyBtn.onClick.AddListener(CopyGameLogToClipboard);
        // The overlay's dismiss Button also fires on this tap — that's fine; copy then close.
        GameObject copyLbl = new GameObject("Lbl");
        copyLbl.transform.SetParent(copyGo.transform, false);
        RectTransform clr = copyLbl.AddComponent<RectTransform>();
        clr.anchorMin = Vector2.zero; clr.anchorMax = Vector2.one;
        clr.offsetMin = clr.offsetMax = Vector2.zero;
        TextMeshProUGUI copyTmp = copyLbl.AddComponent<TextMeshProUGUI>();
        copyTmp.text = "📋 Copy";
        copyTmp.fontSize = 9;
        copyTmp.alignment = TextAlignmentOptions.Center;
        copyTmp.color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.75f);
        copyTmp.raycastTarget = false;

        // Thin gold divider
        GameObject divider = new GameObject("Div");
        divider.transform.SetParent(_historyOverlay.transform, false);
        RectTransform dr = divider.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.02f, 0.87f); dr.anchorMax = new Vector2(0.98f, 0.87f);
        dr.offsetMin = Vector2.zero; dr.offsetMax = new Vector2(0, 1);
        divider.AddComponent<Image>().color = new Color(P_Gold.r, P_Gold.g, P_Gold.b, 0.25f);

        _historyText = CreateText(_historyOverlay, "", 10,
            new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.86f),
            TextAlignmentOptions.TopLeft);
        _historyText.color = new Color(0.72f, 0.88f, 0.72f, 1f);
        _historyText.overflowMode = TextOverflowModes.Truncate;
        _historyText.raycastTarget = false;
    }

    private void OnCardRevealedHandler(Card card, string revealerName, bool wasCorrect,
        string action, string declaredRank, int _)
    {
        var state = _gameManager?.GetState();
        string liarName = state?.LastBetPlayer?.Name ?? "";
        TrackAndDisplayCardReveal(card, revealerName, wasCorrect, action, declaredRank, liarName);
    }

    // Called from both the online event handler and the offline Believe/Bluff paths.
    // liarName should be the LastBetPlayer name captured BEFORE state is mutated.
    private void TrackAndDisplayCardReveal(Card card, string revealerName, bool wasCorrect,
        string action, string declaredRank, string liarName)
    {
        string sym       = CardView.SuitSymbol(card.Suit);
        string rankShort = CardView.RankShort(card.Rank);
        string outcome   = wasCorrect ? "<color=#55ff55>✓</color>" : "<color=#ff5555>✗</color>";

        // Colour-code the revealer name
        var state = _gameManager?.GetState();
        int revealerIdx = state?.Players.FindIndex(p => p.Name == revealerName) ?? -1;
        string nameTag = revealerIdx >= 0
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(GetPlayerColor(revealerIdx))}>{revealerName}</color>"
            : revealerName;
        string verb = action == "Bluff" ? "called bluff" : "believed";
        AddActionLog($"{nameTag} {verb} → {rankShort}{sym}  {outcome}");

        // Accumulate per-player stats
        if (!_stats.ContainsKey(revealerName)) _stats[revealerName] = default;
        var s = _stats[revealerName];
        if (!wasCorrect)
        {
            // revealer took the pile (wrong belief or wrong bluff call)
            s.PilesTaken++;
            s.BadChallenges++;
        }
        else if (action == "Bluff")
        {
            // revealer correctly caught a bluff; liar takes the pile
            s.BluffsCaught++;
            // Credit the liar's PilesTaken
            if (!string.IsNullOrEmpty(liarName) && liarName != revealerName)
            {
                if (!_stats.ContainsKey(liarName)) _stats[liarName] = default;
                var ls = _stats[liarName];
                ls.PilesTaken++;
                _stats[liarName] = ls;
            }
        }
        _stats[revealerName] = s;

        // Toast + pile animation with contextual message
        bool pileTaken = (action == "Bluff") || (action == "Believe" && !wasCorrect);
        if (pileTaken)
        {
            string taker;
            Color toastColor;
            if (action == "Bluff" && wasCorrect)
            {
                // Liar caught — the bettor (liar) takes the pile
                taker = $"{(!string.IsNullOrEmpty(liarName) ? liarName : revealerName)} (liar)";
                toastColor = new Color(1f, 0.4f, 0.4f);
            }
            else
            {
                // Doubter was wrong, or believer was wrong — revealer takes pile
                taker = revealerName;
                toastColor = new Color(1f, 0.75f, 0.2f);
            }
            ShowToast($"{taker} takes the pile! ({rankShort}{sym} revealed)", toastColor);
            TriggerPileShake();
            if (_displayedPileCount > 1)
                ShowFloatingPileLabel(_displayedPileCount);
        }
        else
        {
            ShowToast($"Correct! Pile → discard  {rankShort}{sym}", new Color(0.4f, 1f, 0.4f));
        }

        // After each pile resolution, add a round separator to the log
        _roundNumber++;
        AddActionLog($"<color=#445544>── round {_roundNumber} ──────────────────</color>");
    }

    // ── OFFLINE BOT AUTO-PLAY ────────────────────────────────

    private void MaybeScheduleBotTurn(GameState state)
    {
        if (!(_gameManager is GameManager)) return;
        if (state == null || state.Phase != GamePhase.Playing) return;
        if (state.CurrentPlayer?.Id == _localPlayerId) return;
        if (_botTurnRoutine != null) StopCoroutine(_botTurnRoutine);
        _botTurnRoutine = StartCoroutine(BotTurnRoutine());
    }

    private System.Collections.IEnumerator BotTurnRoutine()
    {
        // Wait for the card-reveal animation to finish before the bot acts
        float waited = 0f;
        while (GuessingScreenUI.Instance != null && GuessingScreenUI.Instance.IsVisible && waited < 8f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (!(_gameManager is GameManager gm)) yield break;

        // Show "thinking" indicator in status bar while the bot pauses
        GameState preState = gm.GetState();
        if (preState?.CurrentPlayer != null && preState.CurrentPlayer.Id != _localPlayerId)
        {
            string tHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(preState.CurrentPlayerIndex));
            if (_statusText != null)
            {
                _statusText.text      = $"<color=#{tHex}>{preState.CurrentPlayer.Name}</color> is thinking...";
                _statusText.fontStyle = FontStyles.Italic;
            }
        }

        // Slightly randomised delay — feels less robotic
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.50f, 1.05f));
        _botTurnRoutine = null;

        GameState state = gm.GetState();
        if (state == null || state.Phase != GamePhase.Playing) yield break;
        if (state.CurrentPlayer?.Id == _localPlayerId) yield break;

        var (action, revealedCard, correct, playerName, liarName, rankStr) = gm.TryBotAction();

        if (action == "Believe" || action == "Bluff")
        {
            AudioManager.PlayCardRevealed(correct, action);
            TrackAndDisplayCardReveal(revealedCard, playerName, correct, action, rankStr, liarName);
        }

        if (action != "")
            RefreshUI(gm.GetState(), _localPlayerId);
    }

    private void ShowFloatingPileLabel(int count)
    {
        StartCoroutine(FloatingLabelRoutine($"+{count}", new Color(1f, 0.75f, 0.15f)));
    }

    private System.Collections.IEnumerator FloatingLabelRoutine(string text, Color col)
    {
        GameObject go = new GameObject("FloatLabel");
        go.transform.SetParent(_canvas.transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 172;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.40f);
        rt.anchorMax = new Vector2(0.55f, 0.52f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = col;

        float elapsed = 0f;
        const float duration = 1.5f;
        const float rise = 0.14f;
        Vector2 baseMin = rt.anchorMin;
        Vector2 baseMax = rt.anchorMax;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t    = elapsed / duration;
            float yOff = rise * Mathf.SmoothStep(0f, 1f, t);
            rt.anchorMin = baseMin + new Vector2(0f, yOff);
            rt.anchorMax = baseMax + new Vector2(0f, yOff);
            tmp.color = new Color(col.r, col.g, col.b, 1f - t * t);
            yield return null;
        }

        Destroy(go);
    }

    private System.Collections.IEnumerator FlashTextBackToWhite(TextMeshProUGUI tmp)
    {
        yield return new WaitForSeconds(0.9f);
        float e = 0f;
        Color red   = new Color(1f, 0.35f, 0.35f);
        Color white = Color.white;
        while (e < 0.4f)
        {
            e += Time.deltaTime;
            tmp.color = Color.Lerp(red, white, e / 0.4f);
            yield return null;
        }
        tmp.color = white;
    }

    // ── CARD HAND DISPLAY ────────────────────────────────────

    // Card size constants for hand display — no cap; all cards always visible
    private const float HandCardW = 62f;
    private const float HandCardH = 92f;

    private Vector2 ComputeCardAnchoredPosition(int index, int displayCount)
    {
        // Span-based: compresses as low as 10px min so all cards stay accessible.
        // Max span 260px keeps cards on screen for typical phones.
        float spacing = displayCount <= 1 ? 0f
            : Mathf.Clamp(260f / (displayCount - 1), 10f, 68f);
        float totalSpan = displayCount <= 1 ? 0f : (displayCount - 1) * spacing;
        float startX = -totalSpan * 0.5f;
        float t  = displayCount > 1 ? (float)index / (displayCount - 1) : 0.5f;
        float nt = (t - 0.5f) * 2f; // -1..+1
        // Inverted parabola arc: centre cards highest, edges lowest (always >= 8)
        float arcH = Mathf.Min(24f, displayCount * 1.8f);
        float y = arcH * (1f - nt * nt) + 8f;
        return new Vector2(startX + index * spacing, y);
    }

    private void BuildHandCards(List<Card> hand)
    {
        foreach (Transform child in _handContainer.transform)
            Destroy(child.gameObject);
        _handCardViews.Clear();
        _selectedCardIndices.Clear();

        if (hand.Count == 0) return;

        int display = hand.Count;
        float maxRot = Mathf.Min(24f, display * 1.5f);

        for (int i = 0; i < display; i++)
        {
            int index = i;

            GameObject cardGo = new GameObject($"Card_{i}");
            cardGo.transform.SetParent(_handContainer.transform, false);

            RectTransform rect = cardGo.AddComponent<RectTransform>();
            rect.sizeDelta        = new Vector2(HandCardW, HandCardH);
            rect.anchorMin        = new Vector2(0.5f, 0f);
            rect.anchorMax        = new Vector2(0.5f, 0f);
            rect.pivot            = new Vector2(0.5f, 0f);
            rect.anchoredPosition = ComputeCardAnchoredPosition(i, display);

            float t = display > 1 ? (float)i / (display - 1) : 0.5f;
            // Edge cards tilt outward, centre cards are upright
            float tilt = Mathf.Lerp(-maxRot, maxRot, t);
            rect.localRotation = Quaternion.Euler(0f, 0f, -tilt);

            cardGo.AddComponent<Image>().color = Color.white;

            CardView cardView = cardGo.AddComponent<CardView>();
            cardView.Setup(hand[i], index);
            cardView.OnCardClicked += OnHandCardClicked;

            // Green glow on cards that match the declared rank (helps player spot re-bet candidates)
            GameState hState = _gameManager?.GetState();
            if (hState != null && hState.HasActiveBet && hand[i].Rank == hState.LastDeclaredRank)
            {
                GameObject glowGo = new GameObject("MatchGlow");
                glowGo.transform.SetParent(cardGo.transform, false);
                RectTransform glowR = glowGo.AddComponent<RectTransform>();
                glowR.anchorMin = Vector2.zero; glowR.anchorMax = Vector2.one;
                glowR.offsetMin = new Vector2(-3, -3); glowR.offsetMax = new Vector2(3, 3);
                glowGo.AddComponent<Image>().color = new Color(0.2f, 1f, 0.3f, 0.18f);
                glowGo.transform.SetAsLastSibling(); // on top of card content (low alpha overlay)
            }

            _handCardViews.Add(cardView);
        }

        if (_pendingDealAnimation)
        {
            _pendingDealAnimation = false;
            StartCoroutine(DealHandAnimation());
        }
    }

    private void OnHandCardClicked(int index)
    {
        // Audio is already played by CardView's own onClick listener — no double-fire here
        if (_gameManager == null) return;
        GameState state = _gameManager.GetState();
        if (state == null || state.Players.Count == 0) return;
        Player localPlayer = state.Players.Find(p => p.Id == _localPlayerId);
        if (localPlayer == null) return;
        if (index >= localPlayer.Hand.Count) return;
        if (state.CurrentPlayer.Id != _localPlayerId) return;

        // Check if already selected - deselect
        if (_selectedCardIndices.Contains(index))
        {
            _selectedCardIndices.Remove(index);
            if (index < _handCardViews.Count)
            {
                _handCardViews[index].SetSelected(false);
                RectTransform rect = _handCardViews[index].GetComponent<RectTransform>();
                rect.anchoredPosition = ComputeCardAnchoredPosition(index,
                    localPlayer.Hand.Count);
                _handCardViews[index].transform.SetSiblingIndex(index); // restore natural z-order
            }
        }
        else
        {
            if (_selectedCardIndices.Count >= 4)
            {
                ShowToast("Max 4 cards per bet", new Color(1f, 0.65f, 0.2f));
                return;
            }
            _selectedCardIndices.Add(index);
            if (index < _handCardViews.Count)
            {
                _handCardViews[index].SetSelected(true);
                RectTransform rect = _handCardViews[index].GetComponent<RectTransform>();
                rect.anchoredPosition += new Vector2(0, 28f);
                _handCardViews[index].transform.SetAsLastSibling(); // bring selected card on top
            }
        }

        UpdateSelectionInfo();
    }

    private void UpdateSelectionInfo()
    {
        int count = _selectedCardIndices.Count;
        bool hasBet = _gameManager?.GetState()?.HasActiveBet ?? false;
        _rebetButton.gameObject.SetActive(true);
        if (count == 0)
        {
            _selectionInfoText.text = hasBet
                ? "Select cards to Re-bet — or Believe / Bluff"
                : "Select 1–4 cards to start a new bet";
        }
        else
        {
            _selectionInfoText.text = hasBet
                ? $"{count} card(s) selected — Re-bet at current rank"
                : $"{count} card(s) selected — tap Bet to pick a rank";
        }
    }

    // ── BUTTON HANDLERS ──────────────────────────────────────

    private void OnBelieveClicked()
    {
        if (_gameManager == null) return;
        GameState state = _gameManager.GetState();
        NetworkedGameManager.Instance?.AnnounceGuessing("Believe");
        GuessingScreenUI.Instance?.ShowForGuesser(
            state.LastBetCards.Count, "Believe", state.LastDeclaredRank.ToString(),
            (cardIndex) =>
            {
                if (_gameManager is GameManager)
                {
                    // Capture pre-resolve data before state mutates
                    string revealerName   = state.CurrentPlayer?.Name ?? _localPlayerId;
                    string liarName       = state.LastBetPlayer?.Name ?? "";
                    string declaredRankStr = state.LastDeclaredRank.ToString();
                    Card revealedCard = state.LastBetCards.Count > cardIndex ? state.LastBetCards[cardIndex] : new Card(Suit.Spades, Rank.Ace);
                    bool correct = GameRules.CheckCard(revealedCard, state.LastDeclaredRank);
                    _gameManager.Believe(cardIndex);
                    AudioManager.PlayCardRevealed(correct, "Believe");
                    TrackAndDisplayCardReveal(revealedCard, revealerName, correct, "Believe", declaredRankStr, liarName);
                    if (GuessingScreenUI.Instance != null)
                        GuessingScreenUI.Instance.ShowOfflineResult(cardIndex, revealedCard, correct, () =>
                            RefreshUI(_gameManager.GetState(), _localPlayerId));
                    else
                        RefreshUI(_gameManager.GetState(), _localPlayerId);
                }
                else
                {
                    _gameManager.Believe(cardIndex);
                }
            });
    }

    private void OnBluffClicked()
    {
        if (_gameManager == null) return;
        GameState state = _gameManager.GetState();
        NetworkedGameManager.Instance?.AnnounceGuessing("Bluff");
        GuessingScreenUI.Instance?.ShowForGuesser(
            state.LastBetCards.Count, "Bluff", state.LastDeclaredRank.ToString(),
            (cardIndex) =>
            {
                if (_gameManager is GameManager)
                {
                    // Capture pre-resolve data before state mutates
                    string revealerName    = state.CurrentPlayer?.Name ?? _localPlayerId;
                    string liarName        = state.LastBetPlayer?.Name ?? "";
                    string declaredRankStr = state.LastDeclaredRank.ToString();
                    Card revealedCard = state.LastBetCards.Count > cardIndex ? state.LastBetCards[cardIndex] : new Card(Suit.Spades, Rank.Ace);
                    bool caughtLying = !GameRules.CheckCard(revealedCard, state.LastDeclaredRank);
                    _gameManager.Bluff(cardIndex);
                    AudioManager.PlayCardRevealed(caughtLying, "Bluff");
                    TrackAndDisplayCardReveal(revealedCard, revealerName, caughtLying, "Bluff", declaredRankStr, liarName);
                    if (GuessingScreenUI.Instance != null)
                        GuessingScreenUI.Instance.ShowOfflineResult(cardIndex, revealedCard, caughtLying, () =>
                            RefreshUI(_gameManager.GetState(), _localPlayerId));
                    else
                        RefreshUI(_gameManager.GetState(), _localPlayerId);
                }
                else
                {
                    _gameManager.Bluff(cardIndex);
                }
            });
    }

    private void OnBetClicked()
    {
        if (_gameManager == null) return;
        if (_selectedCardIndices.Count == 0)
        {
            _selectionInfoText.text  = "Select at least 1 card first!";
            _selectionInfoText.color = new Color(1f, 0.35f, 0.35f);
            StartCoroutine(FlashTextBackToWhite(_selectionInfoText));
            return;
        }

        GameState state = _gameManager.GetState();
        int[] cardIndices = _selectedCardIndices.ToArray();

        if (state.HasActiveBet)
        {
            _gameManager.PlaceBet(cardIndices, (int)state.LastDeclaredRank);
            if (_gameManager is GameManager) RefreshUI(_gameManager.GetState(), _localPlayerId);
            _selectedCardIndices.Clear();
            return;
        }

        bool shortDeck = NetworkedGameManager.Instance != null && NetworkedGameManager.Instance.IsShortDeck;
        RankPickerUI.Instance.Show((rank) =>
        {
            _gameManager.PlaceBet(cardIndices, (int)rank);
            if (_gameManager is GameManager) RefreshUI(_gameManager.GetState(), _localPlayerId);
            _selectedCardIndices.Clear();
        }, shortDeck, cardIndices.Length);
    }

    // ── REFRESH ──────────────────────────────────────────────

    public void RefreshUI(GameState state, string localPlayerId)
    {
        _localPlayerId = localPlayerId;

        // Offline game-over: NetworkedGameManager never fires OnGameOver for offline play
        if (_gameManager is GameManager && state.Phase == GamePhase.GameOver && state.Loser != null)
        {
            if (!_gameOverOverlay.activeSelf)
                ShowGameOver(state.Loser.Name);
            return;
        }

        bool isSpectator = localPlayerId == "-2";
        Player localPlayer = isSpectator ? null : state.Players.Find(p => p.Id == localPlayerId);
        if (!isSpectator && localPlayer == null) return;

        // Opponent card fans
        string lastBetId = state.HasActiveBet ? state.LastBetPlayer?.Id ?? "" : "";
        BuildOpponentFans(state.Players, localPlayerId, state.CurrentPlayer?.Id ?? "", lastBetId);

        // Room code + spectator count (refresh each frame so spectator count stays current)
        if (_roomCodeText != null)
        {
            string code = NetworkedGameManager.Instance?.Runner?.SessionInfo.Name ?? "";
            if (!string.IsNullOrEmpty(code))
            {
                int specs = NetworkedGameManager.Instance?.SpectatorCount ?? 0;
                string specStr = specs > 0 ? $"\n<size=9><color=#8899aa>👁 {specs}</color></size>" : "";
                var ngmInfo = NetworkedGameManager.Instance;
                string deckStr = ngmInfo != null ? (ngmInfo.IsShortDeck ? "36♠" : "52♠") : "";
                string timerStr = ngmInfo != null ? $"{ngmInfo.TurnTimeout:0}s" : "";
                string settingsStr = (deckStr.Length > 0 || timerStr.Length > 0)
                    ? $"\n<size=8><color=#556688>{deckStr}  {timerStr}</color></size>" : "";
                _roomCodeText.text = $"Room\n{code}{specStr}{settingsStr}";
            }
        }

        // Guard: if no current player (rare edge case mid-transition) skip refresh
        if (state.CurrentPlayer == null) return;

        // Status + turn pulse
        bool isMyTurn = state.CurrentPlayer.Id == localPlayerId;
        int curPlayerIdx = state.CurrentPlayerIndex;
        Color curPlayerColor = GetPlayerColor(curPlayerIdx);
        if (isMyTurn)
        {
            int myCards = localPlayer?.Hand.Count ?? 0;
            string cardHint = myCards > 0 ? $"  <size=13>({myCards})</size>" : "";
            _statusText.text      = $"YOUR TURN{cardHint}";
            _statusText.color     = P_Dark;
            _statusText.fontStyle = FontStyles.Bold;
        }
        else
        {
            string curHex = ColorUtility.ToHtmlStringRGB(curPlayerColor);
            int curCards = state.CurrentPlayer.CardCount;
            string cardHint = curCards > 0 ? $"  <size=11>({curCards})</size>" : "";
            _statusText.text      = $"<color=#{curHex}>{state.CurrentPlayer.Name}</color>'s turn{cardHint}";
            _statusText.color     = Color.white;
            _statusText.fontStyle = FontStyles.Normal;
        }
        if (_statusBg != null)
            _statusBg.color = isMyTurn ? P_Gold : P_Pane;
        UpdateTurnPulse(isMyTurn);

        // Bet info + action log for new bets
        if (state.HasActiveBet)
        {
            _currentBetText.color = Color.white;
            Color betterColor = GetPlayerColor(state.LastBetPlayerIndex);
            string betterHex = ColorUtility.ToHtmlStringRGB(betterColor);
            _currentBetText.text =
                $"<color=#{betterHex}><b>{state.LastBetPlayer.Name}</b></color>" +
                $"  declared  " +
                $"<color=#D4AF37><b>{state.LastBetCards.Count}×</b></color>" +
                $"  <b>{state.LastDeclaredRank}</b>";
            if (_betRankBig != null)
                _betRankBig.text = CardView.RankShort(state.LastDeclaredRank);

            if (state.LastBetPlayerIndex != _lastBetPlayerIndex ||
                state.LastBetCards.Count != _lastBetCount)
            {
                _lastBetPlayerIndex = state.LastBetPlayerIndex;
                _lastBetCount       = state.LastBetCards.Count;
                AddActionLog($"<color=#{betterHex}>{state.LastBetPlayer.Name}</color> bet " +
                    $"{state.LastBetCards.Count}× {state.LastDeclaredRank}");
                // Offline: GameManager.TryPlaceBet already calls AudioManager.PlayBetPlaced()
                if (!(_gameManager is GameManager))
                    AudioManager.PlayBetPlaced();
            }
        }
        else
        {
            _currentBetText.color = P_Muted;
            _currentBetText.text  = "No active bet  —  start a new round";
            if (_betRankBig != null) _betRankBig.text = "";
            _lastBetPlayerIndex  = -1;
            _lastBetCount        = -1;
        }

        // Bet button label: "Bet" when opening a round, "Re-bet" when continuing
        if (_rebetButtonLabel != null)
            _rebetButtonLabel.text = state.HasActiveBet ? "Re-bet" : "Bet";

        // Pile visual + discard (count tweened smoothly)
        BuildPileVisual(state.Pile.Count);
        TweenPileCount(state.Pile.Count);
        TweenDiscardCount(state.Discard.Count);

        // Hand cards and button states — spectators get emoji reaction buttons
        if (isSpectator)
        {
            BuildHandCards(new List<Card>());
            _selectedCardIndices.Clear();
            _selectionInfoText.text  = "👁  Spectating";
            _selectionInfoText.color = new Color(P_Muted.r, P_Muted.g, P_Muted.b, 0.6f);
            if (_localPlayerInfoText != null) _localPlayerInfoText.text = "";
            BuildSpectatorReactionButtons();
            return;
        }

        BuildHandCards(localPlayer.Hand);
        UpdateSelectionInfo();

        // Local player info strip
        if (_localPlayerInfoText != null)
        {
            int myCards = localPlayer.Hand.Count;
            Color myColor = GetPlayerColor(int.TryParse(localPlayerId, out int lIdx) ? lIdx : 0);
            string myHex = ColorUtility.ToHtmlStringRGB(myColor);
            string cardStr = myCards == 0 ? "no cards"
                : myCards <= 2        ? $"<color=#ff8844>{myCards} cards</color>"
                : $"{myCards} cards";
            _localPlayerInfoText.text = $"<color=#{myHex}>{localPlayer.Name}</color>  ·  {cardStr}";
        }

        // Notify once when local player empties their hand
        if (localPlayer.Hand.Count == 0 && !_warnedHandEmpty)
        {
            _warnedHandEmpty = true;
            ShowToast("No more cards! Believe or Bluff only.", new Color(0.4f, 0.85f, 1f));
        }
        else if (localPlayer.Hand.Count > 0)
        {
            _warnedHandEmpty = false;
        }

        // Button states
        bool hasBet = state.LastBetCards.Count > 0;
        bool canChallenge = hasBet && state.LastBetPlayer != null && state.LastBetPlayer.Id != localPlayerId;
        bool hasCards = localPlayer.Hand.Count > 0;

        if (!isMyTurn)
        {
            _believeButton.interactable = false;
            _bluffButton.interactable   = false;
            _rebetButton.interactable   = false;
            _selectionInfoText.text     = $"Waiting for {state.CurrentPlayer.Name}...";
        }
        else if (!hasCards && hasBet)
        {
            // No cards but must challenge
            _believeButton.interactable = true;
            _bluffButton.interactable   = true;
            _rebetButton.interactable   = false;
            _selectionInfoText.text     = "No cards left — Believe or Bluff!";
        }
        else if (!hasBet)
        {
            // No active bet — must open a new round
            _believeButton.interactable = false;
            _bluffButton.interactable   = false;
            _rebetButton.interactable   = true;
            _selectionInfoText.text = _selectedCardIndices.Count > 0
                ? $"{_selectedCardIndices.Count} card(s) selected — tap Bet to declare a rank"
                : "Select 1–4 cards, then tap Bet";
        }
        else
        {
            // Has cards and active bet — all options available
            _believeButton.interactable = canChallenge;
            _bluffButton.interactable   = canChallenge;
            _rebetButton.interactable   = true;
            int sel = _selectedCardIndices.Count;
            int remaining = hasCards ? localPlayer.Hand.Count - sel : 0;
            if (sel > 0)
            {
                string remHint = remaining > 0
                    ? $"  <size=10><color=#8899aa>({remaining} left)</color></size>"
                    : "  <size=10><color=#ffaa55>(all in)</color></size>";
                _selectionInfoText.text = canChallenge
                    ? $"{sel} card(s) selected — Re-bet{remHint}  ·  or Believe / Bluff"
                    : $"{sel} card(s) selected — Re-bet{remHint}";
            }
            else
            {
                _selectionInfoText.text = canChallenge
                    ? "Believe · Bluff · or select cards to Re-bet"
                    : "Your bet — select cards to Re-bet";
            }
        }

        MaybeScheduleBotTurn(state);
    }

    // ── CONNECTION LOST OVERLAY ──────────────────────────────

    private void BuildConnectionLostOverlay()
    {
        _connectionLostOverlay = new GameObject("ConnectionLostOverlay");
        _connectionLostOverlay.transform.SetParent(_canvas.transform, false);

        RectTransform r = _connectionLostOverlay.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        // Sits above everything — game, lobby, countdown
        Canvas c = _connectionLostOverlay.AddComponent<Canvas>();
        c.overrideSorting = true; c.sortingOrder = 300;
        _connectionLostOverlay.AddComponent<GraphicRaycaster>();
        _connectionLostOverlay.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.97f);

        // Top + bottom red strips for urgency
        AddHorizontalStrip(_connectionLostOverlay, atBottom: false, new Color(0.7f, 0.1f, 0.1f), 3f);
        AddHorizontalStrip(_connectionLostOverlay, atBottom: true,  new Color(0.7f, 0.1f, 0.1f), 3f);

        // Warning icon + title
        TextMeshProUGUI title = CreateText(_connectionLostOverlay, "CONNECTION LOST", 36,
            new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.80f),
            TextAlignmentOptions.Center);
        title.color     = new Color(1f, 0.28f, 0.28f, 1f);
        title.fontStyle = FontStyles.Bold;

        // Subtitle
        TextMeshProUGUI sub = CreateText(_connectionLostOverlay, "Lost connection to the server.", 18,
            new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.60f),
            TextAlignmentOptions.Center);
        sub.color = new Color(0.55f, 0.62f, 0.70f, 1f);

        // Single button — return to menu
        CreateButton(_connectionLostOverlay, "Return to Menu", P_Red,
            new Vector2(0.25f, 0.28f), new Vector2(0.75f, 0.43f),
            OnConnectionLostReturnClicked);

        _connectionLostOverlay.SetActive(false);
    }

    private void OnConnectionLostReturnClicked()
    {
        // Don't call NetworkManager.Disconnect() here — runner may already be dead.
        // Just reload the scene which resets everything.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnConnectionLostHandler()
    {
        _connectionLostOverlay.SetActive(true);
    }

    public void ShowGameOver(string loserName)
    {
        _believeButton.interactable = false;
        _bluffButton.interactable = false;
        _rebetButton.interactable = false;

        // Stop grace banner if still counting
        if (_disconnectGraceRoutine != null) { StopCoroutine(_disconnectGraceRoutine); _disconnectGraceRoutine = null; }
        if (_disconnectBanner != null) _disconnectBanner.SetActive(false);

        // Distinguish between normal loser and disconnect-ended game
        bool wasDisconnect = loserName.EndsWith(" disconnected");
        string displayName = wasDisconnect
            ? loserName.Substring(0, loserName.Length - " disconnected".Length)
            : loserName;
        _gameOverText.text = wasDisconnect
            ? $"{displayName} left the game"
            : $"{displayName} LOSES!";

        GameState state = _gameManager?.GetState();
        if (state != null)
        {
            string roundStr = _roundNumber > 0 ? $"  <size=12><color=#8899aa>·  {_roundNumber} rounds</color></size>" : "";
            var winners = state.Players.FindAll(p => p.Name != displayName);
            if (winners.Count > 0)
            {
                var winParts = winners.ConvertAll(w => {
                    int wIdx = state.Players.IndexOf(w);
                    string hex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(wIdx));
                    return $"<color=#{hex}>{w.Name}</color>";
                });
                _gameOverWinnersText.text = "Winners: " + string.Join("  &  ", winParts) + roundStr;
            }
            else
                _gameOverWinnersText.text = roundStr.Trim();

            // Build per-player stats table
            string myName = state.Players.Find(p => p.Id == _localPlayerId)?.Name ?? "";
            if (_statsText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<color=#aabbff><b>  PLAYER           PILES  CATCHES  MISTAKES</b></color>");
                foreach (var player in state.Players)
                {
                    _stats.TryGetValue(player.Name, out PlayerStats ps);
                    bool isMe    = player.Name == myName;
                    bool isLoser = player.Name == displayName;
                    string suffix  = isMe ? " (you)" : "";
                    string rawName = player.Name + suffix;
                    string nameCol = rawName.Length > 14
                        ? rawName.Substring(0, 13) + "…" : rawName;
                    string color = isLoser ? "#ff6666" : (isMe ? "#ffffaa" : "#ddffdd");
                    sb.AppendLine(
                        $"<color={color}>  {nameCol,-16}" +
                        $"  {ps.PilesTaken,4}   {ps.BluffsCaught,5}   {ps.BadChallenges,5}</color>");
                }
                _statsText.text = sb.ToString();
            }

            // Update and show lifetime stats via PlayerPrefs
            if (!string.IsNullOrEmpty(myName))
            {
                bool iLost = myName == displayName;
                int ltGames  = PlayerPrefs.GetInt("bluff_games",  0) + 1;
                int ltLosses = PlayerPrefs.GetInt("bluff_losses", 0) + (iLost ? 1 : 0);
                int ltWins   = PlayerPrefs.GetInt("bluff_wins",   0) + (iLost ? 0 : 1);
                int streak   = PlayerPrefs.GetInt("bluff_streak", 0);
                streak = iLost ? 0 : streak + 1;
                int bestStreak = Mathf.Max(PlayerPrefs.GetInt("bluff_best_streak", 0), streak);
                PlayerPrefs.SetInt("bluff_games",       ltGames);
                PlayerPrefs.SetInt("bluff_losses",      ltLosses);
                PlayerPrefs.SetInt("bluff_wins",        ltWins);
                PlayerPrefs.SetInt("bluff_streak",      streak);
                PlayerPrefs.SetInt("bluff_best_streak", bestStreak);

                // Accumulate per-game play stats
                _stats.TryGetValue(myName, out PlayerStats myPs);
                int ltPiles    = PlayerPrefs.GetInt("bluff_total_piles",    0) + myPs.PilesTaken;
                int ltCaught   = PlayerPrefs.GetInt("bluff_total_caught",   0) + myPs.BluffsCaught;
                int ltMistakes = PlayerPrefs.GetInt("bluff_total_mistakes", 0) + myPs.BadChallenges;
                PlayerPrefs.SetInt("bluff_total_piles",    ltPiles);
                PlayerPrefs.SetInt("bluff_total_caught",   ltCaught);
                PlayerPrefs.SetInt("bluff_total_mistakes", ltMistakes);

                // Save game history entry (last 5 completed non-disconnect games)
                if (!wasDisconnect)
                {
                    string histResult = iLost ? "L" : "W";
                    var opponents = state.Players.FindAll(p => p.Name != myName);
                    string oppStr = string.Join(",", opponents.ConvertAll(p => p.Name));
                    string histEntry = $"{histResult}|{_roundNumber}|{oppStr}";
                    string existing = PlayerPrefs.GetString("bluff_history", "");
                    var histList = new System.Collections.Generic.List<string>(
                        string.IsNullOrEmpty(existing) ? new string[0] : existing.Split('\n'));
                    histList.Insert(0, histEntry);
                    if (histList.Count > 5) histList.RemoveRange(5, histList.Count - 5);
                    PlayerPrefs.SetString("bluff_history", string.Join("\n", histList));
                }

                PlayerPrefs.Save();
                string streakStr = streak >= 2 ? $"  🔥<b>{streak}</b> streak" : "";
                string bestStr   = bestStreak >= 3 ? $"  ·  best <b>{bestStreak}</b>" : "";
                string pilesStr  = ltPiles > 0 ? $"  ·  piles {ltPiles}" : "";
                if (_lifetimeText != null)
                    _lifetimeText.text =
                        $"Your record:  <b>{ltWins}W</b> / <b>{ltLosses}L</b>  ({ltGames} games){streakStr}{bestStr}{pilesStr}";
            }
        }

        bool isOffline = NetworkedGameManager.Instance == null;
        bool isHost    = isOffline || NetworkedGameManager.LocalIsHost;
        _playAgainFrame?.SetActive(isHost);
        if (_waitingForHostText != null)
            _waitingForHostText.gameObject.SetActive(!isHost && !isOffline);
        if (isHost) _playAgainButton.interactable = true;

        _gameOverOverlay.SetActive(true);
    }
}