using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public sealed class NetBootstrap : MonoBehaviour
{
    public static NetBootstrap Instance { get; private set; }

    [Header("Session")]
    [SerializeField] private int _maxPlayers = 4;
    [SerializeField] private string _defaultSessionName = "Session";

    [Header("Scenes")]
    [SerializeField] private string _lobbySceneName = "Lobby";
    [SerializeField] private string _defaultGameSceneName = "Game";

    [Header("Player Prefab")]
    [SerializeField] private NetworkObject _playerPrefab;

    [Header("Temporary Roam Spawns")]
    [SerializeField] private Vector3 _roamSpawnOrigin = Vector3.zero;
    [SerializeField] private float _roamSpawnSpacing = 3f;

    [Header("Final Start Spawns")]
    [SerializeField] private Vector3 _finalSpawnOrigin = new Vector3(0f, 1f, 0f);
    [SerializeField] private float _finalSpawnSpacing = 3f;

    private NetworkManager _networkManager;
    private UnityTransport _transport;

    private bool _servicesReady;
    private bool _initInProgress;

    private ISession _currentSession;
    private string _currentSessionCode;

    private string _activeTargetGameScene = "";
    private bool _loadingLobby;
    private bool _loadingGame;

    private readonly Dictionary<ulong, Vector3> _reservedRoamSpawns = new();
    private readonly Dictionary<ulong, Vector3> _reservedFinalSpawns = new();

    public string CurrentSessionCode => _currentSessionCode;
    public bool IsServer => _networkManager != null && _networkManager.IsServer;
    public bool IsInLobbyScene => SceneManager.GetActiveScene().name == _lobbySceneName;

    public event Action<string> OnAllClientsLoadedGameScene;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _networkManager = GetComponent<NetworkManager>();
        _transport = GetComponent<UnityTransport>();

        if (_networkManager == null || _transport == null)
        {
            Debug.LogError("[NetBootstrap] Missing NetworkManager or UnityTransport.");
            return;
        }

        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        _networkManager.OnServerStarted += OnServerStarted;
        _networkManager.ConnectionApprovalCallback = ApprovalCheck;

        await InitializeServicesAsync();
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback -= OnClientConnected;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            _networkManager.OnServerStarted -= OnServerStarted;
            _networkManager.ConnectionApprovalCallback = null;

            if (_networkManager.SceneManager != null)
            {
                _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                _networkManager.SceneManager.OnLoadComplete -= OnLoadComplete;
            }
        }

        if (Instance == this) Instance = null;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false;
        response.Pending = false;
    }

    public async Task<bool> InitializeServicesAsync()
    {
        if (_servicesReady) return true;

        while (_initInProgress)
            await Task.Yield();

        if (_servicesReady) return true;

        _initInProgress = true;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            _servicesReady = true;
            Debug.Log($"[NetBootstrap] Services initialized. PlayerId={AuthenticationService.Instance.PlayerId}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetBootstrap] Failed to initialize services.\n{e}");
            return false;
        }
        finally
        {
            _initInProgress = false;
        }
    }

    public async void StartSessionHost(string sessionName, bool isPublic)
    {
        bool ready = await InitializeServicesAsync();
        if (!ready) return;

        ShutdownIfRunning();

        try
        {
            string finalName = string.IsNullOrWhiteSpace(sessionName) ? _defaultSessionName : sessionName.Trim();

            var options = new SessionOptions
            {
                MaxPlayers = _maxPlayers,
                Name = finalName,
                IsPrivate = !isPublic
            }.WithRelayNetwork();

            _currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            _currentSessionCode = _currentSession.Code;

            Debug.Log($"[NetBootstrap] Session created. Id={_currentSession.Id}, Code={_currentSessionCode}, Name={finalName}, Public={isPublic}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetBootstrap] Failed to host session.\n{e}");
        }
    }

    public async Task JoinSessionByCodeAsync(string joinCode)
    {
        bool ready = await InitializeServicesAsync();
        if (!ready) return;
        if (string.IsNullOrWhiteSpace(joinCode)) return;

        ShutdownIfRunning();

        try
        {
            _currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim().ToUpperInvariant());
            _currentSessionCode = _currentSession.Code;

            Debug.Log($"[NetBootstrap] Joined session by code. Id={_currentSession.Id}, Code={_currentSessionCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetBootstrap] Failed to join session by code.\n{e}");
        }
    }

    public async Task JoinSessionByIdAsync(string sessionId)
    {
        bool ready = await InitializeServicesAsync();
        if (!ready) return;
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        ShutdownIfRunning();

        try
        {
            _currentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
            _currentSessionCode = _currentSession.Code;

            Debug.Log($"[NetBootstrap] Joined session by id. Id={_currentSession.Id}, Code={_currentSessionCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetBootstrap] Failed to join session by id.\n{e}");
        }
    }

    public void ShutdownIfRunning()
    {
        _currentSessionCode = string.Empty;
        _activeTargetGameScene = "";
        _loadingLobby = false;
        _loadingGame = false;

        _reservedRoamSpawns.Clear();
        _reservedFinalSpawns.Clear();

        if (_networkManager != null && _networkManager.IsListening)
            _networkManager.Shutdown();
    }

    public void LoadLobbyScene()
    {
        if (_networkManager == null || !_networkManager.IsServer) return;
        if (_loadingLobby) return;
        if (SceneManager.GetActiveScene().name == _lobbySceneName) return;

        _loadingLobby = true;
        _networkManager.SceneManager.LoadScene(_lobbySceneName, LoadSceneMode.Single);
    }

    public void StartGameFromLobby(string sceneName)
    {
        if (_networkManager == null || !_networkManager.IsServer) return;
        if (!IsInLobbyScene) return;
        if (_loadingGame) return;

        string targetScene = string.IsNullOrWhiteSpace(sceneName) ? _defaultGameSceneName : sceneName.Trim();

        _activeTargetGameScene = targetScene;
        _loadingGame = true;

        ReserveGameSpawns();
        _networkManager.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }

    public void StartGameFromLobby()
    {
        StartGameFromLobby(_defaultGameSceneName);
    }

    private void OnServerStarted()
    {
        if (_networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;

            _networkManager.SceneManager.OnLoadComplete -= OnLoadComplete;
            _networkManager.SceneManager.OnLoadComplete += OnLoadComplete;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!_networkManager.IsServer) return;

        string activeScene = SceneManager.GetActiveScene().name;

        if (activeScene == _lobbySceneName || (_loadingGame && activeScene == _activeTargetGameScene))
            return;

        LoadLobbyScene();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetBootstrap] Client disconnected: {clientId}");
        _reservedRoamSpawns.Remove(clientId);
        _reservedFinalSpawns.Remove(clientId);
    }

    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (!_networkManager.IsServer) return;
        if (!_loadingGame) return;
        if (!string.Equals(sceneName, _activeTargetGameScene, StringComparison.Ordinal)) return;

        SpawnOrMovePlayerForClient(clientId, GetReservedRoamSpawn(clientId));
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!_networkManager.IsServer) return;

        if (string.Equals(sceneName, _lobbySceneName, StringComparison.Ordinal))
        {
            _loadingLobby = false;
            LobbyState lobbyState = FindFirstObjectByType<LobbyState>();
            if (lobbyState != null) lobbyState.OnHostLobbySceneLoaded();
            return;
        }

        if (_loadingGame && string.Equals(sceneName, _activeTargetGameScene, StringComparison.Ordinal))
        {
            ResetPlayersToFinalGameSpawns();
            _loadingGame = false;

            OnAllClientsLoadedGameScene?.Invoke(sceneName);
        }
    }

    private void ReserveGameSpawns()
    {
        _reservedRoamSpawns.Clear();
        _reservedFinalSpawns.Clear();

        foreach (ulong clientId in _networkManager.ConnectedClientsIds)
        {
            _reservedRoamSpawns[clientId] = GetRoamSpawn(clientId);
            _reservedFinalSpawns[clientId] = GetFinalSpawn(clientId);
        }
    }

    private Vector3 GetReservedRoamSpawn(ulong clientId)
    {
        if (_reservedRoamSpawns.TryGetValue(clientId, out Vector3 pos))
            return pos;

        pos = GetRoamSpawn(clientId);
        _reservedRoamSpawns[clientId] = pos;
        return pos;
    }

    private Vector3 GetReservedFinalSpawn(ulong clientId)
    {
        if (_reservedFinalSpawns.TryGetValue(clientId, out Vector3 pos))
            return pos;

        pos = GetFinalSpawn(clientId);
        _reservedFinalSpawns[clientId] = pos;
        return pos;
    }

    private Vector3 GetRoamSpawn(ulong clientId)
    {
        int index = GetClientIndex(clientId);
        return _roamSpawnOrigin + new Vector3(index * _roamSpawnSpacing, 1f, 0f);
    }

    private Vector3 GetFinalSpawn(ulong clientId)
    {
        int index = GetClientIndex(clientId);
        return _finalSpawnOrigin + new Vector3(index * _finalSpawnSpacing, 0f, 0f);
    }

    private int GetClientIndex(ulong clientId)
    {
        int index = 0;
        foreach (ulong id in _networkManager.ConnectedClientsIds)
        {
            if (id == clientId)
                return index;
            index++;
        }

        return 0;
    }

    private void SpawnOrMovePlayerForClient(ulong clientId, Vector3 spawnPos)
    {
        if (_playerPrefab == null)
        {
            Debug.LogError("[NetBootstrap] Player prefab is not assigned.");
            return;
        }

        NetworkObject existing = _networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        if (existing != null)
        {
            MovePlayer(existing, spawnPos);
            return;
        }

        NetworkObject player = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
        player.SpawnAsPlayerObject(clientId, true);
    }

    private void ResetPlayersToFinalGameSpawns()
    {
        foreach (ulong clientId in _networkManager.ConnectedClientsIds)
        {
            NetworkObject player = _networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            if (player == null) continue;

            MovePlayer(player, GetReservedFinalSpawn(clientId));
        }
    }

    private void MovePlayer(NetworkObject player, Vector3 worldPos)
    {
        if (player == null) return;

        player.transform.SetPositionAndRotation(worldPos, Quaternion.identity);

        if (player.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.position = worldPos;
            rb.rotation = Quaternion.identity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}