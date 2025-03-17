using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ConstantBuffConfig", menuName = "ScriptableObjects/ConstantBuffConfig")]
    public class ConstantBuffConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<BuffData> buffs = new List<BuffData>();
        
        public BuffData GetBuff(BuffExtraData extraData)
        {
            return GetBuffData(extraData.buffId);
        }

        public BuffData GetBuffData(int buffId, CollectObjectBuffSize collectObjectBuffSize = CollectObjectBuffSize.Small)
        {
            var buff = buffs.Find(b => b.buffId == buffId);
            for (var i = 0; i < buff.increaseDataList.Count; i++)
            {
                var buffData = buff.increaseDataList[i];
                buffData.increaseValue *= BuffDataReaderWriter.GetBuffRatioBySize(collectObjectBuffSize);
                buff.increaseDataList[i] = buffData;
            }

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