using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
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

        public static void ExecuteSkill(PlayerSkillState skillState, SkillConfigData skillConfigData,
            SkillCommand skillCommand)
        {
            var header = skillCommand.Header;
            Vector3 position = Vector3.zero;
            if (skillCommand.IsAutoSelectTarget)
            {
                //自动选择目标，找寻离自己最近且在距离内的玩家
                position = Constant.PlayerInGameManager.GetOtherPlayerNearestPlayer(header.ConnectionId, skillConfigData.maxMoveDistance);
            }
            else
            {
                if (Physics.Raycast(Constant.PlayerInGameManager.GetPlayerPosition(header.ConnectionId),
                        skillCommand.DirectionNormalized, out var hit, skillConfigData.maxMoveDistance,
                        Constant.SceneLayerMask))
                {
                    position = hit.point;
                }
            }
            if (position == Vector3.zero)
            {
                //如果没有找到，就在玩家面朝方向最远处释放技能
                position =  Constant.PlayerInGameManager.GetPositionInPlayerDirection(header.ConnectionId, skillCommand.DirectionNormalized, skillConfigData.maxMoveDistance);
            }
            skillState.SkillChecker.Execute(ref skillState.SkillChecker, );
        }

        public static void UpdateSkillEffectAndValue()
        {
            
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