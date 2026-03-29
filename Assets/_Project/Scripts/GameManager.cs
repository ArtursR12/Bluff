using UnityEngine;
using System.Collections.Generic;
using Bluff.Core;

public class GameManager : MonoBehaviour, IGameManager
{
    public static GameManager Instance { get; private set; }

    private GameState _state = new GameState();
    private Deck _deck = new Deck();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartGame(List<string> playerNames)
    {
        bool shortDeck = playerNames.Count <= 3;
        _deck.Initialize(shortDeck);
        _deck.Shuffle();

        List<Player> players = new List<Player>();
        for (int i = 0; i < playerNames.Count; i++)
            players.Add(new Player(i.ToString(), playerNames[i]));

        _state = new GameState();
        _state.StartGame(players);

        int index = 0;
        while (_deck.Count > 0)
        {
            _state.Players[index % _state.Players.Count].AddCard(_deck.Deal());
            index++;
        }

        Debug.Log($"Game started! {_state.CurrentPlayer.Name} goes first!");
        LogGameState();
    }

    public bool TryPlaceBet(List<Card> cards, Rank declaredRank)
    {
        Player current = _state.CurrentPlayer;

        if (!GameRules.CanPlaceBet(_state, current, cards, declaredRank))
        {
            Debug.Log("Invalid bet!");
            return false;
        }

        _state.PlaceBet(current, cards, declaredRank);
        Debug.Log($"{current.Name} bets {cards.Count}x {declaredRank}");
        _state.NextTurn();
        AudioManager.PlayBetPlaced();
        LogGameState();
        return true;
    }

    public void ResolveBelieve(int cardIndex)
    {
        bool correct = GameRules.ResolveBelieve(_state, cardIndex);
        Player challenger = _state.CurrentPlayer;
        int challengerIndex = _state.CurrentPlayerIndex;

        if (correct)
        {
            Debug.Log($"{challenger.Name} checked correctly! Pile goes to discard!");
            _state.ResolveDiscard();
        }
        else
        {
            Debug.Log($"{challenger.Name} was wrong! Takes the pile!");
            _state.GivePileToPlayer(challenger);
        }

        _state.CheckLoser();
        if (_state.Phase == GamePhase.GameOver)
            Debug.Log($"Game over! {_state.Loser.Name} is the loser!");

        _state.NextTurn(correct, challengerIndex);
        LogGameState();
    }

    public void ResolveBluff(int cardIndex)
    {
        bool caughtLying = GameRules.ResolveBluff(_state, cardIndex);
        Player betPlayer = _state.LastBetPlayer;
        Player doubter = _state.CurrentPlayer;
        int doubterIndex = _state.CurrentPlayerIndex;

        if (caughtLying)
        {
            Debug.Log($"{betPlayer.Name} was lying! Takes the pile!");
            _state.GivePileToPlayer(betPlayer);
        }
        else
        {
            Debug.Log($"{doubter.Name} was wrong! Takes the pile!");
            _state.GivePileToPlayer(doubter);
        }

        _state.CheckLoser();
        if (_state.Phase == GamePhase.GameOver)
            Debug.Log($"Game over! {_state.Loser.Name} is the loser!");

        _state.NextTurn(caughtLying, doubterIndex);
        LogGameState();
    }

    public GameState GetState() => _state;

    // ── IGameManager ─────────────────────────────────────────

    public void PlaceBet(int[] cardIndices, int declaredRankInt)
    {
        Player localPlayer = _state.CurrentPlayer;
        if (localPlayer == null) return;
        List<Card> cards = new List<Card>();
        foreach (int idx in cardIndices)
            if (idx >= 0 && idx < localPlayer.Hand.Count)
                cards.Add(localPlayer.Hand[idx]);
        TryPlaceBet(cards, (Rank)declaredRankInt);
    }

    public void Believe(int cardIndex) => ResolveBelieve(cardIndex);
    public void Bluff(int cardIndex) => ResolveBluff(cardIndex);

    // ── BOT AI ────────────────────────────────────────────────
    // Returns (action, revealedCard, correct, playerName, liarName, rankStr).
    // action = "Bet" | "Believe" | "Bluff" | "" (nothing to do / local player's turn).
    public (string action, Card revealedCard, bool correct, string playerName, string liarName, string rankStr)
        TryBotAction()
    {
        if (_state.CurrentPlayer == null || _state.Phase != GamePhase.Playing)
            return ("", null, false, "", "", "");
        if (_state.CurrentPlayer.Id == "0")
            return ("", null, false, "", "", "");

        Player bot = _state.CurrentPlayer;
        string playerName = bot.Name;

        if (!_state.HasActiveBet)
        {
            if (bot.Hand.Count == 0) return ("", null, false, "", "", "");
            int count = Mathf.Min(UnityEngine.Random.Range(1, 3), bot.Hand.Count);
            var cards = bot.Hand.GetRange(0, count);
            Rank rank = cards[0].Rank;
            // 30% chance to declare a different rank (bluff)
            // Rank enum: Two=2..Ace=14 (13 values); map to 0-based index, offset, map back.
            if (UnityEngine.Random.value < 0.30f)
                rank = (Rank)(2 + ((int)rank - 2 + UnityEngine.Random.Range(1, 13)) % 13);
            TryPlaceBet(cards, rank);
            return ("Bet", null, false, playerName, "", "");
        }

        bool canChallenge = GameRules.CanChallenge(_state, bot);
        bool hasCards     = bot.Hand.Count > 0;
        bool doChallenge  = canChallenge && (!hasCards || UnityEngine.Random.value < 0.40f);

        if (doChallenge)
        {
            int cardIndex   = UnityEngine.Random.Range(0, _state.LastBetCards.Count);
            string liarName = _state.LastBetPlayer?.Name ?? "";
            string rankStr  = _state.LastDeclaredRank.ToString();
            Card revealed   = _state.LastBetCards.Count > cardIndex
                ? _state.LastBetCards[cardIndex]
                : new Card(Suit.Spades, Rank.Ace);

            if (UnityEngine.Random.value < 0.55f)
            {
                bool correct = GameRules.CheckCard(revealed, _state.LastDeclaredRank);
                ResolveBelieve(cardIndex);
                return ("Believe", revealed, correct, playerName, liarName, rankStr);
            }
            else
            {
                bool caught = !GameRules.CheckCard(revealed, _state.LastDeclaredRank);
                ResolveBluff(cardIndex);
                return ("Bluff", revealed, caught, playerName, liarName, rankStr);
            }
        }
        else if (hasCards)
        {
            int count = Mathf.Min(UnityEngine.Random.Range(1, 3), bot.Hand.Count);
            var cards = bot.Hand.GetRange(0, count);
            TryPlaceBet(cards, _state.LastDeclaredRank);
            return ("Bet", null, false, playerName, "", "");
        }

        return ("", null, false, "", "", "");
    }

    private void LogGameState()
    {
        Debug.Log("--- Game State ---");
        Debug.Log($"Current turn: {_state.CurrentPlayer?.Name ?? "None"}");
        Debug.Log($"Pile: {_state.Pile.Count} cards");
        Debug.Log($"Discard: {_state.Discard.Count} cards");
        foreach (Player p in _state.Players)
            Debug.Log($"{p.Name}: {p.CardCount} cards in hand");
        Debug.Log("-----------------");
    }
}