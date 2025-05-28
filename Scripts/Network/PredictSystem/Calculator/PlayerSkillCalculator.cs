using System;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Skill;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerSkillCalculator
    {
        private static SkillCalculatorConstant Constant;
        
        public static void SetConstant(SkillCalculatorConstant constant)
        {
            Constant = constant;
        }

        private bool CheckSkillCdAndCost(PlayerSkillState skillState, SkillConfigData skillConfigData,
            PropertyCalculator propertyCalculator)
        {
            return skillState.SkillChecker.IsSkillNotCd() && SkillConfig.IsSkillCostEnough(skillConfigData, propertyCalculator);
        }

        public static bool ExecuteSkill(PlayerSkillState skillState, SkillConfigData skillConfigData,
            SkillCommand skillCommand, Func<Vector3, IColliderConfig, int[]> isHitFunc, Func<int, PropertyCalculatorData> getPropertyCalculatorDataFunc)
        {
            var header = skillCommand.Header;
            var position = Vector3.zero;
            if (skillCommand.IsAutoSelectTarget)
            {
                //自动选择目标，找寻离自己最近且在距离内的玩家
                position = Constant.PlayerInGameManager.GetOtherPlayerNearestPlayer(header.ConnectionId, skillConfigData.maxDistance);
            }
            else
            {
                if (Physics.Raycast(Constant.PlayerInGameManager.GetPlayerPosition(header.ConnectionId),
                        skillCommand.DirectionNormalized, out var hit, skillConfigData.maxDistance,
                        Constant.SceneLayerMask))
                {
                    position = hit.point;
                }
            }
            if (position == Vector3.zero)
            {
                //如果没有找到，就在玩家面朝方向最远处释放技能
                position = Constant.PlayerInGameManager.GetPositionInPlayerDirection(header.ConnectionId, skillCommand.DirectionNormalized, skillConfigData.maxDistance);
            }

            var commonParam = new SkillCheckerParams
            {
                PlayerPosition = Constant.PlayerInGameManager.GetPlayerPosition(header.ConnectionId),
                TargetPosition = position,
                Radius = skillConfigData.radius,
            };

            if (!skillState.SkillChecker.Execute(ref skillState.SkillChecker, commonParam, isHitFunc, getPropertyCalculatorDataFunc))
            {
                Debug.Log("技能条件不满足");
                return false;
            }
            return true;
        }

        public static void UpdateSkillFlyEffect(int connectionId, float deltaTime, PlayerSkillState skillState, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            var hitPlayers = skillState.SkillChecker.UpdateFly(deltaTime, isHitFunc);
            if (hitPlayers.Length == 0)
            {
                Debug.Log("没有命中任何玩家");
                return;
            }

            var command = new PropertySkillCommand();
            command.Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate);
            command.SkillId = skillState.CurrentSkillConfigId;
            command.HitPlayerIds = hitPlayers;
            Constant.GameSyncManager.EnqueueServerCommand(command);
        }
    }

    public class SkillCalculatorConstant
    {
        public GameSyncManager GameSyncManager;
        public SkillConfig SkillConfig;
        public LayerMask SceneLayerMask;
        public bool IsServer;
        public PlayerInGameManager PlayerInGameManager;
    }
}