using TMPro;
using UnityEngine;

public sealed class MenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField _ipInput;
    [SerializeField] private TMP_InputField _serverNameInput;
    [SerializeField] private ServerBrowser _serverBrowser;

    public void OnClickHost()
    {
        string serverName = _serverNameInput != null ? _serverNameInput.text : string.Empty;
        NetBootstrap.Instance.StartHost(serverName);
    }

    public void OnClickJoin()
    {
        string ip = _ipInput != null ? _ipInput.text : NetBootstrap.Instance.DefaultIP;
        NetBootstrap.Instance.StartClient(ip);
    }

    public void OnClickLoadGame()
    {
        NetBootstrap.Instance.LoadGameScene();
    }

    public void OnClickShutdown()
    {
        NetBootstrap.Instance.ShutdownIfRunning();
    }

    public void OnClickRefreshServers()
    {
        if (_serverBrowser != null)
            _serverBrowser.RefreshAllNow();
    }
}