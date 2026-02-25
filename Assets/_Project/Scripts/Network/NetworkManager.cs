using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance { get; private set; }

    private NetworkRunner _runner;

    public bool IsConnected => _runner != null && _runner.IsRunning;
    public bool IsHost => _runner != null && _runner.IsServer;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public async Task CreateRoom(string roomCode, string playerName)
    {
        Debug.Log($"Creating room: {roomCode}");
        await StartFusion(GameMode.Host, roomCode);
    }

    public async Task JoinRoom(string roomCode, string playerName)
    {
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
            GameMode = mode,
            SessionName = roomCode,
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
            Debug.Log($"Fusion started! Mode: {mode}, Room: {roomCode}");
        else
            Debug.LogError($"Fusion failed: {result.ShutdownReason}");
    }

    public void Disconnect()
    {
        if (_runner != null)
        {
            _runner.Shutdown();
            _runner = null;
        }
    }

    // ── INetworkRunnerCallbacks ──────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        => Debug.Log($"Player joined: {player}");

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        => Debug.Log($"Player left: {player}");

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        => Debug.Log($"Shutdown: {reason}");

    public void OnConnectRequest(NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress,
        NetConnectFailedReason reason)
        => Debug.LogError($"Connect failed: {reason}");

    public void OnDisconnectedFromServer(NetworkRunner runner,
        NetDisconnectReason reason)
        => Debug.Log($"Disconnected: {reason}");

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