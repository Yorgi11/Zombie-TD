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

    [Header("Player Spawning")]
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private Vector3 _spawnOrigin = Vector3.zero;
    [SerializeField] private float _spawnSpacing = 3f;

    private NetworkManager _networkManager;
    private UnityTransport _transport;

    private bool _gameSceneLoaded;
    private bool _servicesReady;
    private bool _initInProgress;

    private ISession _currentSession;
    private string _currentSessionCode;

    public string CurrentSessionCode => _currentSessionCode;

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
                _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
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
        {
            await Task.Yield();
        }

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
        _gameSceneLoaded = false;
        _currentSessionCode = string.Empty;

        if (_networkManager != null && _networkManager.IsListening)
            _networkManager.Shutdown();
    }

    public void LoadGameScene()
    {
        if (_networkManager == null || !_networkManager.IsServer || _gameSceneLoaded) return;

        _gameSceneLoaded = true;
        _networkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }

    private void OnServerStarted()
    {
        if (_networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!_networkManager.IsServer) return;

        if (!_gameSceneLoaded)
        {
            LoadGameScene();
            return;
        }

        if (SceneManager.GetActiveScene().name == "Game")
            SpawnPlayerForClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetBootstrap] Client disconnected: {clientId}");
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!_networkManager.IsServer) return;
        if (sceneName != "Game") return;

        SpawnPlayersForConnectedClients();
    }

    private void SpawnPlayersForConnectedClients()
    {
        if (_playerPrefab == null)
        {
            Debug.LogError("[NetBootstrap] Player prefab is not assigned.");
            return;
        }

        foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            SpawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (_playerPrefab == null) return;
        if (_networkManager.SpawnManager.GetPlayerNetworkObject(clientId) != null) return;

        Vector3 spawnPos = _spawnOrigin + new Vector3((_networkManager.ConnectedClientsIds.Count - 1) * _spawnSpacing, 1f, 0f);
        NetworkObject player = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
        player.SpawnAsPlayerObject(clientId, true);
    }
}