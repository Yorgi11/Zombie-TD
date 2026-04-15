using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ServerEntryUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI _serverNameText;
    [SerializeField] private TextMeshProUGUI _serverIPText;
    [SerializeField] private TextMeshProUGUI _serverStatusText;
    [SerializeField] private TextMeshProUGUI _serverPingText;
    [SerializeField] private TextMeshProUGUI _serverPlayerCountText;

    [Header("Images")]
    [SerializeField] private Image _serverPingImage;
    [SerializeField] private Image _serverStatusImage;

    [Header("Buttons")]
    [SerializeField] private Button _deleteButton;
    [SerializeField] private Button _joinButton;

    [Header("Status Colors")]
    [SerializeField] private Color _onlineColor = Color.green;
    [SerializeField] private Color _offlineColor = Color.red;
    [SerializeField] private Color _unknownColor = Color.yellow;

    private SavedServerEntry _entry;
    private ServerBrowser _browser;

    public SavedServerEntry Entry => _entry;

    public void Bind(SavedServerEntry entry, ServerBrowser browser)
    {
        _entry = entry;
        _browser = browser;

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

        RefreshStatic();
        SetUnknown();
    }

    public void RefreshStatic()
    {
        if (_entry == null) return;

        if (_serverNameText != null)
            _serverNameText.text = string.IsNullOrWhiteSpace(_entry.Name) ? "Unknown Server" : _entry.Name;

        if (_serverIPText != null)
            _serverIPText.text = $"IP: {_entry.IP}:{_entry.Port}";
    }

    public void SetRefreshing()
    {
        SetStatus("Refreshing", _unknownColor);
        SetPing("...", _unknownColor);
        SetPlayers("-/-");
        SetJoinInteractable(false);
    }

    public void SetUnknown()
    {
        SetStatus("Unknown", _unknownColor);
        SetPing("---", _unknownColor);
        SetPlayers("-/-");
        SetJoinInteractable(false);
    }

    public void SetOffline()
    {
        SetStatus("Offline", _offlineColor);
        SetPing("---", _offlineColor);
        SetPlayers("-/-");
        SetJoinInteractable(false);
    }

    public void SetOnline(ServerStatusResponse response, int pingMs)
    {
        if (_serverNameText != null)
        {
            string displayName = !string.IsNullOrWhiteSpace(_entry?.Name)
                ? _entry.Name
                : !string.IsNullOrWhiteSpace(response?.Name)
                    ? response.Name
                    : "Unknown Server";

            _serverNameText.text = displayName;
        }

        SetStatus("Online", _onlineColor);
        SetPing(Mathf.Clamp(pingMs, 0, 999).ToString(), _onlineColor);

        if (response != null)
            SetPlayers($"{Mathf.Max(0, response.CurrentPlayers)}/{Mathf.Max(1, response.MaxPlayers)}");
        else
            SetPlayers("-/-");

        SetJoinInteractable(true);
    }

    private void SetStatus(string value, Color color)
    {
        if (_serverStatusText != null) _serverStatusText.text = value;
        if (_serverStatusImage != null) _serverStatusImage.color = color;
    }

    private void SetPing(string value, Color color)
    {
        if (_serverPingText != null) _serverPingText.text = value;
        if (_serverPingImage != null) _serverPingImage.color = color;
    }

    private void SetPlayers(string value)
    {
        if (_serverPlayerCountText != null) _serverPlayerCountText.text = value;
    }

    private void SetJoinInteractable(bool value)
    {
        if (_joinButton != null) _joinButton.interactable = value;
    }

    private void OnClickDelete()
    {
        if (_browser != null && _entry != null)
            _browser.RemoveServer(_entry);
    }

    private void OnClickJoin()
    {
        if (_browser != null && _entry != null)
            _browser.JoinServer(_entry);
    }

    private void OnDestroy()
    {
        if (_deleteButton != null) _deleteButton.onClick.RemoveAllListeners();
        if (_joinButton != null) _joinButton.onClick.RemoveAllListeners();
    }
}