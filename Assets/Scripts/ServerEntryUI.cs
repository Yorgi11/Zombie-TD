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

    private string _sessionId;
    private SessionBrowser _browser;

    public void BindPublicSession(SessionBrowser browser, string sessionId, string sessionName, int currentPlayers, int maxPlayers, bool joinable)
    {
        _browser = browser;
        _sessionId = sessionId;

        if (_serverNameText != null)
            _serverNameText.text = string.IsNullOrWhiteSpace(sessionName) ? "Unnamed Session" : sessionName;

        if (_serverIPText != null)
            _serverIPText.text = "Public Session";

        if (_serverStatusText != null)
            _serverStatusText.text = joinable ? "Open" : "Full";

        if (_serverPingText != null)
            _serverPingText.text = "---";

        if (_serverPlayerCountText != null)
            _serverPlayerCountText.text = $"{Mathf.Max(0, currentPlayers)}/{Mathf.Max(1, maxPlayers)}";

        if (_serverStatusImage != null)
            _serverStatusImage.color = joinable ? _onlineColor : _offlineColor;

        if (_serverPingImage != null)
            _serverPingImage.color = _unknownColor;

        if (_joinButton != null)
        {
            _joinButton.interactable = joinable;
            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(OnClickJoin);
        }

        if (_deleteButton != null)
        {
            _deleteButton.gameObject.SetActive(false);
            _deleteButton.onClick.RemoveAllListeners();
        }
    }

    public void SetRefreshing()
    {
        if (_serverStatusText != null) _serverStatusText.text = "Refreshing";
        if (_serverPingText != null) _serverPingText.text = "---";
        if (_serverPlayerCountText != null) _serverPlayerCountText.text = "-/-";

        if (_serverStatusImage != null) _serverStatusImage.color = _unknownColor;
        if (_serverPingImage != null) _serverPingImage.color = _unknownColor;

        if (_joinButton != null)
            _joinButton.interactable = false;
    }

    private void OnClickJoin()
    {
        if (_browser != null && !string.IsNullOrWhiteSpace(_sessionId))
            _browser.JoinSessionById(_sessionId);
    }

    private void OnDestroy()
    {
        if (_joinButton != null) _joinButton.onClick.RemoveAllListeners();
        if (_deleteButton != null) _deleteButton.onClick.RemoveAllListeners();
    }
}