using TMPro;
using UnityEngine;

public sealed class MenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField _ipInput;

    public void OnClickHost()
    {
        NetBootstrap.Instance.StartHost();
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
}