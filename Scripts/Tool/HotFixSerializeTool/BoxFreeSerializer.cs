using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using AOTScripts.Data;
using AOTScripts.Data.NetworkMes;
using AOTScripts.Data.State;
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
        public static void Register<T>()
        {
            var type = typeof(T);
            if (type.GetCustomAttribute(typeof(MemoryPackableAttribute)) != null)
            {
                _memorySerializers[typeof(T)] = new Func<T, byte[]>(SerializeInternal<T>);
                _memoryDeserializers[typeof(T)] = new Func<byte[], T>(DeserializeInternal<T>);
            }
            else if (type.GetCustomAttribute(typeof(JsonSerializableAttribute)) != null)
            {
                _jsonSerializers[typeof(T)] = new Func<T, string>(JsonSerializeInternal<T>);
                _jsonDeserializers[typeof(T)] = new Func<string, T>(JsonDeserializeInternal<T>);
            }
        }
        
        public static void Unregister<T>()
        {
            _memorySerializers.Remove(typeof(T));
            _memoryDeserializers.Remove(typeof(T));
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

            Register<T>();
            json = JsonUtility.ToJson(value);
            return json;
        }

        public static T JsonDeserialize<T>(string json)
        {
            var type = typeof(T);
            if (_jsonDeserializers.TryGetValue(type, out var deserializer))
            {
                return ((Func<string, T>)deserializer)(json);
            }
            Register<T>();

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
        
            Register<T>();
            return MemoryPackSerializer.Serialize(value);
        }
    
        public static T MemoryDeserialize<T>(byte[] data)
        {
            var type = typeof(T);
            if (_memoryDeserializers.TryGetValue(type, out var deserializer))
            {
                return ((Func<byte[], T>)deserializer)(data);
            }
            Register<T>();
        
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
        public static void InitTownSceneData()
        {
            BoxingFreeSerializer.Register<MirrorPlayerConnectMessage>();
            BoxingFreeSerializer.Register<PlayerAuthMessage>();
            BoxingFreeSerializer.Register<MirrorCountdownMessage>();
            BoxingFreeSerializer.Register<MirrorGameStartMessage>();
            BoxingFreeSerializer.Register<MirrorGameWarmupMessage>();
            BoxingFreeSerializer.Register<MirrorPickerPickUpCollectMessage>();
            BoxingFreeSerializer.Register<MirrorPickerPickUpChestMessage>();
            BoxingFreeSerializer.Register<MirrorPlayerInputInfoMessage>();
            BoxingFreeSerializer.Register<MirrorPlayerStateMessage>();
            BoxingFreeSerializer.Register<MirrorPlayerConnectedMessage>();
            BoxingFreeSerializer.Register<MirrorPlayerRecoveryMessage>();
            BoxingFreeSerializer.Register<AttackData>();
            
            BoxingFreeSerializer.Register<PlayerEquipmentState>();
            BoxingFreeSerializer.Register<PlayerInputState>();
            BoxingFreeSerializer.Register<PlayerItemState>();
            BoxingFreeSerializer.Register<PlayerPropertyState>();
            BoxingFreeSerializer.Register<PlayerShopState>();
            BoxingFreeSerializer.Register<PlayerSkillState>();
            BoxingFreeSerializer.Register<CooldownSnapshotData>();
            
            BoxingFreeSerializer.Register<ConditionHeader>();
            BoxingFreeSerializer.Register<AttackHitConditionParam>();
            BoxingFreeSerializer.Register<SkillCastConditionParam>();
            BoxingFreeSerializer.Register<TakeDamageConditionParam>();
            BoxingFreeSerializer.Register<KillConditionParam>();
            BoxingFreeSerializer.Register<HpChangeConditionParam>();
            BoxingFreeSerializer.Register<MpChangeConditionParam>();
            BoxingFreeSerializer.Register<CriticalHitConditionParam>();
            BoxingFreeSerializer.Register<DodgeConditionParam>();
            BoxingFreeSerializer.Register<AttackConditionParam>();
            BoxingFreeSerializer.Register<SkillHitConditionParam>();
            BoxingFreeSerializer.Register<DeathConditionParam>();
            BoxingFreeSerializer.Register<MoveConditionParam>();
            BoxingFreeSerializer.Register<KeyframeData>();
            
            BoxingFreeSerializer.Register<AttackChecker>();
            BoxingFreeSerializer.Register<AttackHitChecker>();
            BoxingFreeSerializer.Register<SkillCastChecker>();
            BoxingFreeSerializer.Register<SkillHitChecker>();
            BoxingFreeSerializer.Register<TakeDamageChecker>();
            BoxingFreeSerializer.Register<KillChecker>();
            BoxingFreeSerializer.Register<HpChangeChecker>();
            BoxingFreeSerializer.Register<MpChangeChecker>();
            BoxingFreeSerializer.Register<CriticalHitChecker>();
            BoxingFreeSerializer.Register<DodgeChecker>();
            BoxingFreeSerializer.Register<DeathChecker>();
            BoxingFreeSerializer.Register<MoveChecker>();
            BoxingFreeSerializer.Register<NoConditionChecker>();
            
            BoxingFreeSerializer.Register<RandomItemsData>();
            BoxingFreeSerializer.Register<ItemOtherData>();
            BoxingFreeSerializer.Register<SkillConfigEventData>();
            BoxingFreeSerializer.Register<SkillHitExtraEffectData>();
            BoxingFreeSerializer.Register<ElementConfigData>();
            BoxingFreeSerializer.Register<GameLoopData>();
            BoxingFreeSerializer.Register<WeatherInfo>();
            
            BoxingFreeSerializer.Register<PropertyAutoRecoverCommand>();
            BoxingFreeSerializer.Register<PropertyClientAnimationCommand>();
            BoxingFreeSerializer.Register<PropertyServerAnimationCommand>();
            BoxingFreeSerializer.Register<PropertyBuffCommand>();
            BoxingFreeSerializer.Register<PropertyAttackCommand>();
            BoxingFreeSerializer.Register<PropertySkillCommand>();
            BoxingFreeSerializer.Register<PropertyEnvironmentChangeCommand>();
            BoxingFreeSerializer.Register<InputCommand>();
            BoxingFreeSerializer.Register<AnimationCommand>();
            BoxingFreeSerializer.Register<InteractionCommand>();
            BoxingFreeSerializer.Register<ItemsUseCommand>();
            BoxingFreeSerializer.Register<ItemLockCommand>();
            BoxingFreeSerializer.Register<ItemEquipCommand>();
            BoxingFreeSerializer.Register<ItemDropCommand>();
            BoxingFreeSerializer.Register<ItemExchangeCommand>();
            BoxingFreeSerializer.Register<ItemsSellCommand>();
            BoxingFreeSerializer.Register<ItemsBuyCommand>();
            BoxingFreeSerializer.Register<GoldChangedCommand>();
            BoxingFreeSerializer.Register<PropertyInvincibleChangedCommand>();
            BoxingFreeSerializer.Register<PropertyEquipmentPassiveCommand>();
            BoxingFreeSerializer.Register<PropertyEquipmentChangedCommand>();
            BoxingFreeSerializer.Register<NoUnionPlayerAddMoreScoreAndGoldCommand>();
            BoxingFreeSerializer.Register<SkillCommand>();
            BoxingFreeSerializer.Register<PlayerDeathCommand>();
            BoxingFreeSerializer.Register<PlayerRebornCommand>();
            BoxingFreeSerializer.Register<PlayerTouchedBaseCommand>();
            BoxingFreeSerializer.Register<PlayerTraceOtherPlayerHpCommand>();
            BoxingFreeSerializer.Register<ItemsGetCommand>();
            BoxingFreeSerializer.Register<EquipmentCommand>();
            BoxingFreeSerializer.Register<BuyCommand>();
            BoxingFreeSerializer.Register<RefreshShopCommand>();
            BoxingFreeSerializer.Register<SellCommand>();
            BoxingFreeSerializer.Register<SkillLoadCommand>();
            BoxingFreeSerializer.Register<TriggerCommand>();
            BoxingFreeSerializer.Register<SkillChangedCommand>();
            BoxingFreeSerializer.Register<PropertyUseSkillCommand>();
            BoxingFreeSerializer.Register<ItemSkillEnableCommand>();
            BoxingFreeSerializer.Register<PropertyGetScoreGoldCommand>();
            BoxingFreeSerializer.Register<SkillLoadOverloadAnimationCommand>();
        }

        public static void InitMainSceneData()
        {
            BoxingFreeSerializer.Register<PlayerInternalData>();
            BoxingFreeSerializer.Register<PlayerReadOnlyData>();
            BoxingFreeSerializer.Register<GameResultData>();
            BoxingFreeSerializer.Register<PlayerGameResultData>();
            BoxingFreeSerializer.Register<NonFriendOnlinePlayersResult>();
            BoxingFreeSerializer.Register<FriendData>();
            BoxingFreeSerializer.Register<FriendList>();
            BoxingFreeSerializer.Register<PlayerInfo>();
            BoxingFreeSerializer.Register<RoomGlobalInfo>();
            BoxingFreeSerializer.Register<RoomsData>();
            BoxingFreeSerializer.Register<RoomCustomInfo>();
            BoxingFreeSerializer.Register<MainGameInfo>();
            BoxingFreeSerializer.Register<GamePlayerInfo>();
            BoxingFreeSerializer.Register<RoomData>();
            BoxingFreeSerializer.Register<AOTScripts.Data.Message>();
            BoxingFreeSerializer.Register<SendMessageResponse>();
            BoxingFreeSerializer.Register<GetNewMessagesResponse>();
            BoxingFreeSerializer.Register<InvitationMessage>();
            BoxingFreeSerializer.Register<ApproveJoinRoomMessage>();
            BoxingFreeSerializer.Register<ApplyJoinRoomMessage>();
            BoxingFreeSerializer.Register<DownloadFileMessage>();
            BoxingFreeSerializer.Register<LeaveRoomMessage>();
            BoxingFreeSerializer.Register<GameInfoChangedMessage>();
            BoxingFreeSerializer.Register<StartGameMessage>();
            BoxingFreeSerializer.Register<ChangeGameInfoMessage>();
            BoxingFreeSerializer.Register<LeaveGameMessage>();
            BoxingFreeSerializer.Register<GameStartConnectionMessage>();
        }

        public static void UnregisterMainSceneData()
        {
            BoxingFreeSerializer.Unregister<PlayerInternalData>();
            BoxingFreeSerializer.Unregister<PlayerReadOnlyData>();
            BoxingFreeSerializer.Unregister<GameResultData>();
            BoxingFreeSerializer.Unregister<PlayerGameResultData>();
            BoxingFreeSerializer.Unregister<NonFriendOnlinePlayersResult>();
            BoxingFreeSerializer.Unregister<FriendData>();
            BoxingFreeSerializer.Unregister<FriendList>();
            BoxingFreeSerializer.Unregister<PlayerInfo>();
            BoxingFreeSerializer.Unregister<RoomGlobalInfo>();
            BoxingFreeSerializer.Unregister<RoomsData>();
            BoxingFreeSerializer.Unregister<RoomCustomInfo>();
            BoxingFreeSerializer.Unregister<MainGameInfo>();
            BoxingFreeSerializer.Unregister<GamePlayerInfo>();
            BoxingFreeSerializer.Unregister<RoomData>();
            BoxingFreeSerializer.Unregister<AOTScripts.Data.Message>();
            BoxingFreeSerializer.Unregister<SendMessageResponse>();
            BoxingFreeSerializer.Unregister<GetNewMessagesResponse>();
            BoxingFreeSerializer.Unregister<InvitationMessage>();
            BoxingFreeSerializer.Unregister<ApproveJoinRoomMessage>();
            BoxingFreeSerializer.Unregister<ApplyJoinRoomMessage>();
            BoxingFreeSerializer.Unregister<DownloadFileMessage>();
            BoxingFreeSerializer.Unregister<LeaveRoomMessage>();
            BoxingFreeSerializer.Unregister<GameInfoChangedMessage>();
            BoxingFreeSerializer.Unregister<StartGameMessage>();
            BoxingFreeSerializer.Unregister<ChangeGameInfoMessage>();
            BoxingFreeSerializer.Unregister<LeaveGameMessage>();
            BoxingFreeSerializer.Unregister<GameStartConnectionMessage>();
        }
    }
}