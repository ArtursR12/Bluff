using System.Collections.Generic;

namespace Bluff.Core
{
    public class Deck
    {
        private List<Card> _cards = new List<Card>();

        public int Count => _cards.Count;

        public void Initialize(bool shortDeck = false)
        {
            _cards.Clear();

            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
                {
                    // Short deck: only 6 and above (6,7,8,9,10,J,Q,K,A = 9 ranks x 4 suits = 36 cards)
                    if (shortDeck && (int)rank < (int)Rank.Six)
                        continue;

                    _cards.Add(new Card(suit, rank));
                }
            }
        }

        public void Shuffle()
        {
            System.Random rng = new System.Random();
            int n = _cards.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (_cards[k], _cards[n]) = (_cards[n], _cards[k]);
            }
        }

        public Card Deal()
        {
            if (_cards.Count == 0) return null;
            Card card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }

        public List<Card> DealMultiple(int count)
        {
            List<Card> dealt = new List<Card>();
            for (int i = 0; i < count && _cards.Count > 0; i++)
                dealt.Add(Deal());
            return dealt;
        }
    }
}