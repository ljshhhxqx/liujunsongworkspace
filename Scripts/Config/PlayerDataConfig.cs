using System;
using Config;
using UnityEngine;
using UnityEngine.Serialization;

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
    // public float SlopeLimit = 30f;
    // public float MaxPredictPositionTime = 5f;
    // public float MaxPredictDistance = 0.5f;

    #endregion
}