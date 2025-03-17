using System.Collections.Generic;
using System.Collections.Immutable;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using MemoryPack;

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
        // [MemoryPackOrder(1)] 
        // public ImmutableList<byte[]> PlayerCheckerParameters;

        public static bool EquipPlayerOrNot(ref PlayerPredictablePropertyState playerState, PlayerEquipmentState equipmentState, uint equipmentId, bool equip)
        {
            var equipmentData = equipmentState.EquipmentDatas;
            for (int i = 0; i < equipmentData.Count; i++)
            {
                if (equipmentData[i].EquipmentId == equipmentId)
                {
                    if (equip)
                    {
                        return equipmentData[i].EquipPlayer(ref playerState);
                    }
                    return equipmentData[i].UnEquipPlayer(ref playerState);
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
                if (checkerParameterHeader.ConditionConfigData.triggerType == conditionConfigData.TriggerType)
                {
                    var checkOver = checkers[i].Check(ref checker, checkerParameter);
                    IConditionChecker.TakeEffect(ref checker);
                    equipmentState.ConditionCheckers = checkers.SetItem(i, checker);
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
        public ImmutableList<BuffData> EquipmentBuffData;
        [MemoryPackOrder(1)]
        public ImmutableList<BattleEffectConditionConfigData> BattleEffectConditions;
        [MemoryPackOrder(2)]
        public uint EquipmentId;
        [MemoryPackOrder(3)]
        public bool IsEquipped;
        [MemoryPackOrder(4)]
        public EquipmentPart EquipmentPartType;
        
        [MemoryPackConstructor]
        public EquipmentData(ImmutableList<BuffData> equipmentBuffData, ImmutableList<BattleEffectConditionConfigData> battleEffectConditions, uint equipmentId
            , EquipmentPart equipmentPartType)
        {
            EquipmentBuffData = equipmentBuffData;
            BattleEffectConditions = battleEffectConditions;
            EquipmentId = equipmentId;
            IsEquipped = false;
            EquipmentPartType = equipmentPartType;
        }

        public bool EquipPlayer(ref PlayerPredictablePropertyState playerState)
        {
            if (IsEquipped)
            {
                return false;
            }
            IsEquipped = true;
            foreach (var buffData in EquipmentBuffData)
            {
                if (playerState.Properties.TryGetValue(buffData.propertyType, out var calculator))
                {
                    playerState.Properties[buffData.propertyType] = calculator.UpdateCalculator(buffData.increaseDataList);
                }
            }
            return IsEquipped;
        }
        
        public bool UnEquipPlayer(ref PlayerPredictablePropertyState playerState)
        {
            if (!IsEquipped)
            {
                return false;
            }
            IsEquipped = false;
            foreach (var buffData in EquipmentBuffData)
            {
                if (playerState.Properties.TryGetValue(buffData.propertyType, out var calculator))
                {
                    for (var i = 0; i < buffData.increaseDataList.Count; i++)
                    {
                        var increaseData = buffData.increaseDataList[i];
                        increaseData.operationType = BuffOperationType.Subtract;
                        buffData.increaseDataList[i] = increaseData;
                    }
                    playerState.Properties[buffData.propertyType] = calculator.UpdateCalculator(buffData.increaseDataList);
                }
            }
            return true;
        }
    }
}