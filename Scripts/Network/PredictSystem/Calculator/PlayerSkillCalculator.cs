using System;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
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

        private static bool CheckSkillCdAndCost(ISkillChecker skillChecker, SkillConfigData skillConfigData,
            PropertyCalculator propertyCalculator, string key)
        {
            if (skillChecker == null)
            {
                Debug.LogError($"Skill Checker {key} not found");
                return false;
            }

            return SkillConfig.IsSkillCostEnough(skillConfigData, propertyCalculator) && skillChecker.IsSkillNotCd();
        }

        public static ISkillChecker CreateSkillChecker(SkillConfigData skillConfigData)
        {
            var commonParams = CreateSkillCheckerCommon(skillConfigData);
            ISkillChecker skillChecker = null;
            switch (skillConfigData.skillType)
            {
                case SkillType.SingleFly:
                    skillChecker = new SingleTargetFlyEffectSkillChecker(commonParams.Item1, commonParams.Item2, null);
                    break;
                case SkillType.Single:
                    //skillChecker = new SingleTargetSkillChecker(commonParams.Item1, commonParams.Item2, commonParams.Item3);
                    break;
                case SkillType.AreaRanged:
                    skillChecker = new AreaOfRangedSkillChecker(commonParams.Item1, commonParams.Item2, null);
                    break;
                case SkillType.AreaFly:
                    skillChecker = new AreaOfRangedFlySkillChecker(commonParams.Item1, commonParams.Item2, null);
                    break;
                case SkillType.Dash:
                    skillChecker = new DashSkillChecker(commonParams.Item1, commonParams.Item2, null);
                    break;
                case SkillType.DelayedAreaRanged:
                    skillChecker = new AreaOfRangedDelayedSkillChecker(commonParams.Item1, commonParams.Item2, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return skillChecker;
        }

        private static (CooldownHeader, CommonSkillCheckerHeader) CreateSkillCheckerCommon(
            SkillConfigData skillConfigData)
        {
            var cooldownHeader = new CooldownHeader
            {
                Cooldown = skillConfigData.cooldown,
                CurrentTime = 0,
            };
            var commonSkillCheckerHeader = new CommonSkillCheckerHeader
            {
                ConfigId = skillConfigData.id,
                MaxDistance = skillConfigData.maxDistance,
                Radius = skillConfigData.radius,
                CooldownTime = skillConfigData.cooldown,
                SkillEffectPrefabName = skillConfigData.particleName,
                ExistTime = skillConfigData.duration,
            };
            return (cooldownHeader, commonSkillCheckerHeader);
        }

        public static bool ExecuteSkill(PlayerSkillState skillState, SkillConfigData skillConfigData, PropertyCalculator propertyCalculator, 
            SkillCommand skillCommand, string key, Func<Vector3, IColliderConfig, int[]> isHitFunc, out Vector3 position)
        {
            var skillChecker = skillState.SkillCheckers[key];
            position = Vector3.zero;
            if (!CheckSkillCdAndCost(skillChecker, skillConfigData, propertyCalculator, key))
            {
                return false;
            }
            var header = skillCommand.Header;
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

            if (!skillChecker.Execute(ref skillChecker, commonParam, isHitFunc))
            {
                Debug.Log("技能条件不满足");
                return false;
            }
            return true;
        }

        public static int[] UpdateSkillFlyEffect(int connectionId, float deltaTime, ISkillChecker skillChecker, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            var hitPlayers = skillChecker.UpdateFly(deltaTime, isHitFunc);
            if (hitPlayers.Length == 0)
            {
                Debug.Log("没有命中任何玩家");
                return Array.Empty<int>();
            }

            var commonSkillCheckerHeader = skillChecker.GetCommonSkillCheckerHeader();
            var command = new PropertySkillCommand();
            command.Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate);
            command.SkillId = commonSkillCheckerHeader.ConfigId;
            command.HitPlayerIds = commonSkillCheckerHeader.IsAreaOfRanged ? hitPlayers : new int[] { hitPlayers[0] };
            Constant.GameSyncManager.EnqueueServerCommand(command);
            return hitPlayers;
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