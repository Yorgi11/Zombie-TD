using UnityEngine;
using QF_Tools.QF_Utilities;
public class GameManager : QF_Singleton<GameManager>
{
    [SerializeField] private NetBootstrap _netBoot;
    [SerializeField] private LayerMask _bulletHitMask;
    public Gun[] _guns;
    private ServerBulletPool _bulletPool;
    protected override void Awake()
    {
        base.Awake();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 240;
    }
    private void Update()
    {
        if (_bulletPool) _bulletPool.UpdateBullets(Time.deltaTime);
        else if (_netBoot && _netBoot.IsServer)
        {
            _bulletPool = gameObject.AddComponent<ServerBulletPool>();
            _bulletPool.BulletHitMask = _bulletHitMask;
        }
    }
}