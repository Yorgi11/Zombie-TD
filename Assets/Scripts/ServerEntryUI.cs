using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ServerEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _serverNameText;
    [SerializeField] private TextMeshProUGUI _serverIPText;
    [SerializeField] private TextMeshProUGUI _serverStatusText;
    [SerializeField] private TextMeshProUGUI _serverPingText;
    [SerializeField] private TextMeshProUGUI _serverPlayerCountText;
    [SerializeField] private Image _serverPingImage;
    [SerializeField] private Image _serverStatusImage;
    [SerializeField] private Button _deleteButton;
    [SerializeField] private Button _joinButton;

    [Header("Status Colors")]
    [SerializeField] private Color _onlineColor = Color.green;
    [SerializeField] private Color _offlineColor = Color.red;

    private SavedServerEntry _entry;
    private ServerBrowser _browser;

    public void Bind(SavedServerEntry entry, ServerBrowser browser)
    {
        _entry = entry;
        _browser = browser;

        _serverNameText.text = "Unknown Server";
        _serverIPText.text = $"IP: {entry.IP}:{entry.Port}";
        _serverStatusText.text = "Offline";
        _serverPingText.text = "---";
        _serverPlayerCountText.text = "-/-";

        if (_serverStatusImage != null) _serverStatusImage.color = _offlineColor;
        if (_serverPingImage != null) _serverPingImage.color = _offlineColor;

        if (_deleteButton != null)
        {
            _deleteButton.onClick.RemoveAllListeners();
            _deleteButton.onClick.AddListener(OnClickDelete);
        }

        if (_joinButton != null)
        {
            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(OnClickJoin);
        }
    }
    private void OnClickDelete()
    {
        if (_browser) _browser.RemoveServer(_entry);
    }
    private void OnClickJoin()
    {
        if (_browser) _browser.JoinServer(_entry);
    }
}