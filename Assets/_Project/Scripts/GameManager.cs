using UnityEngine;
using System.Collections.Generic;
using Bluff.Core;

public class GameManager : MonoBehaviour
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
        _deck.Initialize();
        _deck.Shuffle();

        List<Player> players = new List<Player>();
        for (int i = 0; i < playerNames.Count; i++)
            players.Add(new Player(i.ToString(), playerNames[i]));

        _state.StartGame(players);

        // Deal all cards evenly
        int index = 0;
        while (_deck.Count > 0)
        {
            _state.Players[index % _state.Players.Count]
                .AddCard(_deck.Deal());
            index++;
        }

        Debug.Log($"Game started! {_state.CurrentPlayer.Name} goes first!");
        LogGameState();
    }

    public bool TryPlaceBet(List<Card> cards, Rank declaredRank)
    {
        Player current = _state.CurrentPlayer;

        if (!GameRules.CanPlaceBet(_state, current, cards))
        {
            Debug.Log("Invalid bet!");
            return false;
        }

        _state.PlaceBet(current, cards, declaredRank);
        Debug.Log($"{current.Name} bets {cards.Count}x {declaredRank}");
        _state.NextTurn();
        LogGameState();
        return true;
    }

    public void ResolveVeryu(int cardIndex)
    {
        bool correct = GameRules.ResolveVeryu(_state, cardIndex);
        Player challenger = _state.CurrentPlayer;
        Player betPlayer = _state.LastBetPlayer;

        if (correct)
        {
            Debug.Log($"{challenger.Name} checked correctly! Pile goes to BITA!");
            _state.ResolveBita();
        }
        else
        {
            Debug.Log($"{challenger.Name} was wrong! Takes the pile!");
            _state.GivePileToPlayer(challenger);
        }

        _state.CheckLoser();
        if (_state.Phase == Bluff.Core.GamePhase.GameOver)
            Debug.Log($"GAME OVER! {_state.Loser.Name} is the loser!");

        _state.NextTurn();
        LogGameState();
    }

    public void ResolveNeVeryu(int cardIndex)
    {
        bool caughtLying = GameRules.ResolveNeVeryu(_state, cardIndex);
        Player doubter = _state.CurrentPlayer;
        Player betPlayer = _state.LastBetPlayer;

        if (caughtLying)
        {
            Debug.Log($"{betPlayer.Name} was lying! Takes the pile!");
            _state.GivePileToPlayer(betPlayer);
            _state.NextTurn();
        }
        else
        {
            Debug.Log($"{doubter.Name} was wrong! Pile goes to BITA!");
            _state.ResolveBita();
            _state.NextTurn();
        }

        _state.CheckLoser();
        if (_state.Phase == Bluff.Core.GamePhase.GameOver)
            Debug.Log($"GAME OVER! {_state.Loser.Name} is the loser!");

        LogGameState();
    }

    public GameState GetState() => _state;

    private void LogGameState()
    {
        Debug.Log($"--- Game State ---");
        Debug.Log($"Current turn: {_state.CurrentPlayer.Name}");
        Debug.Log($"Pile: {_state.Pile.Count} cards");
        Debug.Log($"Bita: {_state.Bita.Count} cards");
        foreach (Player p in _state.Players)
            Debug.Log($"{p.Name}: {p.CardCount} cards in hand");
        Debug.Log($"-----------------");
    }
}