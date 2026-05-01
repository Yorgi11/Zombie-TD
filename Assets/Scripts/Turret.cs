using Unity.Netcode;
using UnityEngine;
[RequireComponent(typeof(NetworkObject))]
public class Turret : NetworkBehaviour
{
    [SerializeField] private Transform _rotor;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _barrel;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private float _turnSpeed = 180f;
    [SerializeField] private float _pitchSpeed = 180f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;
    [SerializeField] private float _minFireAngle = 3f;
    [SerializeField] private float _targetDistance = 20f;
    [SerializeField] private float _targetRefreshInterval = 0.2f;
    [Space]
    [SerializeField] private int _placementCost;
    [Space]
    [SerializeField] private LayerMask _enemyMask;
    [Space]
    [SerializeField] private Gun _attachedGun;

    private Transform _target;
    private readonly Collider[] _targetHits = new Collider[32];
    private Transform _t;
    private float _targetDistanceSqr;
    private float _nextTargetRefreshTime;
    private Vector3 _lastDirectionToTarget;

    public int PlacementCost => _placementCost;
    private void Awake()
    {
        _t = transform;
        _targetDistanceSqr = _targetDistance * _targetDistance;
        if (_attachedGun != null) _attachedGun.OnShotRequested += HandleGunShotRequested;
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        if (_attachedGun != null) _attachedGun.OnShotRequested -= HandleGunShotRequested;
    }
    public override void OnNetworkSpawn()
    {
        if (!IsServer) enabled = false;
    }
    private void Update()
    {
        if (!IsServer) return;

        if (_target == null || Time.time >= _nextTargetRefreshTime || !IsTargetInRange())
        {
            _nextTargetRefreshTime = Time.time + _targetRefreshInterval;
            SetTarget();
        }

        PointAtTarget();
    }
    private Vector3 GetDirectionToTarget()
    {
        _lastDirectionToTarget = (_target.position + 0.5f * Vector3.up) - _barrel.position;
        return _lastDirectionToTarget;
    }
    private void HandleGunShotRequested(Gun gun)
    {
        if (!IsServer) return;
        if (gun == null) return;
        if (ServerBulletPool.Instance == null) return;
        if (_barrel == null) return;
        if (_target == null) return;
        ServerBulletPool.Instance.SpawnBullet(
            _barrel.position,
            _barrel.forward * gun.BulletVelocity,
            gun.BulletDamage,
            gun.BulletPenetration,
            ulong.MaxValue
        );
    }
    public void PointAtTarget()
    {
        if (_target == null || _rotor == null || _head == null || _barrel == null) return;

        Vector3 directionToTarget = GetDirectionToTarget();
        RotateRotor();
        RotateHead();
        if (_attachedGun != null && Vector3.Angle(_barrel.forward, directionToTarget) <= _minFireAngle) _attachedGun.TryShoot();
    }
    private void RotateRotor()
    {
        if (_rotor == null || _rotor.parent == null || _barrel == null) return;
        _rotor.forward = Vector3.RotateTowards(_rotor.forward,
            (Vector3.ProjectOnPlane(_lastDirectionToTarget, Vector3.up)).normalized,
            Mathf.Deg2Rad * _turnSpeed * Time.deltaTime, 0f);
    }
    private void RotateHead()
    {
        if (_head == null || _head.parent == null || _barrel == null) return;
        _head.forward = Vector3.RotateTowards(_head.forward,
            (Vector3.ProjectOnPlane(_lastDirectionToTarget, _head.right)).normalized,
            Mathf.Deg2Rad * _pitchSpeed * Time.deltaTime, 0f);
    }
    public void SetTarget()
    {
        _target = null;
        Vector3 origin = _t.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            _targetDistance,
            _targetHits,
            _enemyMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0)
            return;

        _target = GetClosestTarget(origin, hitCount);
    }

    private bool IsTargetInRange()
    {
        if (_target == null)
            return false;

        return (_target.position - _t.position).sqrMagnitude <= _targetDistanceSqr;
    }
    private Transform GetClosestTarget(Vector3 origin, int hitCount)
    {
        Transform closestTarget = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _targetHits[i];
            if (hit == null) continue;

            float sqrDistance = (hit.transform.position - origin).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestTarget = hit.transform;
            }
        }
        return closestTarget;
    }
}
