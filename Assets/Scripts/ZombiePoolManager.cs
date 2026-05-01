using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using QF_Tools.QF_Utilities;
public class ZombiePoolManager : QF_Singleton<ZombiePoolManager>
{
    [System.Serializable]
    public struct ZombiePoolConfig
    {
        public GameManager.EnemyType EnemyType;
        public Zombie Prefab;
        public int PrewarmCount;
    }

    [SerializeField] private ZombiePoolConfig[] _configs;

    private readonly Dictionary<GameManager.EnemyType, ZombiePool> _zombiePools = new();

    protected override void Awake()
    {
        base.Awake();

        for (int i = 0; i < _configs.Length; i++)
        {
            ZombiePoolConfig cfg = _configs[i];
            if (cfg.Prefab == null || _zombiePools.ContainsKey(cfg.EnemyType))
                continue;

            ZombiePool pool = new ZombiePool(cfg.EnemyType, cfg.Prefab);
            _zombiePools.Add(cfg.EnemyType, pool);

            for (int j = 0; j < Mathf.Max(0, cfg.PrewarmCount); j++)
                pool.AddZombie();
        }
    }

    public void EnsurePool(GameManager.EnemyType enemyType, Zombie prefab, int prewarmCount = 0)
    {
        if (_zombiePools.ContainsKey(enemyType))
            return;

        if (prefab == null)
        {
            Debug.LogError($"[ZombiePoolManager] Cannot create pool for {enemyType}. Prefab is null.");
            return;
        }

        ZombiePool pool = new ZombiePool(enemyType, prefab);
        _zombiePools.Add(enemyType, pool);

        for (int i = 0; i < Mathf.Max(0, prewarmCount); i++)
            pool.AddZombie();
    }

    public Zombie SpawnZombie(Vector3 position, Quaternion rotation, GameManager.EnemyType enemyType)
    {
        if (!IsServer())
            return null;

        if (!_zombiePools.TryGetValue(enemyType, out ZombiePool pool))
        {
            Debug.LogError($"[ZombiePoolManager] No pool exists for {enemyType}.");
            return null;
        }

        return pool.SpawnZombie(position, rotation);
    }

    public void ReturnZombie(Zombie zombie)
    {
        if (!IsServer() || zombie == null)
            return;

        if (!_zombiePools.TryGetValue(zombie.EnemyType, out ZombiePool pool))
        {
            Debug.LogError($"[ZombiePoolManager] No pool exists for returned zombie type {zombie.EnemyType}.");
            return;
        }

        pool.ReturnZombie(zombie);
    }

    private bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}
public class ZombiePool
{
    private readonly GameManager.EnemyType _enemyType;
    private readonly Zombie _prefab;

    private readonly List<Zombie> _reserveZombies = new();
    private readonly List<Zombie> _activeZombies = new();
    private readonly Dictionary<Zombie, int> _activeZombieIndices = new();

    public ZombiePool(GameManager.EnemyType enemyType, Zombie prefab)
    {
        _enemyType = enemyType;
        _prefab = prefab;
    }

    public void AddZombie()
    {
        Zombie zombie = Object.Instantiate(_prefab);
        zombie.InitializeServer(_enemyType);

        NetworkObject no = zombie.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError($"[ZombiePool] Zombie prefab '{_prefab.name}' is missing NetworkObject.");
            Object.Destroy(zombie.gameObject);
            return;
        }

        if (!no.IsSpawned)
            no.Spawn(true);

        zombie.OnReturnedToPool();
        _reserveZombies.Add(zombie);
    }

    public Zombie SpawnZombie(Vector3 position, Quaternion rotation)
    {
        if (_reserveZombies.Count <= 0)
            AddZombie();

        int lastIndex = _reserveZombies.Count - 1;
        Zombie zombie = _reserveZombies[lastIndex];
        _reserveZombies.RemoveAt(lastIndex);

        zombie.OnTakenFromPool(position, rotation);
        _activeZombieIndices[zombie] = _activeZombies.Count;
        _activeZombies.Add(zombie);

        return zombie;
    }

    public void ReturnZombie(Zombie zombie)
    {
        if (_activeZombieIndices.TryGetValue(zombie, out int index))
        {
            int last = _activeZombies.Count - 1;
            Zombie lastZombie = _activeZombies[last];
            _activeZombies[index] = lastZombie;
            _activeZombies.RemoveAt(last);

            if (index < last)
                _activeZombieIndices[lastZombie] = index;

            _activeZombieIndices.Remove(zombie);
        }

        zombie.OnReturnedToPool();
        _reserveZombies.Add(zombie);
    }
}
