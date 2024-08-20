using System;
using UnityEngine;

namespace Config
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
    public class ChestDataConfig : ConfigBase
    { 
        [SerializeField]
        private ChestConfigData chestConfigData;
        public ChestConfigData ChestConfigData => chestConfigData;
    }

    [Serializable]
    public class ChestConfigData : CollectObjectData
    {
        public float OpenSpeed;
        public Vector3 InitEulerAngles;
    }
}