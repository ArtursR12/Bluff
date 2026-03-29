using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bluff.Core;
using System;

public class CardView : MonoBehaviour
{
    private Card _card;
    private bool _isSelected = false;
    private bool _isFaceDown = false;
    private int _cardIndex;

    private Image _borderImage;

    private static readonly Color BorderNormal   = new Color(0.76f, 0.76f, 0.80f, 1f);
    private static readonly Color BorderSelected = new Color(0.95f, 0.78f, 0.08f, 1f);
    private static readonly Color BackDeep      = new Color(0.09f, 0.12f, 0.34f, 1f);
    private static readonly Color BackMid       = new Color(0.14f, 0.20f, 0.50f, 1f);
    private static readonly Color BackAccent    = new Color(0.22f, 0.32f, 0.72f, 1f);
    private static readonly Color BackGold      = new Color(0.80f, 0.65f, 0.18f, 1f);
    private static readonly Color RedSuit        = new Color(0.82f, 0.07f, 0.07f, 1f);
    private static readonly Color BlackSuit      = new Color(0.08f, 0.08f, 0.09f, 1f);

    public Action<int> OnCardClicked;

    public Card Card     => _card;
    public bool IsSelected => _isSelected;
    public int  CardIndex  => _cardIndex;

    public void Setup(Card card, int index, bool faceDown = false)
    {
        _card      = card;
        _cardIndex = index;
        _isFaceDown = faceDown;

        foreach (Transform child in transform)
            Destroy(child.gameObject);

        _borderImage = GetComponent<Image>();
        if (_borderImage == null) _borderImage = gameObject.AddComponent<Image>();

        if (faceDown)
            BuildBack();
        else
            BuildFace(card);

        Button btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            AudioManager.PlayCardClick();
            OnCardClicked?.Invoke(_cardIndex);
        });
        btn.transition = Selectable.Transition.None;
    }

    // ── FACE-UP ──────────────────────────────────────────────

    private void BuildFace(Card card)
    {
        _borderImage.color = BorderNormal;

        bool isRed = card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds;
        Color ink = isRed ? RedSuit : BlackSuit;
        string suit = SuitSymbol(card.Suit);
        string rank = RankShort(card.Rank);

        // White card face (inset 2 px from border)
        GameObject face = MakeChild("Face", Vector2.zero, Vector2.one,
            new Vector2(2, 2), new Vector2(-2, -2));
        face.AddComponent<Image>().color = Color.white;

        // Subtle tinted corner triangles for red/black suits
        GameObject tint = MakeChild("Tint", Vector2.zero, Vector2.one,
            new Vector2(2, 2), new Vector2(-2, -2), face);
        tint.AddComponent<Image>().color = isRed
            ? new Color(1f, 0.92f, 0.92f, 0.28f)
            : new Color(0.92f, 0.93f, 0.96f, 0.18f);

        // Top-left corner (rank + suit stacked)
        MakeLabel(face, "TL", $"<b>{rank}</b>\n{suit}", 12, ink,
            new Vector2(0f, 0.56f), new Vector2(0.52f, 1f),
            new Vector2(4, -2), new Vector2(-1, -2),
            TextAlignmentOptions.TopLeft);

        // Centre suit symbol — larger, more dominant
        MakeLabel(face, "Suit", suit, 28, ink,
            new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.80f),
            Vector2.zero, Vector2.zero,
            TextAlignmentOptions.Center);

        // Bottom-right corner (rotated 180°)
        GameObject br = MakeChild("BR", new Vector2(0.48f, 0f), new Vector2(1f, 0.44f),
            new Vector2(0, 2), new Vector2(-4, 0), face);
        br.transform.localRotation = Quaternion.Euler(0, 0, 180);
        TextMeshProUGUI brTmp = br.AddComponent<TextMeshProUGUI>();
        brTmp.text          = $"<b>{rank}</b>\n{suit}";
        brTmp.fontSize      = 12;
        brTmp.color         = ink;
        brTmp.alignment     = TextAlignmentOptions.TopLeft;
        brTmp.raycastTarget = false;
    }

    // ── FACE-DOWN ────────────────────────────────────────────

    private void BuildBack()
    {
        // Outer border — deep navy
        _borderImage.color = BackDeep;

        // Card body — medium navy inset 2px
        GameObject body = MakeChild("Body", Vector2.zero, Vector2.one,
            new Vector2(2, 2), new Vector2(-2, -2));
        body.AddComponent<Image>().color = BackMid;

        // Thin gold outer frame line
        for (int side = 0; side < 4; side++)
        {
            GameObject line = new GameObject($"GL_{side}");
            line.transform.SetParent(body.transform, false);
            RectTransform lr = line.AddComponent<RectTransform>();
            switch (side)
            {
                case 0: lr.anchorMin=new Vector2(0,1); lr.anchorMax=Vector2.one;     lr.offsetMin=new Vector2(4,-1); lr.offsetMax=new Vector2(-4,0); break;
                case 1: lr.anchorMin=Vector2.zero;     lr.anchorMax=new Vector2(1,0); lr.offsetMin=new Vector2(4,0); lr.offsetMax=new Vector2(-4,1); break;
                case 2: lr.anchorMin=Vector2.zero;     lr.anchorMax=new Vector2(0,1); lr.offsetMin=new Vector2(0,4); lr.offsetMax=new Vector2(1,-4); break;
                default:lr.anchorMin=new Vector2(1,0); lr.anchorMax=Vector2.one;     lr.offsetMin=new Vector2(-1,4);lr.offsetMax=new Vector2(0,-4); break;
            }
            line.AddComponent<Image>().color = BackGold;
        }

        // Inner accent panel
        GameObject inner = MakeChild("Inner", Vector2.zero, Vector2.one,
            new Vector2(5, 5), new Vector2(-5, -5), body);
        inner.AddComponent<Image>().color = BackAccent;

        // Four corner pips — small ✦
        string[] corners = { "TL", "TR", "BL", "BR" };
        Vector2[] cAncMin = { new Vector2(0,0.72f), new Vector2(0.62f,0.72f), new Vector2(0,0), new Vector2(0.62f,0) };
        Vector2[] cAncMax = { new Vector2(0.38f,1f), new Vector2(1,1), new Vector2(0.38f,0.28f), new Vector2(1,0.28f) };
        for (int c = 0; c < 4; c++)
            MakeLabel(inner, corners[c], "✦", 8, new Color(BackGold.r, BackGold.g, BackGold.b, 0.7f),
                cAncMin[c], cAncMax[c], Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);

        // Centre diamond
        MakeLabel(inner, "Icon", "✦", 20, new Color(BackGold.r, BackGold.g, BackGold.b, 0.90f),
            new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.72f),
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
    }

    // ── SELECTION ────────────────────────────────────────────

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (!_isFaceDown)
            _borderImage.color = selected ? BorderSelected : BorderNormal;
    }

    public void Reveal()
    {
        _isFaceDown = false;
        Setup(_card, _cardIndex, false);
    }

    // ── HELPERS ──────────────────────────────────────────────

    private GameObject MakeChild(string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, GameObject parent = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent.transform : transform, false);
        RectTransform r = go.AddComponent<RectTransform>();
        r.anchorMin  = anchorMin;
        r.anchorMax  = anchorMax;
        r.offsetMin  = offsetMin;
        r.offsetMax  = offsetMax;
        return go;
    }

    private TextMeshProUGUI MakeLabel(GameObject parent, string name, string text,
        int fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text        = text;
        tmp.fontSize    = fontSize;
        tmp.color       = color;
        tmp.alignment   = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static string SuitSymbol(Suit suit) => suit switch
    {
        Suit.Spades   => "♠",
        Suit.Hearts   => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs    => "♣",
        _             => "?"
    };

    public static string RankShort(Rank rank) => rank switch
    {
        Rank.Ace   => "A",
        Rank.Jack  => "J",
        Rank.Queen => "Q",
        Rank.King  => "K",
        Rank.Ten   => "10",
        _          => ((int)rank).ToString()
    };
}
