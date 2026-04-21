using QF_Tools.QF_Utilities;
using System;
using UnityEngine;
public class DamageableObject : MonoBehaviour
{
    [SerializeField] private bool _isPlayer;
    [SerializeField] private float _maxHp;
    private NetworkPlayerController _netPlayer;

    public Action Die;
    public float CurrentHP { get; private set; }
    public float MaxHP => _maxHp;
    public bool IsDead => CurrentHP <= 0f;

    public event Action<float> OnHPChanged;

    private void Awake()
    {
        if (!_netPlayer && _isPlayer) gameObject.TryGetComponentInChildren<NetworkPlayerController>(out _netPlayer);
        CurrentHP = _maxHp;
        RaiseHPChanged();
    }

    public bool TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead && _isPlayer) return false;

        CurrentHP -= damage;

        bool died = CurrentHP <= 0f;
        if (died && _isPlayer) CurrentHP = 0f;

        RaiseHPChanged();
        if (_netPlayer && _isPlayer) _netPlayer.ServerSyncHealthState();

        if (died)
        {
            if (_isPlayer) Die?.Invoke();
            gameObject.SetActive(false);
            return true;
        }

        return false;
    }

    public void RestoreFullHP()
    {
        CurrentHP = _maxHp;
        RaiseHPChanged();
        if (_netPlayer && _isPlayer) _netPlayer.ServerSyncHealthState();
    }

    private void RaiseHPChanged()
    {
        OnHPChanged?.Invoke(CurrentHP);
    }
}