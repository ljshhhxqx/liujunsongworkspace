using System;
using System.Collections.Immutable;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.Item;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    /// <summary>
    /// 玩家装备状态
    /// </summary>
    [MemoryPackable]
    public partial struct PlayerEquipmentState : ISyncPropertyState
    {
        [MemoryPackOrder(0)]
        public ImmutableList<EquipmentData> EquipmentDatas;
        [MemoryPackIgnore]
        public ImmutableList<IConditionChecker> ConditionCheckers;

        public static bool TryUnequipped(ref PlayerEquipmentState equipmentState, int itemId, EquipmentPart equipmentPartType)
        {
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var equipmentData = equipmentState.EquipmentDatas[i];
                if (equipmentData.ItemId == itemId || equipmentData.EquipmentPartType == equipmentPartType)
                {
                    equipmentState.EquipmentDatas = equipmentState.EquipmentDatas.RemoveAt(i);
                    equipmentState.ConditionCheckers = equipmentState.ConditionCheckers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public static bool TryAddEquipmentData(ref PlayerEquipmentState equipmentState, int itemId,
            EquipmentPart equipmentPartType, IConditionChecker conditionChecker)
        {
            if (equipmentState.EquipmentDatas.Any(x => x.ItemId == itemId))
            {
                Debug.LogError($"EquipmentId {itemId} already exists in EquipmentDatas");
                return false;
            }
            var equipmentData = new EquipmentData(itemId, equipmentPartType);
            //该部位有装备，则卸下原装备
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var oldEquip = equipmentState.EquipmentDatas[i];
                if (oldEquip.EquipmentPartType == equipmentPartType)
                {
                    equipmentState.EquipmentDatas = equipmentState.EquipmentDatas.RemoveAt(i);
                    equipmentState.ConditionCheckers = equipmentState.ConditionCheckers.RemoveAt(i);
                    break;
                }
            }
            equipmentState.EquipmentDatas = equipmentState.EquipmentDatas.Add(equipmentData);
            equipmentState.ConditionCheckers = equipmentState.ConditionCheckers.Add(conditionChecker);
            return true;
        }

        public static bool TryAddEquipmentPassiveEffectData(ref PlayerEquipmentState equipmentData, int itemId,
            EquipmentPart equipmentPartType, AttributeIncreaseData[] mainIncreaseData, AttributeIncreaseData[] passiveIncreaseData)
        {
            for (int i = 0; i < equipmentData.EquipmentDatas.Count; i++)
            {
                var equipment = equipmentData.EquipmentDatas[i];
                if (equipment.ItemId == itemId && equipment.EquipmentPartType == equipmentPartType)
                {
                    equipmentData.EquipmentDatas = equipmentData.EquipmentDatas.SetItem(i,
                        new EquipmentData(itemId, equipmentPartType,
                            mainIncreaseData, passiveIncreaseData));
                    return true;
                }
            }
            return false;   
        }

        public static void UpdateCheckerCd(ref PlayerEquipmentState equipmentState, float deltaTime)
        {
            var checkers = equipmentState.ConditionCheckers;
            for (int i = 0; i < checkers.Count; i++)
            {
                var checker = checkers[i];
                IConditionChecker.UpdateCd(ref checker, deltaTime);
                checkers = checkers.SetItem(i, checker);
            }
            equipmentState.ConditionCheckers = checkers;
        }

        public static bool CheckConditions<T>(ref PlayerEquipmentState equipmentState, T checkerParameter) where T : IConditionCheckerParameters
        {
            var checkers = equipmentState.ConditionCheckers;
            for (int i = 0; i < checkers.Count; i++)
            {
                var checker = checkers[i];
                var checkerParameterHeader = checker.GetConditionCheckerHeader();
                var conditionConfigData = checkerParameter.GetCommonParameters();
                if (checkerParameterHeader.TriggerType == conditionConfigData.TriggerType)
                {
                    var checkOver = checkers[i].Check(ref checker, checkerParameter);
                    return checkOver;
                }
            }
            return false;
        }
    }
     
    [MemoryPackable]
    public partial struct EquipmentData
    {
        [MemoryPackOrder(0)]
        public int ItemId;
        [MemoryPackOrder(1)]
        public EquipmentPart EquipmentPartType;
        [MemoryPackOrder(2)]
        public AttributeIncreaseData[] EquipmentPassiveEffectData;
        [MemoryPackOrder(2)]
        public AttributeIncreaseData[] EquipmentConstantPropertyData;
        
        [MemoryPackConstructor]
        public EquipmentData(int itemId, EquipmentPart equipmentPartType, AttributeIncreaseData[] equipmentPassiveEffectData = null, AttributeIncreaseData[] equipmentConstantPropertyData = null)
        {
            ItemId = itemId;
            EquipmentPartType = equipmentPartType;
            EquipmentPassiveEffectData = equipmentPassiveEffectData ?? Array.Empty<AttributeIncreaseData>();
            EquipmentConstantPropertyData = equipmentConstantPropertyData ?? Array.Empty<AttributeIncreaseData>();
        }
    }
}