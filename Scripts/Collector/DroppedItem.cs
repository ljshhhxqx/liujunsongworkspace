using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using Mirror;

namespace HotUpdate.Scripts.Collector
{
    public class DroppedItem : NetworkBehaviour
    {
        public DroppedItemSceneData droppedItemSceneData;
    }

    [Serializable]
    public struct DroppedItemSceneData
    {
        public int itemId;
        public int configId;
        public QualityType qualityType;
    }
}