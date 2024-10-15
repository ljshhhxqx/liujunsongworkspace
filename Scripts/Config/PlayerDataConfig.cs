using System;
using System.Collections.Generic;
using Config;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "ScriptableObjects/PlayerData")]
public class PlayerDataConfig : ConfigBase
{
    [SerializeField] 
    private PlayerConfigData playerConfigData;
    public PlayerConfigData PlayerConfigData => playerConfigData;
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

    public float MoveSpeed;
    public float RunSpeed;
    public float RotateSpeed;
    public float OnStairsSpeed = 3f;
    public float JumpSpeed;
    public float StairsJumpSpeed;
    public float GroundCheckRadius;
    public float StairsCheckDistance;
    public List<PropertyType> MaxProperties;
    public SerializableDictionary<AnimationState, float> AnimationStrengthCosts;
    public float StrengthRecoveryPerSecond;
    
    // public float SlopeLimit = 30f;
    // public float MaxPredictPositionTime = 5f;
    // public float MaxPredictDistance = 0.5f;

    #endregion
}
        
public enum PlayerState
{
    InAir,
    OnGround,
    OnStairs
}

public enum AnimationState
{
    Idle,
    Move,
    Sprint,
    Jump,
    Dash,
    Attack,
    Dead,
}