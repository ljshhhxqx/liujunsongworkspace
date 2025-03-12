using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "FrameConfig", menuName = "ScriptableObjects/FrameConfig")]
    public class FrameConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<FrameConfigData> frameConfigDatas = new List<FrameConfigData>();
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            frameConfigDatas.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var frameConfigData = new FrameConfigData();
                frameConfigData.quality = (QualityType) Enum.Parse(typeof(QualityType), data[0]);
                frameConfigData.iconAddress = data[1];
                frameConfigDatas.Add(frameConfigData);
            }
        }
    }

    [Serializable]
    public struct FrameConfigData
    {
        public QualityType quality;
        public string iconAddress;
    }
}