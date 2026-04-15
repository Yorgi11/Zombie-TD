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

    private RecentJoinCodeEntry _savedEntry;
    private SessionBrowser _browser;

    public RecentJoinCodeEntry SavedEntry => _savedEntry;

    public void BindSavedCode(SessionBrowser browser, RecentJoinCodeEntry entry)
    {
        _browser = browser;
        _savedEntry = entry;

        if (_serverNameText != null)
            _serverNameText.text = string.IsNullOrWhiteSpace(entry.Name) ? "Saved Session" : entry.Name;

        if (_serverIPText != null)
            _serverIPText.text = string.IsNullOrWhiteSpace(entry.Code) ? "Code: ---" : $"Code: {entry.Code}";

        SetRefreshing();

        if (_joinButton != null)
        {
            _joinButton.interactable = true;
            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(OnClickJoinSavedCode);
        }

        if (_deleteButton != null)
        {
            _deleteButton.gameObject.SetActive(true);
            _deleteButton.onClick.RemoveAllListeners();
            _deleteButton.onClick.AddListener(OnClickDeleteSavedCode);
        }
    }

    public void SetRefreshing()
    {
        if (_serverStatusText != null) _serverStatusText.text = "Refreshing";
        if (_serverPingText != null) _serverPingText.text = "---";
        if (_serverPlayerCountText != null) _serverPlayerCountText.text = "-/-";

        if (_serverStatusImage != null) _serverStatusImage.color = _unknownColor;
        if (_serverPingImage != null) _serverPingImage.color = _unknownColor;
    }

    public void SetOffline()
    {
        if (_serverStatusText != null) _serverStatusText.text = "Offline";
        if (_serverPingText != null) _serverPingText.text = "---";
        if (_serverPlayerCountText != null) _serverPlayerCountText.text = "-/-";

        if (_serverStatusImage != null) _serverStatusImage.color = _offlineColor;
        if (_serverPingImage != null) _serverPingImage.color = _offlineColor;
    }

    public void SetOnline(string reportedName, int currentPlayers, int maxPlayers)
    {
        if (_serverNameText != null)
            _serverNameText.text = string.IsNullOrWhiteSpace(reportedName)
                ? (string.IsNullOrWhiteSpace(_savedEntry?.Name) ? "Saved Session" : _savedEntry.Name)
                : reportedName;

        if (_serverStatusText != null) _serverStatusText.text = "Online";
        if (_serverPingText != null) _serverPingText.text = "---";
        if (_serverPlayerCountText != null) _serverPlayerCountText.text = $"{Mathf.Max(0, currentPlayers)}/{Mathf.Max(1, maxPlayers)}";

        if (_serverStatusImage != null) _serverStatusImage.color = _onlineColor;
        if (_serverPingImage != null) _serverPingImage.color = _unknownColor;
    }

    private void OnClickJoinSavedCode()
    {
        if (_browser != null && _savedEntry != null)
            _browser.JoinSavedEntry(_savedEntry);
    }

    private void OnClickDeleteSavedCode()
    {
        if (_browser != null && _savedEntry != null)
            _browser.RemoveEntry(_savedEntry);
    }

    private void OnDestroy()
    {
        if (_joinButton != null) _joinButton.onClick.RemoveAllListeners();
        if (_deleteButton != null) _deleteButton.onClick.RemoveAllListeners();
    }
}