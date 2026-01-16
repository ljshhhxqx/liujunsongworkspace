using System.Linq;
using AOTScripts.Data;
using AOTScripts.Data.State;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.State
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

        public static bool TryUnequipped(ref PlayerEquipmentState equipmentState, int itemId, EquipmentPart equipmentPartType, out EquipmentData equipData)
        {
            equipData = null;
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var equipmentData = equipmentState.EquipmentDatas[i];
                if (equipmentData.ItemId == itemId && equipmentData.EquipmentPartType == equipmentPartType)
                {
                    equipmentState.EquipmentDatas.RemoveAt(i);
                    equipData = equipmentData;
                    return true;
                }
            }
            return false;
        }

        public static bool TryAddEquipmentData<T>(ref PlayerEquipmentState equipmentState, int itemId, int equipConfigId,
            EquipmentPart equipmentPartType, T conditionChecker) where T : IConditionChecker
        {
            if (equipmentState.EquipmentDatas.Count > 0 && equipmentState.EquipmentDatas.Any(x => x.ItemId == itemId))
            {
                Debug.LogError($"EquipmentId {itemId} already exists in EquipmentDatas");
                return false;
            }
            var buffer = NetworkCommandExtensions.SerializeBattleChecker(conditionChecker).buffer;
            var equipmentData = new EquipmentData();
            equipmentData.ItemId = itemId;
            equipmentData.EquipConfigId = equipConfigId;
            equipmentData.EquipmentPartType = equipmentPartType;
            equipmentData.EquipmentPassiveEffectData = new MemoryList<AttributeIncreaseData>();
            equipmentData.EquipmentConstantPropertyData = new MemoryList<AttributeIncreaseData>();
            equipmentData.TargetIds = new MemoryList<int>();
            equipmentData.SkillId = 0;
            equipmentData.IsSkillLoad = false;
            equipmentData.ConditionChecker = conditionChecker;
            equipmentData.ConditionCheckerBytes = buffer;
            equipmentData.TriggerType = conditionChecker.GetConditionCheckerHeader().TriggerType;
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
                    var mainData = new MemoryList<AttributeIncreaseData>();
                    var equipmentPassiveEffectData = new MemoryList<AttributeIncreaseData>();
                    mainData.AddRange(mainIncreaseData);
                    equipmentPassiveEffectData.AddRange(passiveIncreaseData);
                    equipmentData.EquipmentDatas[i].ItemId = itemId;
                    equipmentData.EquipmentDatas[i].EquipConfigId = equipConfigId;
                    equipmentData.EquipmentDatas[i].EquipmentPartType = equipmentPartType;
                    equipmentData.EquipmentDatas[i].EquipmentPassiveEffectData = mainData;
                    equipmentData.EquipmentDatas[i].EquipmentConstantPropertyData = equipmentPassiveEffectData;
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
                if (equipment.ConditionChecker == null || !equipment.ConditionChecker.GetCooldownHeader().IsCooldown())
                {
                    continue;
                }
                var cooldownHeader = equipment.ConditionChecker.GetCooldownHeader();
                var newHeader = cooldownHeader.Update(deltaTime);
                equipment.ConditionChecker.SetCooldownHeader(newHeader);
                equipmentState.EquipmentDatas[i] = equipment;
            }
        }

        public static bool CheckConditions<T>(ref PlayerEquipmentState equipmentState, T checkerParameter, out IConditionChecker conditionChecker) where T : IConditionCheckerParameters
        {
            conditionChecker = null;
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var checker = equipmentState.EquipmentDatas[i].ConditionChecker;
                var checkerParameterHeader = checker.GetConditionCheckerHeader();
                var conditionConfigData = checkerParameter.GetCommonParameters();
                if (checkerParameterHeader.TriggerType == conditionConfigData.TriggerType)
                {
                    var checkOver = checker.Check(ref checker, checkerParameter);
                    conditionChecker = checker;
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
        public byte[] ConditionCheckerBytes;
        [MemoryPackOrder(7)]
        public int SkillId;
        [MemoryPackOrder(8)]
        public bool IsSkillLoad;
        [MemoryPackOrder(9)]
        public TriggerType TriggerType;
        [MemoryPackIgnore]
        public IConditionChecker ConditionChecker;
        
       
    }
}