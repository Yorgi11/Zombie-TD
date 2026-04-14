using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class NetworkPlayerController : NetworkBehaviour
{
    private enum MoveState { Crouch, Walking, Running }
    private MoveState _moveState;

    [Header("Movement")]
    [SerializeField] private float _moveAcceleration = 30f;
    [SerializeField] private float[] _moveSpeeds = new float[3] { 3f, 5f, 10f };

    [Header("Camera")]
    [SerializeField] private float _mouseSensitivity = 12f;
    [SerializeField] private Vector2 _camLimits = new(-80f, 80f);
    [SerializeField] private Transform _cameraTarget;

    private InputSystem_Actions _input;

    private Transform _t;
    private Rigidbody _rb;

    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private float _xRot;
    private float _yRot;

    private Camera _cam;
    private Transform _camT;

    private void Awake()
    {
        _t = transform;
        _rb = GetComponent<Rigidbody>();
    }
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        _input ??= new();
        _input.Enable();
        SetupLocalCamera();
    }
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        _input?.Disable();
    }
    private void Update()
    {
        if (!IsOwner) return;

        _moveInput = _input.Player.Move.ReadValue<Vector2>();
        _lookInput = _input.Player.Look.ReadValue<Vector2>();
        _moveState = _input.Player.Crouch.IsPressed() ? MoveState.Crouch :
                     _input.Player.Sprint.IsPressed() ? MoveState.Running :
                     MoveState.Walking;

        UpdateCamera();
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
            Debug.LogWarning("[NetworkPlayerController] No main camera found.");
            return;
        }

        _camT = _cam.transform;
        _yRot = _t.eulerAngles.y;
    }

    private void UpdateCamera()
    {
        Vector2 input = _mouseSensitivity * Time.deltaTime * _lookInput;
        _xRot = Mathf.Clamp(_xRot - input.y, _camLimits.x, _camLimits.y);
        _yRot += input.x;
    }

    private void LateUpdateCamera()
    {
        if (_camT == null || _cameraTarget == null) return;

        Quaternion targetRot = Quaternion.Euler(_xRot, _yRot, 0f);
        _camT.SetPositionAndRotation(_cameraTarget.position, targetRot);
    }
}