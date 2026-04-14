using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public sealed class ServerBrowser : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField _serverIPInput;

    [Header("UI")]
    [SerializeField] private Transform _entryParent;
    [SerializeField] private ServerEntryUI _entryPrefab;

    [Header("Defaults")]
    [SerializeField] private ushort _defaultPort = 7777;

    private readonly List<SavedServerEntry> _servers = new();
    private readonly List<ServerEntryUI> _spawnedEntries = new();

    private string SavePath => Path.Combine(Application.persistentDataPath, "saved_servers.json");

    private void Start()
    {
        LoadServers();
        RebuildUI();
    }

    public void AddServerFromInput()
    {
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
            IP = ip,
            Port = _defaultPort
        };

        _servers.Add(entry);
        SaveServers();
        RebuildUI();

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

    private bool ContainsServer(string ip, ushort port)
    {
        for (int i = 0; i < _servers.Count; i++)
        {
            if (_servers[i].IP == ip && _servers[i].Port == port)
                return true;
        }
        return false;
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
            {
                _servers.AddRange(data.Servers);
            }

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
}