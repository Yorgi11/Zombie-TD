using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QF_Tools.QF_Utilities;

public class GameUI : QF_Singleton<GameUI>
{
    [SerializeField] private GameObject _deathScreen;
    [SerializeField] private Button _respawnButton;
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private TMP_Text _pointsText;
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private TMP_Text _interactText;
    [SerializeField] private Slider _hpSlider;

    [Header("Placement")]
    [SerializeField] private GameObject _placementIndicator;

    private NetworkPlayerController _localPlayer;

    private int _lastAmmoInMag = int.MinValue;
    private int _lastReserveAmmo = int.MinValue;
    private int _lastPoints = int.MinValue;
    private int _lastHP = int.MinValue;
    private string _lastWaveText;
    private string _lastInteractText;
    private bool _lastPlacementVisible = true;
    private float _nextLocalOwnerSearchTime;

    protected override void Awake()
    {
        base.Awake();
        if (_respawnButton != null) _respawnButton.onClick.AddListener(OnRespawnClicked);

        ClearInteractText();
        ShowPlacementIndicator(false);
    }

    private void OnEnable()
    {
        NetworkPlayerController.LocalOwnerInitialized -= HandleLocalOwnerInitialized;
        NetworkPlayerController.LocalOwnerInitialized += HandleLocalOwnerInitialized;
        TryBindLocalOwner();
    }

    private void Update()
    {
        if (_localPlayer != null || Time.unscaledTime < _nextLocalOwnerSearchTime)
            return;

        _nextLocalOwnerSearchTime = Time.unscaledTime + 0.5f;
        TryBindLocalOwner();
    }

    private void OnDisable()
    {
        NetworkPlayerController.LocalOwnerInitialized -= HandleLocalOwnerInitialized;
        UnbindLocalOwner();
    }

    private void HandleLocalOwnerInitialized(NetworkPlayerController player)
    {
        if (player == null || !player.IsOwner)
            return;

        BindLocalOwner(player);
    }

    private void TryBindLocalOwner()
    {
        if (_localPlayer != null) return;

        if (NetworkPlayerController.CurrentLocalOwner != null)
        {
            BindLocalOwner(NetworkPlayerController.CurrentLocalOwner);
            return;
        }

        NetworkPlayerController[] players = FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayerController player = players[i];
            if (player == null || !player.IsOwner) continue;

            BindLocalOwner(player);
            break;
        }
    }

    private void BindLocalOwner(NetworkPlayerController player)
    {
        if (_localPlayer == player)
            return;

        UnbindLocalOwner();

        _localPlayer = player;
        _localPlayer.OnAmmoChanged += HandleAmmoChanged;
        _localPlayer.OnHPChanged += HandleHPChanged;
        _localPlayer.OnPointsChanged += HandlePointsChanged;

        if (_localPlayer.CurrentGun != null)
            HandleAmmoChanged(_localPlayer.CurrentGun.CurrentAmmoInMag, _localPlayer.CurrentGun.CurrentReserveAmmo);

        if (_localPlayer.DamageableObject != null)
            HandleHPChanged(_localPlayer.DamageableObject.CurrentHP);

        HandlePointsChanged(_localPlayer.Points);
    }

    private void UnbindLocalOwner()
    {
        if (_localPlayer == null) return;
        _localPlayer.OnAmmoChanged -= HandleAmmoChanged;
        _localPlayer.OnHPChanged -= HandleHPChanged;
        _localPlayer.OnPointsChanged -= HandlePointsChanged;
        _localPlayer = null;
    }

    private void HandleAmmoChanged(int currentAmmoInMag, int currentReserveAmmo)
    {
        if (_ammoText == null) return;
        if (_lastAmmoInMag == currentAmmoInMag && _lastReserveAmmo == currentReserveAmmo) return;

        _lastAmmoInMag = currentAmmoInMag;
        _lastReserveAmmo = currentReserveAmmo;
        _ammoText.SetText("{0} / {1}", currentAmmoInMag, currentReserveAmmo);
    }

    private void HandleHPChanged(float currentHP)
    {
        if (_hpSlider == null) return;

        int hp = Mathf.CeilToInt(currentHP);
        if (_lastHP == hp) return;

        _lastHP = hp;
        _hpSlider.value = hp;
    }

    private void HandlePointsChanged(int points)
    {
        if (_pointsText == null) return;
        if (_lastPoints == points) return;

        _lastPoints = points;
        _pointsText.SetText("$ {0}", points);
    }

    private void OnRespawnClicked()
    {
        if (_localPlayer != null) _localPlayer.HandleRespawn();
    }

    public void ToggleDeathScreen()
    {
        if (_deathScreen != null) _deathScreen.SetActive(!_deathScreen.activeInHierarchy);
    }

    public void UpdateWaveText(string text)
    {
        if (_waveText == null || _lastWaveText == text) return;

        _lastWaveText = text;
        _waveText.text = text;
    }

    public void UpdateInteractText(string text)
    {
        if (_interactText == null || _lastInteractText == text) return;

        _lastInteractText = text;
        _interactText.text = text;
    }

    public void ClearInteractText()
    {
        UpdateInteractText(string.Empty);
    }

    public void ShowPlacementIndicator(bool show)
    {
        if (_placementIndicator == null || _lastPlacementVisible == show) return;

        _lastPlacementVisible = show;
        _placementIndicator.SetActive(show);
    }

    public void SetPlacementIndicatorPosition(Vector3 worldPosition)
    {
        if (_placementIndicator != null)
            _placementIndicator.transform.position = worldPosition;
    }
}
