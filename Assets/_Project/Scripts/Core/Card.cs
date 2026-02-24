namespace Bluff.Core
{
    public enum Suit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs
    }

    public enum Rank
    {
        Two = 2, Three, Four, Five, Six, Seven,
        Eight, Nine, Ten, Jack, Queen, King, Ace
    }

    [System.Serializable]
    public class Card
    {
        public Suit Suit { get; private set; }
        public Rank Rank { get; private set; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public override string ToString()
        {
            return $"{Rank} of {Suit}";
        }
    }
}