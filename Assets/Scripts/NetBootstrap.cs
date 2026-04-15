using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetBootstrap : MonoBehaviour
{
    public static NetBootstrap Instance { get; private set; }

    [Header("Connection")]
    [SerializeField] private string _defaultIP = "127.0.0.1";
    [SerializeField] private ushort _port = 7777;

    [Header("Player Spawning")]
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private Vector3 _spawnOrigin = Vector3.zero;
    [SerializeField] private float _spawnSpacing = 3f;

    private NetworkManager _networkManager;
    private UnityTransport _transport;
    private bool _gameSceneLoaded;

    public string DefaultIP => _defaultIP;
    public ushort Port => _port;
    public bool IsOnline => _networkManager != null && _networkManager.IsListening;

    private void Awake()
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

        if (_networkManager == null)
        {
            Debug.LogError("[NetBootstrap] NetworkManager missing.");
            return;
        }

        if (_transport == null)
        {
            Debug.LogError("[NetBootstrap] UnityTransport missing.");
            return;
        }

        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        _networkManager.OnServerStarted += OnServerStarted;
        _networkManager.ConnectionApprovalCallback = ApprovalCheck;
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

    public void StartHost()
    {
        ShutdownIfRunning();

        _transport.SetConnectionData("0.0.0.0", _port);
        bool success = _networkManager.StartHost();

        Debug.Log(success
            ? $"[NetBootstrap] Host started on port {_port}."
            : "[NetBootstrap] Failed to start host.");
    }

    public void StartClient(string ip)
    {
        ShutdownIfRunning();

        if (string.IsNullOrWhiteSpace(ip)) ip = _defaultIP;

        _transport.SetConnectionData(ip.Trim(), _port);
        bool success = _networkManager.StartClient();

        Debug.Log(success
            ? $"[NetBootstrap] Client connecting to {ip}:{_port}."
            : "[NetBootstrap] Failed to start client.");
    }

    public void StartClient(string ip, ushort port)
    {
        ShutdownIfRunning();

        if (string.IsNullOrWhiteSpace(ip)) ip = _defaultIP;

        _transport.SetConnectionData(ip.Trim(), port);
        bool success = _networkManager.StartClient();

        Debug.Log(success
            ? $"[NetBootstrap] Client connecting to {ip}:{port}."
            : "[NetBootstrap] Failed to start client.");
    }

    public void StartClient(TMP_InputField input)
    {
        string ip = input != null ? input.text : _defaultIP;
        StartClient(ip);
    }

    public void StartServer()
    {
        ShutdownIfRunning();

        _transport.SetConnectionData("0.0.0.0", _port);
        bool success = _networkManager.StartServer();

        Debug.Log(success
            ? $"[NetBootstrap] Server started on port {_port}."
            : "[NetBootstrap] Failed to start server.");
    }

    public void ShutdownIfRunning()
    {
        _gameSceneLoaded = false;

        if (_networkManager != null && _networkManager.IsListening)
        {
            _networkManager.Shutdown();
        }
    }

    public void LoadGameScene()
    {
        if (_networkManager == null || !_networkManager.IsServer || _gameSceneLoaded) return;
        if (_networkManager.SceneManager == null)
        {
            Debug.LogError("[NetBootstrap] NetworkSceneManager is null.");
            return;
        }

        _gameSceneLoaded = true;
        _networkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }

    private void OnServerStarted()
    {
        Debug.Log("[NetBootstrap] Server started callback.");

        if (_networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }
        else
        {
            Debug.LogError("[NetBootstrap] NetworkSceneManager is null after server start.");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetBootstrap] Client connected: {clientId}");
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

        int index = 0;
        foreach (ulong clientId in _networkManager.ConnectedClientsIds)
        {
            if (_networkManager.SpawnManager.GetPlayerNetworkObject(clientId) != null)
                continue;

            Vector3 spawnPos = _spawnOrigin + new Vector3(index * _spawnSpacing, 1f, 0f);
            Quaternion spawnRot = Quaternion.identity;

            NetworkObject player = Instantiate(_playerPrefab, spawnPos, spawnRot);
            player.SpawnAsPlayerObject(clientId, true);
            index++;
        }
    }
}