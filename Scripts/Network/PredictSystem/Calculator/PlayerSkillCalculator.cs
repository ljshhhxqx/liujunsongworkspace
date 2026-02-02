using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Skill;
using UnityEngine;
using AnimationState = AOTScripts.Data.AnimationState;
using PropertyCalculator = AOTScripts.Data.PropertyCalculator;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerSkillCalculator : IPlayerStateCalculator
    {
        public static SkillCalculatorConstant Constant { get; private set; }

        public static void SetConstant(SkillCalculatorConstant constant)
        {
            Constant = constant;
        }

        private static bool CheckSkillCost(ISkillChecker skillChecker, SkillConfigData skillConfigData,
            PropertyCalculator propertyCalculator, AnimationState key)
        {
            if (skillChecker == null)
            {
                Debug.LogError($"Skill Checker {key} not found");
                return false;
            }

            return SkillConfig.IsSkillCostEnough(skillConfigData, propertyCalculator);
        }

        public static ISkillChecker CreateSkillChecker(SkillConfigData skillConfigData, AnimationState key)
        {
            var commonParams = CreateSkillCheckerCommon(skillConfigData, key);
            ISkillChecker skillChecker = null;
            SkillEffectLifeCycle skillEffectLifeCycle = null;
            switch (skillConfigData.skillType)
            {
                case SkillType.SingleFly:
                    skillChecker = new SingleTargetFlyEffectSkillChecker(commonParams.Item1, commonParams.Item2, skillEffectLifeCycle);
                    break;
                case SkillType.AreaRanged:
                    skillChecker = new AreaOfRangedSkillChecker(commonParams.Item1, commonParams.Item2, skillEffectLifeCycle);
                    break;
                case SkillType.AreaFly:
                    skillChecker = new AreaOfRangedFlySkillChecker(commonParams.Item1, commonParams.Item2, skillEffectLifeCycle);
                    break;
                case SkillType.Dash:
                    skillChecker = new DashSkillChecker(commonParams.Item1, commonParams.Item2, skillEffectLifeCycle);
                    break;
                case SkillType.DelayedAreaRanged:
                    skillChecker = new AreaOfRangedDelayedSkillChecker(commonParams.Item1, commonParams.Item2, skillEffectLifeCycle);
                    break;
                default:
                    Debug.LogError($"Skill Checker {skillConfigData.id} {skillConfigData.skillType} not found");
                    break;
            }
            return skillChecker;
        }

        private static (CooldownHeader, CommonSkillCheckerHeader) CreateSkillCheckerCommon(
            SkillConfigData skillConfigData, AnimationState key)
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
                AnimationState = key
            };
            return (cooldownHeader, commonSkillCheckerHeader);
        }

        public static bool ExecuteSkill(PlayerComponentController playerController, SkillConfigData skillConfigData, PropertyCalculator propertyCalculator, 
            SkillCommand skillCommand, AnimationState key, Func<uint, Vector3, IColliderConfig, HashSet<DynamicObjectData>> isHitFunc, out Vector3 position)
        {
            var checkers = playerController.SkillCheckerDict;
            var skillChecker = checkers[key];
            var skillLifeCycle = skillChecker.GetSkillEffectLifeCycle();
            position = Vector3.zero; 
            // if (!CheckSkillCost(skillChecker, skillConfigData, propertyCalculator, key))
            // {
            //     return false;
            // }
            var header = skillCommand.Header;
            var playerNetId = playerController.netId;
            if (skillCommand.IsAutoSelectTarget)
            {
                //自动选择目标，找寻离自己最近且在距离内的玩家
                position = Constant.InteractSystem.GetNearestObject(playerNetId, skillConfigData.maxDistance);
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
            if (skillLifeCycle == null)
            {
                var skillEventData = new List<SkillEventData>();
                foreach (var skillEvent in skillConfigData.events)
                {
                    skillEventData.Add(new SkillEventData
                    {
                        SkillEventType = skillEvent.skillEventType,
                        FireTime = skillEvent.fireTime,
                    });
                }
                var skillEffectLifeCycle = new SkillEffectLifeCycle(commonParam.PlayerPosition, commonParam.TargetPosition,
                    skillConfigData.radius,  skillConfigData.flySpeed, skillConfigData.duration, playerNetId, skillEventData: skillEventData);
                skillChecker.SetSkillEffectLifeCycle(skillEffectLifeCycle);
                checkers[key] = skillChecker;
                playerController.SkillCheckerDict = checkers;
            }

            if (!skillChecker.Execute(ref skillChecker, commonParam, isHitFunc))
            {
                Debug.Log("技能条件不满足");
                return false;
            }
            return true;
        }

        public static uint[] UpdateSkillFlyEffect(int connectionId, float deltaTime, ISkillChecker skillChecker, Func<uint, Vector3, IColliderConfig, HashSet<DynamicObjectData>> isHitFunc)
        {
            if (skillChecker.IsSkillEffect())
            {
                
            }
            var hitPlayers = skillChecker.UpdateFly(deltaTime, isHitFunc);
            if (hitPlayers == null || hitPlayers.Count == 0)
            {
                return null;
            }

            var hits = hitPlayers.Select(x => x.NetId).ToArray();
            var commonSkillCheckerHeader = skillChecker.GetCommonSkillCheckerHeader();
            var command = new PropertySkillCommand();
            command.Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate);
            command.SkillId = commonSkillCheckerHeader.ConfigId;
            command.HitPlayerIds = commonSkillCheckerHeader.IsAreaOfRanged ? hits.ToArray() : new uint[] { hits[0] };
            foreach (var playerId in command.HitPlayerIds)
            {
                Debug.Log($"技能命中玩家{playerId}");
                
            }
            Constant.GameSyncManager.EnqueueServerCommand(command);
            return hits;
        }
    }

    public class SkillCalculatorConstant
    {
        public GameSyncManager GameSyncManager;
        public SkillConfig SkillConfig;
        public InteractSystem InteractSystem;
        public LayerMask SceneLayerMask;
        public bool IsServer;
        public uint CasterId;
        public PlayerInGameManager PlayerInGameManager;
    }
}