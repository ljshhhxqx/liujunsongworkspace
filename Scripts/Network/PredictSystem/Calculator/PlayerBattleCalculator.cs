using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerBattleCalculator
    {
        public PlayerBattleComponent PlayerBattleComponent;
        public static AttackConfigData AttackConfigData;
        
        public static void SetAttackConfigData(AttackConfigData attackConfigData)
        {
            AttackConfigData = attackConfigData;
        }
        
        public PlayerBattleCalculator(PlayerBattleComponent playerBattleComponent)
        {
            PlayerBattleComponent = playerBattleComponent;
        }
        
        public int[] IsInAttackRange(AttackParams attackParams, bool isServer = true)
        {
            var hitPlayers = new HashSet<uint>();
        
            // 获取攻击者所在Grid
            var attackerGrid = PlayerBattleComponent.MapBoundDefiner.GetGridPosition(attackParams.AttackPos);
        
            // 计算检测半径对应的Grid范围
            var gridRadius = Mathf.CeilToInt(AttackConfigData.AttackRadius / PlayerBattleComponent.MapBoundDefiner.GridSize);
        
            // 获取周围Grid中的玩家
            var nearbyGrids = PlayerBattleComponent.MapBoundDefiner.GetSurroundingGrids(attackerGrid, gridRadius);
            var candidates = PlayerBattleComponent.PlayerInGameManager.GetPlayersInGrids(nearbyGrids);

            foreach (var candidate in candidates)
            {
                if (candidate == attackParams.AttackerNetId) continue;

                var identity = isServer ? 
                    NetworkServer.spawned[candidate] : 
                    NetworkClient.spawned[candidate];
            
                if (!identity) continue;

                // 精确检测
                if (IsInAttackSector(
                        attackParams.AttackPos,
                        attackParams.AttackDir,
                        identity.transform.position,
                        attackParams.AttackConfigData.AttackRadius,
                        attackParams.AttackConfigData.AttackRange,
                        attackParams.AttackConfigData.AttackHeight))
                {
                    hitPlayers.Add(candidate);
                }
            }

            return hitPlayers.Select(x => (int)x).ToArray();
        }
        
        #region 辅助方法

        // 扇形区域检测（优化版）
        private bool IsInAttackSector(Vector3 origin, Vector3 direction, Vector3 targetPos, float radius, float angle, float height)
        {
            Vector3 toTarget = targetPos - origin;
            float sqrDistance = toTarget.sqrMagnitude;
            
            // 快速距离检查
            if (sqrDistance > radius * radius) return false;
            // 高度检查
            if (Mathf.Abs(toTarget.y - origin.y) > height) return false;
            
            // 精确角度检查
            float cosAngle = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            float dot = Vector3.Dot(direction.normalized, toTarget.normalized);
            return dot >= cosAngle;
        }
        #endregion
    }

    public class PlayerBattleComponent
    {
        public Transform Transform;
        public MapBoundDefiner MapBoundDefiner;
        public PlayerInGameManager PlayerInGameManager;
        
        public PlayerBattleComponent(Transform transform, MapBoundDefiner mapBoundDefiner, PlayerInGameManager playerInGameManager)
        {
            Transform = transform;
            MapBoundDefiner = mapBoundDefiner;
            PlayerInGameManager = playerInGameManager;
        }
    }

    public struct AttackParams
    {
        public Vector3 AttackPos;
        public Vector3 AttackDir;
        public int AttackerId;
        public uint AttackerNetId;
        public AttackConfigData AttackConfigData;
        
        public AttackParams(Vector3 attackPos, Vector3 attackDir, int attackerId, uint attackerNetId, AttackConfigData attackConfigData)
        {
            AttackPos = attackPos;
            AttackDir = attackDir;
            AttackerId = attackerId;
            AttackerNetId = attackerNetId;
            AttackConfigData = attackConfigData;
        }
    }
}