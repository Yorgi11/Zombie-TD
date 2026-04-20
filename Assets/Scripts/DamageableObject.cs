using System;
using UnityEngine;

[RequireComponent(typeof(NetworkPlayerController))]
public class DamageableObject : MonoBehaviour
{
    [SerializeField] private float _maxHp;
    [SerializeField] private NetworkPlayerController _netPlayer;

    public Action Die;
    public float CurrentHP { get; private set; }
    public float MaxHP => _maxHp;
    public bool IsDead => CurrentHP <= 0f;

    public event Action<float> OnHPChanged;

    private void Awake()
    {
        if (!_netPlayer) _netPlayer = GetComponent<NetworkPlayerController>();
        CurrentHP = _maxHp;
        RaiseHPChanged();
    }

    public bool TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead) return false;

        CurrentHP -= damage;

        bool died = CurrentHP <= 0f;
        if (died) CurrentHP = 0f;

        RaiseHPChanged();
        if (_netPlayer) _netPlayer.ServerSyncHealthState();

        if (died)
        {
            Die?.Invoke();
            return true;
        }

        return false;
    }

    public void RestoreFullHP()
    {
        CurrentHP = _maxHp;
        RaiseHPChanged();
        if (_netPlayer) _netPlayer.ServerSyncHealthState();
    }

    private void RaiseHPChanged()
    {
        OnHPChanged?.Invoke(CurrentHP);
    }
}