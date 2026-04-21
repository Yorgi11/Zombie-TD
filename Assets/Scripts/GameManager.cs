using System;
using System.Collections.Generic;
using QF_Tools.QF_Utilities;
using Unity.Netcode;
using UnityEngine;

public class GameManager : QF_Singleton<GameManager>
{
    public enum EnemyType
    {
        Regular,
        Special1,
        Special2,
        Special3
    }

    [Serializable]
    public struct WaveInfo
    {
        public int _enemyCountNTotal;
        public int _enemyCountN;

        [Space]
        public int _enemyCountSp1Total;
        public int _enemyCountSp1;

        [Space]
        public int _enemyCountSp2Total;
        public int _enemyCountSp2;

        [Space]
        public int _enemyCountSp3Total;
        public int _enemyCountSp3;

        [Space]
        public float _spawnTime;
    }

    [Header("Enemy Prefabs")]
    [SerializeField] private Zombie _regularZombiePrefab;
    [SerializeField] private Zombie _special1ZombiePrefab;
    [SerializeField] private Zombie _special2ZombiePrefab;
    [SerializeField] private Zombie _special3ZombiePrefab;

    [SerializeField] private int _prewarmRegular = 16;
    [SerializeField] private int _prewarmSpecial1 = 4;
    [SerializeField] private int _prewarmSpecial2 = 4;
    [SerializeField] private int _prewarmSpecial3 = 2;

    [Header("Wave Settings")]
    [SerializeField] private float _safePhaseTime = 15f;
    [SerializeField] private WaveInfo[] _waveInfos;

    [Header("Points")]
    [SerializeField] private int _pointsRegular = 100;
    [SerializeField] private int _pointsSpecial1 = 200;
    [SerializeField] private int _pointsSpecial2 = 500;
    [SerializeField] private int _pointsSpecial3 = 1000;

    [Header("References")]
    [SerializeField] private NetBootstrap _netBoot;

    [Header("Combat")]
    [SerializeField] private LayerMask _bulletHitMask;
    public Gun[] _guns;

    private bool _canSpawnEnemies;
    private bool _inSafePhase;

    private int _currentWaveIndex;

    private int _currentNCount;
    private int _totalSpawnedNCountThisWave;

    private int _currentSp1Count;
    private int _totalSpawnedSp1CountThisWave;

    private int _currentSp2Count;
    private int _totalSpawnedSp2CountThisWave;

    private int _currentSp3Count;
    private int _totalSpawnedSp3CountThisWave;

    private float _spawnTimer1;
    private float _spawnTimer2;
    private float _spawnTimer3;
    private float _spawnTimer4;
    private float _safePhaseTimer;

    private ServerBulletPool _bulletPool;
    private GameUI _gameUI;

    private readonly List<NetworkPlayerController> _serverPlayers = new();

    private WaveInfo CurrentWaveInfo => _waveInfos[_currentWaveIndex];
    public int CurrentWave => _currentWaveIndex + 1;
    public Transform Tower => Map.Instance.Tower;
    public IReadOnlyList<NetworkPlayerController> ServerPlayers => _serverPlayers;

    protected override void Awake()
    {
        base.Awake();

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 240;

        if (NetBootstrap.Instance != null)
            NetBootstrap.Instance.OnAllClientsLoadedGameScene += OnAllClientsLoadedGameScene;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (NetBootstrap.Instance != null)
            NetBootstrap.Instance.OnAllClientsLoadedGameScene -= OnAllClientsLoadedGameScene;
    }

    private void Update()
    {
        if (_netBoot == null || !_netBoot.IsServer)
            return;

        if (_bulletPool != null)
            _bulletPool.UpdateBullets(Time.deltaTime);

        if (_waveInfos == null || _waveInfos.Length < 1)
            return;

        if (_inSafePhase)
        {
            _safePhaseTimer += Time.deltaTime;

            if (_safePhaseTimer > _safePhaseTime)
            {
                _inSafePhase = false;
                _canSpawnEnemies = true;
                _safePhaseTimer = 0f;

                if (_gameUI != null)
                    _gameUI.UpdateWaveText(CurrentWave.ToString());
            }

            return;
        }

        if (!_canSpawnEnemies)
            return;

        _spawnTimer1 += Time.deltaTime;
        _spawnTimer2 += Time.deltaTime;
        _spawnTimer3 += Time.deltaTime;
        _spawnTimer4 += Time.deltaTime;

        if (_totalSpawnedNCountThisWave < CurrentWaveInfo._enemyCountNTotal &&
            _currentNCount < CurrentWaveInfo._enemyCountN &&
            _spawnTimer1 > CurrentWaveInfo._spawnTime)
        {
            SpawnEnemy(EnemyType.Regular);
            _spawnTimer1 = 0f;
        }

        if (_totalSpawnedSp1CountThisWave < CurrentWaveInfo._enemyCountSp1Total &&
            _currentSp1Count < CurrentWaveInfo._enemyCountSp1 &&
            _spawnTimer2 > CurrentWaveInfo._spawnTime)
        {
            SpawnEnemy(EnemyType.Special1);
            _spawnTimer2 = 0f;
        }

        if (_totalSpawnedSp2CountThisWave < CurrentWaveInfo._enemyCountSp2Total &&
            _currentSp2Count < CurrentWaveInfo._enemyCountSp2 &&
            _spawnTimer3 > CurrentWaveInfo._spawnTime)
        {
            SpawnEnemy(EnemyType.Special2);
            _spawnTimer3 = 0f;
        }

        if (_totalSpawnedSp3CountThisWave < CurrentWaveInfo._enemyCountSp3Total &&
            _currentSp3Count < CurrentWaveInfo._enemyCountSp3 &&
            _spawnTimer4 > CurrentWaveInfo._spawnTime)
        {
            SpawnEnemy(EnemyType.Special3);
            _spawnTimer4 = 0f;
        }
    }

    private void OnAllClientsLoadedGameScene(string sceneName)
    {
        Debug.Log("[GameManager] All clients loaded game scene.");

        _gameUI = FindFirstObjectByType<GameUI>();
        if (_gameUI != null)
            _gameUI.UpdateWaveText(CurrentWave.ToString());

        if (_netBoot == null || !_netBoot.IsServer)
            return;

        EnsureBulletPool();
        RebuildServerPlayerList();

        if (ZombiePoolManager.Instance != null)
        {
            if (_regularZombiePrefab) ZombiePoolManager.Instance.EnsurePool(EnemyType.Regular, _regularZombiePrefab, _prewarmRegular);
            if (_special1ZombiePrefab) ZombiePoolManager.Instance.EnsurePool(EnemyType.Special1, _special1ZombiePrefab, _prewarmSpecial1);
            if (_special2ZombiePrefab) ZombiePoolManager.Instance.EnsurePool(EnemyType.Special2, _special2ZombiePrefab, _prewarmSpecial2);
            if (_special3ZombiePrefab) ZombiePoolManager.Instance.EnsurePool(EnemyType.Special3, _special3ZombiePrefab, _prewarmSpecial3);
        }

        _canSpawnEnemies = true;
        _inSafePhase = false;
    }

    private void EnsureBulletPool()
    {
        if (_bulletPool != null)
            return;

        _bulletPool = GetComponent<ServerBulletPool>();
        if (_bulletPool == null)
            _bulletPool = gameObject.AddComponent<ServerBulletPool>();

        _bulletPool.BulletHitMask = _bulletHitMask;
    }

    public void RegisterServerPlayer(NetworkPlayerController player)
    {
        if (_netBoot == null || !_netBoot.IsServer || player == null)
            return;

        if (_serverPlayers.Contains(player))
            return;

        _serverPlayers.Add(player);
    }

    public void UnregisterServerPlayer(NetworkPlayerController player)
    {
        if (player == null)
            return;

        _serverPlayers.Remove(player);
    }

    public void RebuildServerPlayerList()
    {
        _serverPlayers.Clear();

        var players = FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayerController player = players[i];
            if (player == null)
                continue;

            if (!player.IsSpawned)
                continue;

            RegisterServerPlayer(player);
        }
    }

    private void SpawnEnemy(EnemyType type)
    {
        if (_netBoot == null || !_netBoot.IsServer)
            return;

        if (Map.Instance.SpawnPoints == null || Map.Instance.SpawnPoints.Length == 0)
        {
            Debug.LogWarning("[GameManager] No spawn points assigned.");
            return;
        }

        if (ZombiePoolManager.Instance == null)
        {
            Debug.LogError("[GameManager] ZombiePoolManager.Instance is null.");
            return;
        }

        int randIndex = UnityEngine.Random.Range(0, Map.Instance.SpawnPoints.Length);
        Transform spawnPoint = Map.Instance.SpawnPoints[randIndex];

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        Zombie zombie = ZombiePoolManager.Instance.SpawnZombie(spawnPos, spawnRot, type);
        if (zombie == null)
            return;

        switch (type)
        {
            case EnemyType.Regular:
                _currentNCount++;
                _totalSpawnedNCountThisWave++;
                break;

            case EnemyType.Special1:
                _currentSp1Count++;
                _totalSpawnedSp1CountThisWave++;
                break;

            case EnemyType.Special2:
                _currentSp2Count++;
                _totalSpawnedSp2CountThisWave++;
                break;

            case EnemyType.Special3:
                _currentSp3Count++;
                _totalSpawnedSp3CountThisWave++;
                break;
        }
    }

    private Zombie GetPrefabForType(EnemyType type)
    {
        return type switch
        {
            EnemyType.Regular => _regularZombiePrefab,
            EnemyType.Special1 => _special1ZombiePrefab,
            EnemyType.Special2 => _special2ZombiePrefab,
            EnemyType.Special3 => _special3ZombiePrefab,
            _ => null
        };
    }

    private void CheckWaveCompletion()
    {
        if (_currentNCount + _currentSp1Count + _currentSp2Count + _currentSp3Count > 0)
            return;

        _currentWaveIndex++;

        if (_currentWaveIndex >= _waveInfos.Length)
        {
            Debug.Log("[GameManager] All waves completed.");
            _currentWaveIndex = Mathf.Max(0, _waveInfos.Length - 1);
            _canSpawnEnemies = false;
            return;
        }

        _currentNCount = 0;
        _totalSpawnedNCountThisWave = 0;

        _currentSp1Count = 0;
        _totalSpawnedSp1CountThisWave = 0;

        _currentSp2Count = 0;
        _totalSpawnedSp2CountThisWave = 0;

        _currentSp3Count = 0;
        _totalSpawnedSp3CountThisWave = 0;

        _canSpawnEnemies = false;
        _inSafePhase = true;
        _safePhaseTimer = 0f;
    }

    public void OnEnemyKilled(EnemyType type, ulong killerClientId)
    {
        switch (type)
        {
            case EnemyType.Regular:
                _currentNCount = Mathf.Max(0, _currentNCount - 1);
                AwardPoints(killerClientId, _pointsRegular);
                break;

            case EnemyType.Special1:
                _currentSp1Count = Mathf.Max(0, _currentSp1Count - 1);
                AwardPoints(killerClientId, _pointsSpecial1);
                break;

            case EnemyType.Special2:
                _currentSp2Count = Mathf.Max(0, _currentSp2Count - 1);
                AwardPoints(killerClientId, _pointsSpecial2);
                break;

            case EnemyType.Special3:
                _currentSp3Count = Mathf.Max(0, _currentSp3Count - 1);
                AwardPoints(killerClientId, _pointsSpecial3);
                break;
        }

        CheckWaveCompletion();
    }
    private void AwardPoints(ulong clientId, int points)
    {
        if (_netBoot == null || !_netBoot.IsServer)
            return;

        if (points <= 0)
            return;

        for (int i = 0; i < _serverPlayers.Count; i++)
        {
            NetworkPlayerController player = _serverPlayers[i];
            if (player == null || !player.IsSpawned)
                continue;

            if (player.OwnerClientId != clientId)
                continue;

            player.AddPoints(points);
            return;
        }
    }
}