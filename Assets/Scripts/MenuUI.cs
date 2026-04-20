using TMPro;
using UnityEngine;

public sealed class MenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField _serverNameInput;
    [SerializeField] private TMP_InputField _joinCodeInput;
    [SerializeField] private TMP_Text _visibilityStateText;
    [SerializeField] private SessionBrowser _sessionBrowser;

    private bool _isPublic = true;

    private void Start()
    {
        RefreshVisibilityLabel();
    }
    public void OnClickToggleVisibility()
    {
        _isPublic = !_isPublic;
        RefreshVisibilityLabel();
    }

    public void OnClickHost()
    {
        string serverName = _serverNameInput != null ? _serverNameInput.text.Trim() : string.Empty;
        NetBootstrap.Instance.StartSessionHost(serverName, _isPublic);
    }

    public async void OnClickJoin()
    {
        string joinCode = _joinCodeInput != null ? _joinCodeInput.text.Trim() : string.Empty;
        await NetBootstrap.Instance.JoinSessionByCodeAsync(joinCode);
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

    private void RefreshVisibilityLabel()
    {
        if (_visibilityStateText != null)
            _visibilityStateText.text = _isPublic ? "Public" : "Private";
    }
}