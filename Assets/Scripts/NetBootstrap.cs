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

    private NetworkManager _networkManager;
    private UnityTransport _transport;

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
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback -= OnClientConnected;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            _networkManager.OnServerStarted -= OnServerStarted;
        }

        if (Instance == this) Instance = null;
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
    public void StartClient(TMP_InputField input)
    {
        ShutdownIfRunning();

        string ip = input != null ? input.text : _defaultIP;
        if (string.IsNullOrWhiteSpace(ip)) ip = _defaultIP;



        _transport.SetConnectionData(ip.Trim(), _port);
        bool success = _networkManager.StartClient();

        Debug.Log(success
            ? $"[NetBootstrap] Client connecting to {ip}:{_port}."
            : "[NetBootstrap] Failed to start client.");
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
        if (_networkManager == null || !_networkManager.IsServer) return;
        _networkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }

    private void OnServerStarted()
    {
        Debug.Log("[NetBootstrap] Server started callback.");
    }

    private bool _gameSceneLoaded;
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetBootstrap] Client connected: {clientId}");

        if (!_networkManager.IsServer || _gameSceneLoaded) return;

        _gameSceneLoaded = true;
        _networkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetBootstrap] Client disconnected: {clientId}");
    }
}