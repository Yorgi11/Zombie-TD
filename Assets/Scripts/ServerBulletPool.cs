using UnityEngine;
using QF_Tools.QF_Utilities;
using System.Collections.Generic;
public class ServerBulletPool : QF_Singleton<ServerBulletPool>
{
    [SerializeField] private int _initialPoolSize = 128;
    [SerializeField] private float _maxFlightTime = 3f;
    [SerializeField] private float _gravity = 9.81f;
    public LayerMask BulletHitMask;
    private class ServerBullet
    {
        public int _penetration;
        public float _damage;
        public float _currentFlightTime;
        public Vector3 _position;
        public Vector3 _lastPosition;
        public Vector3 _velocity;
        public readonly List<DamageableObject> _hitObjs = new(6);
        public void Reset()
        {
            _penetration = 0;
            _damage = 0f;
            _currentFlightTime = 0f;
            _position = Vector3.zero;
            _lastPosition = Vector3.zero;
            _velocity = Vector3.zero;
            _hitObjs.Clear();
        }
    }
    private readonly List<ServerBullet> _reserveBullets = new();
    private readonly List<ServerBullet> _activeBullets = new();
    private readonly RaycastHit[] _hits = new RaycastHit[16];
    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < _initialPoolSize; i++) AddBullet();
    }
    private void AddBullet()
    {
        ServerBullet bullet = new();
        bullet.Reset();
        _reserveBullets.Add(bullet);
    }
    private void RemoveBulletAt(int i)
    {
        ServerBullet b = _activeBullets[i];
        _activeBullets.RemoveAt(i);
        b.Reset();
        _reserveBullets.Add(b);
    }
    public void SpawnBullet(Vector3 position, Vector3 velocity, float damage, int penetration)
    {
        if (_reserveBullets.Count <= 0) AddBullet();

        ServerBullet b = _reserveBullets[^1];
        _reserveBullets.RemoveAt(_reserveBullets.Count - 1);

        b._position = position;
        b._lastPosition = position;
        b._velocity = velocity;
        b._damage = damage;
        b._penetration = penetration;
        b._currentFlightTime = 0f;
        b._hitObjs.Clear();

        _activeBullets.Add(b);
    }
    public void UpdateBullets(float dt)
    {
        if (_activeBullets.Count <= 0) return;
        Vector3 gravityStep = Vector3.down * (_gravity * dt);

        for (int i = _activeBullets.Count - 1; i >= 0; i--)
        {
            ServerBullet b = _activeBullets[i];
            b._currentFlightTime += dt;
            if (b._currentFlightTime > _maxFlightTime || b._damage <= 0.1f || b._velocity.sqrMagnitude <= 0.01f)
            {
                RemoveBulletAt(i);
                continue;
            }
            b._lastPosition = b._position;
            b._velocity += gravityStep;
            b._position += b._velocity * dt;

            Vector3 travel = b._position - b._lastPosition;
            float travelSqr = travel.sqrMagnitude;
            if (travelSqr <= 0.00000001f) continue;

            float travelDistance = Mathf.Sqrt(travelSqr);
            int hitCount = Physics.RaycastNonAlloc(b._lastPosition, travel / travelDistance, _hits, travelDistance, BulletHitMask, QueryTriggerInteraction.Ignore);
            if (hitCount > 1) System.Array.Sort(_hits, 0, hitCount, RaycastHitDistanceComparer.Instance);
            bool bulletRemoved = false;
            for (int h = 0; h < hitCount; h++)
            {
                Collider col = _hits[h].collider;
                if (!col || !col.TryGetComponentInParent<DamageableObject>(out var d) || AlreadyHit(b._hitObjs, d)) continue;

                b._hitObjs.Add(d);
                d.TakeDamage(b._damage);

                b._position = _hits[h].point;
                b._velocity *= 0.8f;
                b._damage *= 0.8f;

                if (--b._penetration < 0 || b._damage <= 0.1f || b._velocity.sqrMagnitude <= 0.01f)
                {
                    RemoveBulletAt(i);
                    bulletRemoved = true;
                    break;
                }
            }
            if (bulletRemoved) continue;
        }
    }
    private static bool AlreadyHit(List<DamageableObject> hitObjs, DamageableObject obj)
    {
        for (int i = 0; i < hitObjs.Count; i++)
            if (hitObjs[i] == obj) return true;
        return false;
    }
    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}