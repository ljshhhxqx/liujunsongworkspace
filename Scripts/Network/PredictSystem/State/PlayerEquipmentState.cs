using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    /// <summary>
    /// 玩家装备状态
    /// </summary>
    [MemoryPackable]
    public partial class PlayerEquipmentState : ISyncPropertyState
    {
        [MemoryPackOrder(0)]
        public MemoryList<EquipmentData> EquipmentDatas;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerEquipment;

        public static bool TryUnequipped(ref PlayerEquipmentState equipmentState, int itemId, EquipmentPart equipmentPartType)
        {
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var equipmentData = equipmentState.EquipmentDatas[i];
                if (equipmentData.ItemId == itemId || equipmentData.EquipmentPartType == equipmentPartType)
                {
                    equipmentState.EquipmentDatas.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public static bool TryAddEquipmentData<T>(ref PlayerEquipmentState equipmentState, int itemId, int equipConfigId,
            EquipmentPart equipmentPartType, T conditionChecker) where T : IConditionChecker
        {
            if (equipmentState.EquipmentDatas.Count >0 && equipmentState.EquipmentDatas.Any(x => x.ItemId == itemId))
            {
                Debug.LogError($"EquipmentId {itemId} already exists in EquipmentDatas");
                return false;
            }
            var memories = new MemoryList<byte>();
            var buffer = NetworkCommandExtensions.SerializeBattleChecker(conditionChecker).buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                memories.Add(buffer[i]);
            }
            var equipmentData = new EquipmentData(itemId, equipConfigId, equipmentPartType);
            equipmentData.ConditionChecker = conditionChecker;
            equipmentData.ConditionCheckerBytes = memories;
            //该部位有装备，则卸下原装备
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var oldEquip = equipmentState.EquipmentDatas[i];
                if (oldEquip.EquipmentPartType == equipmentPartType)
                {
                    equipmentState.EquipmentDatas.RemoveAt(i);
                    break;
                }
            }
            equipmentState.EquipmentDatas.Add(equipmentData);
            return true;
        }

        public static bool TryAddEquipmentPassiveEffectData(ref PlayerEquipmentState equipmentData, int itemId, int equipConfigId,
            EquipmentPart equipmentPartType, AttributeIncreaseData[] mainIncreaseData, AttributeIncreaseData[] passiveIncreaseData)
        {
            for (int i = 0; i < equipmentData.EquipmentDatas.Count; i++)
            {
                var equipment = equipmentData.EquipmentDatas[i];
                if (equipment.ItemId == itemId && equipment.EquipmentPartType == equipmentPartType)
                {
                    equipmentData.EquipmentDatas[i] = new EquipmentData(itemId, equipConfigId, equipmentPartType,
                        new MemoryList<AttributeIncreaseData>(mainIncreaseData), new MemoryList<AttributeIncreaseData>(passiveIncreaseData));
                    return true;
                }
            }
            return false;   
        }

        public static void UpdateCheckerCd(ref PlayerEquipmentState equipmentState, float deltaTime)
        {
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var equipment = equipmentState.EquipmentDatas[i];
                var cooldownHeader = equipment.ConditionChecker.GetCooldownHeader();
                var newHeader = cooldownHeader.Update(deltaTime);
                equipment.ConditionChecker.SetCooldownHeader(newHeader);
                equipmentState.EquipmentDatas[i] = equipment;
            }
        }

        public static bool CheckConditions<T>(ref PlayerEquipmentState equipmentState, T checkerParameter) where T : IConditionCheckerParameters
        {
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var checker = equipmentState.EquipmentDatas[i].ConditionChecker;
                var checkerParameterHeader = checker.GetConditionCheckerHeader();
                var conditionConfigData = checkerParameter.GetCommonParameters();
                if (checkerParameterHeader.TriggerType == conditionConfigData.TriggerType)
                {
                    var checkOver = checker.Check(ref checker, checkerParameter);
                    return checkOver;
                }
            }
            return false;
        }
    }
     
    [MemoryPackable]
    public partial class EquipmentData
    {
        [MemoryPackOrder(0)]
        public int ItemId;
        [MemoryPackOrder(1)]
        public int EquipConfigId;
        [MemoryPackOrder(2)]
        public EquipmentPart EquipmentPartType;
        [MemoryPackOrder(3)]
        public MemoryList<AttributeIncreaseData> EquipmentPassiveEffectData;
        [MemoryPackOrder(4)]
        public MemoryList<AttributeIncreaseData> EquipmentConstantPropertyData;

        [MemoryPackOrder(5)]
        public MemoryList<int> TargetIds;
        [MemoryPackOrder(6)]
        public MemoryList<byte> ConditionCheckerBytes;
        [MemoryPackIgnore]
        public IConditionChecker ConditionChecker;
        
        [MemoryPackConstructor]
        public EquipmentData(int itemId, int equipConfigId, EquipmentPart equipmentPartType, MemoryList<AttributeIncreaseData> equipmentPassiveEffectData = null, 
            MemoryList<AttributeIncreaseData> equipmentConstantPropertyData = null)
        {
            ItemId = itemId;
            EquipConfigId = equipConfigId;
            EquipmentPartType = equipmentPartType;
            EquipmentPassiveEffectData = equipmentPassiveEffectData;
            EquipmentConstantPropertyData = equipmentConstantPropertyData;
            ConditionChecker = null;
            TargetIds = new MemoryList<int>();
            ConditionCheckerBytes = new MemoryList<byte>();
        }
    }
}