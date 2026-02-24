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

    private Image _background;
    private TextMeshProUGUI _label;

    private Color _normalColor;
    private Color _selectedColor = new Color(0.9f, 0.8f, 0.1f, 1f);
    private Color _faceDownColor = new Color(0.2f, 0.2f, 0.6f, 1f);

    public Action<int> OnCardClicked;

    public Card Card => _card;
    public bool IsSelected => _isSelected;
    public int CardIndex => _cardIndex;

    public void Setup(Card card, int index, bool faceDown = false)
    {
        _card = card;
        _cardIndex = index;
        _isFaceDown = faceDown;

        _background = GetComponent<Image>();
        if (_background == null)
            _background = gameObject.AddComponent<Image>();

        _label = GetComponentInChildren<TextMeshProUGUI>();
        if (_label == null)
        {
            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 11;
            _label.alignment = TextAlignmentOptions.Center;
        }

        _normalColor = GetCardColor(card);
        _background.color = faceDown ? _faceDownColor : _normalColor;
        _label.text = faceDown ? "?" : GetCardLabel(card);
        _label.color = GetTextColor(card);

        Button btn = gameObject.GetComponent<Button>();
        if (btn == null)
            btn = gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    private Color GetTextColor(Card card)
    {
        if (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds)
            return new Color(0.8f, 0.1f, 0.1f, 1f);
        else
            return new Color(0.1f, 0.1f, 0.1f, 1f);
    }

    private void OnClicked()
    {
        OnCardClicked?.Invoke(_cardIndex);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (!_isFaceDown)
            _background.color = selected ? _selectedColor : _normalColor;
    }

    public void Reveal()
    {
        _isFaceDown = false;
        _background.color = _normalColor;
        _label.text = GetCardLabel(_card);
    }

    private Color GetCardColor(Card card)
    {
        if (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds)
            return new Color(0.85f, 0.92f, 0.85f, 1f);
        else
            return new Color(0.92f, 0.92f, 0.92f, 1f);
    }

    private string GetCardLabel(Card card)
    {
        string suit = card.Suit switch
        {
            Suit.Hearts => "H",
            Suit.Diamonds => "D",
            Suit.Clubs => "C",
            Suit.Spades => "S",
            _ => "?"
        };
        return $"{card.Rank}\n{suit}";
    }
}