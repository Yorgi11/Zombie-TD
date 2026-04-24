using UnityEngine;
public class Turret : MonoBehaviour
{
    [SerializeField] private Transform _rotor;  // Y rotation / yaw
    [SerializeField] private Transform _head;   // X rotation / pitch
    [SerializeField] private Transform _barrel;
    [SerializeField] private float _turnSpeed = 180f;
    [SerializeField] private float _pitchSpeed = 180f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;
    [SerializeField] private float _minFireAngle = 3f;
    [Space]
    [SerializeField] private Gun _attachedGun;

    private Transform _target;
    public void SetTarget(Transform target) => _target = target;
    public void PointAtTarget()
    {
        if (_target == null || _rotor == null || _head == null || _barrel == null) return;
        Vector3 targetPosition = _target.position;
        Vector3 toTarget = targetPosition - _barrel.position;
        if (toTarget.sqrMagnitude <= 0.0001f) return;
        RotateRotor(toTarget);
        RotateHead(targetPosition);
        if (Vector3.Angle(_barrel.forward, toTarget) <= _minFireAngle) _attachedGun.TryShoot();
    }
    private void RotateRotor(Vector3 worldDirectionToTarget)
    {
        Vector3 flatDirection = Vector3.ProjectOnPlane(worldDirectionToTarget, _rotor.up);
        if (flatDirection.sqrMagnitude <= 0.0001f) return;
        _rotor.rotation = Quaternion.RotateTowards(
            _rotor.rotation,
            Quaternion.LookRotation(flatDirection, _rotor.up),
            _turnSpeed * Time.deltaTime
        );
    }
    private void RotateHead(Vector3 targetPosition)
    {
        Vector3 localDirection =
            _head.parent.InverseTransformPoint(targetPosition) -
            _head.parent.InverseTransformPoint(_head.position);
        float pitch = -Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
        _head.localRotation = Quaternion.RotateTowards(
            _head.localRotation,
            Quaternion.Euler(pitch, 0f, 0f),
            _pitchSpeed * Time.deltaTime
        );
    }
}