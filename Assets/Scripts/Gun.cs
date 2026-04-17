using UnityEngine;
using QF_Tools.QF_Utilities;
public class Gun : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int _fireRate;
    [SerializeField] private int _bulletVelocity;
    [SerializeField] private int _bulletDamage;
    [Space]
    [SerializeField] private int _ammoPerMag;
    [SerializeField] private int _totalAmmo;
    [SerializeField] private float _reloadTime;
    [Space]
    [SerializeField] private Transform _bulletSpawn;

    [Header("Animations")]
    [SerializeField] private Vector3 _aimPosition;
    [SerializeField] private Vector3 _hipPosition;
    [SerializeField] private float _aimTransitionSpeed;
    [Space]
    [SerializeField] private float _recoilSlide;
    [SerializeField] private Vector3 _recoilRot;
    [SerializeField] private float _animationSnap;

    private bool _canShoot = true;
    private bool _isReloading = false;
    private int _currentAmmoInMag;
    private int _currentReserveAmmo;
    private Vector3 _currentBasePosition;
    private Transform _t;

    public float TimeBetweenShots => 1f / (_fireRate / 60f);
    private void Awake()
    {
        _t = transform;
    }
    public void RunUpdate(bool isAiming, float dt, Vector3 aimTarget)
    {
        _currentBasePosition = isAiming ? _aimPosition : _hipPosition;
        _currentPosition = Vector3.Lerp(_currentPosition, _targetPosition, _animationSnap * dt);
        _currentRotation = Vector3.Lerp(_currentRotation, _targetRotation, _animationSnap * dt);
        _t.SetLocalPositionAndRotation(_currentPosition, Quaternion.Euler(_currentRotation));
        _targetPosition = Vector3.Lerp(_targetPosition, _currentBasePosition, _aimTransitionSpeed * dt);
        _targetRotation = Vector3.Lerp(_targetRotation, (aimTarget - _t.position).normalized, _animationSnap * dt);
    }
    public void Shoot()
    {
        if (!_canShoot || _isReloading || _currentAmmoInMag <= 0) return;
        AddRecoil();
        _currentAmmoInMag--;
        if (_currentAmmoInMag <= 0 && _currentReserveAmmo > 0) Reload();
        StartCoroutine(QF_Coroutines.DelayBoolChange(false, true, TimeBetweenShots, v => _canShoot = v));
    }
    public static void FireBullet(Transform spawnPoint, int velocity, int damage)
    {
        //Rigidbody b = bulletPool.SpawnBullet(spawnPoint.position, spawnPoint.rotation, damage);
        //b.velocity = spawnPoint.forward * velocity;
    }
    public void Reload()
    {
        StartCoroutine(QF_Coroutines.DelayRunFunction(true, false, _reloadTime, (() => ExecuteReload(), v => _isReloading = v)));
    }
    private void ExecuteReload()
    {
        int ammoToAdd = _ammoPerMag;
        if (_ammoPerMag > _currentReserveAmmo) ammoToAdd = _currentReserveAmmo;

        _currentAmmoInMag = ammoToAdd;
        _currentReserveAmmo -= ammoToAdd;
    }
    private Vector3 _targetRotation;
    private Vector3 _targetPosition;
    private Vector3 _currentRotation;
    private Vector3 _currentPosition;
    private void AddRecoil()
    {
        _targetRotation += _recoilRot;
        _targetPosition += Vector3.back * _recoilSlide;
    }
}