using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using QF_Tools.QF_Utilities;

public class TurretManager : QF_Singleton<TurretManager>
{
    [SerializeField] private float _placeDistance = 3f;
    [SerializeField] private Turret _turretPrefab;
    [SerializeField] private Transform[] _turretPlacementPoints;

    private readonly List<Turret> _turrets = new();
    private bool[] _occupied;
    private Turret[] _placedTurrets;

    public float PlaceDistance => _placeDistance;
    public IReadOnlyList<Turret> Turrets => _turrets;

    protected override void Awake()
    {
        base.Awake();

        int count = _turretPlacementPoints != null ? _turretPlacementPoints.Length : 0;
        _occupied = new bool[count];
        _placedTurrets = new Turret[count];
    }

    public bool TryGetNearestPlacementInfo(Vector3 playerPosition, out int placementIndex, out bool occupied, out Transform placementPoint)
    {
        placementIndex = -1;
        occupied = false;
        placementPoint = null;

        if (_turretPlacementPoints == null || _turretPlacementPoints.Length == 0)
            return false;

        float maxDistSqr = _placeDistance * _placeDistance;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < _turretPlacementPoints.Length; i++)
        {
            Transform point = _turretPlacementPoints[i];
            if (point == null) continue;

            float distSqr = (point.position - playerPosition).sqrMagnitude;
            if (distSqr > maxDistSqr) continue;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                placementIndex = i;
                placementPoint = point;
            }
        }

        if (placementIndex < 0)
            return false;

        occupied = _occupied != null && placementIndex < _occupied.Length && _occupied[placementIndex];
        return true;
    }

    public int GetNearestPlacementIndex(Vector3 playerPosition)
    {
        if (!TryGetNearestPlacementInfo(playerPosition, out int placementIndex, out _, out _))
            return -1;

        return placementIndex;
    }

    public bool IsOccupied(int placementIndex)
    {
        return _occupied != null &&
               placementIndex >= 0 &&
               placementIndex < _occupied.Length &&
               _occupied[placementIndex];
    }

    public Transform GetPlacementPoint(int placementIndex)
    {
        if (_turretPlacementPoints == null) return null;
        if (placementIndex < 0 || placementIndex >= _turretPlacementPoints.Length) return null;
        return _turretPlacementPoints[placementIndex];
    }

    public Turret GetPlacedTurret(int placementIndex)
    {
        if (_placedTurrets == null) return null;
        if (placementIndex < 0 || placementIndex >= _placedTurrets.Length) return null;
        return _placedTurrets[placementIndex];
    }

    public bool CanPlaceTurretAt(int placementIndex, Vector3 playerPosition, int playerPoints)
    {
        if (!IsServer()) return false;
        if (_turretPrefab == null) return false;
        if (_turretPlacementPoints == null) return false;
        if (placementIndex < 0 || placementIndex >= _turretPlacementPoints.Length) return false;
        if (_occupied == null || placementIndex >= _occupied.Length) return false;
        if (_occupied[placementIndex]) return false;

        Transform point = _turretPlacementPoints[placementIndex];
        if (point == null) return false;

        float distSqr = (point.position - playerPosition).sqrMagnitude;
        if (distSqr > _placeDistance * _placeDistance) return false;

        if (playerPoints < _turretPrefab.PlacementCost) return false;

        return true;
    }

    public bool TryPlaceTurretServer(NetworkPlayerController player, int placementIndex)
    {
        if (!IsServer()) return false;
        if (player == null || !player.IsSpawned) return false;
        if (_turretPrefab == null) return false;

        if (!CanPlaceTurretAt(placementIndex, player.transform.position, player.Points))
            return false;

        if (!player.TrySpendPoints(_turretPrefab.PlacementCost))
            return false;

        Transform point = _turretPlacementPoints[placementIndex];
        Turret turretInstance = Instantiate(_turretPrefab, point.position, point.rotation);

        if (!turretInstance.TryGetComponent<NetworkObject>(out var no))
        {
            Debug.LogError("[TurretManager] Turret prefab is missing NetworkObject.");
            Destroy(turretInstance.gameObject);
            player.AddPoints(_turretPrefab.PlacementCost);
            return false;
        }

        no.Spawn(true);

        _occupied[placementIndex] = true;
        _placedTurrets[placementIndex] = turretInstance;
        _turrets.Add(turretInstance);

        return true;
    }

    private bool IsServer()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }
}