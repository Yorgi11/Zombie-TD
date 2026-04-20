using System;
using UnityEngine;
public class DamageableObject : MonoBehaviour
{
    [SerializeField] private float _maxHp;
    [SerializeField] private NetworkPlayerController _netPlayer;
    public Action Die;
    public float CurrentHP { get; private set; }
    private void Awake()
    {
        CurrentHP = _maxHp;
    }
    public bool TakeDamage(float damage)
    {
        CurrentHP -= damage;
        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            Die?.Invoke();
            return true;
        }
        return false;
    }
}