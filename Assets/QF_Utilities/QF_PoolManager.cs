using System;
using System.Collections.Generic;
using UnityEngine;

namespace QF_Tools.QF_Utilities
{
    public class QF_PoolManager : QF_Singleton<QF_PoolManager>
    {
        [Header("Authoring")]
        public List<QF_PoolConfig> Configs = new();
        readonly Dictionary<Type, IQF_PoolRuntime> _pools = new();

        protected override void Awake()
        {
            base.Awake();
            BuildPools();
        }
        void BuildPools()
        {
            _pools.Clear();
            foreach (var cfg in Configs)
            {
                if (cfg == null || cfg.prefab == null)
                {
                    Debug.LogWarning("[PoolManager] Skipping invalid config.");
                    continue;
                }

                var parent = cfg.parent != null ? cfg.parent : transform;
                var type = cfg.prefab.GetType(); // concrete Component type

                // Create PoolRuntime<T> with the prefab’s concrete type
                var runtimeType = typeof(QF_PoolRuntime<>).MakeGenericType(type);
                var runtime = (IQF_PoolRuntime)System.Activator.CreateInstance(
                    runtimeType,
                    cfg.prefab,
                    parent,
                    cfg.initialSize
                );

                _pools.Add(type, runtime);
            }
        }
        // Public API //
        public T Get<T>() where T : Component
        {
            if (!_pools.TryGetValue(typeof(T), out var pool))
            {
                Debug.LogError($"[PoolManager] No pool with tag '{tag}'.");
                return null;
            }
            return pool.Get() as T;
        }
        public T Get<T>(Vector3 pos, Quaternion rot) where T : Component
        {
            if (!_pools.TryGetValue(typeof(T), out var pool))
            {
                Debug.LogError($"[PoolManager] No pool with tag '{tag}'.");
                return null;
            }
            return pool.Get(pos, rot) as T;
        }
        public T Get<T>(Transform parent, bool worldPositionStays = false) where T : Component
        {
            if (!_pools.TryGetValue(typeof(T), out var pool))
            {
                Debug.LogError($"[PoolManager] No pool with tag '{tag}'.");
                return null;
            }
            return pool.Get(parent, worldPositionStays) as T;
        }
        public void Return(Component c)
        {
            if (c == null) return;
            if (_pools.TryGetValue(c.GetType(), out var pool)) pool.Return(c);
        }
    }
    [System.Serializable]
    public class QF_PoolConfig
    {
        public Component prefab;
        public int initialSize = 0;
        public Transform parent;
    }
    interface IQF_PoolRuntime
    {
        Component Get();
        Component Get(Vector3 pos, Quaternion rot);
        Component Get(Transform parent, bool worldPositionStays);
        void Return(Component c);
    }
    class QF_PoolRuntime<T> : IQF_PoolRuntime where T : Component
    {
        readonly T _prefab;
        readonly Transform _parent;
        readonly List<T> _reserved = new();
        readonly HashSet<T> _active = new();

        public QF_PoolRuntime(T prefab, Transform parent, int prewarm)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < prewarm; i++) AddOne();
        }
        void AddOne()
        {
            var inst = UnityEngine.Object.Instantiate(_prefab, _parent);
            inst.gameObject.SetActive(false);
            _reserved.Add(inst);
        }
        T Rent()
        {
            if (_reserved.Count == 0) AddOne();
            int last = _reserved.Count - 1;
            var obj = _reserved[last];
            _reserved.RemoveAt(last);
            _active.Add(obj);
            obj.gameObject.SetActive(true);
            return obj;
        }
        public Component Get() => Rent();
        public Component Get(Vector3 pos, Quaternion rot)
        {
            var o = Rent();
            o.transform.SetPositionAndRotation(pos, rot);
            return o;
        }
        public Component Get(Transform parent, bool worldPositionStays)
        {
            var o = Rent();
            o.transform.SetParent(parent, worldPositionStays);
            o.transform.localPosition = Vector3.zero;
            o.transform.localRotation = Quaternion.identity;
            return o;
        }
        public void Return(Component c)
        {
            if (c is not T t || !_active.Remove(t)) return;
            t.transform.SetParent(_parent, false);
            t.gameObject.SetActive(false);
            _reserved.Add(t);
        }
    }
}