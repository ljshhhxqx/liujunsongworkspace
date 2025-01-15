using System;
using System.Collections.Generic;
using System.Threading;
using HotUpdate.Scripts.Config.JsonConfig;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

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
    public struct PlayerInputCommand
    {
        public Vector3 movement;
        public bool isJumpRequested;
        public bool isRollRequested;
        public bool isAttackRequested;
        public bool isSprinting;
    }

    // 客户端发送给服务器的输入数据
    [Serializable]
    public struct InputData
    {
        public int sequence;             // 输入序号，用于状态和解时确定输入顺序
        public float timestamp;          // 输入发生的时间戳，用于输入插值和延迟检测
        public AnimationState command;    // 具体的动作命令（移动、跳跃等）
        public Vector2 moveDirection;    // 移动方向（用于移动和翻滚等）
        public Vector3 targetPosition;   // 目标位置（用于攻击等定向动作）
        public Quaternion rotation;      // 角色朝向
    }

    // 服务器发送给客户端的状态数据
    [Serializable]
    public struct ServerState
    {
        public int lastProcessedInput;   // 服务器最后处理的输入序号，用于状态和解
        public float timestamp;          // 状态的时间戳，用于状态插值
        public Vector3 position;         // 角色位置
        public Vector3 velocity;         // 角色速度
        public Quaternion rotation;      // 角色朝向
        public ActionType actionType;    // 动作类型
        public AnimationState command;   // 当前执行的命令
        public float health;             // 生命值等游戏状态
    }
    
    [Serializable]
    public struct MirrorPlayerInputInfoMessage : NetworkMessage
    {
        public InputData input;
        public MirrorPlayerInputInfoMessage(InputData input)
        {
            this.input = input;
        }
    }   
    
    [Serializable]
    public struct MirrorPlayerStateMessage : NetworkMessage
    {
        public ServerState state;
        public MirrorPlayerStateMessage(ServerState state)
        {
            this.state = state;
        }
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
    public struct MirrorPlayerRecoveryMessage : NetworkMessage
    {
        public uint frame;
        public float strengthRecovered;
        public float healthRecovered;
        
        public MirrorPlayerRecoveryMessage(uint frame, float strengthRecovered, float healthRecovered)
        {
            this.frame = frame;
            this.strengthRecovered = strengthRecovered;
            this.healthRecovered = healthRecovered;
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