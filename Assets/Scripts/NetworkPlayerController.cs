using Unity.Netcode;
using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DamageableObject))]
public sealed class NetworkPlayerController : NetworkBehaviour
{
    private enum MoveState { Crouch, Walking, Running }

    [Header("Movement")]
    [SerializeField] private float _moveAcceleration = 30f;
    [SerializeField] private float[] _moveSpeeds = new float[3] { 3f, 5f, 10f };

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1f;
    [SerializeField] private float _groundCheckRadius = 0.5f;
    [SerializeField] private float _groundCheckOffset = -0.05f;
    [SerializeField] private float _jumpBufferTime = 0.15f;
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("Camera")]
    [SerializeField] private float _mouseSensitivity = 12f;
    [SerializeField] private Vector2 _camLimits = new(-80f, 80f);
    [SerializeField] private Transform _cameraTarget;
    [SerializeField, Range(0f, 1f)] private float _animationCamShake = 1f;
    [SerializeField] private float _maxLookDeltaPerFrame = 50f;
    [Space]
    [SerializeField] private Transform _aimTarget;

    [Header("Networking")]
    [SerializeField] private float _inputSendRate = 30f;

    private InputSystem_Actions _input;
    private DamageableObject _damageableObject;

    private Transform _t;
    private Rigidbody _rb;

    private Gun _currentGun;
    private Gun _equippedGunDefinition;

    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private bool _menuInteract;

    private bool _jumpInput;
    private bool _attackHeld;
    private bool _isAiming;

    private float _localXRot;
    private float _localYRot;
    private float _serverYaw;

    private Camera _cam;
    private Transform _camT;

    private Vector2 _serverMoveInput;
    private MoveState _serverMoveState;

    private bool _networkInitialized;
    private bool _localOwnerInitialized;

    private float _nextInputSendTime;
    private Vector2 _lastSentMoveInput;
    private float _lastSentYaw;
    private bool _lastSentJumpInput;
    private MoveState _lastSentMoveState;

    private float _serverNextAllowedShotTime;
    private float _jumpLockUntil;

    private readonly Collider[] _groundHits = new Collider[8];
    public Vector3 SpawnPos { get; private set; }
    private void Awake()
    {
        _t = transform;
        _rb = GetComponent<Rigidbody>();
        _damageableObject = GetComponent<DamageableObject>();
    }
    public override void OnNetworkSpawn() =>InitializeNetworkState();
    public override void OnGainedOwnership() => InitializeNetworkState();
    public override void OnNetworkDespawn()
    {
        CleanupLocalOwnerState();
        _networkInitialized = false;
    }
    public override void OnLostOwnership() => CleanupLocalOwnerState();
    private void InitializeNetworkState()
    {
        if (!IsSpawned) return;
        if (!_networkInitialized)
        {
            _networkInitialized = true;
            if (GameManager.Instance != null && GameManager.Instance._guns != null && GameManager.Instance._guns.Length > 0)
                _equippedGunDefinition = GameManager.Instance._guns[0];
        }
        if (IsOwner && !_localOwnerInitialized)
        {
            _localOwnerInitialized = true;
            _input ??= new();
            _input.Enable();
            ToggleMouse();
            SetupLocalCamera();
            if (_equippedGunDefinition != null && _camT != null)
            {
                _currentGun = Instantiate(_equippedGunDefinition, _equippedGunDefinition.HipPosition, Quaternion.identity, _camT);
                _currentGun.OnShotRequested += HandleLocalShotRequested;
            }
            if (_aimTarget != null && _camT != null) _aimTarget.SetParent(_camT, true);
            _nextInputSendTime = 0f;
            _lastSentMoveInput = new Vector2(999f, 999f);
            _lastSentYaw = float.MaxValue;
            _lastSentJumpInput = false;
            _lastSentMoveState = MoveState.Walking;
        }
        SpawnPos = _t.position;
        _damageableObject.Die += HandleDeath;
    }
    private void CleanupLocalOwnerState()
    {
        if (!_localOwnerInitialized) return;
        _localOwnerInitialized = false;
        _input?.Disable();
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (_currentGun != null)
        {
            _currentGun.OnShotRequested -= HandleLocalShotRequested;
            Destroy(_currentGun.gameObject);
            _currentGun = null;
        }
        _cam = null;
        _camT = null;
    }
    private void ToggleMouse()
    {
        switch (Cursor.lockState)
        {
            case CursorLockMode.None:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _menuInteract = false;
                break;
            case CursorLockMode.Locked:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _menuInteract = true;
                break;
        }
    }
    private void HandleDeath()
    {
        if (!IsOwner) return;
        GameUI.Instance.ToggleDeathScreen();
        ToggleMouse();
    }
    public void HandleRespawn()
    {
        if (!IsOwner) return;
        _t.position = SpawnPos;
    }
    private void Update()
    {
        if (!IsSpawned || !IsOwner || !_localOwnerInitialized || _menuInteract) return;
        ReadLocalInput();

        UpdateCamera();
        if (_currentGun != null)
        {
            _currentGun.RunUpdate(_isAiming, Time.deltaTime);
            if (_attackHeld) _currentGun.TryShoot();
            if (_input.Player.Attack.WasReleasedThisFrame()) _currentGun.ReleaseTrigger();
        }

        SendInputToServerIfNeeded();
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || _menuInteract) return;
        HandleJump();
        _t.rotation = Quaternion.Euler(0f, _serverYaw, 0f);
        FixedUpdateMovement();
    }
    private void LateUpdate()
    {
        if (!IsSpawned || !IsOwner || !_localOwnerInitialized || _menuInteract) return;
        LateUpdateCamera();
    }
    private void ReadLocalInput()
    {
        _moveInput = _input.Player.Move.ReadValue<Vector2>();
        _lookInput = _input.Player.Look.ReadValue<Vector2>();
        _jumpInput = _input.Player.Jump.IsPressed();
        _attackHeld = _input.Player.Attack.IsPressed();
        _isAiming = _input.UI.RightClick.IsPressed();
    }
    private MoveState GetCurrentMoveState()
    {
        if (_input.Player.Crouch.IsPressed()) return MoveState.Crouch;
        if (_input.Player.Sprint.IsPressed()) return MoveState.Running;
        return MoveState.Walking;
    }
    private void SetupLocalCamera()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogWarning($"[NetworkPlayerController] No main camera found on {name}. IsOwner={IsOwner}");
            return;
        }
        if (_cameraTarget == null)
        {
            Debug.LogWarning($"[NetworkPlayerController] _cameraTarget is null on {name}");
            return;
        }
        _camT = _cam.transform;
        _localYRot = _t.eulerAngles.y;
        _serverYaw = _t.eulerAngles.y;
        _camT.SetPositionAndRotation(
            _cameraTarget.position,
            Quaternion.Euler(_localXRot, _localYRot, 0f)
        );
    }
    private void UpdateCamera()
    {
        float clampedLookX = Mathf.Clamp(_lookInput.x, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        float clampedLookY = Mathf.Clamp(_lookInput.y, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        Vector2 input = _mouseSensitivity * new Vector2(clampedLookX, clampedLookY);
        _localXRot = Mathf.Clamp(_localXRot - input.y, _camLimits.x, _camLimits.y);
        _localYRot += input.x;
    }
    private void LateUpdateCamera()
    {
        if (_camT == null || _cameraTarget == null) return;
        Quaternion targetRot = Quaternion.Euler(_localXRot, _localYRot, 0f);
        _camT.SetPositionAndRotation(
            _cameraTarget.position,
            Quaternion.Slerp(_camT.rotation, targetRot, _animationCamShake)
        );
    }
    private void SendInputToServerIfNeeded()
    {
        float interval = _inputSendRate > 0f ? (1f / _inputSendRate) : 0.0333f;
        if (Time.unscaledTime < _nextInputSendTime) return;
        _nextInputSendTime = Time.unscaledTime + interval;
        Vector2 clampedMove = Vector2.ClampMagnitude(_moveInput, 1f);
        MoveState moveState = GetCurrentMoveState();
        float yaw = _localYRot;
        bool changed =
            clampedMove != _lastSentMoveInput ||
            _jumpInput != _lastSentJumpInput ||
            moveState != _lastSentMoveState ||
            Mathf.Abs(Mathf.DeltaAngle(_lastSentYaw, yaw)) > 0.05f;
        if (!changed) return;
        _lastSentMoveInput = clampedMove;
        _lastSentJumpInput = _jumpInput;
        _lastSentMoveState = moveState;
        _lastSentYaw = yaw;
        if (IsServer)
        {
            ApplyInputAuthoritative(clampedMove, (int)moveState, yaw, _jumpInput);
            return;
        }
        SubmitInputServerRpc(clampedMove, (int)moveState, yaw, _jumpInput);
    }
    private void ApplyInputAuthoritative(Vector2 moveInput, int moveStateIndex, float yaw, bool jumpInput)
    {
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        if (moveStateIndex < 0 || moveStateIndex >= _moveSpeeds.Length) moveStateIndex = (int)MoveState.Walking;
        _serverMoveInput = moveInput;
        _serverMoveState = (MoveState)moveStateIndex;
        _serverYaw = yaw;
        _jumpInput = jumpInput;
    }
    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 moveInput, int moveStateIndex, float yaw, bool jumpInput)
    => ApplyInputAuthoritative(moveInput, moveStateIndex, yaw, jumpInput);
    private void FixedUpdateMovement()
    {
        Vector3 moveDir = (_t.forward * _serverMoveInput.y + _t.right * _serverMoveInput.x);
        if (moveDir.sqrMagnitude > 1e-6f) moveDir.Normalize();
        float speed = _moveSpeeds[(int)_serverMoveState];
        Vector3 targetVel = moveDir * speed;
        Vector3 vel = _rb.linearVelocity;
        Vector3 velXZ = new(vel.x, 0f, vel.z);
        Vector3 accel = (targetVel - velXZ) * _moveAcceleration;
        _rb.AddForce(accel, ForceMode.Acceleration);
    }
    private bool CheckGround(out Vector3 groundPoint)
    {
        groundPoint = _t.position;
        Vector3 sphereCenter = _t.position + Vector3.up * _groundCheckOffset + (_groundCheckRadius * Vector3.up);
        int hitCount = Physics.OverlapSphereNonAlloc(
            sphereCenter,
            _groundCheckRadius,
            _groundHits,
            _groundMask,
            QueryTriggerInteraction.Ignore
        );
        if (hitCount <= 0) return false;

        float bestDistSqr = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _groundHits[i];
            if (hit == null) continue;
            if (hit.transform.IsChildOf(_t)) continue;
            if (hit.attachedRigidbody == _rb) continue;
            Vector3 closest = hit.ClosestPoint(_t.position);
            float distSqr = (_t.position - closest).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                groundPoint = closest;
                found = true;
            }
        }
        return found;
    }
    private void HandleJump()
    {
        if (!_jumpInput || Time.time < _jumpLockUntil) return;
        if (!CheckGround(out Vector3 groundPoint)) return;
        _rb.position = groundPoint;

        Vector3 vel = _rb.linearVelocity;
        vel.y = 0f;
        _rb.linearVelocity = vel;
        float jumpVelocity = Mathf.Sqrt(-2f * Physics.gravity.y * Mathf.Max(0.01f, _jumpHeight));
        _rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
        _jumpLockUntil = Time.time + _jumpBufferTime;
    }
    private void HandleLocalShotRequested(Gun gun)
    {
        if (gun == null || gun.BulletSpawn == null) return;
        Vector3 origin = gun.BulletSpawn.position;
        Vector3 direction = gun.BulletSpawn.forward;
        if (direction.sqrMagnitude <= 0.0001f) return;
        direction.Normalize();
        if (IsServer)
        {
            ServerHandleFireRequest(origin, direction);
            return;
        }
        RequestFireServerRpc(origin, direction);
    }
    [ServerRpc]
    private void RequestFireServerRpc(Vector3 origin, Vector3 direction)
    => ServerHandleFireRequest(origin, direction);
    private void ServerHandleFireRequest(Vector3 origin, Vector3 direction)
    {
        if (!IsServer) return;
        if (ServerBulletPool.Instance == null) return;
        if (_equippedGunDefinition == null) return;
        if (direction.sqrMagnitude <= 0.0001f) return;
        float now = Time.time;
        float timeBetweenShots = Mathf.Max(0.01f, _equippedGunDefinition.TimeBetweenShots);
        if (now < _serverNextAllowedShotTime) return;
        direction.Normalize();
        _serverNextAllowedShotTime = now + timeBetweenShots;
        ServerBulletPool.Instance.SpawnBullet(
            origin,
            direction * _equippedGunDefinition.BulletVelocity,
            _equippedGunDefinition.BulletDamage,
            _equippedGunDefinition.BulletPenetration
        );
    }
    private void OnDrawGizmosSelected()
    {
        if (!_t) return;
        Vector3 sphereCenter = _t.position + Vector3.up * _groundCheckOffset + (_groundCheckRadius * Vector3.up);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sphereCenter, _groundCheckRadius);
    }
}