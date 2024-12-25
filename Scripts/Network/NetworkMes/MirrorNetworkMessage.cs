using System;
using System.Collections.Generic;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using UnityEngine.Serialization;

namespace Network.NetworkMes
{
    public struct MirrorNetworkMessage : NetworkMessage
    {
        
    }

    [Serializable]
    public struct MirrorPlayerConnectMessage : NetworkMessage
    {
        public string UID;
        public int ConnectionID;
        public string Name;

        public MirrorPlayerConnectMessage(string uid, int connectionID, string name)
        {            
            UID = uid;
            ConnectionID = connectionID;
            Name = name;
        }
    }

    // public struct PlayerDisconnectMessage : NetworkMessage
    // {
    //     public int ConnectionID { get; private set; }
    //     
    //     public PlayerDisconnectMessage(int connectionID)
    //     {
    //         ConnectionID = connectionID;
    //     }
    // }
    
    [Serializable]
    public struct MirrorCountdownMessage : NetworkMessage
    {
        public float RemainingTime;
        
        public MirrorCountdownMessage(float remainingTime)
        {
            RemainingTime = remainingTime;
        }
    }
    
    // public struct GameReadyMessage : NetworkMessage
    // {
    //     public string MapName { get; private set; }
    //     public GameReadyMessage(string mapName)
    //     {
    //         MapName = mapName;
    //     }
    // }

    [Serializable]
    public struct MirrorGameStartMessage : NetworkMessage
    {
        public GameInfo GameInfo;
        public MirrorGameStartMessage(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }

    [Serializable]
    public struct MirrorGameWarmupMessage : NetworkMessage
    {
        public float TimeLeft;

        public MirrorGameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }

    [Serializable]
    public struct MirrorPickerPickUpCollectMessage : NetworkMessage
    {
        public uint PickerID;
        public int ItemID;

        public MirrorPickerPickUpCollectMessage(uint pickerID, int itemID)
        {
            PickerID = pickerID;
            ItemID = itemID;
        }
    }

    [Serializable]
    public struct MirrorPickerPickUpChestMessage : NetworkMessage
    {
        public uint PickerID;
        public uint ChestID;

        public MirrorPickerPickUpChestMessage(uint pickerID, uint chestID)
        {
            PickerID = pickerID;
            ChestID = chestID;
        }
    }

    [Serializable]
    public struct PlayerInputInfo
    {
        public uint frame;
        public uint playerId;
        public Vector3 movement;
        public bool isJumpRequested;
        public bool isRollRequested;
        public bool isAttackRequested;
        public bool isSprinting;
    }

    [Serializable]
    public struct MirrorPlayerInputMessage : NetworkMessage
    {
        public PlayerInputInfo playerInputInfo;
        
        public MirrorPlayerInputMessage(PlayerInputInfo playerInputInfo)
        {
            this.playerInputInfo = playerInputInfo;
        }
    }
    
    [Serializable]
    public struct MirrorFrameUpdateMessage : NetworkMessage
    {
        public uint frame;
        public List<PlayerInputInfo> playerInputs;
        public MirrorFrameUpdateMessage(uint frame, List<PlayerInputInfo> playerInputs)
        {
            this.frame = frame;
            this.playerInputs = playerInputs;
        }
    }

    [Serializable]
    public struct MirrorFrameAttackResultMessage : NetworkMessage
    {
        public uint frame;
        public List<DamageResult> damageResults;
        public MirrorFrameAttackResultMessage(uint frame, List<DamageResult> damageResults)
        {
            this.frame = frame;
            this.damageResults = damageResults;
        }
    }

    [Serializable]
    public struct AttackData
    {
        public uint attackerId;
        public Vector3 attackOrigin;
        public Vector3 attackDirection;
        public float angle;
        public float radius;
        public float minHeight;
        public float attack;
        public float criticalRate;
        public float criticalDamageRatio;
    }

    [Serializable]
    public struct DamageResult
    {
        public uint targetId;
        public float damageAmount;
        public bool isDead;
    }

    [Serializable]
    public struct MirrorPlayerAttackHitMessage : NetworkMessage
    {
        public uint frame;
        public AttackData attackData;
        
        public MirrorPlayerAttackHitMessage(AttackData attackData, uint frame)
        {
            this.frame = frame;
            this.attackData = attackData;
        }
    }
}