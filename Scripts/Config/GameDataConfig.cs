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
public class GameConfigData
{
    public float MapWidth;
    public float MapDepth;
    public LayerMask GroundSceneLayer;
    public float SyncTime = 0.016f;
    public float SafetyMargin = 5.0f;
    public float FixedSpacing = 1.0f;
    public readonly string DevelopKey = "DevelopKey";
    public readonly string DevelopKeyValue = "MultiplayerDemo";
    public LayerMask StairSceneLayer; 
}