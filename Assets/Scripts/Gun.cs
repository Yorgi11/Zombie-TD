using System;
using UnityEngine;
public class Gun : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private int _fireRate;
    [SerializeField] private int _bulletVelocity;
    [SerializeField] private int _bulletDamage;
    [SerializeField] private int _bulletPenetration;
    [Space]
    [SerializeField] private Transform _bulletSpawn;

    [Header("Ammo")]
    [SerializeField] private bool _unlimitedAmmo = false;
    [SerializeField] private int _ammoPerMag;
    [SerializeField] private int _totalAmmo;
    [SerializeField] private float _reloadTime;

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

    private bool _isReloading;
    private bool _semiAutoTriggerLocked;

    private int _currentAmmoInMag;
    private int _currentReserveAmmo;

    private float _timeBetweenShots;
    private float _nextAllowedShotTime;
    private float _reloadCompleteTime = -1f;

    private Transform _t;

    private Vector3 _currentBasePosition;
    private Vector3 _targetBasePosition;

    private Vector3 _currentRecoilPosition;
    private Vector3 _targetRecoilPosition;

    private Vector3 _currentRecoilRotation;
    private Vector3 _targetRecoilRotation;

    public bool UnlimitedAmmo => _unlimitedAmmo;
    public int CurrentAmmoInMag => _currentAmmoInMag;
    public int CurrentReserveAmmo => _currentReserveAmmo;
    public int BulletVelocity => _bulletVelocity;
    public int BulletDamage => _bulletDamage;
    public int BulletPenetration => _bulletPenetration;
    public float TimeBetweenShots => _timeBetweenShots;
    public Vector3 HipPosition => _hipPosition;
    public Transform BulletSpawn => _bulletSpawn;
    public event Action<Gun> OnShotRequested;
    public event Action<int, int> OnAmmoChanged;
    private void Awake()
    {
        _t = transform;
        _currentAmmoInMag = _ammoPerMag;
        _currentReserveAmmo = _unlimitedAmmo ? _totalAmmo : _totalAmmo;

        _currentBasePosition = _hipPosition;
        _targetBasePosition = _hipPosition;

        _timeBetweenShots = _fireRate > 0 ? 60f / _fireRate : 999f;
        RaiseAmmoChanged();
    }
    public void RunUpdate(bool isAiming, float dt, Vector3 aimTargetWorld)
    {
        if (_isReloading && Time.time >= _reloadCompleteTime)
        {
            _isReloading = false;
            ExecuteReload();
            _reloadCompleteTime = -1f;
        }
        _targetBasePosition = isAiming ? _aimPosition : _hipPosition;
        _currentBasePosition = Vector3.Lerp(_currentBasePosition, _targetBasePosition, _aimTransitionSpeed * dt);

        _targetRecoilPosition = Vector3.Lerp(_targetRecoilPosition, Vector3.zero, _recoilReturnSpeed * dt);
        _targetRecoilRotation = Vector3.Lerp(_targetRecoilRotation, Vector3.zero, _recoilReturnSpeed * dt);

        _currentRecoilPosition = Vector3.Lerp(_currentRecoilPosition, _targetRecoilPosition, _animationSnap * dt);
        _currentRecoilRotation = Vector3.Lerp(_currentRecoilRotation, _targetRecoilRotation, _animationSnap * dt);

        _t.localPosition = _currentBasePosition + _currentRecoilPosition;
        ApplyAimRotation(aimTargetWorld);
    }
    private void ApplyAimRotation(Vector3 aimTargetWorld)
    {
        Transform pivot = _bulletSpawn != null ? _bulletSpawn : _t;
        Vector3 aimDir = aimTargetWorld - pivot.position;
        if (aimDir.sqrMagnitude <= 0.0001f)
        {
            _t.localRotation = Quaternion.Euler(_currentRecoilRotation);
            return;
        }
        aimDir.Normalize();
        Vector3 up = _t.parent != null ? _t.parent.up : Vector3.up;
        _t.rotation = Quaternion.LookRotation(aimDir, up) * Quaternion.Euler(_currentRecoilRotation);
    }
    public void TryShoot()
    {
        if (_isSemiAuto && _semiAutoTriggerLocked) return;
        Shoot();
    }
    public void ReleaseTrigger() => _semiAutoTriggerLocked = false;
    private void Shoot()
    {
        if (_isReloading) return;
        if (!_unlimitedAmmo && _currentAmmoInMag <= 0)
        {
            if (_currentReserveAmmo > 0) Reload();
            return;
        }
        if (Time.time < _nextAllowedShotTime) return;
        _nextAllowedShotTime = Time.time + _timeBetweenShots;
        if (_recoilRot.magnitude > 0.0001f) AddRecoil();
        if (!_unlimitedAmmo)
        {
            _currentAmmoInMag--;
            RaiseAmmoChanged();
        }
        OnShotRequested?.Invoke(this);
        if (_isSemiAuto) _semiAutoTriggerLocked = true;
        if (!_unlimitedAmmo && _currentAmmoInMag <= 0 && _currentReserveAmmo > 0) Reload();
    }
    public void Reload()
    {
        if (_unlimitedAmmo) return;
        if (_isReloading) return;
        if (_currentReserveAmmo <= 0) return;
        if (_currentAmmoInMag >= _ammoPerMag) return;
        _isReloading = true;
        _reloadCompleteTime = Time.time + _reloadTime;
    }
    private void ExecuteReload()
    {
        if (_unlimitedAmmo)
        {
            _currentAmmoInMag = _ammoPerMag;
            RaiseAmmoChanged();
            return;
        }
        int missingAmmo = _ammoPerMag - _currentAmmoInMag;
        int ammoToAdd = Mathf.Min(missingAmmo, _currentReserveAmmo);
        _currentAmmoInMag += ammoToAdd;
        _currentReserveAmmo -= ammoToAdd;
        RaiseAmmoChanged();
    }
    private void AddRecoil()
    {
        _targetRecoilPosition += Vector3.back * _recoilSlide;
        _targetRecoilRotation += _recoilRot;
    }
    private void RaiseAmmoChanged() => OnAmmoChanged?.Invoke(_currentAmmoInMag, _currentReserveAmmo);
}