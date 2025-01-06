using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "ConstantBuffConfig", menuName = "ScriptableObjects/ConstantBuffConfig")]
    public class ConstantBuffConfig : ConfigBase
    {
        [ReadOnly]
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
                var buff = new BuffData();
                buff.buffId = int.Parse(data[0]);
                buff.propertyType = Enum.Parse<PropertyTypeEnum>(data[1]);
                buff.duration = float.Parse(data[2]);
                var json = JsonConvert.DeserializeObject<List<BuffIncreaseData>>(data[3]);
                buff.increaseDataList = json;
                buffs.Add(buff);
            }
        }
    }
}