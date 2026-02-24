using System.Collections.Generic;

namespace Bluff.Core
{
    public static class GameRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 6;
        public const int MaxBetCards = 4;

        public static bool CanPlaceBet(GameState state, Player player, List<Card> cards)
        {
            // Must be current player's turn
            if (state.CurrentPlayer != player) return false;

            // Must bet between 1 and 4 cards
            if (cards.Count < 1 || cards.Count > MaxBetCards) return false;

            // Player must actually have these cards
            foreach (Card card in cards)
                if (!player.Hand.Contains(card)) return false;

            return true;
        }

        public static bool CanChallenge(GameState state, Player player)
        {
            // Must be current player's turn
            if (state.CurrentPlayer != player) return false;

            // Must have something to challenge
            if (state.LastBetCards.Count == 0) return false;

            // Cant challenge your own bet
            if (state.LastBetPlayer == player) return false;

            return true;
        }

        // Returns true if checked card matches declared rank
        public static bool CheckCard(Card card, Rank declaredRank)
        {
            return card.Rank == declaredRank;
        }

        // Veryu - player believes, picks 1 card from last bet
        // Returns true if card matches (pile goes to bita)
        // Returns false if card doesnt match (challenger takes pile)
        public static bool ResolveVeryu(GameState state, int cardIndex)
        {
            Card checkedCard = state.LastBetCards[cardIndex];
            return CheckCard(checkedCard, state.LastDeclaredRank);
        }

        // Ne veryu - player doesnt believe, picks 1 card from last bet
        // Returns true if card doesnt match (liar takes pile)
        // Returns false if card matches (doubter takes pile... wait)
        public static bool ResolveNeVeryu(GameState state, int cardIndex)
        {
            Card checkedCard = state.LastBetCards[cardIndex];
            return !CheckCard(checkedCard, state.LastDeclaredRank);
        }
    }
}