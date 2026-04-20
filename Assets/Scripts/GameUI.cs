using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QF_Tools.QF_Utilities;
public class GameUI : QF_Singleton<GameUI>
{
    [SerializeField] private GameObject _deathScreen;
    [SerializeField] private Button _respawnButton;
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private Slider _hpSlider;

    private NetworkPlayerController _localPlayer;
    protected override void Awake()
    {
        base.Awake();
        if (_respawnButton != null) _respawnButton.onClick.AddListener(OnRespawnClicked);
    }
    private void OnEnable()
    {
        TryBindLocalOwner();
    }
    private void Update()
    {
        if (_localPlayer == null) TryBindLocalOwner();
    }
    private void OnDisable()
    {
        UnbindLocalOwner();
    }
    private void TryBindLocalOwner()
    {
        if (_localPlayer != null) return;
        NetworkPlayerController[] players = FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayerController player = players[i];
            if (player == null || !player.IsOwner) continue;

            _localPlayer = player;
            _localPlayer.OnAmmoChanged += HandleAmmoChanged;
            _localPlayer.OnHPChanged += HandleHPChanged;

            if (_localPlayer.CurrentGun != null) HandleAmmoChanged(_localPlayer.CurrentGun.CurrentAmmoInMag, _localPlayer.CurrentGun.CurrentReserveAmmo);
            if (_localPlayer.DamageableObject != null) HandleHPChanged(_localPlayer.DamageableObject.CurrentHP);
            break;
        }
    }
    private void UnbindLocalOwner()
    {
        if (_localPlayer == null) return;
        _localPlayer.OnAmmoChanged -= HandleAmmoChanged;
        _localPlayer.OnHPChanged -= HandleHPChanged;
        _localPlayer = null;
    }
    private void HandleAmmoChanged(int currentAmmoInMag, int currentReserveAmmo)
    {
        if (_ammoText != null) _ammoText.text = $"{currentAmmoInMag} / {currentReserveAmmo}";
    }
    private void HandleHPChanged(float currentHP)
    {
        if (_hpSlider != null) _hpSlider.value = Mathf.CeilToInt(currentHP);
    }
    private void OnRespawnClicked()
    {
        if (_localPlayer != null) _localPlayer.HandleRespawn();
    }
    public void ToggleDeathScreen()
    {
        if (_deathScreen != null) _deathScreen.SetActive(!_deathScreen.activeInHierarchy);
    }
}