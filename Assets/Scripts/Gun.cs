using System;
using UnityEngine;
using QF_Tools.QF_Utilities;

public class Gun : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int _fireRate;
    [SerializeField] private int _bulletVelocity;
    [SerializeField] private int _bulletDamage;
    [SerializeField] private int _bulletPenetration;
    [Space]
    [SerializeField] private int _ammoPerMag;
    [SerializeField] private int _totalAmmo;
    [SerializeField] private float _reloadTime;
    [Space]
    [SerializeField] private Transform _bulletSpawn;

    [Header("Fire Mode")]
    [SerializeField] private bool _isSemiAuto = false;

    [Header("Animations")]
    [SerializeField] private Vector3 _aimPosition;
    [SerializeField] private Vector3 _hipPosition;
    [SerializeField] private float _aimTransitionSpeed = 12f;
    [Space]
    [SerializeField] private float _recoilSlide = 0.08f;
    [SerializeField] private Vector3 _recoilRot = new(-6f, 0f, 0f);
    [SerializeField] private float _animationSnap = 20f;
    [SerializeField] private float _recoilReturnSpeed = 14f;

    private bool _canShoot = true;
    private bool _isReloading = false;
    private bool _semiAutoTriggerLocked = false;

    private int _currentAmmoInMag;
    private int _currentReserveAmmo;

    private Transform _t;

    private Vector3 _currentBasePosition;
    private Vector3 _targetBasePosition;

    private Vector3 _currentRecoilPosition;
    private Vector3 _targetRecoilPosition;

    private Vector3 _currentRecoilRotation;
    private Vector3 _targetRecoilRotation;

    public float TimeBetweenShots => 1f / (_fireRate / 60f);
    public Vector3 HipPosition => _hipPosition;

    public int BulletVelocity => _bulletVelocity;
    public int BulletDamage => _bulletDamage;
    public int BulletPenetration => _bulletPenetration;
    public Transform BulletSpawn => _bulletSpawn;

    public event Action<Gun> OnShotRequested;

    private void Awake()
    {
        _t = transform;
        _currentAmmoInMag = _ammoPerMag;
        _currentReserveAmmo = _totalAmmo;
        _currentBasePosition = _hipPosition;
        _targetBasePosition = _hipPosition;
    }

    public void RunUpdate(bool isAiming, float dt, Vector3 aimTarget)
    {
        _targetBasePosition = isAiming ? _aimPosition : _hipPosition;
        _currentBasePosition = Vector3.Lerp(_currentBasePosition, _targetBasePosition, _aimTransitionSpeed * dt);

        _targetRecoilPosition = Vector3.Lerp(_targetRecoilPosition, Vector3.zero, _recoilReturnSpeed * dt);
        _targetRecoilRotation = Vector3.Lerp(_targetRecoilRotation, Vector3.zero, _recoilReturnSpeed * dt);

        _currentRecoilPosition = Vector3.Lerp(_currentRecoilPosition, _targetRecoilPosition, _animationSnap * dt);
        _currentRecoilRotation = Vector3.Lerp(_currentRecoilRotation, _targetRecoilRotation, _animationSnap * dt);

        Vector3 finalLocalPos = _currentBasePosition + _currentRecoilPosition;
        Quaternion finalLocalRot = Quaternion.Euler(_currentRecoilRotation);
        _t.SetLocalPositionAndRotation(finalLocalPos, finalLocalRot);
    }

    public void TryShoot()
    {
        if (_isSemiAuto && _semiAutoTriggerLocked) return;
        Shoot();
    }

    public void ReleaseTrigger()
    {
        _semiAutoTriggerLocked = false;
    }

    private void Shoot()
    {
        if (!_canShoot || _isReloading || _currentAmmoInMag <= 0) return;

        AddRecoil();

        _currentAmmoInMag--;
        OnShotRequested?.Invoke(this);

        if (_isSemiAuto) _semiAutoTriggerLocked = true;
        if (_currentAmmoInMag <= 0 && _currentReserveAmmo > 0) Reload();

        StartCoroutine(QF_Coroutines.DelayBoolChange(false, true, TimeBetweenShots, v => _canShoot = v));
    }

    public void Reload()
    {
        if (_isReloading || _currentReserveAmmo <= 0 || _currentAmmoInMag >= _ammoPerMag) return;

        StartCoroutine(QF_Coroutines.DelayRunFunction(
            true,
            false,
            _reloadTime,
            (() => ExecuteReload(), v => _isReloading = v)
        ));
    }

    private void ExecuteReload()
    {
        int missingAmmo = _ammoPerMag - _currentAmmoInMag;
        int ammoToAdd = Mathf.Min(missingAmmo, _currentReserveAmmo);

        _currentAmmoInMag += ammoToAdd;
        _currentReserveAmmo -= ammoToAdd;
    }

    private void AddRecoil()
    {
        _targetRecoilPosition += Vector3.back * _recoilSlide;
        _targetRecoilRotation += _recoilRot;
    }
}