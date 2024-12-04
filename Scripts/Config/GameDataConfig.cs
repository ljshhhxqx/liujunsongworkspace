using System;
using Config;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GameData", menuName = "ScriptableObjects/GameData")]
public class GameDataConfig : ConfigBase
{
    [SerializeField] private GameConfigData gameConfigData;
    public GameConfigData GameConfigData => gameConfigData;
}

[Serializable]
public struct GameConfigData
{
    public LayerMask GroundSceneLayer;
    public float SyncTime;
    public float SafetyMargin;
    public float FixedSpacing;
    public float WarmupTime;
    public string DevelopKey;
    public string DevelopKeyValue;
    public LayerMask StairSceneLayer; 
    public Vector3 SafePosition;
    public float SafeHorizontalOffsetY;
}