using UnityEngine;
using QF_Tools.QF_Utilities;
public class Map : QF_Singleton<Map>
{
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private Transform _tower;
    public Transform Tower => _tower;
    public Transform[] SpawnPoints => _spawnPoints;
}