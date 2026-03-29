using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bluff.Core;
using System;
using System.Collections;
using System.Collections.Generic;

public class GuessingScreenUI : MonoBehaviour
{
    public static GuessingScreenUI Instance { get; private set; }

    public bool IsVisible => _panel != null && _panel.activeSelf;

    private GameObject _panel;
    private TextMeshProUGUI _headerText;
    private TextMeshProUGUI _resultText;
    private List<GameObject> _cardObjects = new List<GameObject>();
    private Action<int> _onCardPicked;
    private Action _onRevealComplete;
    private string _currentAction = "";
    private string _guesserName = "";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildPanel();
        _panel.SetActive(false);
        NetworkedGameManager.OnGuessingStarted += OnGuessingStartedEvent;
        NetworkedGameManager.OnCardRevealed   += OnCardRevealedEvent;
    }

    void OnDestroy()
    {
        NetworkedGameManager.OnGuessingStarted -= OnGuessingStartedEvent;
        NetworkedGameManager.OnCardRevealed   -= OnCardRevealedEvent;
    }

    // ── PANEL BUILD ──────────────────────────────────────────

    private void BuildPanel()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        _panel = new GameObject("GuessingPanel");
        _panel.transform.SetParent(canvas.transform, false);

        RectTransform rect = _panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Canvas pc = _panel.AddComponent<Canvas>();
        pc.overrideSorting = true;
        pc.sortingOrder = 120;
        _panel.AddComponent<GraphicRaycaster>();

        // Dark felt background
        Image bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.10f, 0.03f, 0.97f);

        // Gold top strip
        GameObject topStrip = new GameObject("TopStrip");
        topStrip.transform.SetParent(_panel.transform, false);
        RectTransform tsr = topStrip.AddComponent<RectTransform>();
        tsr.anchorMin = new Vector2(0f, 0.97f); tsr.anchorMax = Vector2.one;
        tsr.offsetMin = tsr.offsetMax = Vector2.zero;
        topStrip.AddComponent<Image>().color = new Color(0.83f, 0.685f, 0.215f, 1f);

        _headerText = MakeText(_panel, "", 18,
            new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.94f),
            TextAlignmentOptions.Center);
        _headerText.color = new Color(0.83f, 0.685f, 0.215f, 1f);

        _resultText = MakeText(_panel, "", 34,
            new Vector2(0.1f, 0.14f), new Vector2(0.9f, 0.36f),
            TextAlignmentOptions.Center);
        _resultText.gameObject.SetActive(false);
    }

    // ── PUBLIC API ────────────────────────────────────────────

    /// <summary>Called by UIManager for the local player who is guessing.</summary>
    public void ShowForGuesser(int cardCount, string action, string declaredRank, Action<int> onCardPicked)
    {
        _onCardPicked = onCardPicked;
        _currentAction = action;
        _guesserName = "You";
        _resultText.gameObject.SetActive(false);
        _headerText.text =
            $"You chose  <b>{action}</b>     Declared: <b>{declaredRank}</b>\n" +
            $"<size=15>Tap a card to reveal it</size>";
        BuildCardRow(cardCount, interactive: true);
        _panel.SetActive(true);
    }

    public void ForceHide() => _panel?.SetActive(false);

    /// <summary>
    /// Called by UIManager in offline mode: flip the chosen card and show the result,
    /// then invoke onComplete (refresh UI) after the animation finishes.
    /// </summary>
    public void ShowOfflineResult(int cardIndex, Card card, bool wasCorrect, Action onComplete)
    {
        _onRevealComplete = onComplete;
        StopAllCoroutines();
        StartCoroutine(FlipAndReveal(cardIndex, card, wasCorrect));
    }

    // ── EVENT HANDLERS ────────────────────────────────────────

    private void OnGuessingStartedEvent(string guesserName, string action,
        string declaredRank, int cardCount, bool isLocalGuesser)
    {
        if (isLocalGuesser) return; // guesser already shown via ShowForGuesser
        _onCardPicked = null;
        _currentAction = action;
        _guesserName = guesserName;
        _resultText.gameObject.SetActive(false);
        _headerText.text =
            $"<b>{guesserName}</b>  chose  <b>{action}</b>     Declared: <b>{declaredRank}</b>\n" +
            $"<size=15>Waiting for them to pick a card...</size>";
        BuildCardRow(cardCount, interactive: false);
        _panel.SetActive(true);
    }

    private void OnCardRevealedEvent(Card card, string revealerName, bool wasCorrect,
        string action, string declaredRank, int cardIndex)
    {
        if (!_panel.activeSelf) return;
        StopAllCoroutines();
        StartCoroutine(FlipAndReveal(cardIndex, card, wasCorrect));
    }

    // ── CARD ROW ──────────────────────────────────────────────

    private void BuildCardRow(int count, bool interactive)
    {
        foreach (GameObject go in _cardObjects)
            if (go != null) Destroy(go);
        _cardObjects.Clear();

        // Scale card size based on count
        float cardW   = count <= 2 ? 0.13f : count <= 3 ? 0.11f : 0.09f;
        float cardH   = count <= 2 ? 0.36f : count <= 3 ? 0.30f : 0.26f;
        float spacing = count <= 2 ? 0.18f : count <= 3 ? 0.14f : 0.12f;

        float totalWidth = (count - 1) * spacing + cardW;
        float startX = 0.5f - totalWidth / 2f;
        float cardY  = 0.36f;

        for (int i = 0; i < count; i++)
        {
            int idx = i;
            float x = startX + i * spacing;

            GameObject go = new GameObject($"BetCard_{i}");
            go.transform.SetParent(_panel.transform, false);

            RectTransform r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(x, cardY);
            r.anchorMax = new Vector2(x + cardW, cardY + cardH);
            r.offsetMin = new Vector2(5, 5);
            r.offsetMax = new Vector2(-5, -5);

            go.AddComponent<Image>();
            CardView cv = go.AddComponent<CardView>();
            cv.Setup(new Card(Suit.Spades, Rank.Ace), idx, faceDown: true);

            if (interactive)
                cv.OnCardClicked += OnCardClickedHandler;

            _cardObjects.Add(go);
        }
    }

    private void OnCardClickedHandler(int index)
    {
        if (_onCardPicked == null) return;
        Action<int> cb = _onCardPicked;
        _onCardPicked = null; // prevent double-fire

        // Remove listeners from all cards
        foreach (var go in _cardObjects)
            go?.GetComponent<Button>()?.onClick.RemoveAllListeners();

        _headerText.text = "<size=18>Card chosen...</size>";
        cb.Invoke(index);
    }

    // ── FLIP ANIMATION ────────────────────────────────────────

    private IEnumerator FlipAndReveal(int cardIndex, Card card, bool wasCorrect)
    {
        // If the card index is valid, animate the flip
        if (cardIndex >= 0 && cardIndex < _cardObjects.Count &&
            _cardObjects[cardIndex] != null)
        {
            GameObject cardObj = _cardObjects[cardIndex];
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            CardView cv = cardObj.GetComponent<CardView>();

            const float halfDuration = 0.22f;

            // Phase 1 — squish to flat
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                rt.localScale = new Vector3(1f - t, 1f, 1f);
                yield return null;
            }
            rt.localScale = new Vector3(0f, 1f, 1f);

            // Swap to face-up at the halfway point
            cv.Setup(card, cardIndex, faceDown: false);

            // Phase 2 — expand back
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                rt.localScale = new Vector3(t, 1f, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;

            yield return new WaitForSeconds(0.4f);
        }

        // Show result
        ShowResult(card, wasCorrect);
        yield return new WaitForSeconds(2.8f);
        Action cb = _onRevealComplete;
        _onRevealComplete = null;
        _panel.SetActive(false);
        cb?.Invoke();
    }

    private void ShowResult(Card card, bool wasCorrect)
    {
        _resultText.gameObject.SetActive(true);

        // Build a human-readable outcome headline
        string headline;
        string col;
        // "You" uses "were", named players use "was"
        string wasWere = _guesserName == "You" ? "were" : "was";
        if (_currentAction == "Bluff")
        {
            if (wasCorrect)
            {
                headline = $"BLUFF CAUGHT!\n<size=18>{_guesserName} {wasWere} right</size>";
                col = "#55ff88";
            }
            else
            {
                headline = $"NOT A BLUFF!\n<size=18>{_guesserName} {wasWere} wrong</size>";
                col = "#ff5555";
            }
        }
        else // Believe
        {
            if (wasCorrect)
            {
                headline = $"CORRECT!\n<size=18>Pile → discard</size>";
                col = "#55ff88";
            }
            else
            {
                headline = $"WRONG!\n<size=18>{_guesserName} takes the pile</size>";
                col = "#ff5555";
            }
        }

        _resultText.text     = $"<color={col}><b>{headline}</b></color>";
        _resultText.fontSize = 26;
        _headerText.text     =
            $"<color=#D4AF37><b>{CardView.RankShort(card.Rank)}</b>  " +
            $"{CardView.SuitSymbol(card.Suit)}</color>" +
            $"  <size=14><color=#8899AA>({card.Suit})</color></size>";
    }

    // ── HELPER ────────────────────────────────────────────────

    private TextMeshProUGUI MakeText(GameObject parent, string text, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);
        RectTransform r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = alignment;
        return tmp;
    }
}
