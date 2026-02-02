using System;
using System.Collections.Generic;
using AOTScripts.Data;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.HotFixSerializeTool
{
    public static class BoxingFreeSerializer
    {
        private static Dictionary<Type, Delegate> _jsonSerializers = new Dictionary<Type, Delegate>();
        private static Dictionary<Type, Delegate> _jsonDeserializers = new Dictionary<Type, Delegate>();
        private static Dictionary<Type, Delegate> _memorySerializers = new Dictionary<Type, Delegate>();
        private static Dictionary<Type, Delegate> _memoryDeserializers = new Dictionary<Type, Delegate>();
    
        // 注册结构体的序列化方法，避免运行时反射
        public static void RegisterMemory<T>()
        {
            _memorySerializers[typeof(T)] = new Func<T, byte[]>(SerializeInternal<T>);
            _memoryDeserializers[typeof(T)] = new Func<byte[], T>(DeserializeInternal<T>);
        }
        
        public static void RegisterJson<T>()
        {
            _jsonSerializers[typeof(T)] = new Func<T, string>(JsonSerializeInternal<T>);
            _jsonDeserializers[typeof(T)] = new Func<string, T>(JsonDeserializeInternal<T>);
        }
        
        public static void UnregisterMemory<T>()
        {
            _memorySerializers.Remove(typeof(T));
            _memoryDeserializers.Remove(typeof(T));
        }

        public static void UnregisterJson<T>()
        {
            _jsonSerializers.Remove(typeof(T));
            _jsonDeserializers.Remove(typeof(T));
        }

        public static string JsonSerialize<T>(T value)
        {
            var type = typeof(T);
            string json;
            if (_jsonSerializers.TryGetValue(type, out var serializer))
            {
                json = ((Func<T, string>)serializer)(value);
                return json;
            }

            RegisterJson<T>();
            json = JsonUtility.ToJson(value);
            return json;
        }

        public static T JsonDeserialize<T>(string json)
        {
#if UNITY_EDITOR
            return JsonUtility.FromJson<T>(json);
#endif
            var type = typeof(T);
            if (_jsonDeserializers.TryGetValue(type, out var deserializer))
            {
                return ((Func<string, T>)deserializer)(json);
            }
            RegisterJson<T>();

            return (T)JsonUtility.FromJson(json, type);
        }

        // 序列化（避免装箱）
        public static byte[] MemorySerialize<T>(T value)
        {
            var type = typeof(T);
            if (_memorySerializers.TryGetValue(type, out var serializer))
            {
                return ((Func<T, byte[]>)serializer)(value);
            }
        
            RegisterMemory<T>();
            return MemoryPackSerializer.Serialize(value);
        }
    
        public static T MemoryDeserialize<T>(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<T>(data);
#if UNITY_EDITOR
#endif
            var type = typeof(T);
            if (_memoryDeserializers.TryGetValue(type, out var deserializer))
            {
                return ((Func<byte[], T>)deserializer)(data);
            }
            RegisterMemory<T>();
        
            return (T)MemoryPackSerializer.Deserialize(type, data);
        }
        private static byte[] SerializeInternal<T>(T value)
        {
            return MemoryPackSerializer.Serialize(typeof(T), value);
        }
    
        private static T DeserializeInternal<T>(byte[] data)
        {
            return (T)MemoryPackSerializer.Deserialize(typeof(T), data);
        }

        private static T JsonDeserializeInternal<T>(string arg)
        {
            return (T)JsonUtility.FromJson(arg, typeof(T));
        }

        private static string JsonSerializeInternal<T>(T arg)
        {
            return JsonUtility.ToJson(arg);
        }
    }

    public static class BoxingFreeExtension
    {
        // public static void InitTownSceneData()
        // {
        //     BoxingFreeSerializer.RegisterMemory<MirrorPlayerConnectMessage>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerAuthMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorCountdownMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorGameStartMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorGameWarmupMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorPickerPickUpCollectMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorPickerPickUpChestMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorPlayerInputInfoMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorPlayerStateMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorPlayerConnectedMessage>();
        //     BoxingFreeSerializer.RegisterMemory<MirrorPlayerRecoveryMessage>();
        //     BoxingFreeSerializer.RegisterMemory<AttackData>();
        //     
        //     BoxingFreeSerializer.RegisterMemory<PlayerEquipmentState>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerInputState>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerItemState>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerPropertyState>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerShopState>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerSkillState>();
        //     BoxingFreeSerializer.RegisterMemory<CooldownSnapshotData>();
        //     
        //     BoxingFreeSerializer.RegisterMemory<ConditionHeader>();
        //     BoxingFreeSerializer.RegisterMemory<AttackHitConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<SkillCastConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<TakeDamageConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<KillConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<HpChangeConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<MpChangeConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<CriticalHitConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<DodgeConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<AttackConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<SkillHitConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<DeathConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<MoveConditionParam>();
        //     BoxingFreeSerializer.RegisterMemory<KeyframeData>();
        //     
        //     BoxingFreeSerializer.RegisterMemory<AttackChecker>();
        //     BoxingFreeSerializer.RegisterMemory<AttackHitChecker>();
        //     BoxingFreeSerializer.RegisterMemory<SkillCastChecker>();
        //     BoxingFreeSerializer.RegisterMemory<SkillHitChecker>();
        //     BoxingFreeSerializer.RegisterMemory<TakeDamageChecker>();
        //     BoxingFreeSerializer.RegisterMemory<KillChecker>();
        //     BoxingFreeSerializer.RegisterMemory<HpChangeChecker>();
        //     BoxingFreeSerializer.RegisterMemory<MpChangeChecker>();
        //     BoxingFreeSerializer.RegisterMemory<CriticalHitChecker>();
        //     BoxingFreeSerializer.RegisterMemory<DodgeChecker>();
        //     BoxingFreeSerializer.RegisterMemory<DeathChecker>();
        //     BoxingFreeSerializer.RegisterMemory<MoveChecker>();
        //     BoxingFreeSerializer.RegisterMemory<NoConditionChecker>();
        //     
        //     BoxingFreeSerializer.RegisterMemory<RandomItemsData>();
        //     BoxingFreeSerializer.RegisterMemory<ItemOtherData>();
        //     BoxingFreeSerializer.RegisterMemory<SkillConfigEventData>();
        //     BoxingFreeSerializer.RegisterMemory<SkillHitExtraEffectData>();
        //     BoxingFreeSerializer.RegisterMemory<ElementConfigData>();
        //     BoxingFreeSerializer.RegisterMemory<GameLoopData>();
        //     BoxingFreeSerializer.RegisterMemory<WeatherInfo>();
        //     
        //     BoxingFreeSerializer.RegisterMemory<PropertyAutoRecoverCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyClientAnimationCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyServerAnimationCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyBuffCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyAttackCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertySkillCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyEnvironmentChangeCommand>();
        //     BoxingFreeSerializer.RegisterMemory<InputCommand>();
        //     BoxingFreeSerializer.RegisterMemory<AnimationCommand>();
        //     BoxingFreeSerializer.RegisterMemory<InteractionCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemsUseCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemLockCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemEquipCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemDropCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemExchangeCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemsSellCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemsBuyCommand>();
        //     BoxingFreeSerializer.RegisterMemory<GoldChangedCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyInvincibleChangedCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyEquipmentPassiveCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyEquipmentChangedCommand>();
        //     BoxingFreeSerializer.RegisterMemory<NoUnionPlayerAddMoreScoreAndGoldCommand>();
        //     BoxingFreeSerializer.RegisterMemory<SkillCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerDeathCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerRebornCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerTouchedBaseCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PlayerTraceOtherPlayerHpCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemsGetCommand>();
        //     BoxingFreeSerializer.RegisterMemory<EquipmentCommand>();
        //     BoxingFreeSerializer.RegisterMemory<BuyCommand>();
        //     BoxingFreeSerializer.RegisterMemory<RefreshShopCommand>();
        //     BoxingFreeSerializer.RegisterMemory<SellCommand>();
        //     BoxingFreeSerializer.RegisterMemory<SkillLoadCommand>();
        //     BoxingFreeSerializer.RegisterMemory<TriggerCommand>();
        //     BoxingFreeSerializer.RegisterMemory<SkillChangedCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyUseSkillCommand>();
        //     BoxingFreeSerializer.RegisterMemory<ItemSkillEnableCommand>();
        //     BoxingFreeSerializer.RegisterMemory<PropertyGetScoreGoldCommand>();
        //     BoxingFreeSerializer.RegisterMemory<SkillLoadOverloadAnimationCommand>();
        // }

        public static void UnregisterMainSceneData()
        {
            BoxingFreeSerializer.UnregisterJson<PlayerInternalData>();
            BoxingFreeSerializer.UnregisterJson<PlayerReadOnlyData>();
            BoxingFreeSerializer.UnregisterJson<GameResultData>();
            BoxingFreeSerializer.UnregisterJson<PlayerGameResultData>();
            BoxingFreeSerializer.UnregisterJson<NonFriendOnlinePlayersResult>();
            BoxingFreeSerializer.UnregisterJson<FriendData>();
            BoxingFreeSerializer.UnregisterJson<FriendList>();
            BoxingFreeSerializer.UnregisterJson<PlayerInfo>();
            BoxingFreeSerializer.UnregisterJson<RoomGlobalInfo>();
            BoxingFreeSerializer.UnregisterJson<RoomsData>();
            BoxingFreeSerializer.UnregisterJson<RoomCustomInfo>();
            BoxingFreeSerializer.UnregisterJson<MainGameInfo>();
            BoxingFreeSerializer.UnregisterJson<GamePlayerInfo>();
            BoxingFreeSerializer.UnregisterJson<RoomData>();
            BoxingFreeSerializer.UnregisterJson<AOTScripts.Data.Message>();
            BoxingFreeSerializer.UnregisterJson<SendMessageResponse>();
            BoxingFreeSerializer.UnregisterJson<GetNewMessagesResponse>();
            BoxingFreeSerializer.UnregisterJson<InvitationMessage>();
            BoxingFreeSerializer.UnregisterJson<ApproveJoinRoomMessage>();
            BoxingFreeSerializer.UnregisterJson<ApplyJoinRoomMessage>();
            BoxingFreeSerializer.UnregisterJson<DownloadFileMessage>();
            BoxingFreeSerializer.UnregisterJson<LeaveRoomMessage>();
            BoxingFreeSerializer.UnregisterJson<GameInfoChangedMessage>();
            BoxingFreeSerializer.UnregisterJson<StartGameMessage>();
            BoxingFreeSerializer.UnregisterJson<ChangeGameInfoMessage>();
            BoxingFreeSerializer.UnregisterJson<LeaveGameMessage>();
            BoxingFreeSerializer.UnregisterJson<GameStartConnectionMessage>();
        }
    }
}