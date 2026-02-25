using UnityEngine;
using Fusion;
using Bluff.Core;
using System.Collections.Generic;

public class NetworkedGameManager : NetworkBehaviour
{
    public static NetworkedGameManager Instance { get; private set; }

    // Networked properties - automatically synced across all players
    [Networked] public int CurrentPlayerIndex { get; set; }
    [Networked] public int PileCount { get; set; }
    [Networked] public int DiscardCount { get; set; }
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool GameOver { get; set; }
    [Networked] public int LastDeclaredRankInt { get; set; }
    [Networked] public int LastBetPlayerIndex { get; set; }
    [Networked] public int LastBetCount { get; set; }

    // Local game state (logic lives here)
    private GameState _localState = new GameState();
    private Deck _deck = new Deck();

    // Player registry
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

    public void LocalPlayerJoined(PlayerRef player, string playerName)
    {
        if (Object.HasStateAuthority)
        {
            RegisterPlayer(player, playerName);
        }
        else
        {
            RPC_RegisterPlayer(playerName);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RegisterPlayer(string playerName, RpcInfo info = default)
    {
        RegisterPlayer(info.Source, playerName);
    }

    // ── PLAYER REGISTRATION ──────────────────────────────────

    public void RegisterPlayer(PlayerRef player, string name)
    {
        if (!Object.HasStateAuthority) return;

        int index = _playerNames.Count;
        _playerIndexMap[player] = index;
        _playerNames.Add(name);

        Debug.Log($"Registered player {name} as index {index}");

        // Auto start when 2+ players registered (for testing)
        // In production, host manually starts
        if (_playerNames.Count >= 2)
            StartGame();
    }

    // ── GAME START ───────────────────────────────────────────

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        _deck.Initialize();
        _deck.Shuffle();

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

    // ── RPCS ─────────────────────────────────────────────────

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GameStarted(int startingPlayerIndex)
    {
        Debug.Log($"RPC_GameStarted received! Starting player: {startingPlayerIndex}");

        // Hide lobby, show game UI
        LobbyUI.Instance?.Hide();
        UIManager.Instance?.ShowGameUI();
        UIManager.Instance?.RefreshUI(_localState, GetLocalPlayerId());
    }

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

        // Update local state for non-host players
        if (!Object.HasStateAuthority)
        {
            // Clients need to remove cards from that player's hand
            // and add to pile - simplified sync
        }

        UIManager.Instance?.RefreshUI(_localState, GetLocalPlayerId());
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
        bool pileToDiscard = correct;

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

        RPC_BelieveResolved(cardIndex, (int)revealedCard.Suit,
            (int)revealedCard.Rank, pileToDiscard, playerIndex,
            _localState.Phase == GamePhase.GameOver,
            _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BelieveResolved(int cardIndex, int suitInt, int rankInt,
        bool pileToDiscard, int challengerIndex, bool gameOver, int loserIndex)
    {
        Debug.Log($"RPC_BelieveResolved: card was {(Rank)rankInt} of {(Suit)suitInt}");
        Debug.Log(pileToDiscard ? "Pile goes to discard!" : "Challenger takes pile!");

        if (gameOver && loserIndex >= 0)
        {
            string loserName = _localState.Players.Count > loserIndex
                ? _localState.Players[loserIndex].Name : "Unknown";
            UIManager.Instance?.ShowGameOver(loserName);
        }

        UIManager.Instance?.RefreshUI(_localState, GetLocalPlayerId());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Bluff(int cardIndex, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        PlayerRef sender = info.Source;
        if (!_playerIndexMap.ContainsKey(sender)) return;

        int playerIndex = _playerIndexMap[sender];
        if (_localState.CurrentPlayerIndex != playerIndex) return;

        Player doubter = _localState.Players[playerIndex];
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

        RPC_BluffResolved(cardIndex, (int)revealedCard.Suit,
            (int)revealedCard.Rank, caughtLying, betPlayerIdx,
            _localState.Phase == GamePhase.GameOver,
            _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BluffResolved(int cardIndex, int suitInt, int rankInt,
        bool caughtLying, int betPlayerIndex, bool gameOver, int loserIndex)
    {
        Debug.Log($"RPC_BluffResolved: card was {(Rank)rankInt} of {(Suit)suitInt}");
        Debug.Log(caughtLying ? "Liar caught!" : "Bluff call wrong - discard!");

        if (gameOver && loserIndex >= 0)
        {
            string loserName = _localState.Players.Count > loserIndex
                ? _localState.Players[loserIndex].Name : "Unknown";
            UIManager.Instance?.ShowGameOver(loserName);
        }

        UIManager.Instance?.RefreshUI(_localState, GetLocalPlayerId());
    }

    // ── HELPERS ──────────────────────────────────────────────

    private string GetLocalPlayerId()
    {
        if (Runner == null) return "0";
        if (_playerIndexMap.TryGetValue(Runner.LocalPlayer, out int index))
            return index.ToString();
        return "0";
    }

    private void SendInitialStateToClients()
    {
        foreach (var kvp in _playerIndexMap)
        {
            PlayerRef player = kvp.Key;
            int playerIndex = kvp.Value;
            Player gamePlayer = _localState.Players[playerIndex];

            // Build card data arrays
            int[] suits = new int[gamePlayer.Hand.Count];
            int[] ranks = new int[gamePlayer.Hand.Count];
            string[] names = new string[_localState.Players.Count];

            for (int i = 0; i < gamePlayer.Hand.Count; i++)
            {
                suits[i] = (int)gamePlayer.Hand[i].Suit;
                ranks[i] = (int)gamePlayer.Hand[i].Rank;
            }

            for (int i = 0; i < _localState.Players.Count; i++)
                names[i] = _localState.Players[i].Name;

            int[] cardCounts = new int[_localState.Players.Count];
            for (int i = 0; i < _localState.Players.Count; i++)
                cardCounts[i] = _localState.Players[i].CardCount;

            RPC_ReceiveInitialState(suits, ranks, names, cardCounts,
                _localState.CurrentPlayerIndex, playerIndex);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReceiveInitialState(int[] suits, int[] ranks,
        string[] playerNames, int[] cardCounts,
        int currentPlayerIndex, int receiverPlayerIndex)
    {
        Debug.Log($"RPC_ReceiveInitialState received! I am player {receiverPlayerIndex}");

        string localId = GetLocalPlayerId();

        // Only process if this matches our player index
        if (localId != receiverPlayerIndex.ToString()) return;

        // Rebuild local state for this client
        List<Player> players = new List<Player>();
        for (int i = 0; i < playerNames.Length; i++)
            players.Add(new Player(i.ToString(), playerNames[i]));

        _localState.StartGame(players);
        _localState.ForceSetCurrentPlayer(currentPlayerIndex);

        // Give this player their hand
        Player localPlayer = _localState.Players[receiverPlayerIndex];
        for (int i = 0; i < suits.Length; i++)
            localPlayer.AddCard(new Card((Suit)suits[i], (Rank)ranks[i]));

        // Set card counts for other players (they don't see actual cards)
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
        UIManager.Instance?.RefreshUI(_localState, localId);

        Debug.Log($"Game UI shown for player {localId}!");
    }

    public GameState GetLocalState() => _localState;
}