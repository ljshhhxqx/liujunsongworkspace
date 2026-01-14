using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
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

        public HashSet<uint> IsInAttackRange(AttackParams attackParams, bool isServer = true)
        {
            var hitPlayers = new HashSet<uint>();
        
            // 获取攻击者所在Grid
            var attackerGrid = MapBoundDefiner.Instance.GetGridPosition(attackParams.attackPos);
        
            // 计算检测半径对应的Grid范围
            var gridRadius = Mathf.CeilToInt(AttackConfigData.AttackRadius / MapBoundDefiner.Instance.GridSize);
        
            var nearbyGrids = MapBoundDefiner.Instance.GetSurroundingGrids(attackerGrid, gridRadius);
            var candidates = GameObjectContainer.Instance.GetDynamicObjectIdsByGrids(nearbyGrids);

            foreach (var candidate in candidates)
            {
                if (candidate == attackParams.attackerNetId) continue;

                var identity = GameStaticExtensions.GetNetworkIdentity(candidate);
            
                if (!identity) continue;

                Debug.Log($"Start check attack {candidate}:" +
                          $"attackPos:{attackParams.attackPos},attackDir:{attackParams.attackDir},attackerNetId:{attackParams.attackerNetId},targetPos:{identity.transform.position},attackRadius:{attackParams.AttackConfigData.AttackRadius},attackRange:{attackParams.AttackConfigData.AttackRange},attackHeight:{attackParams.AttackConfigData.AttackHeight}");
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

            return hitPlayers;
        }
        
        #region 辅助方法

        private bool IsInAttackSector(Vector3 origin, Vector3 direction, Vector3 targetPos, float radius, float angle, float height)
        {
            Vector3 toTarget = targetPos - origin;
            float sqrDistance = toTarget.sqrMagnitude;
    
            Debug.Log($"sqrDistance: {sqrDistance}, radius^2: {radius*radius}");
    
            if (sqrDistance > radius * radius) 
            {
                Debug.Log("Failed: Distance check");
                return false;
            }
    
            float heightDiff = Mathf.Abs(origin.y - targetPos.y);
            Debug.Log($"Height diff: {heightDiff}, max height: {height}");
    
            if (heightDiff > height) 
            {
                Debug.Log("Failed: Height check");
                return false;
            }
    
            float cosAngle = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            Vector3 dirNormalized = direction.normalized;
            Vector3 toTargetNormalized = toTarget.normalized;
            float dot = Vector3.Dot(dirNormalized, toTargetNormalized);
    
            Debug.Log($"cosAngle: {cosAngle}, dot: {dot}, angle: {Mathf.Acos(dot) * Mathf.Rad2Deg}");
    
            bool result = dot >= cosAngle;
            if (!result) Debug.Log("Failed: Angle check");
            return result;
        }
        #endregion
    }

    public class PlayerBattleComponent
    {
        public Transform Transform;
        public InteractSystem InteractSystem;
        
        public PlayerBattleComponent(Transform transform, InteractSystem interactSystem)
        {
            Transform = transform;
            InteractSystem = interactSystem;
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