using Unity.Netcode;
using UnityEngine;
using QF_Tools.QF_Utilities;
[RequireComponent(typeof(Rigidbody))]
public sealed class NetworkPlayerController : NetworkBehaviour
{
    private enum MoveState { Crouch, Walking, Running }

    [Header("Movement")]
    [SerializeField] private float _moveAcceleration = 30f;
    [SerializeField] private float[] _moveSpeeds = new float[3] { 3f, 5f, 10f };

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _groundCheckRadius = 0.25f;
    [SerializeField] private float _groundCheckOffset = 0.95f;
    [SerializeField] private float _groundSnapOffset = 1f;
    [SerializeField] private float _jumpBufferTime = 0.15f;
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("Camera")]
    [SerializeField] private float _mouseSensitivity = 12f;
    [SerializeField] private Vector2 _camLimits = new(-80f, 80f);
    [SerializeField] private Transform _cameraTarget;
    [SerializeField, Range(0f, 1f)] private float _animationCamShake = 1f;
    [SerializeField] private float _maxLookDeltaPerFrame = 50f;
    [Space]
    [Space]
    [SerializeField] private Transform _aimTarget;

    private InputSystem_Actions _input;

    private Transform _t;
    private Rigidbody _rb;
    private CapsuleCollider _capsule;

    private Gun _currentGun;

    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private bool _jumpHeld;
    private bool _jumpPressedThisFrame;

    private float _localXRot;
    private float _localYRot;
    private float _serverYaw;

    private Camera _cam;
    private Transform _camT;

    private Vector2 _serverMoveInput;
    private MoveState _serverMoveState;

    private bool _serverJumpHeld;
    private bool _serverJumpPressedThisFrame;
    private float _serverJumpBufferCounter;
    private bool _serverIsGrounded;
    private bool _serverWasGrounded;
    private bool _serverJumpConsumedSinceGrounded;

    private bool _networkInitialized;
    private bool _localOwnerInitialized;

    private readonly Collider[] _groundHits = new Collider[8];

    private void Awake()
    {
        _t = transform;
        _rb = GetComponent<Rigidbody>();
        if (!gameObject.TryGetComponentInChildren(out _capsule)) Debug.LogWarning($"[NetworkPlayerController] No capsule found on or in children of {name}.");
    }
    public override void OnNetworkSpawn()
    {
        InitializeNetworkState();
    }
    public override void OnGainedOwnership()
    {
        InitializeNetworkState();
    }
    public override void OnNetworkDespawn()
    {
        CleanupLocalOwnerState();
        _networkInitialized = false;
    }
    public override void OnLostOwnership()
    {
        CleanupLocalOwnerState();
    }
    private void InitializeNetworkState()
    {
        if (!IsSpawned) return;
        if (!_networkInitialized) _networkInitialized = true;
        if (IsOwner && !_localOwnerInitialized)
        {
            _localOwnerInitialized = true;
            _input ??= new();
            _input.Enable();
            ToggleMouse();
            SetupLocalCamera();
            Gun gun = GameManager.Instance._guns[0];
            _currentGun = Instantiate(gun, gun.HipPosition, Quaternion.identity, _camT);
            _aimTarget.SetParent(_camT);
        }
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
                break;
            case CursorLockMode.Locked:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
        }
    }
    private void Update()
    {
        if (!IsSpawned || !IsOwner || !_localOwnerInitialized) return;

        _moveInput = _input.Player.Move.ReadValue<Vector2>();
        _lookInput = _input.Player.Look.ReadValue<Vector2>();
        _jumpHeld = _input.Player.Jump.IsPressed();
        _jumpPressedThisFrame = _input.Player.Jump.WasPressedThisFrame();
        MoveState moveState = _input.Player.Crouch.IsPressed() ? MoveState.Crouch : _input.Player.Sprint.IsPressed() ? MoveState.Running : MoveState.Walking;
        
        UpdateCamera();
        if (_currentGun)
        {
            _currentGun.RunUpdate(_input.UI.RightClick.IsPressed(), Time.deltaTime, _aimTarget.position);
            if (_input.Player.Attack.IsPressed()) _currentGun.Shoot();
        }

        SubmitInputToServer(moveState);
    }
    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer) return;
        UpdateGrounding();
        HandleJump();
        _t.rotation = Quaternion.Euler(0f, _serverYaw, 0f); // FixedUpdateServerRotation
        FixedUpdateMovement();
    }
    private void LateUpdate()
    {
        if (!IsSpawned || !IsOwner || !_localOwnerInitialized) return;
        LateUpdateCamera();
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
        _camT.SetPositionAndRotation(_cameraTarget.position, Quaternion.Euler(_localXRot, _localYRot, 0f));
        Debug.Log(
            $"[NetworkPlayerController] Local owner init on {name}. " +
            $"Camera={_cam.name}, CameraTarget={_cameraTarget.name}, " +
            $"IsOwner={IsOwner}, IsServer={IsServer}, OwnerClientId={OwnerClientId}"
        );
    }
    private void UpdateCamera()
    {
        Vector2 input = _mouseSensitivity * Time.deltaTime * _lookInput;
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
    private void UpdateGrounding()
    {
        _serverWasGrounded = _serverIsGrounded;
        _serverIsGrounded = CheckGround(out _);
        if (_serverIsGrounded && !_serverWasGrounded) _serverJumpConsumedSinceGrounded = false;
    }
    private bool CheckGround(out Vector3 groundPoint)
    {
        groundPoint = _t.position;
        if (_capsule == null) return false;
        Bounds b = _capsule.bounds;
        Vector3 sphereCenter = new(
            b.center.x,
            b.min.y - _groundCheckOffset,
            b.center.z
        );
        int hitCount = Physics.OverlapSphereNonAlloc(
            sphereCenter,
            _groundCheckRadius,
            _groundHits,
            _groundMask,
            QueryTriggerInteraction.Ignore
        );
        if (hitCount <= 0) return false;
        float bestDistance = float.MaxValue;
        bool foundGround = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _groundHits[i];
            if (col == null) continue;
            if (col == _capsule) continue;
            if (col.transform.IsChildOf(_t)) continue;
            if (col.attachedRigidbody == _rb) continue;
            Vector3 closest = col.ClosestPoint(sphereCenter);
            float dist = Vector3.Distance(sphereCenter, closest);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                groundPoint = closest;
                foundGround = true;
            }
        }
        return foundGround;
    }
    private void HandleJump()
    {
        bool wantsJump = _serverJumpPressedThisFrame || (_serverJumpHeld && _serverIsGrounded && !_serverJumpConsumedSinceGrounded);
        if (wantsJump) _serverJumpBufferCounter = _jumpBufferTime;
        else if (_serverJumpBufferCounter > 0f) _serverJumpBufferCounter -= Time.fixedDeltaTime;

        _serverJumpPressedThisFrame = false;
        if (_serverJumpBufferCounter <= 0f || !_serverIsGrounded || _serverJumpConsumedSinceGrounded) return;
        if (!CheckGround(out Vector3 groundPoint)) return;
        SnapToGround(groundPoint);

        Vector3 vel = _rb.linearVelocity;
        vel.y = 0f;
        _rb.linearVelocity = vel;
        float jumpVelocity = Mathf.Sqrt(-2f * Physics.gravity.y * Mathf.Max(0.01f, _jumpHeight));
        _rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);

        _serverJumpBufferCounter = 0f;
        _serverJumpConsumedSinceGrounded = true;
        _serverIsGrounded = false;
    }
    private void SnapToGround(Vector3 groundPoint)
    {
        if (_capsule == null) return;
        float currentBottomY = _capsule.bounds.min.y;
        float targetBottomY = groundPoint.y + _groundSnapOffset;
        float deltaY = targetBottomY - currentBottomY;
        Vector3 pos = _rb.position;
        pos.y += deltaY;
        _rb.position = pos;
    }
    private void SubmitInputToServer(MoveState moveState)
    {
        Vector2 clampedMove = Vector2.ClampMagnitude(_moveInput, 1f);
        float lookX = Mathf.Clamp(_lookInput.x, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        float lookY = Mathf.Clamp(_lookInput.y, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        Vector2 clampedLook = new(lookX, lookY);
        if (IsServer)
        {
            ApplyInputAuthoritative(clampedMove, clampedLook, (int)moveState, _localYRot, _jumpHeld, _jumpPressedThisFrame);
            return;
        }
        SubmitInputServerRpc(clampedMove, clampedLook, (int)moveState, _localYRot, _jumpHeld, _jumpPressedThisFrame);
    }

    private void ApplyInputAuthoritative(Vector2 moveInput, Vector2 lookInput, int moveStateIndex, float yaw, bool jumpHeld, bool jumpPressedThisFrame)
    {
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        lookInput.x = Mathf.Clamp(lookInput.x, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        lookInput.y = Mathf.Clamp(lookInput.y, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        if (moveStateIndex < 0 || moveStateIndex >= _moveSpeeds.Length) moveStateIndex = (int)MoveState.Walking;

        _serverMoveInput = moveInput;
        _serverMoveState = (MoveState)moveStateIndex;
        _serverYaw = yaw;
        _serverJumpHeld = jumpHeld;
        if (jumpPressedThisFrame) _serverJumpPressedThisFrame = true;
    }
    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 moveInput, Vector2 lookInput, int moveStateIndex, float yaw, bool jumpHeld, bool jumpPressedThisFrame)
    {
        ApplyInputAuthoritative(moveInput, lookInput, moveStateIndex, yaw, jumpHeld, jumpPressedThisFrame);
    }
    private void OnDrawGizmosSelected()
    {
        CapsuleCollider capsule = GetComponentInChildren<CapsuleCollider>();
        if (capsule == null) return;

        Bounds b = capsule.bounds;

        Vector3 sphereCenter = new(
            b.center.x,
            b.min.y - _groundCheckOffset,
            b.center.z
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sphereCenter, _groundCheckRadius);
    }
}