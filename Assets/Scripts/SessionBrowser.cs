using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;

public sealed class SessionBrowser : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField _serverNameInput;
    [SerializeField] private TMP_InputField _joinCodeInput;

    [Header("UI")]
    [SerializeField] private Transform _entryParent;
    [SerializeField] private ServerEntryUI _entryPrefab;

    [Header("Refresh")]
    [SerializeField] private int _queryCount = 50;

    private readonly List<RecentJoinCodeEntry> _savedEntries = new();
    private readonly List<ServerEntryUI> _spawnedEntries = new();

    private bool _refreshInProgress;

    private string SavePath => Path.Combine(Application.persistentDataPath, "saved_join_codes.json");

    private void Start()
    {
        LoadSavedEntries();
        RebuildUI();
        RefreshNow();
    }

    public void AddEntryFromInput()
    {
        string displayName = _serverNameInput != null ? _serverNameInput.text.Trim() : string.Empty;
        string joinCode = _joinCodeInput != null ? _joinCodeInput.text.Trim().ToUpperInvariant() : string.Empty;

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[SessionBrowser] Cannot add entry. Join code is blank.");
            return;
        }

        if (ContainsCode(joinCode))
        {
            Debug.LogWarning($"[SessionBrowser] Join code already saved: {joinCode}");
            return;
        }

        RecentJoinCodeEntry entry = new()
        {
            Name = string.IsNullOrWhiteSpace(displayName) ? "Saved Session" : displayName,
            Code = joinCode
        };

        _savedEntries.Add(entry);
        SaveEntries();
        RebuildUI();
        RefreshNow();

        if (_serverNameInput != null) _serverNameInput.text = string.Empty;
        if (_joinCodeInput != null) _joinCodeInput.text = string.Empty;
    }

    public void RemoveEntry(RecentJoinCodeEntry entry)
    {
        if (entry == null) return;

        _savedEntries.Remove(entry);
        SaveEntries();
        RebuildUI();
    }

    public async void JoinSavedEntry(RecentJoinCodeEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Code)) return;
        await NetBootstrap.Instance.JoinSessionByCodeAsync(entry.Code);
    }

    public void RefreshNow()
    {
        if (_refreshInProgress) return;
        _ = RefreshNowAsync();
    }

    private async Task RefreshNowAsync()
    {
        _refreshInProgress = true;

        try
        {
            if (NetBootstrap.Instance == null)
            {
                RebuildUI();
                return;
            }

            bool servicesReady = await NetBootstrap.Instance.InitializeServicesAsync();
            if (!servicesReady)
            {
                RebuildUI();
                return;
            }

            RebuildUI();
            SetAllRefreshing();

            QuerySessionsOptions options = new()
            {
                Count = _queryCount
            };

            QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(options);

            Dictionary<string, ISessionInfo> sessionsByJoinCode = BuildJoinCodeLookup(results);

            for (int i = 0; i < _savedEntries.Count && i < _spawnedEntries.Count; i++)
            {
                RecentJoinCodeEntry saved = _savedEntries[i];
                ServerEntryUI ui = _spawnedEntries[i];
                if (saved == null || ui == null) continue;

                if (sessionsByJoinCode.TryGetValue(saved.Code, out ISessionInfo session))
                {
                    int maxPlayers = session.MaxPlayers;
                    int availableSlots = session.AvailableSlots;
                    int currentPlayers = Mathf.Max(0, maxPlayers - availableSlots);

                    ui.SetOnline(session.Name, currentPlayers, maxPlayers);
                }
                else
                {
                    ui.SetOffline();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionBrowser] Failed to refresh saved sessions.\n{e}");

            for (int i = 0; i < _spawnedEntries.Count; i++)
            {
                if (_spawnedEntries[i] != null)
                    _spawnedEntries[i].SetOffline();
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private Dictionary<string, ISessionInfo> BuildJoinCodeLookup(QuerySessionsResults results)
    {
        Dictionary<string, ISessionInfo> lookup = new(StringComparer.OrdinalIgnoreCase);

        if (results == null || results.Sessions == null)
            return lookup;

        foreach (ISessionInfo session in results.Sessions)
        {
            if (session == null) continue;

            // We expect the host to publish the join code in a public property named "joinCode".
            if (session.Properties == null) continue;
            if (!session.Properties.TryGetValue("joinCode", out var joinCodeProperty)) continue;
            if (joinCodeProperty == null) continue;

            string joinCode = joinCodeProperty.Value;
            if (string.IsNullOrWhiteSpace(joinCode)) continue;

            if (!lookup.ContainsKey(joinCode))
                lookup.Add(joinCode, session);
        }

        return lookup;
    }

    private void SetAllRefreshing()
    {
        for (int i = 0; i < _spawnedEntries.Count; i++)
        {
            if (_spawnedEntries[i] != null)
                _spawnedEntries[i].SetRefreshing();
        }
    }

    private bool ContainsCode(string joinCode)
    {
        for (int i = 0; i < _savedEntries.Count; i++)
        {
            if (string.Equals(_savedEntries[i].Code, joinCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void LoadSavedEntries()
    {
        _savedEntries.Clear();

        if (!File.Exists(SavePath))
        {
            Debug.Log($"[SessionBrowser] No saved join-code file found at: {SavePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            SavedServerData data = JsonUtility.FromJson<SavedServerData>(json);

            if (data != null && data.RecentJoinCodes != null)
                _savedEntries.AddRange(data.RecentJoinCodes);

            Debug.Log($"[SessionBrowser] Loaded {_savedEntries.Count} saved join-code entrie(s).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionBrowser] Failed to load saved join codes.\n{e}");
        }
    }

    private void SaveEntries()
    {
        try
        {
            SavedServerData data = new()
            {
                RecentJoinCodes = new List<RecentJoinCodeEntry>(_savedEntries)
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);

            Debug.Log($"[SessionBrowser] Saved {_savedEntries.Count} join-code entrie(s) to: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SessionBrowser] Failed to save join codes.\n{e}");
        }
    }

    private void RebuildUI()
    {
        ClearSpawnedUI();

        for (int i = 0; i < _savedEntries.Count; i++)
        {
            ServerEntryUI ui = Instantiate(_entryPrefab, _entryParent);
            ui.BindSavedCode(this, _savedEntries[i]);
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