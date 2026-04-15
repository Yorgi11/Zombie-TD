using TMPro;
using UnityEngine;

public sealed class MenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField _serverNameInput;
    [SerializeField] private TMP_InputField _joinCodeInput;
    [SerializeField] private TMP_Text _currentJoinCodeText;
    [SerializeField] private SessionBrowser _sessionBrowser;

    private void Update()
    {
        if (_currentJoinCodeText != null && NetBootstrap.Instance != null)
        {
            string code = NetBootstrap.Instance.CurrentSessionCode;
            _currentJoinCodeText.text = string.IsNullOrWhiteSpace(code) ? "Code: ---" : $"Code: {code}";
        }
    }

    public void OnClickHost()
    {
        string serverName = _serverNameInput != null ? _serverNameInput.text.Trim() : string.Empty;
        NetBootstrap.Instance.StartSessionHost(serverName);
    }

    public async void OnClickJoin()
    {
        string joinCode = _joinCodeInput != null ? _joinCodeInput.text.Trim() : string.Empty;
        await NetBootstrap.Instance.JoinSessionByCodeAsync(joinCode);
    }

    public void OnClickAddSavedCode()
    {
        if (_sessionBrowser != null)
            _sessionBrowser.AddEntryFromInput();
    }

    public void OnClickRefresh()
    {
        if (_sessionBrowser != null)
            _sessionBrowser.RefreshNow();
    }

    public void OnClickShutdown()
    {
        NetBootstrap.Instance.ShutdownIfRunning();
    }

    public void OnClickCopyCode()
    {
        if (NetBootstrap.Instance == null) return;
        if (string.IsNullOrWhiteSpace(NetBootstrap.Instance.CurrentSessionCode)) return;

        GUIUtility.systemCopyBuffer = NetBootstrap.Instance.CurrentSessionCode;
    }
}