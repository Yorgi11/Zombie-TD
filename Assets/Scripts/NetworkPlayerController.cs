using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class NetworkPlayerController : NetworkBehaviour
{
    private enum MoveState { Crouch, Walking, Running }

    [Header("Movement")]
    [SerializeField] private float _moveAcceleration = 30f;
    [SerializeField] private float[] _moveSpeeds = new float[3] { 3f, 5f, 10f };

    [Header("Camera")]
    [SerializeField] private float _mouseSensitivity = 12f;
    [SerializeField] private Vector2 _camLimits = new(-80f, 80f);
    [SerializeField] private Transform _cameraTarget;
    [SerializeField, Range(0f, 1f)] private float _animationCamShake = 1f;

    [Header("Server Validation")]
    [SerializeField] private float _maxLookDeltaPerFrame = 50f;

    private InputSystem_Actions _input;

    private Transform _t;
    private Rigidbody _rb;

    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private float _localXRot;
    private float _localYRot;
    private float _serverYaw;

    private Camera _cam;
    private Transform _camT;

    // Latest input accepted by the server
    private Vector2 _serverMoveInput;
    private Vector2 _serverLookInput;
    private MoveState _serverMoveState;

    private void Awake()
    {
        _t = transform;
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            _input ??= new();
            _input.Enable();
            SetupLocalCamera();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            _input?.Disable();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        _moveInput = _input.Player.Move.ReadValue<Vector2>();
        _lookInput = _input.Player.Look.ReadValue<Vector2>();

        MoveState moveState =
            _input.Player.Crouch.IsPressed() ? MoveState.Crouch :
            _input.Player.Sprint.IsPressed() ? MoveState.Running :
            MoveState.Walking;

        UpdateCamera();
        SubmitInputToServer(moveState);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        FixedUpdateServerRotation();
        FixedUpdateMovement();
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;
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

        _camT = _cam.transform;
        _localYRot = _t.eulerAngles.y;
        _serverYaw = _t.eulerAngles.y;

        Debug.Log(
            $"[NetworkPlayerController] Camera setup on {name}. " +
            $"Camera={_cam.name}, CameraTarget={(_cameraTarget != null ? _cameraTarget.name : "NULL")}, " +
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
        if (_camT == null)
        {
            Debug.LogWarning($"[NetworkPlayerController] _camT is null on {name}");
            return;
        }

        if (_cameraTarget == null)
        {
            Debug.LogWarning($"[NetworkPlayerController] _cameraTarget is null on {name}");
            return;
        }

        Quaternion targetRot = Quaternion.Euler(_localXRot, _localYRot, 0f);

        _camT.SetPositionAndRotation(
            _cameraTarget.position,
            Quaternion.Slerp(_camT.rotation, targetRot, _animationCamShake)
        );
    }

    private void FixedUpdateServerRotation()
    {
        _t.rotation = Quaternion.Euler(0f, _serverYaw, 0f);
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

    private void SubmitInputToServer(MoveState moveState)
    {
        Vector2 clampedMove = Vector2.ClampMagnitude(_moveInput, 1f);

        float lookX = Mathf.Clamp(_lookInput.x, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        float lookY = Mathf.Clamp(_lookInput.y, -_maxLookDeltaPerFrame, _maxLookDeltaPerFrame);
        Vector2 clampedLook = new(lookX, lookY);

        if (IsServer)
        {
            ApplyInputAuthoritative(clampedMove, clampedLook, (int)moveState, _localYRot);
            return;
        }

        SubmitInputServerRpc(clampedMove, clampedLook, (int)moveState, _localYRot);
    }
    private void ApplyInputAuthoritative(Vector2 moveInput, Vector2 lookInput, int moveStateIndex, float yaw)
    {
        if (moveStateIndex < 0 || moveStateIndex >= _moveSpeeds.Length) moveStateIndex = (int)MoveState.Walking;
        _serverMoveInput = moveInput;
        _serverLookInput = lookInput;
        _serverMoveState = (MoveState)moveStateIndex;
        _serverYaw = yaw;
    }

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 moveInput, Vector2 lookInput, int moveStateIndex, float yaw)
    {
        ApplyInputAuthoritative(moveInput, lookInput, moveStateIndex, yaw);
    }
}