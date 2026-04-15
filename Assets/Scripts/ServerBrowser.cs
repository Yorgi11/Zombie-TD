using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public sealed class ServerBrowser : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField _serverNameInput;
    [SerializeField] private TMP_InputField _serverIPInput;

    [Header("UI")]
    [SerializeField] private Transform _entryParent;
    [SerializeField] private ServerEntryUI _entryPrefab;

    [Header("Defaults")]
    [SerializeField] private ushort _defaultPort = 7777;
    [SerializeField] private float _refreshInterval = 5f;
    [SerializeField] private float _pingTimeoutSeconds = 0.75f;

    private readonly List<SavedServerEntry> _servers = new();
    private readonly List<ServerEntryUI> _spawnedEntries = new();

    private Coroutine _refreshLoopRoutine;
    private bool _refreshInProgress;

    private string SavePath => Path.Combine(Application.persistentDataPath, "saved_servers.json");

    private void Start()
    {
        LoadServers();
        RebuildUI();
        StartRefreshLoopIfBootstrap();
        RefreshAllNow();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        StopRefreshLoop();
    }

    public void AddServerFromInput()
    {
        string name = _serverNameInput != null ? _serverNameInput.text.Trim() : string.Empty;
        string ip = _serverIPInput != null ? _serverIPInput.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(ip))
        {
            Debug.LogWarning("[ServerBrowser] Cannot add server. IP is blank.");
            return;
        }

        if (ContainsServer(ip, _defaultPort))
        {
            Debug.LogWarning($"[ServerBrowser] Server already saved: {ip}:{_defaultPort}");
            return;
        }

        SavedServerEntry entry = new()
        {
            Name = name,
            IP = ip,
            Port = _defaultPort
        };

        _servers.Add(entry);
        SaveServers();
        RebuildUI();
        RefreshAllNow();

        if (_serverNameInput != null) _serverNameInput.text = string.Empty;
        if (_serverIPInput != null) _serverIPInput.text = string.Empty;
    }

    public void RemoveServer(SavedServerEntry entry)
    {
        if (entry == null) return;

        _servers.Remove(entry);
        SaveServers();
        RebuildUI();
    }

    public void JoinServer(SavedServerEntry entry)
    {
        if (entry == null) return;

        Debug.Log($"[ServerBrowser] Joining server {entry.IP}:{entry.Port}");
        NetBootstrap.Instance.StartClient(entry.IP, entry.Port);
    }

    public void RefreshAllNow()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_refreshInProgress) return;
        _ = RefreshAllNowAsync();
    }

    private async Task RefreshAllNowAsync()
    {
        _refreshInProgress = true;

        try
        {
            for (int i = 0; i < _spawnedEntries.Count; i++)
            {
                if (_spawnedEntries[i] != null)
                    _spawnedEntries[i].SetRefreshing();
            }

            for (int i = 0; i < _servers.Count; i++)
            {
                if (i >= _spawnedEntries.Count) break;

                SavedServerEntry entry = _servers[i];
                ServerEntryUI ui = _spawnedEntries[i];
                if (ui == null || entry == null) continue;

                ServerQueryResult result = await QueryServerAsync(entry);

                if (result.Success)
                    ui.SetOnline(result.Response, result.PingMs);
                else
                    ui.SetOffline();
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task<ServerQueryResult> QueryServerAsync(SavedServerEntry entry)
    {
        ServerQueryResult result = new() { Success = false, PingMs = 999 };

        try
        {
            using UdpClient udp = new();
            udp.Client.SendTimeout = Mathf.CeilToInt(_pingTimeoutSeconds * 1000f);
            udp.Client.ReceiveTimeout = Mathf.CeilToInt(_pingTimeoutSeconds * 1000f);

            ServerStatusRequest request = new();
            string json = JsonUtility.ToJson(request);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            Stopwatch stopwatch = Stopwatch.StartNew();

            await udp.SendAsync(payload, payload.Length, entry.IP, entry.Port + 1);

            Task<UdpReceiveResult> receiveTask = udp.ReceiveAsync();
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(_pingTimeoutSeconds));

            Task completed = await Task.WhenAny(receiveTask, timeoutTask);
            if (completed != receiveTask)
                return result;

            UdpReceiveResult packet = receiveTask.Result;
            stopwatch.Stop();

            string responseJson = Encoding.UTF8.GetString(packet.Buffer);
            ServerStatusResponse response = JsonUtility.FromJson<ServerStatusResponse>(responseJson);

            if (response == null)
                return result;

            result.Success = true;
            result.Response = response;
            result.PingMs = Mathf.Clamp((int)stopwatch.ElapsedMilliseconds, 0, 999);
            return result;
        }
        catch
        {
            return result;
        }
    }

    private void LoadServers()
    {
        _servers.Clear();

        if (!File.Exists(SavePath))
        {
            Debug.Log($"[ServerBrowser] No saved server file found at: {SavePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            SavedServerData data = JsonUtility.FromJson<SavedServerData>(json);

            if (data != null && data.Servers != null)
                _servers.AddRange(data.Servers);

            Debug.Log($"[ServerBrowser] Loaded {_servers.Count} saved server(s).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerBrowser] Failed to load saved servers.\n{e}");
        }
    }

    private void SaveServers()
    {
        try
        {
            SavedServerData data = new()
            {
                Servers = new List<SavedServerEntry>(_servers)
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

            Debug.Log($"[ServerBrowser] Saved {_servers.Count} server(s) to: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerBrowser] Failed to save servers.\n{e}");
        }
    }

    private bool ContainsServer(string ip, ushort port)
    {
        for (int i = 0; i < _servers.Count; i++)
        {
            if (_servers[i].IP == ip && _servers[i].Port == port)
                return true;
        }

        return false;
    }

    private void RebuildUI()
    {
        ClearSpawnedUI();

        for (int i = 0; i < _servers.Count; i++)
        {
            ServerEntryUI ui = Instantiate(_entryPrefab, _entryParent);
            ui.Bind(_servers[i], this);
            _spawnedEntries.Add(ui);
        }
    }

    private void ClearSpawnedUI()
    {
        for (int i = 0; i < _spawnedEntries.Count; i++)
        {
            if (_spawnedEntries[i] != null)
                Destroy(_spawnedEntries[i].gameObject);
        }

        _spawnedEntries.Clear();
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (newScene.name == "Bootstrap")
        {
            StartRefreshLoopIfBootstrap();
            RefreshAllNow();
        }
        else
        {
            StopRefreshLoop();
        }
    }

    private void StartRefreshLoopIfBootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Bootstrap") return;
        if (_refreshLoopRoutine != null) return;

        _refreshLoopRoutine = StartCoroutine(RefreshLoop());
    }

    private void StopRefreshLoop()
    {
        if (_refreshLoopRoutine == null) return;
        StopCoroutine(_refreshLoopRoutine);
        _refreshLoopRoutine = null;
    }

    private IEnumerator RefreshLoop()
    {
        while (SceneManager.GetActiveScene().name == "Bootstrap")
        {
            RefreshAllNow();
            yield return new WaitForSeconds(_refreshInterval);
        }

        _refreshLoopRoutine = null;
    }

    private struct ServerQueryResult
    {
        public bool Success;
        public int PingMs;
        public ServerStatusResponse Response;
    }
}