using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SessionBrowser : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField _joinCodeInput;

    [Header("UI")]
    [SerializeField] private Transform _entryParent;
    [SerializeField] private ServerEntryUI _entryPrefab;

    [Header("Refresh")]
    [SerializeField] private float _refreshInterval = 5f;
    [SerializeField] private int _queryCount = 50;

    private readonly List<ServerEntryUI> _spawnedEntries = new();

    private Coroutine _refreshRoutine;
    private bool _refreshInProgress;

    private IEnumerator Start()
    {
        yield return null;
        StartRefreshLoopIfBootstrap();
        RefreshNow();
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

    public void RefreshNow()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_refreshInProgress) return;
        _ = RefreshNowAsync();
    }

    public async void JoinByCodeFromInput()
    {
        string joinCode = _joinCodeInput != null ? _joinCodeInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(joinCode)) return;

        await NetBootstrap.Instance.JoinSessionByCodeAsync(joinCode);
    }

    public async void JoinSessionById(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        await NetBootstrap.Instance.JoinSessionByIdAsync(sessionId);
    }

    private async Task RefreshNowAsync()
    {
        _refreshInProgress = true;

        try
        {
            SetAllRefreshing();

            if (NetBootstrap.Instance == null)
            {
                Debug.LogWarning("[SessionBrowser] NetBootstrap instance is null.");
                ClearSpawnedUI();
                return;
            }

            bool servicesReady = await NetBootstrap.Instance.InitializeServicesAsync();
            if (!servicesReady)
            {
                Debug.LogWarning("[SessionBrowser] Multiplayer services are not ready yet.");
                ClearSpawnedUI();
                return;
            }

            QuerySessionsOptions options = new()
            {
                Count = _queryCount
            };

            QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(options);
            RebuildUIFromResults(results);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SessionBrowser] Failed to query sessions.\n{e}");
            ClearSpawnedUI();
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void RebuildUIFromResults(QuerySessionsResults results)
    {
        ClearSpawnedUI();

        if (results == null || results.Sessions == null) return;

        foreach (ISessionInfo session in results.Sessions)
        {
            if (session == null) continue;

            string sessionId = session.Id;
            string sessionName = session.Name;

            int maxPlayers = session.MaxPlayers;

            int availableSlots = session.AvailableSlots;
            int currentPlayers = Mathf.Max(0, maxPlayers - availableSlots);
            bool joinable = !session.IsLocked && availableSlots > 0;

            // Browser query results do not expose the short join code directly.
            string displayCode = string.IsNullOrWhiteSpace(sessionId)
                ? "---"
                : sessionId.Length > 8 ? sessionId.Substring(0, 8) : sessionId;

            ServerEntryUI ui = Instantiate(_entryPrefab, _entryParent);
            ui.Bind(this, sessionId, sessionName, displayCode, currentPlayers, maxPlayers, joinable);
            _spawnedEntries.Add(ui);
        }
    }

    private void SetAllRefreshing()
    {
        for (int i = 0; i < _spawnedEntries.Count; i++)
        {
            if (_spawnedEntries[i] != null)
                _spawnedEntries[i].SetRefreshing();
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
            RefreshNow();
        }
        else
        {
            StopRefreshLoop();
        }
    }

    private void StartRefreshLoopIfBootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Bootstrap") return;
        if (_refreshRoutine != null) return;

        _refreshRoutine = StartCoroutine(RefreshLoop());
    }

    private void StopRefreshLoop()
    {
        if (_refreshRoutine == null) return;
        StopCoroutine(_refreshRoutine);
        _refreshRoutine = null;
    }

    private IEnumerator RefreshLoop()
    {
        while (SceneManager.GetActiveScene().name == "Bootstrap")
        {
            RefreshNow();
            yield return new WaitForSeconds(_refreshInterval);
        }

        _refreshRoutine = null;
    }
}