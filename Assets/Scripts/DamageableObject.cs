using UnityEngine;
public class DamageableObject : MonoBehaviour
{
    [SerializeField] private float _maxHp;
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
            return true;
        }
        return false;
    }
}