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

    private float _targetPlayerDistanceSqr;
    private float _nextRetargetTime;
    private float _nextDestinationRefreshTime;

    private bool _serverInitialized;
    private bool _deathHandled;

    private GameManager.EnemyType _enemyType = GameManager.EnemyType.Regular;

    private void Awake()
    {
        _t = transform;
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _damageableObject = GetComponent<DamageableObject>();
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

        ServerInitialize();
    }

    public override void OnNetworkDespawn()
    {
        if (_damageableObject != null)
            _damageableObject.Die -= HandleDeathServer;
    }

    private void ServerInitialize()
    {
        if (_serverInitialized)
            return;

        _serverInitialized = true;
        _deathHandled = false;

        if (GameManager.Instance != null)
            _tower = GameManager.Instance.Tower;

        if (_damageableObject != null)
        {
            _damageableObject.Die -= HandleDeathServer;
            _damageableObject.Die += HandleDeathServer;
        }

        RestoreZombieState();
    }

    private void Update()
    {
        if (!IsServer)
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
        if (!IsServer)
            return;

        if (_currentTarget == null || _navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
            return;

        _navMeshAgent.SetDestination(_currentTarget.position);
    }

    public void RestoreZombieState()
    {
        if (!IsServer)
            return;

        if (_navMeshAgent != null && _navMeshAgent.isOnNavMesh)
            _navMeshAgent.ResetPath();

        if (_damageableObject != null)
            _damageableObject.RestoreFullHP();

        _tower = GameManager.Instance != null ? GameManager.Instance.Tower : null;
        _currentTarget = null;
        _nextRetargetTime = 0f;
        _nextDestinationRefreshTime = 0f;

        SelectBestTarget();
    }

    private void HandleDeathServer()
    {
        if (!IsServer || _deathHandled)
            return;

        _deathHandled = true;

        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyKilled(_enemyType);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}