using System;
using Unity.Netcode;
using UnityEngine;

public class DamageableObject : MonoBehaviour
{
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private bool _serverAuthoritative = true;

    private NetworkObject _networkObject;

    public event Action<ulong> Die;
    public event Action<float> OnHPChanged;

    public float CurrentHP { get; private set; }
    public float MaxHP => _maxHp;
    public bool IsDead => CurrentHP <= 0f;

    private void Awake()
    {
        _networkObject = GetComponentInParent<NetworkObject>();
        CurrentHP = _maxHp;
        RaiseHPChanged();
    }

    public bool TakeDamage(float damage, ulong attackerClientId)
    {
        if (damage <= 0f || IsDead)
            return false;

        if (_serverAuthoritative && !HasServerAuthority())
            return false;

        CurrentHP -= damage;

        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            RaiseHPChanged();
            Die?.Invoke(attackerClientId);
            return true;
        }

        RaiseHPChanged();
        return false;
    }

    public void RestoreFullHP()
    {
        if (_serverAuthoritative && !HasServerAuthority())
            return;

        CurrentHP = _maxHp;
        RaiseHPChanged();
    }

    private void RaiseHPChanged()
    {
        OnHPChanged?.Invoke(CurrentHP);
    }

    private bool HasServerAuthority()
    {
        if (_networkObject == null)
            return true;

        if (!_networkObject.IsSpawned)
            return true;

        return _networkObject.NetworkManager != null &&
               _networkObject.NetworkManager.IsServer;
    }
}