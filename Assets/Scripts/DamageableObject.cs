using System;
using UnityEngine;

public class DamageableObject : MonoBehaviour
{
    [SerializeField] private float _maxHp;
    [SerializeField] private NetworkPlayerController _netPlayer;

    public Action Die;
    public float CurrentHP { get; private set; }
    public float MaxHP => _maxHp;

    public event Action<float> OnHPChanged;

    private void Awake()
    {
        CurrentHP = _maxHp;
        RaiseHPChanged();
    }

    public bool TakeDamage(float damage)
    {
        CurrentHP -= damage;
        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            RaiseHPChanged();
            Die?.Invoke();
            return true;
        }

        RaiseHPChanged();
        return false;
    }

    public void RestoreFullHP()
    {
        CurrentHP = _maxHp;
        RaiseHPChanged();
    }

    private void RaiseHPChanged()
    {
        OnHPChanged?.Invoke(CurrentHP);
    }
}