using UnityEngine;
using QF_Tools.QF_Utilities;
using System;
using UnityEngine.UI;
public class GameUI : QF_Singleton<GameUI>
{
    [SerializeField] private GameObject _deathScreen;
    [SerializeField] private Button _respawnButton;
    protected override void Awake()
    {
        base.Awake();
        StartCoroutine(QF_Coroutines.DelayRunFunctionUntilTrue(() => FindFirstObjectByType<NetworkPlayerController>() && _respawnButton,
            () => _respawnButton.onClick.AddListener(() => FindFirstObjectByType<NetworkPlayerController>().HandleRespawn())));
    }
    public void ToggleDeathScreen()
    {
        _deathScreen.SetActive(!_deathScreen.activeInHierarchy);
    }
}