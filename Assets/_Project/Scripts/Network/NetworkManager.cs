using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance { get; private set; }

    [SerializeField] private NetworkObject _gameManagerPrefab;

    private NetworkRunner _runner;
    private bool _intentionalDisconnect;

    public bool IsConnected => _runner != null && _runner.IsRunning;
    public bool IsHost => _runner != null && _runner.IsServer;
    public int MaxPlayers { get; private set; } = 6;
    private string _localPlayerName = "";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Mobile optimisations — safe to call on all platforms
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        // Portrait-only; comment out if landscape is ever needed
        Screen.orientation = ScreenOrientation.Portrait;
    }

    public async Task CreateRoom(string roomCode, string playerName, int maxPlayers = 6)
    {
        _localPlayerName = playerName;
        MaxPlayers = maxPlayers;
        Debug.Log($"Creating room: {roomCode} (max {maxPlayers})");
        await StartFusion(GameMode.Host, roomCode);
    }

    public async Task JoinRoom(string roomCode, string playerName)
    {
        _localPlayerName = playerName;
        Debug.Log($"Joining room: {roomCode}");
        await StartFusion(GameMode.Client, roomCode);
    }

    private async Task StartFusion(GameMode mode, string roomCode)
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        var scene = SceneRef.FromIndex(0);
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(scene);

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode     = mode,
            SessionName  = roomCode,
            Scene        = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log($"Fusion started! Mode: {mode}, Room: {roomCode}");
        }
        else
        {
            _intentionalDisconnect = true; // don't show connection-lost overlay
            Debug.LogError($"Fusion failed: {result.ShutdownReason}");
            // Destroy the failed runner so the next attempt starts clean
            if (_runner != null) { Destroy(_runner); _runner = null; }
            throw new System.Exception(result.ShutdownReason.ToString());
        }
    }

    public void Disconnect()
    {
        _intentionalDisconnect = true;
        if (_runner != null)
        {
            _runner.Shutdown();
            _runner = null;
        }
    }

    // ── INetworkRunnerCallbacks ──────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");

        if (runner.IsServer)
        {
            if (NetworkedGameManager.Instance == null)
            {
                runner.Spawn(_gameManagerPrefab, Vector3.zero, Quaternion.identity);
                Debug.Log("NetworkedGameManager spawned!");
            }
        }

        // Register local player when they join
        if (player == runner.LocalPlayer)
        {
            StartCoroutine(RegisterWhenReady(player));
        }
    }

    private System.Collections.IEnumerator RegisterWhenReady(PlayerRef player)
    {
        // Wait until NetworkedGameManager is spawned and ready
        while (NetworkedGameManager.Instance == null)
            yield return null;

        Debug.Log($"Registering player: {_localPlayerName}");
        NetworkedGameManager.Instance.LocalPlayerJoined(player, _localPlayerName);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");
        NetworkedGameManager.Instance?.HandlePlayerDisconnected(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        Debug.Log($"Shutdown: {reason}");
        if (!_intentionalDisconnect)
            NetworkedGameManager.NotifyConnectionLost();
        _intentionalDisconnect = false;
    }

    public void OnConnectRequest(NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress,
        NetConnectFailedReason reason)
        => Debug.LogError($"Connect failed: {reason}");

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from server: {reason}");
        if (!_intentionalDisconnect)
            NetworkedGameManager.NotifyConnectionLost();
    }

    public void OnConnectedToServer(NetworkRunner runner)
        => Debug.Log("Connected to server!");

    public void OnSessionListUpdated(NetworkRunner runner,
        List<SessionInfo> sessionList)
    { }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player,
        NetworkInput input)
    { }

    public void OnUserSimulationMessage(NetworkRunner runner,
        SimulationMessagePtr message)
    { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner,
        Dictionary<string, object> data)
    { }

    public void OnHostMigration(NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player,
        ReliableKey key, ArraySegment<byte> data)
    { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player,
        ReliableKey key, float progress)
    { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj,
        PlayerRef player)
    { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj,
        PlayerRef player)
    { }
}