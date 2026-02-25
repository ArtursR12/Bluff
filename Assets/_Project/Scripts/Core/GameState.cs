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

        public Player CurrentPlayer => Players[CurrentPlayerIndex];
        public Player LastBetPlayer => Players[LastBetPlayerIndex];
        public Player Loser { get; private set; }

        public void StartGame(List<Player> players)
        {
            Players = players;
            Phase = GamePhase.Playing;
            CurrentPlayerIndex = new System.Random().Next(players.Count);
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

        public void NextTurn()
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
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
    }
}