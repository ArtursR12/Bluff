using System.Collections.Generic;

namespace Bluff.Core
{
    public enum GamePhase
    {
        WaitingForPlayers,
        Playing,
        GameOver
    }

    public class GameState
    {
        public GamePhase Phase { get; private set; } = GamePhase.WaitingForPlayers;
        public List<Player> Players { get; private set; } = new List<Player>();
        public List<Card> Pile { get; private set; } = new List<Card>();
        public List<Card> Discard { get; private set; } = new List<Card>();
        public int CurrentPlayerIndex { get; private set; } = 0;

        public List<Card> LastBetCards { get; private set; } = new List<Card>();
        public Rank LastDeclaredRank { get; private set; }
        public int LastBetPlayerIndex { get; private set; }

        public Player CurrentPlayer => CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count ? Players[CurrentPlayerIndex] : null;
        public Player LastBetPlayer => LastBetPlayerIndex >= 0 && LastBetPlayerIndex < Players.Count ? Players[LastBetPlayerIndex] : null;
        public Player Loser { get; private set; }
        public bool HasActiveBet => LastBetCards.Count > 0;


        public void StartGame(List<Player> players)
        {
            Players = players;
            Phase = GamePhase.Playing;
            CurrentPlayerIndex = new System.Random().Next(players.Count);
        }

        public void ClearLastBet()
        {
            LastBetCards.Clear();
        }

        public void SetLastBetCards(List<Card> cards, Rank declaredRank, int betPlayerIndex)
        {
            LastBetCards = new List<Card>(cards);
            LastDeclaredRank = declaredRank;
            LastBetPlayerIndex = betPlayerIndex;
        }

        public void PlaceBet(Player player, List<Card> cards, Rank declaredRank)
        {
            player.RemoveCards(cards);
            Pile.AddRange(cards);
            LastBetCards = new List<Card>(cards);
            LastDeclaredRank = declaredRank;
            LastBetPlayerIndex = Players.IndexOf(player);
        }

        public void ResolveDiscard()
        {
            Discard.AddRange(Pile);
            Pile.Clear();
            LastBetCards.Clear();
        }

        public void GivePileToPlayer(Player player)
        {
            player.AddCards(new List<Card>(Pile));
            Pile.Clear();
            LastBetCards.Clear();
        }

        public void NextTurn(bool challengerWon = false, int challengerIndex = 0)
        {
            int start = challengerWon
                ? challengerIndex
                : (CurrentPlayerIndex + 1) % Players.Count;
            CurrentPlayerIndex = AdvanceToNextActivePlayer(start);
        }

        // Skip players with no cards when there's no active bet (they're already "out")
        private int AdvanceToNextActivePlayer(int startIndex)
        {
            int index = startIndex;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[index].HasCards() || HasActiveBet)
                    return index;
                index = (index + 1) % Players.Count;
            }
            return startIndex;
        }

        public void CheckLoser()
        {
            int playersWithCards = 0;
            Player lastWithCards = null;
            foreach (Player p in Players)
            {
                if (p.HasCards())
                {
                    playersWithCards++;
                    lastWithCards = p;
                }
            }
            if (playersWithCards == 1)
            {
                Loser = lastWithCards;
                Phase = GamePhase.GameOver;
            }
        }

        public void ForceSetCurrentPlayer(int index)
        {
            CurrentPlayerIndex = index;
        }

        public void ClearAllHands()
        {
            foreach (Player p in Players)
                p.Hand.Clear();
        }
    }
}