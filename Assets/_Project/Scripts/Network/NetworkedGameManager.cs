using Bluff.Core;
using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class NetworkedGameManager : NetworkBehaviour, IGameManager
{
    public static NetworkedGameManager Instance { get; private set; }

    // UI events — subscribe in UIManager / LobbyUI, never call UI singletons directly
    public static event System.Action OnGameStarted;
    public static event System.Action<GameState, string> OnStateRefresh;
    public static event System.Action<string> OnGameOver;
    public static event System.Action<int> OnPlayerCountChanged;
    public static event System.Action<int> OnCountdownTick;
    public static event System.Action<Card, string, bool, string, string, int> OnCardRevealed;
    public static event System.Action<string, string, string, int, bool> OnGuessingStarted;
    public static event System.Action OnGameResetting;
    public static event System.Action OnConnectionLost;
    public static event System.Action<string> OnTurnTimedOut;
    public static event System.Action<string[]> OnPlayerListChanged;
    public static event System.Action<string, int> OnDisconnectGrace;
    public static event System.Action<string> OnJoinRejected; // fired on rejecting client
    public static event System.Action<string, string> OnSpectatorReaction; // (playerName, emoji)
    public static event System.Action<string> OnPlayerReconnected; // playerName

    public static bool LocalIsHost { get; private set; }

    // Networked so all clients can read the correct value for the timer bar
    [Networked] public float TurnTimeout { get; set; }
    // Host-only: 0 = auto (short deck for ≤3 players), 1 = short (36 cards), 2 = full (52 cards)
    private int _deckChoice  = 0;
    private int _maxPlayers  = 6;
    [Networked] public NetworkBool IsShortDeck { get; set; }
    [Networked] public int NetworkedDeckChoice { get; set; }  // 0=Auto 1=36 2=52
    [Networked] public int NetworkedMaxPlayers { get; set; }

    // Called by NetworkManager when an unexpected shutdown/disconnect happens
    public static void NotifyConnectionLost() => OnConnectionLost?.Invoke();

    [Networked] public int CurrentPlayerIndex { get; set; }
    [Networked] public int PileCount { get; set; }
    [Networked] public int DiscardCount { get; set; }
    [Networked] public NetworkBool GameStarted { get; set; }
    [Networked] public NetworkBool GameOver { get; set; }
    [Networked] public int LastDeclaredRankInt { get; set; }
    [Networked] public int LastBetPlayerIndex { get; set; }
    [Networked] public int LastBetCount { get; set; }
    [Networked] public TickTimer TurnTimer { get; set; }
    [Networked] public TickTimer DisconnectGraceTimer { get; set; }
    [Networked] public int SpectatorCount { get; set; }

    private GameState _localState = new GameState();
    private Deck _deck = new Deck();
    private int _localPlayerIndex = -1;
    private string _pendingDisconnectName = "";

    private Dictionary<PlayerRef, int> _playerIndexMap = new();
    private List<string> _playerNames = new();
    private readonly List<PlayerRef> _spectatorRefs = new List<PlayerRef>();
    // Maps disconnected player name → their original player index (for reconnect within grace period)
    private readonly Dictionary<string, int> _pendingReconnectIndex = new Dictionary<string, int>();

    /// <summary>True when local client is spectating (joined after game started).</summary>
    public bool IsSpectator => _localPlayerIndex == -2;

    public override void Spawned()
    {
        if (Instance != null) { Runner.Despawn(Object); return; }
        Instance = this;

        LocalIsHost = Object.HasStateAuthority;
        if (Object.HasStateAuthority)
        {
            TurnTimeout = 30f;
            NetworkedDeckChoice  = 0;
            NetworkedMaxPlayers  = 6;
            Debug.Log("NetworkedGameManager spawned - I am host!");
        }
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

        if (GameStarted)
        {
            // Reconnect: same player rejoining during grace period → restore their slot
            if (DisconnectGraceTimer.IsRunning && _pendingReconnectIndex.TryGetValue(name, out int reconnectIdx))
            {
                Debug.Log($"{name} reconnected — restoring as player {reconnectIdx}.");
                _pendingReconnectIndex.Remove(name);
                _playerIndexMap[player] = reconnectIdx;
                if (Runner.LocalPlayer == player) _localPlayerIndex = reconnectIdx;
                RPC_AssignPlayerIndex(reconnectIdx, player);
                DisconnectGraceTimer = default;
                _pendingDisconnectName = "";
                // Give the reconnecting player a fresh turn if it's their turn
                if (_localState.CurrentPlayerIndex == reconnectIdx)
                    ResetTurnTimer();
                SendReconnectStateToPlayer(reconnectIdx);
                RPC_NotifyReconnected(name);
                return;
            }

            Debug.Log($"Game in progress — {name} joins as spectator.");
            _spectatorRefs.Add(player);
            SpectatorCount = _spectatorRefs.Count;
            // -2 = spectator marker; assign before sending state so the check fires correctly
            RPC_AssignPlayerIndex(-2, player);
            SendSpectatorState();
            return;
        }

        if (_playerNames.Count >= _maxPlayers)
        {
            Debug.LogWarning($"Room full ({_maxPlayers} players). Ignoring registration from {name}.");
            RPC_RejectJoin(GameStarted ? "Game already started" : "Room is full");
            return;
        }

        int index = _playerNames.Count;
        _playerIndexMap[player] = index;
        _playerNames.Add(name);

        Debug.Log($"Registered player {name} as index {index}");

        // Set local index immediately for the host without waiting for the RPC round-trip
        if (Runner != null && Runner.LocalPlayer == player)
            _localPlayerIndex = index;

        RPC_AssignPlayerIndex(index, player);
        RPC_UpdatePlayerCount(_playerNames.Count);
        RPC_UpdatePlayerList(_playerNames.ToArray());
    }

    private void SendSpectatorState()
    {
        if (!Object.HasStateAuthority || _localState == null) return;

        string[] names      = new string[_localState.Players.Count];
        int[]    cardCounts = new int[_localState.Players.Count];
        for (int i = 0; i < _localState.Players.Count; i++)
        {
            names[i]      = _localState.Players[i].Name;
            cardCounts[i] = _localState.Players[i].CardCount;
        }

        int lastRank      = _localState.HasActiveBet ? (int)_localState.LastDeclaredRank : -1;
        int lastBetPlayer = _localState.HasActiveBet ? _localState.LastBetPlayerIndex : -1;
        int lastBetCnt    = _localState.HasActiveBet ? _localState.LastBetCards.Count  : 0;

        RPC_SyncSpectatorState(names, cardCounts, _localState.CurrentPlayerIndex,
            lastRank, lastBetPlayer, lastBetCnt,
            _localState.Pile.Count, _localState.Discard.Count);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncSpectatorState(
        string[] names, int[] cardCounts, int currentPlayerIndex,
        int lastDeclaredRankInt, int lastBetPlayerIndex, int lastBetCount,
        int pileCount, int discardCount)
    {
        // Only the joining spectator (index -2) processes this
        if (_localPlayerIndex != -2) return;

        List<Player> players = new List<Player>();
        for (int i = 0; i < names.Length; i++)
            players.Add(new Player(i.ToString(), names[i]));

        _localState = new GameState();
        _localState.StartGame(players);
        _localState.ClearAllHands();
        _localState.ForceSetCurrentPlayer(currentPlayerIndex);

        for (int i = 0; i < names.Length; i++)
            for (int j = 0; j < cardCounts[i]; j++)
                players[i].AddCard(new Card(Suit.Spades, Rank.Ace));

        for (int i = 0; i < pileCount; i++)
            _localState.Pile.Add(new Card(Suit.Spades, Rank.Ace));

        for (int i = 0; i < discardCount; i++)
            _localState.Discard.Add(new Card(Suit.Spades, Rank.Ace));

        if (lastBetPlayerIndex >= 0 && lastBetCount > 0 && lastDeclaredRankInt >= 0)
        {
            var betCards = new List<Card>();
            for (int i = 0; i < lastBetCount; i++)
                betCards.Add(new Card(Suit.Spades, Rank.Ace));
            _localState.SetLastBetCards(betCards, (Rank)lastDeclaredRankInt, lastBetPlayerIndex);
        }

        OnGameStarted?.Invoke();
        OnStateRefresh?.Invoke(_localState, "-2");
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SpectatorReaction(string emoji, string playerName)
    {
        OnSpectatorReaction?.Invoke(playerName, emoji);
    }

    private void SendReconnectStateToPlayer(int playerIndex)
    {
        if (_localState == null || playerIndex < 0 || playerIndex >= _localState.Players.Count) return;

        string[] names      = new string[_localState.Players.Count];
        int[]    cardCounts = new int[_localState.Players.Count];
        for (int i = 0; i < _localState.Players.Count; i++)
        {
            names[i]      = _localState.Players[i].Name;
            cardCounts[i] = _localState.Players[i].CardCount;
        }

        var hand = _localState.Players[playerIndex].Hand;
        int[] suits = new int[hand.Count];
        int[] ranks = new int[hand.Count];
        for (int j = 0; j < hand.Count; j++)
        {
            suits[j] = (int)hand[j].Suit;
            ranks[j] = (int)hand[j].Rank;
        }

        int lastRank      = _localState.HasActiveBet ? (int)_localState.LastDeclaredRank  : -1;
        int lastBetPlayer = _localState.HasActiveBet ? _localState.LastBetPlayerIndex      : -1;
        int lastBetCnt    = _localState.HasActiveBet ? _localState.LastBetCards.Count      : 0;

        RPC_ReceiveReconnectState(suits, ranks, names, cardCounts,
            _localState.CurrentPlayerIndex, playerIndex,
            _localState.Pile.Count, _localState.Discard.Count,
            lastRank, lastBetPlayer, lastBetCnt);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReceiveReconnectState(
        int[] suits, int[] ranks,
        string[] playerNames, int[] cardCounts,
        int currentPlayerIndex, int receiverPlayerIndex,
        int pileCount, int discardCount,
        int lastRank, int lastBetPlayer, int lastBetCnt)
    {
        // Only the reconnecting player processes this; others skip it
        if (_localPlayerIndex != receiverPlayerIndex) return;

        StartCoroutine(ApplyReconnectState(suits, ranks, playerNames, cardCounts,
            currentPlayerIndex, receiverPlayerIndex,
            pileCount, discardCount, lastRank, lastBetPlayer, lastBetCnt));
    }

    private System.Collections.IEnumerator ApplyReconnectState(
        int[] suits, int[] ranks,
        string[] playerNames, int[] cardCounts,
        int currentPlayerIndex, int receiverPlayerIndex,
        int pileCount, int discardCount,
        int lastRank, int lastBetPlayer, int lastBetCnt)
    {
        // Wait for the player index assignment RPC to arrive if needed
        float timeout = 5f;
        while (_localPlayerIndex != receiverPlayerIndex && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (_localPlayerIndex != receiverPlayerIndex) yield break;

        List<Player> players = new List<Player>();
        for (int i = 0; i < playerNames.Length; i++)
            players.Add(new Player(i.ToString(), playerNames[i]));

        _localState = new GameState();
        _localState.StartGame(players);
        _localState.ClearAllHands();
        _localState.ForceSetCurrentPlayer(currentPlayerIndex);

        // Restore own cards
        Player me = _localState.Players[receiverPlayerIndex];
        for (int i = 0; i < suits.Length; i++)
            me.AddCard(new Card((Suit)suits[i], (Rank)ranks[i]));

        // Restore pile / discard counts with placeholder cards
        for (int i = 0; i < pileCount;   i++) _localState.Pile.Add(new Card(Suit.Spades, Rank.Ace));
        for (int i = 0; i < discardCount; i++) _localState.Discard.Add(new Card(Suit.Spades, Rank.Ace));

        if (lastBetPlayer >= 0 && lastBetCnt > 0 && lastRank >= 0)
        {
            var betCards = new List<Card>();
            for (int i = 0; i < lastBetCnt; i++)
                betCards.Add(new Card(Suit.Spades, Rank.Ace));
            _localState.SetLastBetCards(betCards, (Rank)lastRank, lastBetPlayer);
        }

        // Game UI is already visible — just refresh state
        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyReconnected(string playerName)
    {
        OnPlayerReconnected?.Invoke(playerName);
    }

    /// <summary>Called by LobbyUI (host only) to configure settings before the game starts.</summary>
    public void SetLobbySettings(float timerSeconds, int deckChoice)
    {
        if (!Object.HasStateAuthority) return;
        TurnTimeout = timerSeconds;
        _deckChoice = deckChoice;
        NetworkedDeckChoice = deckChoice;
        RPC_UpdatePlayerList(_playerNames.ToArray()); // Refresh display for all clients
    }

    public void SetMaxPlayers(int max)
    {
        if (!Object.HasStateAuthority) return;
        _maxPlayers = Mathf.Clamp(max, 2, 6);
        NetworkedMaxPlayers = _maxPlayers;
        RPC_UpdatePlayerList(_playerNames.ToArray());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RejectJoin(string reason)
    {
        // Only clients that were never assigned a player index are the rejected ones
        if (_localPlayerIndex == -1)
            OnJoinRejected?.Invoke(reason);
    }

    public void RequestStartGame()
    {
        if (!Object.HasStateAuthority) return;
        if (_playerNames.Count < 2) return;
        StartCoroutine(CountdownThenBegin());
    }

    public void RequestPlayAgain()
    {
        if (!Object.HasStateAuthority) return;
        RPC_NotifyRestart();
        StartCoroutine(CountdownThenRestart());
    }

    private System.Collections.IEnumerator CountdownThenRestart()
    {
        RPC_ShowCountdown(3); yield return new WaitForSeconds(1f);
        RPC_ShowCountdown(2); yield return new WaitForSeconds(1f);
        RPC_ShowCountdown(1); yield return new WaitForSeconds(1f);
        RPC_ShowCountdown(0);
        RestartGame();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyRestart()
    {
        OnGameResetting?.Invoke();
    }

    private void RestartGame()
    {
        DisconnectGraceTimer = default;
        _pendingDisconnectName = "";
        _pendingReconnectIndex.Clear();
        bool shortDeck = _deckChoice == 1 || (_deckChoice == 0 && _playerNames.Count <= 3);
        IsShortDeck = shortDeck;
        _deck.Initialize(shortDeck);
        _deck.Shuffle();

        List<Player> players = new List<Player>();
        for (int i = 0; i < _playerNames.Count; i++)
            players.Add(new Player(i.ToString(), _playerNames[i]));

        _localState = new GameState();
        _localState.StartGame(players);

        int index = 0;
        while (_deck.Count > 0)
        {
            _localState.Players[index % _localState.Players.Count].AddCard(_deck.Deal());
            index++;
        }

        GameStarted = true;
        GameOver    = false;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        SendInitialStateToClients();
        if (_spectatorRefs.Count > 0) SendSpectatorState();
        ResetTurnTimer();

        OnGameStarted?.Invoke();
        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerCount(int count)
    {
        OnPlayerCountChanged?.Invoke(count);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerList(string[] names)
    {
        OnPlayerListChanged?.Invoke(names);
    }

    public void AnnounceGuessing(string action)
    {
        if (_localState.CurrentPlayerIndex != _localPlayerIndex) return;
        RPC_GuessingStarted(_localPlayerIndex, action,
            _localState.LastBetCards.Count, _localState.LastDeclaredRank.ToString());
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_GuessingStarted(int guesserIndex, string action,
        int cardCount, string declaredRank)
    {
        string guesserName = guesserIndex < _localState.Players.Count
            ? _localState.Players[guesserIndex].Name : "Player";
        bool isLocal = guesserIndex == _localPlayerIndex;
        OnGuessingStarted?.Invoke(guesserName, action, declaredRank, cardCount, isLocal);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowCountdown(int secondsLeft)
    {
        OnCountdownTick?.Invoke(secondsLeft);
    }

    private System.Collections.IEnumerator CountdownThenBegin()
    {
        RPC_ShowCountdown(3);
        yield return new WaitForSeconds(1f);
        RPC_ShowCountdown(2);
        yield return new WaitForSeconds(1f);
        RPC_ShowCountdown(1);
        yield return new WaitForSeconds(1f);
        RPC_ShowCountdown(0);
        StartGame();
    }

    // ── GAME START ───────────────────────────────────────────

    private void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        bool shortDeck = _deckChoice == 1 || (_deckChoice == 0 && _playerNames.Count <= 3);
        IsShortDeck = shortDeck;
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
        GameOver    = false;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        Debug.Log($"Game started! {_localState.CurrentPlayer.Name} goes first!");

        SendInitialStateToClients();

        ResetTurnTimer();

        // Host transitions to game UI directly — clients do it via ApplyInitialState
        OnGameStarted?.Invoke();
        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
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
        // Host already has the correct authoritative state from StartGame()
        if (Object.HasStateAuthority) yield break;

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

        OnGameStarted?.Invoke();
        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());

        Debug.Log($"Game UI shown for player {_localPlayerIndex} with {suits.Length} cards!");
    }

    // ── RPCS ─────────────────────────────────────────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlaceBet(int[] cardIndices, int declaredRankInt,
    RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;
        if (!TryGetCurrentPlayerIndex(info, out int playerIndex)) return;

        Player player = _localState.Players[playerIndex];

        List<Card> cards = new List<Card>();
        foreach (int idx in cardIndices)
            if (idx < player.Hand.Count)
                cards.Add(player.Hand[idx]);

        Rank rank = (Rank)declaredRankInt;

        if (!GameRules.CanPlaceBet(_localState, player, cards, rank))
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
        AudioManager.PlayBetPlaced();

        ResetTurnTimer();
        RPC_BetPlaced(playerIndex, cardIndices, declaredRankInt,
            _localState.CurrentPlayerIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BetPlaced(int betPlayerIndex, int[] cardIndices,
        int declaredRankInt, int nextPlayerIndex)
    {
        Debug.Log($"RPC_BetPlaced: Player {betPlayerIndex} bet " +
            $"{cardIndices.Length}x {(Rank)declaredRankInt}");

        Debug.Log($"RPC_BetPlaced: cardIndices.Length={cardIndices.Length}, LastBetCards after={_localState.LastBetCards.Count}, HasAuth={Object.HasStateAuthority}");

        if (!Object.HasStateAuthority)
        {
            if (_localState.Players.Count > betPlayerIndex)
            {
                Player betPlayer = _localState.Players[betPlayerIndex];
                // Remove in descending order so earlier indices aren't shifted
                System.Array.Sort(cardIndices);
                for (int i = cardIndices.Length - 1; i >= 0; i--)
                {
                    if (cardIndices[i] < betPlayer.Hand.Count)
                        betPlayer.Hand.RemoveAt(cardIndices[i]);
                }

                List<Card> betCards = new List<Card>();
                for (int i = 0; i < cardIndices.Length; i++)
                {
                    Card placeholder = new Card(Suit.Spades, Rank.Ace);
                    _localState.Pile.Add(placeholder);
                    betCards.Add(placeholder);
                }

                _localState.SetLastBetCards(betCards, (Rank)declaredRankInt, betPlayerIndex);
                _localState.ForceSetCurrentPlayer(nextPlayerIndex);
            }
        }
        else
        {
            // Host also needs LastBetCards set correctly after PlaceBet
            _localState.ForceSetCurrentPlayer(nextPlayerIndex);
        }

        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Believe(int cardIndex, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;
        if (!TryGetCurrentPlayerIndex(info, out int playerIndex)) return;
        if (cardIndex < 0 || cardIndex >= _localState.LastBetCards.Count) return;

        Player challenger = _localState.Players[playerIndex];
        int pileSize = _localState.Pile.Count;
        bool correct = GameRules.ResolveBelieve(_localState, cardIndex);
        Card revealedCard = _localState.LastBetCards[cardIndex];

        if (correct)
        {
            _localState.ResolveDiscard();
            Debug.Log("Believe correct - pile to discard!");
        }
        else
        {
            List<Card> pileCards = new List<Card>(_localState.Pile);
            _localState.GivePileToPlayer(challenger);
            Debug.Log($"Believe wrong - {challenger.Name} takes pile!");
            SendCardsToPlayer(playerIndex, pileCards);
        }

        _localState.CheckLoser();
        _localState.NextTurn(correct, playerIndex);

        PileCount = _localState.Pile.Count;
        DiscardCount = _localState.Discard.Count;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        bool believeGameOver = _localState.Phase == GamePhase.GameOver;
        if (believeGameOver) GameOver = true;
        else ResetTurnTimer();
        RPC_BelieveResolved((int)revealedCard.Suit, (int)revealedCard.Rank,
            correct, playerIndex, cardIndex, pileSize,
            _localState.Phase == GamePhase.GameOver,
            _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BelieveResolved(int suitInt, int rankInt,
        bool pileToDiscard, int challengerIndex, int pickedCardIndex, int pileSize, bool gameOver, int loserIndex)
    {
        Debug.Log($"Believe: card was {(Rank)rankInt} of {(Suit)suitInt}");
        Debug.Log(pileToDiscard ? "Pile to discard!" : "Challenger takes pile!");
        Debug.Log($"BelieveResolved: gameOver={gameOver}, loserIndex={loserIndex}, players={_localState.Players.Count}");

        Card believeCard = new Card((Suit)suitInt, (Rank)rankInt);
        string believerName = challengerIndex < _localState.Players.Count
            ? _localState.Players[challengerIndex].Name : "Player";
        string believeDeclared = _localState.LastDeclaredRank.ToString();
        OnCardRevealed?.Invoke(believeCard, believerName, pileToDiscard, "Believe", believeDeclared, pickedCardIndex);

        if (!Object.HasStateAuthority)
        {
            if (pileToDiscard)
            {
                _localState.Discard.AddRange(_localState.Pile);
                _localState.Pile.Clear();
                _localState.ClearLastBet();
            }
            else
            {
                // Real cards are sent via RPC_ReceiveCards to the local player if they're the challenger.
                // For opponent challengers, add placeholder cards so their displayed count is correct.
                if (challengerIndex != _localPlayerIndex && challengerIndex < _localState.Players.Count)
                {
                    Player opp = _localState.Players[challengerIndex];
                    for (int i = 0; i < pileSize; i++)
                        opp.AddCard(new Card(Suit.Spades, Rank.Ace));
                }
                _localState.Pile.Clear();
                _localState.ClearLastBet();
            }
            _localState.ForceSetCurrentPlayer(CurrentPlayerIndex);
        }

        if (gameOver && loserIndex >= 0)
        {
            string loserName = _localState.Players.Count > loserIndex
                ? _localState.Players[loserIndex].Name : "Unknown";
            OnGameOver?.Invoke(loserName);
        }

        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Bluff(int cardIndex, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;
        if (!TryGetCurrentPlayerIndex(info, out int playerIndex)) return;
        if (cardIndex < 0 || cardIndex >= _localState.LastBetCards.Count) return;

        int pileSize = _localState.Pile.Count;
        bool caughtLying = GameRules.ResolveBluff(_localState, cardIndex);
        Card revealedCard = _localState.LastBetCards[cardIndex];
        int betPlayerIdx = _localState.LastBetPlayerIndex;

        List<Card> pileCards = new List<Card>(_localState.Pile);
        int pileReceiverIndex;
        if (caughtLying)
        {
            pileReceiverIndex = betPlayerIdx;
            _localState.GivePileToPlayer(_localState.LastBetPlayer);
            Debug.Log($"Bluff caught! {_localState.LastBetPlayer.Name} takes pile!");
        }
        else
        {
            pileReceiverIndex = playerIndex;
            _localState.GivePileToPlayer(_localState.Players[playerIndex]);
            Debug.Log($"Bluff wrong - {_localState.Players[playerIndex].Name} takes pile!");
        }
        SendCardsToPlayer(pileReceiverIndex, pileCards);

        _localState.CheckLoser();
        _localState.NextTurn(caughtLying, playerIndex);

        PileCount = _localState.Pile.Count;
        DiscardCount = _localState.Discard.Count;
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;

        bool bluffGameOver = _localState.Phase == GamePhase.GameOver;
        if (bluffGameOver) GameOver = true;
        else ResetTurnTimer();
        RPC_BluffResolved((int)revealedCard.Suit, (int)revealedCard.Rank,
            caughtLying, betPlayerIdx, playerIndex, cardIndex, pileSize,
            _localState.Phase == GamePhase.GameOver,
            _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BluffResolved(int suitInt, int rankInt,
        bool caughtLying, int betPlayerIndex, int doubterIndex, int pickedCardIndex, int pileSize, bool gameOver, int loserIndex)
    {
        Debug.Log($"Bluff: card was {(Rank)rankInt} of {(Suit)suitInt}");
        Debug.Log(caughtLying ? "Liar caught!" : "Bluff wrong - doubter takes pile!");
        Debug.Log($"BluffResolved: gameOver={gameOver}, loserIndex={loserIndex}, players={_localState.Players.Count}");

        Card bluffCard = new Card((Suit)suitInt, (Rank)rankInt);
        string doubterName = doubterIndex < _localState.Players.Count
            ? _localState.Players[doubterIndex].Name : "Player";
        string bluffDeclared = _localState.LastDeclaredRank.ToString();
        OnCardRevealed?.Invoke(bluffCard, doubterName, caughtLying, "Bluff", bluffDeclared, pickedCardIndex);

        if (!Object.HasStateAuthority)
        {
            // Whoever took the pile gets placeholder cards so the badge count updates correctly.
            // Real cards are sent via RPC_ReceiveCards to the local player if they're the receiver.
            int receiverIndex = caughtLying ? betPlayerIndex : doubterIndex;
            if (receiverIndex != _localPlayerIndex && receiverIndex < _localState.Players.Count)
            {
                Player receiver = _localState.Players[receiverIndex];
                for (int i = 0; i < pileSize; i++)
                    receiver.AddCard(new Card(Suit.Spades, Rank.Ace));
            }
            _localState.Pile.Clear();
            _localState.ClearLastBet();
            _localState.ForceSetCurrentPlayer(CurrentPlayerIndex);
        }

        if (gameOver && loserIndex >= 0)
        {
            string loserName = _localState.Players.Count > loserIndex
                ? _localState.Players[loserIndex].Name : "Unknown";
            OnGameOver?.Invoke(loserName);
        }

        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    // ── HELPERS ──────────────────────────────────────────────

    private bool TryGetCurrentPlayerIndex(RpcInfo info, out int playerIndex)
    {
        PlayerRef sender = info.Source;
        if (sender == PlayerRef.None || !_playerIndexMap.ContainsKey(sender))
            playerIndex = _localPlayerIndex;
        else
            playerIndex = _playerIndexMap[sender];

        if (playerIndex < 0 || playerIndex >= _localState.Players.Count) return false;
        if (_localState.CurrentPlayerIndex != playerIndex) return false;
        return true;
    }

    private string GetLocalPlayerId()
    {
        if (_localPlayerIndex >= 0)
            return _localPlayerIndex.ToString();
        if (Runner == null) return "0";
        if (_playerIndexMap.TryGetValue(Runner.LocalPlayer, out int index))
            return index.ToString();
        return "0";
    }

    private void SendCardsToPlayer(int receiverIndex, List<Card> cards)
    {
        int[] suits = new int[cards.Count];
        int[] ranks = new int[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            suits[i] = (int)cards[i].Suit;
            ranks[i] = (int)cards[i].Rank;
        }
        RPC_ReceiveCards(receiverIndex, suits, ranks);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReceiveCards(int receiverIndex, int[] suits, int[] ranks)
    {
        // Only the receiving player processes this
        if (_localPlayerIndex != receiverIndex) return;

        Player localPlayer = _localState.Players[receiverIndex];
        for (int i = 0; i < suits.Length; i++)
            localPlayer.AddCard(new Card((Suit)suits[i], (Rank)ranks[i]));

        Debug.Log($"Player {receiverIndex} received {suits.Length} real cards from pile");
        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    public void HandlePlayerDisconnected(PlayerRef player)
    {
        // Only the host has the playerIndexMap; clients ignore this
        if (!Object.HasStateAuthority) return;

        // Spectator disconnected — just remove from list, no game impact
        if (_spectatorRefs.Contains(player))
        {
            _spectatorRefs.Remove(player);
            SpectatorCount = _spectatorRefs.Count;
            return;
        }

        if (!_playerIndexMap.TryGetValue(player, out int idx)) return;

        string name = idx < _localState.Players.Count
            ? _localState.Players[idx].Name : "Player";

        _playerIndexMap.Remove(player);

        if (!GameStarted)
        {
            // Lobby: just decrement the visible count
            int remaining = _playerNames.Count - (_playerIndexMap.Count < _playerNames.Count
                ? _playerNames.Count - _playerIndexMap.Count : 0);
            RPC_UpdatePlayerCount(Mathf.Max(0, _playerNames.Count - 1));
            return;
        }

        // Mid-game: start 30s grace period before ending the game
        _pendingDisconnectName = name;
        _pendingReconnectIndex[name] = idx;  // remember slot for potential reconnect
        DisconnectGraceTimer = TickTimer.CreateFromSeconds(Runner, 30f);
        RPC_NotifyDisconnectGrace(name, 30);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyDisconnectGrace(string playerName, int seconds)
    {
        OnDisconnectGrace?.Invoke(playerName, seconds);
    }

    /// <summary>Any connected player can call this to immediately end the grace period.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ForceEndGrace(RpcInfo info = default)
    {
        if (!DisconnectGraceTimer.IsRunning) return;
        DisconnectGraceTimer = default;
        string name = _pendingDisconnectName;
        _pendingDisconnectName = "";
        _pendingReconnectIndex.Remove(name);
        RPC_PlayerDisconnectedMidGame(name);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayerDisconnectedMidGame(string playerName)
    {
        // Fires OnGameOver on every client — the existing overlay handles display
        OnGameOver?.Invoke($"{playerName} disconnected");
    }

    // ── TURN TIMER ───────────────────────────────────────────

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!GameStarted || _localState == null) return;
        if (_localState.Phase == GamePhase.GameOver) return;

        // Disconnect grace timer — end game when it expires
        if (DisconnectGraceTimer.IsRunning && DisconnectGraceTimer.Expired(Runner))
        {
            DisconnectGraceTimer = default;
            string name = _pendingDisconnectName;
            _pendingDisconnectName = "";
            _pendingReconnectIndex.Remove(name);
            RPC_PlayerDisconnectedMidGame(name);
            return;
        }

        if (!TurnTimer.IsRunning || !TurnTimer.Expired(Runner)) return;

        TurnTimer = default; // prevent re-entry
        AutoActForCurrentPlayer();
    }

    private void ResetTurnTimer()
    {
        if (Object.HasStateAuthority)
            TurnTimer = TickTimer.CreateFromSeconds(Runner, TurnTimeout > 0f ? TurnTimeout : 30f);
    }

    // Returns seconds remaining; falls back to full timeout when timer isn't running.
    public float GetTurnTimeRemaining()
    {
        float timeout = TurnTimeout > 0f ? TurnTimeout : 30f;
        if (Runner == null || !TurnTimer.IsRunning) return timeout;
        float? rem = TurnTimer.RemainingTime(Runner);
        return rem.HasValue ? rem.Value : timeout;
    }

    private void AutoActForCurrentPlayer()
    {
        int playerIndex = _localState.CurrentPlayerIndex;
        Player player = _localState.CurrentPlayer;
        if (player == null) return;

        string playerName = player.Name;

        if (_localState.HasActiveBet && _localState.LastBetPlayer != player)
        {
            // Auto-believe: pick card 0 from the last bet
            int cardIdx = 0;
            int pileSize = _localState.Pile.Count;
            bool correct = GameRules.ResolveBelieve(_localState, cardIdx);
            Card revealedCard = _localState.LastBetCards[cardIdx];

            if (correct)
            {
                _localState.ResolveDiscard();
            }
            else
            {
                List<Card> pileCards = new List<Card>(_localState.Pile);
                _localState.GivePileToPlayer(player);
                SendCardsToPlayer(playerIndex, pileCards);
            }

            _localState.CheckLoser();
            _localState.NextTurn(correct, playerIndex);

            PileCount    = _localState.Pile.Count;
            DiscardCount = _localState.Discard.Count;
            CurrentPlayerIndex = _localState.CurrentPlayerIndex;
            if (_localState.Phase == GamePhase.GameOver) { GameOver = true; }
            else ResetTurnTimer();

            RPC_TurnTimedOut(playerName);
            RPC_BelieveResolved((int)revealedCard.Suit, (int)revealedCard.Rank,
                correct, playerIndex, cardIdx, pileSize,
                _localState.Phase == GamePhase.GameOver,
                _localState.Loser != null ? int.Parse(_localState.Loser.Id) : -1);
            return;
        }

        if (player.HasCards())
        {
            // Auto-bet: 1 card; use active rank if re-bet, else Ace
            Rank rank = _localState.HasActiveBet ? _localState.LastDeclaredRank : Rank.Ace;
            List<Card> cards = new List<Card> { player.Hand[0] };

            if (!GameRules.CanPlaceBet(_localState, player, cards, rank)) return;

            _localState.PlaceBet(player, cards, rank);
            _localState.NextTurn();

            LastDeclaredRankInt = (int)rank;
            LastBetPlayerIndex  = playerIndex;
            LastBetCount        = 1;
            PileCount           = _localState.Pile.Count;
            CurrentPlayerIndex  = _localState.CurrentPlayerIndex;
            ResetTurnTimer();

            RPC_TurnTimedOut(playerName);
            RPC_BetPlaced(playerIndex, new int[] { 0 }, (int)rank, _localState.CurrentPlayerIndex);
            return;
        }

        // No cards + no active bet — AdvanceToNextActivePlayer should prevent this,
        // but guard here to avoid an infinite timer reset loop.
        _localState.NextTurn();
        CurrentPlayerIndex = _localState.CurrentPlayerIndex;
        if (_localState.Phase != GamePhase.GameOver)
            ResetTurnTimer();
        OnStateRefresh?.Invoke(_localState, _localPlayerIndex.ToString());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TurnTimedOut(string playerName)
    {
        OnTurnTimedOut?.Invoke(playerName);
    }

    // ── IGameManager ─────────────────────────────────────────

    public GameState GetState() => _localState;
    public void PlaceBet(int[] cardIndices, int declaredRankInt) => RPC_PlaceBet(cardIndices, declaredRankInt);
    void IGameManager.Believe(int cardIndex) => RPC_Believe(cardIndex);
    void IGameManager.Bluff(int cardIndex) => RPC_Bluff(cardIndex);
    // NetworkBool IsShortDeck is the networked property; expose as bool through the interface
    bool IGameManager.IsShortDeck => IsShortDeck;

    public GameState GetLocalState() => _localState;
}