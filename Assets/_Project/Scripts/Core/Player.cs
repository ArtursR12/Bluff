using System.Collections.Generic;

namespace Bluff.Core
{
    public class Player
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public List<Card> Hand { get; private set; } = new List<Card>();

        public Player(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public void AddCard(Card card) => Hand.Add(card);

        public void AddCards(List<Card> cards) => Hand.AddRange(cards);

        public void RemoveCards(List<Card> cards)
        {
            foreach (Card card in cards)
                Hand.Remove(card);
        }

        public bool HasCards() => Hand.Count > 0;

        public int CardCount => Hand.Count;

        public override string ToString() => $"{Name} ({Hand.Count} cards)";
    }
}