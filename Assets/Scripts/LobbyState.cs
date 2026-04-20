using QF_Tools.QF_Utilities;
using TMPro;
using UnityEngine;
public sealed class LobbyState : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private string _selectedMapScene = "Game";
    [SerializeField] private string _selectedGameMode = "Default";

    [Header("Optional UI")]
    [SerializeField] private GameObject _hostUI;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text _selectedMapText;
    [SerializeField] private TMP_Text _selectedModeText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _currentJoinCodeText;

    private Coroutine _joinCodeRoutine;
    private void OnEnable()
    {
        if (NetBootstrap.Instance != null) NetBootstrap.Instance.OnAllClientsLoadedGameScene += OnAllClientsLoadedGameScene;
        RefreshLabels();
        TryShowJoinCodeImmediately();
        StartJoinCodeWatcher();
    }
    private void OnDisable()
    {
        if (NetBootstrap.Instance != null) NetBootstrap.Instance.OnAllClientsLoadedGameScene -= OnAllClientsLoadedGameScene;
        if (_joinCodeRoutine != null)
        {
            StopCoroutine(_joinCodeRoutine);
            _joinCodeRoutine = null;
        }
    }
    public void OnHostLobbySceneLoaded()
    {
        if (NetBootstrap.Instance == null || !NetBootstrap.Instance.IsServer) return;
        Debug.Log("[LobbyState] Host entered lobby scene.");
        if (_hostUI != null) _hostUI.SetActive(true);
        if (_statusText != null) _statusText.text = "Host in lobby";
        TryShowJoinCodeImmediately();
    }
    public void SetMapScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        _selectedMapScene = sceneName.Trim();
        RefreshLabels();
    }
    public void SetGameMode(string modeName)
    {
        if (string.IsNullOrWhiteSpace(modeName)) return;
        _selectedGameMode = modeName.Trim();
        RefreshLabels();
    }
    public void OnClickStartGame()
    {
        if (NetBootstrap.Instance == null || !NetBootstrap.Instance.IsServer) return;
        NetBootstrap.Instance.StartGameFromLobby(_selectedMapScene);
    }
    private void OnAllClientsLoadedGameScene(string sceneName)
    {
        if (_statusText != null) _statusText.text = $"Loaded: {sceneName}";
    }
    private void RefreshLabels()
    {
        if (_selectedMapText != null) _selectedMapText.text = _selectedMapScene;
        if (_selectedModeText != null) _selectedModeText.text = _selectedGameMode;
    }
    private void TryShowJoinCodeImmediately()
    {
        if (NetBootstrap.Instance == null)
        {
            ShowJoinCode(null);
            return;
        }
        ShowJoinCode(NetBootstrap.Instance.CurrentSessionCode);
    }
    private void StartJoinCodeWatcher()
    {
        if (_joinCodeRoutine != null)
        {
            StopCoroutine(_joinCodeRoutine);
            _joinCodeRoutine = null;
        }
        _joinCodeRoutine = StartCoroutine(
            QF_Coroutines.DelayRunFunctionUntilTrue(
                () => NetBootstrap.Instance != null && !string.IsNullOrWhiteSpace(NetBootstrap.Instance.CurrentSessionCode),
                () => ShowJoinCode(NetBootstrap.Instance.CurrentSessionCode)
            )
        );
    }
    private void ShowJoinCode(string code)
    {
        if (_currentJoinCodeText == null) return;
        _currentJoinCodeText.text = string.IsNullOrWhiteSpace(code) ? "Join Code: ---" : $"Join Code: {code}";
    }
}