using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "ConstantBuffConfig", menuName = "ScriptableObjects/ConstantBuffConfig")]
    public class ConstantBuffConfig : ConfigBase
    {
        [SerializeField]
        private List<BuffData> buffs = new List<BuffData>();
        private readonly Dictionary<PropertyTypeEnum, List<RandomBuffData>> _randomCollectBuffs = new Dictionary<PropertyTypeEnum, List<RandomBuffData>>();
        
        public BuffData GetBuff(BuffExtraData extraData)
        {
            return GetBuffData(extraData.buffId);
        }

        public BuffData GetBuffData(int buffId)
        {
            return buffs.Find(buff => buff.buffId == buffId);
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            buffs.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var buff = new BuffData
                {
                    buffId = int.Parse(data[0]),
                    propertyType = Enum.Parse<PropertyTypeEnum>(data[1]),
                    duration = float.Parse(data[2])
                };
                var json = JsonUtility.FromJson<List<BuffIncreaseData>>(data[3]);
                buff.increaseDataList = json;
                buffs.Add(buff);
            }
        }
    }
}