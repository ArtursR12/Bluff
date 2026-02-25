using Bluff.Core;
using Fusion;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class NetworkedGameManager : NetworkBehaviour
{
    public static NetworkedGameManager Instance { get; private set; }

    [Networked] public int CurrentPlayerIndex { get; set; }
    [Networked] public int PileCount { get; set; }
    [Networked] public int DiscardCount { get; set; }
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool GameOver { get; set; }
    [Networked] public int LastDeclaredRankInt { get; set; }
    [Networked] public int LastBetPlayerIndex { get; set; }
    [Networked] public int LastBetCount { get; set; }

    private GameState _localState = new GameState();
    private Deck _deck = new Deck();
    private int _localPlayerIndex = -1;

    private Dictionary<PlayerRef, int> _playerIndexMap = new();
    private List<string> _playerNames = new();

    public override void Spawned()
    {
        if (Instance != null) { Runner.Despawn(Object); return; }
        Instance = this;

        if (Object.HasStateAuthority)
            Debug.Log("NetworkedGameManager spawned - I am host!");
        else
            Debug.Log("NetworkedGameManager spawned - I am client!");
    }

    // ── PLAYER REGISTRATION ──────────────────────────────────

    public void LocalPlayerJoined(PlayerRef player, string playerName)
    {
        if (Object.HasStateAuthority)
            RegisterPlayer(player, playerName);
        else
            RPC_RegisterPlayer(playerName);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RegisterPlayer(string playerName, RpcInfo info = default)
    {
        RegisterPlayer(info.Source, playerName);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AssignPlayerIndex(int assignedIndex, PlayerRef targetPlayer)
    {
        if (Runner != null && Runner.LocalPlayer == targetPlayer)
        {
            _localPlayerIndex = assignedIndex;
            Debug.Log($"My player index is: {_localPlayerIndex}");
        }
    }

    public void RegisterPlayer(PlayerRef player, string name)
    {
        if (!Object.HasStateAuthority) return;

        int index = _playerNames.Count;
        _playerIndexMap[player] = index;
        _playerNames.Add(name);

        Debug.Log($"Registered player {name} as index {index}");

        RPC_AssignPlayerIndex(index, player);

        if (_playerNames.Count >= 2)
            StartGame();
    }

    // ── GAME START ───────────────────────────────────────────

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        bool shortDeck = _playerNames.Count <= 3;
        _deck.Initialize(shortDeck);
        _deck.Shuffle();

        Debug.Log($"Starting with {(shortDeck ? "36" : "52")} card deck for {_playerNames.Count} players");

        List<Player> players = new List<Player>();
        for (int i = 0; i < _playerNames.Count; i++)
            players.Add(new Player(i.ToString(), _playerNames[i]));

        _localState.StartGame(players);

        int index = 0;
        while (_deck.Count > 0)
        {
            _localState.Players[index % _localState.Players.Count]
                .AddCard(_deck.Deal());
            index++;
        }

        GameStarted = true;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        Debug.Log($"Game started! {_localState.CurrentPlayer.Name} goes first!");

        SendInitialStateToClients();
    }

    private void SendInitialStateToClients()
    {
        string[] names = new string[_localState.Players.Count];
        int[] cardCounts = new int[_localState.Players.Count];

        for (int i = 0; i < _localState.Players.Count; i++)
        {
            names[i] = _localState.Players[i].Name;
            cardCounts[i] = _localState.Players[i].CardCount;
        }

        for (int p = 0; p < _localState.Players.Count; p++)
        {
            Debug.Log($"Player {p} ({_localState.Players[p].Name}) first card: " +
            $"{_localState.Players[p].Hand[0].Rank} of {_localState.Players[p].Hand[0].Suit}");

            int[] suits = new int[cardCounts[p]];
            int[] ranks = new int[cardCounts[p]];

            for (int j = 0; j < cardCounts[p]; j++)
            {
                suits[j] = (int)_localState.Players[p].Hand[j].Suit;
                ranks[j] = (int)_localState.Players[p].Hand[j].Rank;
            }

            RPC_ReceiveInitialState(suits, ranks, names, cardCounts,
                _localState.CurrentPlayerIndex, p);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReceiveInitialState(int[] suits, int[] ranks,
    string[] playerNames, int[] cardCounts,
    int currentPlayerIndex, int receiverPlayerIndex)
    {
        StartCoroutine(ApplyInitialState(suits, ranks, playerNames,
            cardCounts, currentPlayerIndex, receiverPlayerIndex));
    }

    private System.Collections.IEnumerator ApplyInitialState(int[] suits, int[] ranks,
        string[] playerNames, int[] cardCounts,
        int currentPlayerIndex, int receiverPlayerIndex)
    {
        // Wait until we know our player index
        float timeout = 5f;
        while (_localPlayerIndex == -1 && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (_localPlayerIndex == -1)
        {
            // Last resort - try to get from map
            if (_playerIndexMap.TryGetValue(Runner.LocalPlayer, out int idx))
                _localPlayerIndex = idx;
            else
            {
                Debug.LogError("Could not determine local player index!");
                yield break;
            }
        }

        // Only process packet meant for us
        if (_localPlayerIndex != receiverPlayerIndex) yield break;

        List<Player> players = new List<Player>();
        for (int i = 0; i < playerNames.Length; i++)
            players.Add(new Player(i.ToString(), playerNames[i]));

        _localState = new GameState();
        _localState.StartGame(players);
        _localState.ClearAllHands();
        _localState.ForceSetCurrentPlayer(currentPlayerIndex);

        Player localPlayer = _localState.Players[receiverPlayerIndex];
        for (int i = 0; i < suits.Length; i++)
            localPlayer.AddCard(new Card((Suit)suits[i], (Rank)ranks[i]));

        Debug.Log($"Local player {receiverPlayerIndex} has {localPlayer.Hand.Count} real cards");
        Debug.Log($"First card: {localPlayer.Hand[0].Rank} of {localPlayer.Hand[0].Suit}");

        for (int i = 0; i < players.Count; i++)
        {
            if (i != receiverPlayerIndex)
            {
                for (int j = 0; j < cardCounts[i]; j++)
                    players[i].AddCard(new Card(Suit.Spades, Rank.Ace));
            }
        }

        LobbyUI.Instance?.Hide();
        UIManager.Instance?.ShowGameUI();
        UIManager.Instance?.RefreshUI(_localState, _localPlayerIndex.ToString());

        Debug.Log($"Game UI shown for player {_localPlayerIndex} with {suits.Length} cards!");
    }

    // ── RPCS ─────────────────────────────────────────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlaceBet(int[] cardIndices, int declaredRankInt,
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        PlayerRef sender = info.Source;
        if (!_playerIndexMap.ContainsKey(sender)) return;

        int playerIndex = _playerIndexMap[sender];
        Player player = _localState.Players[playerIndex];

        if (_localState.CurrentPlayerIndex != playerIndex)
        {
            Debug.Log("Not this player's turn!");
            return;
        }

        List<Card> cards = new List<Card>();
        foreach (int idx in cardIndices)
            if (idx < player.Hand.Count)
                cards.Add(player.Hand[idx]);

        Rank rank = (Rank)declaredRankInt;

        if (!GameRules.CanPlaceBet(_localState, player, cards))
        {
            Debug.Log("Invalid bet!");
            return;
        }

        _localState.PlaceBet(player, cards, rank);
        _localState.NextTurn();

        LastDeclaredRankInt = declaredRankInt;
        LastBetPlayerIndex = playerIndex;
        LastBetCount = cards.Count;
        PileCount = _localState.Pile.Count;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        Debug.Log($"Bet placed: {cards.Count}x {rank}");

        RPC_BetPlaced(playerIndex, cardIndices, declaredRankInt,
            _localState.CurrentPlayerIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BetPlaced(int betPlayerIndex, int[] cardIndices,
        int declaredRankInt, int nextPlayerIndex)
    {
        Debug.Log($"RPC_BetPlaced: Player {betPlayerIndex} bet " +
            $"{cardIndices.Length}x {(Rank)declaredRankInt}");

        if (!Object.HasStateAuthority)
        {
            if (_localState.Players.Count > betPlayerIndex)
            {
                Player betPlayer = _localState.Players[betPlayerIndex];
                int removeCount = Mathf.Min(cardIndices.Length, betPlayer.Hand.Count);
                List<Card> toRemove = new List<Card>();
                for (int i = 0; i < removeCount; i++)
                    toRemove.Add(betPlayer.Hand[0]);
                betPlayer.RemoveCards(toRemove);

                for (int i = 0; i < cardIndices.Length; i++)
                    _localState.Pile.Add(new Card(Suit.Spades, Rank.Ace));

                _localState.ForceSetCurrentPlayer(nextPlayerIndex);
                _localState.LastBetPlayerIndex = betPlayerIndex;
            }
        }

        UIManager.Instance?.RefreshUI(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Believe(int cardIndex, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        PlayerRef sender = info.Source;
        if (!_playerIndexMap.ContainsKey(sender)) return;

        int playerIndex = _playerIndexMap[sender];
        if (_localState.CurrentPlayerIndex != playerIndex) return;

        Player challenger = _localState.Players[playerIndex];
        bool correct = GameRules.ResolveBelieve(_localState, cardIndex);
        Card revealedCard = _localState.LastBetCards[cardIndex];

        if (correct)
        {
            _localState.ResolveDiscard();
            Debug.Log("Believe correct - pile to discard!");
        }
        else
        {
            _localState.GivePileToPlayer(challenger);
            Debug.Log($"Believe wrong - {challenger.Name} takes pile!");
        }

        _localState.CheckLoser();
        _localState.NextTurn();

        PileCount = _localState.Pile.Count;
        DiscardCount = _localState.Discard.Count;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        RPC_BelieveResolved((int)revealedCard.Suit, (int)revealedCard.Rank,
            correct, playerIndex,
            _localState.Phase == GamePhase.GameOver,
            _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BelieveResolved(int suitInt, int rankInt,
        bool pileToDiscard, int challengerIndex, bool gameOver, int loserIndex)
    {
        Debug.Log($"Believe: card was {(Rank)rankInt} of {(Suit)suitInt}");
        Debug.Log(pileToDiscard ? "Pile to discard!" : "Challenger takes pile!");

        if (!Object.HasStateAuthority)
        {
            if (pileToDiscard)
            {
                _localState.Pile.Clear();
                _localState.LastBetCards.Clear();
            }
            else
            {
                if (_localState.Players.Count > challengerIndex)
                    _localState.GivePileToPlayer(_localState.Players[challengerIndex]);
            }
            _localState.ForceSetCurrentPlayer(CurrentPlayerIndex);
        }

        if (gameOver && loserIndex >= 0)
        {
            string loserName = _localState.Players.Count > loserIndex
                ? _localState.Players[loserIndex].Name : "Unknown";
            UIManager.Instance?.ShowGameOver(loserName);
        }

        UIManager.Instance?.RefreshUI(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Bluff(int cardIndex, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        PlayerRef sender = info.Source;
        if (!_playerIndexMap.ContainsKey(sender)) return;

        int playerIndex = _playerIndexMap[sender];
        if (_localState.CurrentPlayerIndex != playerIndex) return;

        bool caughtLying = GameRules.ResolveBluff(_localState, cardIndex);
        Card revealedCard = _localState.LastBetCards[cardIndex];
        int betPlayerIdx = _localState.LastBetPlayerIndex;

        if (caughtLying)
        {
            _localState.GivePileToPlayer(_localState.LastBetPlayer);
            Debug.Log($"Bluff caught! {_localState.LastBetPlayer.Name} takes pile!");
        }
        else
        {
            _localState.ResolveDiscard();
            Debug.Log("Bluff wrong - pile to discard!");
        }

        _localState.CheckLoser();
        _localState.NextTurn();

        PileCount = _localState.Pile.Count;
        DiscardCount = _localState.Discard.Count;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        RPC_BluffResolved((int)revealedCard.Suit, (int)revealedCard.Rank,
            caughtLying, betPlayerIdx,
            _localState.Phase == GamePhase.GameOver,
            _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BluffResolved(int suitInt, int rankInt,
        bool caughtLying, int betPlayerIndex, bool gameOver, int loserIndex)
    {
        Debug.Log($"Bluff: card was {(Rank)rankInt} of {(Suit)suitInt}");
        Debug.Log(caughtLying ? "Liar caught!" : "Bluff wrong - discard!");

        if (!Object.HasStateAuthority)
        {
            if (caughtLying)
            {
                if (_localState.Players.Count > betPlayerIndex)
                    _localState.GivePileToPlayer(_localState.Players[betPlayerIndex]);
            }
            else
            {
                _localState.Pile.Clear();
                _localState.LastBetCards.Clear();
            }
            _localState.ForceSetCurrentPlayer(CurrentPlayerIndex);
        }

        if (gameOver && loserIndex >= 0)
        {
            string loserName = _localState.Players.Count > loserIndex
                ? _localState.Players[loserIndex].Name : "Unknown";
            UIManager.Instance?.ShowGameOver(loserName);
        }

        UIManager.Instance?.RefreshUI(_localState, _localPlayerIndex.ToString());
    }

    // ── HELPERS ──────────────────────────────────────────────

    private string GetLocalPlayerId()
    {
        if (_localPlayerIndex >= 0)
            return _localPlayerIndex.ToString();
        if (Runner == null) return "0";
        if (_playerIndexMap.TryGetValue(Runner.LocalPlayer, out int index))
            return index.ToString();
        return "0";
    }

    public GameState GetLocalState() => _localState;
}