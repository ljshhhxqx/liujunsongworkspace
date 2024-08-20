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
    public float RunSpeed = 10f;
    public float RotateSpeed;
    public float JumpSpeed;
    public float GroundCheckRadius = 0.3f;
    public float StairsCheckRadius = 0.6f;
    public float SlopeLimit = 30f;
    public float MaxPredictPositionTime = 5f;
    public float MaxPredictDistance = 0.5f;

    #endregion
}