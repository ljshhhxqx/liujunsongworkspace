using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerBattleCalculator : IPlayerStateCalculator
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

        public uint[] IsInAttackRange(AttackParams attackParams, bool isServer = true)
        {
            var hitPlayers = new HashSet<uint>();
        
            // 获取攻击者所在Grid
            var attackerGrid = MapBoundDefiner.Instance.GetGridPosition(attackParams.attackPos);
        
            // 计算检测半径对应的Grid范围
            var gridRadius = Mathf.CeilToInt(AttackConfigData.AttackRadius / MapBoundDefiner.Instance.GridSize);
        
            // 获取周围Grid中的玩家
            var nearbyGrids = MapBoundDefiner.Instance.GetSurroundingGrids(attackerGrid, gridRadius);
            var candidates = PlayerInGameManager.Instance.GetPlayersInGrids(nearbyGrids);

            foreach (var candidate in candidates)
            {
                if (candidate == attackParams.attackerNetId) continue;

                var identity = isServer ? 
                    NetworkServer.spawned[candidate] : 
                    NetworkClient.spawned[candidate];
            
                if (!identity) continue;

                // 精确检测
                if (IsInAttackSector(
                        attackParams.attackPos,
                        attackParams.attackDir,
                        identity.transform.position,
                        attackParams.AttackConfigData.AttackRadius,
                        attackParams.AttackConfigData.AttackRange,
                        attackParams.AttackConfigData.AttackHeight))
                {
                    hitPlayers.Add(candidate);
                }
            }

            return hitPlayers.ToArray();
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

        public bool IsClient { get; }
    }

    public class PlayerBattleComponent
    {
        public Transform Transform;
        
        public PlayerBattleComponent(Transform transform)
        {
            Transform = transform;
        }
    }

    [Serializable]
    public struct AttackParams
    {
        public Vector3 attackPos;
        public Vector3 attackDir;
        public int attackerId;
        public uint attackerNetId;
        public AttackConfigData AttackConfigData;
        
        public AttackParams(Vector3 attackPos, Vector3 attackDir, int attackerId, uint attackerNetId, AttackConfigData attackConfigData)
        {
            this.attackPos = attackPos;
            this.attackDir = attackDir;
            this.attackerId = attackerId;
            this.attackerNetId = attackerNetId;
            AttackConfigData = attackConfigData;
        }
    }
}