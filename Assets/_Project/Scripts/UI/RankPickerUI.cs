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
    private readonly Dictionary<Rank, GameObject> _unavailableOverlays = new Dictionary<Rank, GameObject>();
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _deckNoteText;

    private static readonly Color C_Gold   = new Color(0.83f,  0.685f, 0.215f, 1f);
    private static readonly Color C_Blue   = new Color(0.08f,  0.135f, 0.45f,  1f);
    private static readonly Color C_Muted  = new Color(0.22f,  0.26f,  0.34f,  1f);
    private static readonly Color C_Red    = new Color(0.72f,  0.10f,  0.10f,  1f);

    private readonly List<Rank> _ranks = new List<Rank>
    {
        Rank.Two, Rank.Three, Rank.Four,  Rank.Five,
        Rank.Six, Rank.Seven, Rank.Eight, Rank.Nine,
        Rank.Ten, Rank.Jack,  Rank.Queen, Rank.King, Rank.Ace
    };

    // Suit symbol to display on each rank button (rotates through suits visually)
    private static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    private static readonly Color[]  SuitColors  =
    {
        new Color(0.80f, 0.80f, 0.95f, 0.18f), // spades — cool
        new Color(0.95f, 0.60f, 0.60f, 0.18f), // hearts — warm
        new Color(0.95f, 0.60f, 0.60f, 0.18f), // diamonds — warm
        new Color(0.80f, 0.80f, 0.95f, 0.18f), // clubs — cool
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
        if (canvas == null) { Debug.LogError("[RankPickerUI] No Canvas found."); return; }

        _panel = new GameObject("RankPickerPanel");
        _panel.transform.SetParent(canvas.transform, false);
        _panel.transform.SetAsLastSibling();

        RectTransform panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Canvas panelCanvas = _panel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder    = 100;
        _panel.AddComponent<GraphicRaycaster>();

        _panel.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.09f, 0.96f);

        // Gold top strip
        GameObject topStrip = new GameObject("TopStrip");
        topStrip.transform.SetParent(_panel.transform, false);
        RectTransform tsr = topStrip.AddComponent<RectTransform>();
        tsr.anchorMin = new Vector2(0f, 0.97f); tsr.anchorMax = Vector2.one;
        tsr.offsetMin = tsr.offsetMax = Vector2.zero;
        topStrip.AddComponent<Image>().color = C_Gold;

        // Title
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_panel.transform, false);
        RectTransform titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.78f);
        titleRect.anchorMax = new Vector2(0.95f, 0.93f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        _titleText = titleGo.AddComponent<TextMeshProUGUI>();
        _titleText.text      = "Declare a Rank";
        _titleText.fontSize  = 20;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color     = C_Gold;
        _titleText.fontStyle = FontStyles.Bold;

        // Deck note (hidden by default, shown for short deck)
        GameObject deckNoteGo = new GameObject("DeckNote");
        deckNoteGo.transform.SetParent(_panel.transform, false);
        RectTransform dnr = deckNoteGo.AddComponent<RectTransform>();
        dnr.anchorMin = new Vector2(0.05f, 0.72f);
        dnr.anchorMax = new Vector2(0.95f, 0.79f);
        dnr.offsetMin = dnr.offsetMax = Vector2.zero;
        _deckNoteText = deckNoteGo.AddComponent<TextMeshProUGUI>();
        _deckNoteText.text      = "36-card deck — ranks 2 through 5 are not available";
        _deckNoteText.fontSize  = 10;
        _deckNoteText.alignment = TextAlignmentOptions.Center;
        _deckNoteText.color     = new Color(0.55f, 0.65f, 0.75f, 0.80f);
        _deckNoteText.fontStyle = FontStyles.Italic;
        deckNoteGo.SetActive(false);

        // Rank buttons — 4 columns, 4 rows (last row has 1 button centred)
        int cols  = 4;
        float btnW  = 0.18f;
        float btnH  = 0.10f;
        float padX  = 0.205f;
        float padY  = 0.115f;
        float startX = 0.09f;
        float startY = 0.70f;

        for (int i = 0; i < _ranks.Count; i++)
        {
            Rank rank = _ranks[i];
            int col = i % cols;
            int row = i / cols;

            float x = startX + col * padX;
            // Last row (Ace): centre it
            if (i == 12) x = 0.5f - btnW * 0.5f;

            float y = startY - row * padY;
            CreateRankButton(rank, new Vector2(x, y - btnH), new Vector2(x + btnW, y), i);
        }

        CreateCancelButton();
    }

    private void CreateRankButton(Rank rank, Vector2 anchorMin, Vector2 anchorMax, int index)
    {
        // Gold border frame
        GameObject frame = new GameObject("Frame_" + rank);
        _rankFrames[rank] = frame;
        frame.transform.SetParent(_panel.transform, false);
        RectTransform fr = frame.AddComponent<RectTransform>();
        fr.anchorMin = anchorMin;
        fr.anchorMax = anchorMax;
        fr.offsetMin = new Vector2(3, 3);
        fr.offsetMax = new Vector2(-3, -3);
        frame.AddComponent<Image>().color = C_Gold;

        // Button body
        GameObject go = new GameObject("Btn_" + rank);
        go.transform.SetParent(frame.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
        go.AddComponent<Image>().color = C_Blue;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => OnRankSelected(rank));

        // Top highlight strip (glass effect)
        GameObject hl = new GameObject("Hl");
        hl.transform.SetParent(go.transform, false);
        RectTransform hlr = hl.AddComponent<RectTransform>();
        hlr.anchorMin = new Vector2(0f, 0.65f); hlr.anchorMax = Vector2.one;
        hlr.offsetMin = hlr.offsetMax = Vector2.zero;
        hl.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        // Suit symbol watermark (background decoration)
        int suitIdx = index % 4;
        GameObject suitGo = new GameObject("Suit");
        suitGo.transform.SetParent(go.transform, false);
        RectTransform sr = suitGo.AddComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        TextMeshProUGUI suitTmp = suitGo.AddComponent<TextMeshProUGUI>();
        suitTmp.text      = SuitSymbols[suitIdx];
        suitTmp.fontSize  = 28;
        suitTmp.alignment = TextAlignmentOptions.BottomRight;
        suitTmp.color     = SuitColors[suitIdx];
        suitTmp.raycastTarget = false;

        // Rank label (on top)
        GameObject label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text      = RankShort(rank);
        tmp.fontSize  = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;

        // Unavailable overlay (shown for ranks not in short deck)
        GameObject overlay = new GameObject("Unavailable");
        _unavailableOverlays[rank] = overlay;
        overlay.transform.SetParent(go.transform, false);
        RectTransform or2 = overlay.AddComponent<RectTransform>();
        or2.anchorMin = Vector2.zero; or2.anchorMax = Vector2.one;
        or2.offsetMin = or2.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.78f);
        GameObject crossGo = new GameObject("Cross");
        crossGo.transform.SetParent(overlay.transform, false);
        RectTransform cr = crossGo.AddComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = cr.offsetMax = Vector2.zero;
        TextMeshProUGUI crossTmp = crossGo.AddComponent<TextMeshProUGUI>();
        crossTmp.text      = "✕";
        crossTmp.fontSize  = 18;
        crossTmp.alignment = TextAlignmentOptions.Center;
        crossTmp.color     = new Color(0.45f, 0.45f, 0.50f, 0.80f);
        crossTmp.raycastTarget = false;
        overlay.SetActive(false);
    }

    private void CreateCancelButton()
    {
        GameObject frame = new GameObject("Frame_Cancel");
        frame.transform.SetParent(_panel.transform, false);
        RectTransform fr = frame.AddComponent<RectTransform>();
        fr.anchorMin = new Vector2(0.25f, 0.06f);
        fr.anchorMax = new Vector2(0.75f, 0.15f);
        fr.offsetMin = new Vector2(2, 2);
        fr.offsetMax = new Vector2(-2, -2);
        frame.AddComponent<Image>().color = C_Gold;

        GameObject go = new GameObject("CancelButton");
        go.transform.SetParent(frame.transform, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
        go.AddComponent<Image>().color = C_Red;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(Hide);

        GameObject label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        RectTransform lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text      = "✕  Cancel";
        tmp.fontSize  = 17;
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
        if (_rankFrames.Count == 0)
        {
            Debug.LogWarning("[RankPickerUI] Show() called before BuildPanel() finished.");
            return;
        }

        _onRankSelected = onRankSelected;

        // Update title
        if (_titleText != null)
            _titleText.text = cardCount > 0
                ? $"Declare a rank  ·  <b>{cardCount}</b> card{(cardCount == 1 ? "" : "s")}"
                : "Declare a Rank";

        // Show/hide short deck note
        if (_deckNoteText != null)
            _deckNoteText.gameObject.SetActive(shortDeck);

        // Show/hide unavailable overlays and disable buttons
        foreach (var kvp in _rankFrames)
        {
            bool available = !shortDeck || (int)kvp.Key >= (int)Rank.Six;

            // Frame border colour
            var frameImg = kvp.Value.GetComponent<Image>();
            if (frameImg != null)
                frameImg.color = available ? C_Gold : C_Muted;

            // Button interactable
            var btn = kvp.Value.GetComponentInChildren<Button>();
            if (btn != null) btn.interactable = available;

            // Unavailable overlay
            if (_unavailableOverlays.TryGetValue(kvp.Key, out GameObject overlay))
                overlay.SetActive(!available);
        }

        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
        _onRankSelected = null;
    }

    private static string RankShort(Rank rank) => rank switch
    {
        Rank.Two   => "2",  Rank.Three => "3",  Rank.Four  => "4",
        Rank.Five  => "5",  Rank.Six   => "6",  Rank.Seven => "7",
        Rank.Eight => "8",  Rank.Nine  => "9",  Rank.Ten   => "10",
        Rank.Jack  => "J",  Rank.Queen => "Q",  Rank.King  => "K",
        Rank.Ace   => "A",  _          => rank.ToString()
    };
}
