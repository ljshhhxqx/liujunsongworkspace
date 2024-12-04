using System;
using System.Collections.Generic;
using Config;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "ScriptableObjects/PlayerData")]
    public class PlayerDataConfig : ConfigBase
    {
        [SerializeField] 
        private PlayerConfigData playerConfigData;
        public PlayerConfigData PlayerConfigData => playerConfigData;

        public float GetPlayerAnimationCost(AnimationState state)
        {
            foreach (var cost in playerConfigData.AnimationStrengthCosts)
            {
                if (cost.State == state)
                {
                    return cost.Cost;
                }
            }
            Debug.LogWarning("Animation cost not found for state: " + state);
            return 0f;
        }
    }

    [Serializable]
    public class PlayerConfigData
    {
        #region Camera

        public float TurnSpeed;
        public float MouseSpeed;
        public Vector3 Offset;

        #endregion
    
        #region Player

        public float SprintSpeedFactor = 1.5f;
        public float RotateSpeed;
        public float OnStairsSpeedRatioFactor = 0.7f;
        public float JumpSpeed;
        public float StairsJumpSpeed;
        public float GroundCheckRadius;
        public float StairsCheckDistance;
        public List<PropertyType> MaxProperties;
        public List<PropertyType> BaseProperties;
        public List<PropertyType> MinProperties;
        public List<AnimationCost> AnimationStrengthCosts;
        public float StrengthRecoveryPerSecond;

        public float RollDistance;
        public float RollForce;
    
        // public float SlopeLimit = 30f;
        // public float MaxPredictPositionTime = 5f;
        // public float MaxPredictDistance = 0.5f;

        #endregion
    }

    [Serializable]
    public struct AnimationCost
    {
        public AnimationState State;
        public float Cost;
    }

    public enum PlayerEnvironmentState
    {
        InAir,
        OnGround,
        OnStairs,
        Swimming,
    }

    public enum AnimationState
    {
        Idle,
        Move,
        Sprint,
        Jump,
        SprintJump,
        Roll,
        Falling,
        Landed,
        Attack,
        Dead,
        Hit,
        Death
    }
}