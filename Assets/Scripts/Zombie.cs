using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(DamageableObject))]
[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : NetworkBehaviour
{
    [SerializeField] private float _targetPlayerDistance = 10f;
    [SerializeField] private float _destinationRefreshInterval = 0.25f;
    [SerializeField] private float _retargetInterval = 0.2f;

    private Transform _t;
    private Transform _tower;
    private Transform _currentTarget;

    private NavMeshAgent _navMeshAgent;
    private DamageableObject _damageableObject;
    private Collider[] _colliders;
    private Renderer[] _renderers;

    private float _targetPlayerDistanceSqr;
    private float _nextRetargetTime;
    private float _nextDestinationRefreshTime;

    private bool _deathHandled;
    private bool _pooledActive;

    private GameManager.EnemyType _enemyType = GameManager.EnemyType.Regular;
    public GameManager.EnemyType EnemyType => _enemyType;

    private void Awake()
    {
        _t = transform;
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _damageableObject = GetComponent<DamageableObject>();
        _colliders = GetComponentsInChildren<Collider>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _targetPlayerDistanceSqr = _targetPlayerDistance * _targetPlayerDistance;
    }

    public void InitializeServer(GameManager.EnemyType enemyType)
    {
        _enemyType = enemyType;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            if (_navMeshAgent != null)
                _navMeshAgent.enabled = false;

            enabled = false;
            return;
        }

        if (_damageableObject != null)
        {
            _damageableObject.Die -= HandleDeathServer;
            _damageableObject.Die += HandleDeathServer;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_damageableObject != null)
            _damageableObject.Die -= HandleDeathServer;
    }

    public void OnTakenFromPool(Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
            return;

        _pooledActive = true;
        _deathHandled = false;

        gameObject.SetActive(true);
        _t.SetPositionAndRotation(position, rotation);

        SetVisualsAndColliders(true);

        _tower = GameManager.Instance != null ? GameManager.Instance.Tower : null;
        _currentTarget = null;
        _nextRetargetTime = 0f;
        _nextDestinationRefreshTime = 0f;

        if (_damageableObject != null)
            _damageableObject.RestoreFullHP();

        if (_navMeshAgent != null)
        {
            _navMeshAgent.enabled = true;

            if (_navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.ResetPath();
                _navMeshAgent.Warp(position);
            }
            else
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas))
                {
                    _t.position = hit.position;
                    _navMeshAgent.Warp(hit.position);
                }
            }
        }

        SelectBestTarget();
        UpdateDestination();
    }

    public void OnReturnedToPool()
    {
        if (!IsServer)
            return;

        _pooledActive = false;
        _currentTarget = null;

        if (_navMeshAgent != null)
        {
            if (_navMeshAgent.isOnNavMesh)
                _navMeshAgent.ResetPath();

            _navMeshAgent.enabled = false;
        }

        SetVisualsAndColliders(false);
        gameObject.SetActive(false);
    }

    private void SetVisualsAndColliders(bool enabledState)
    {
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = enabledState;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = enabledState;
        }
    }

    private void Update()
    {
        if (!IsServer || !_pooledActive)
            return;

        if (_damageableObject != null && _damageableObject.IsDead)
            return;

        if (Time.time >= _nextRetargetTime)
        {
            _nextRetargetTime = Time.time + _retargetInterval;
            SelectBestTarget();
        }

        if (_currentTarget != null && Time.time >= _nextDestinationRefreshTime)
        {
            _nextDestinationRefreshTime = Time.time + _destinationRefreshInterval;
            UpdateDestination();
        }
    }

    private void SelectBestTarget()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return;

        Transform closestPlayer = null;
        float closestPlayerDistSqr = float.MaxValue;
        Vector3 myPos = _t.position;

        var players = gm.ServerPlayers;
        for (int i = players.Count - 1; i >= 0; i--)
        {
            NetworkPlayerController player = players[i];
            if (player == null || !player.IsSpawned)
                continue;

            DamageableObject playerDamageable = player.DamageableObject;
            if (playerDamageable == null || playerDamageable.IsDead)
                continue;

            Transform playerTransform = player.transform;
            float distSqr = (playerTransform.position - myPos).sqrMagnitude;

            if (distSqr < closestPlayerDistSqr)
            {
                closestPlayerDistSqr = distSqr;
                closestPlayer = playerTransform;
            }
        }

        Transform desiredTarget = _tower;
        if (closestPlayer != null && closestPlayerDistSqr <= _targetPlayerDistanceSqr)
            desiredTarget = closestPlayer;

        ChangeCurrentTarget(desiredTarget);
    }

    private void ChangeCurrentTarget(Transform target)
    {
        if (target == null || target == _currentTarget)
            return;

        _currentTarget = target;
        UpdateDestination();
    }

    private void UpdateDestination()
    {
        if (!IsServer || !_pooledActive)
            return;

        if (_currentTarget == null || _navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnNavMesh)
            return;

        _navMeshAgent.SetDestination(_currentTarget.position);
    }

    private void HandleDeathServer(ulong killerClientId)
    {
        if (!IsServer || _deathHandled)
            return;

        _deathHandled = true;

        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyKilled(_enemyType, killerClientId);

        if (ZombiePoolManager.Instance != null)
            ZombiePoolManager.Instance.ReturnZombie(this);
        else
            gameObject.SetActive(false);
    }
}