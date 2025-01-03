using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "GameData", menuName = "ScriptableObjects/GameData")]
    public class GameDataConfig : ConfigBase
    {
        [SerializeField] private GameConfigData gameConfigData;
        public GameConfigData GameConfigData => gameConfigData;
        protected override void ReadFromExcel(string filePath)
        {
        }

        protected override void ReadFromCsv(string filePath)
        {
        }
    }

    [Serializable]
    public struct GameConfigData
    {
        [FormerlySerializedAs("GroundSceneLayer")] public int groundSceneLayer;
        [FormerlySerializedAs("SyncTime")] public float syncTime;
        [FormerlySerializedAs("SafetyMargin")] public float safetyMargin;
        [FormerlySerializedAs("FixedSpacing")] public float fixedSpacing;
        [FormerlySerializedAs("WarmupTime")] public float warmupTime;
        [FormerlySerializedAs("DevelopKey")] public string developKey;
        [FormerlySerializedAs("DevelopKeyValue")] public string developKeyValue;
        [FormerlySerializedAs("StairSceneLayer")] public int stairSceneLayer; 
        [FormerlySerializedAs("SafePosition")] public Vector3 safePosition;
        [FormerlySerializedAs("SafeHorizontalOffsetY")] public float safeHorizontalOffsetY;
    }
}