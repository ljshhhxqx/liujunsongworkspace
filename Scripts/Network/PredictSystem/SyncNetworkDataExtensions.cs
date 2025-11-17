using System;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.ObjectPool;
using Mirror;

namespace HotUpdate.Scripts.Network.PredictSystem
{
    public static class SyncNetworkDataExtensions
    {
        // 基础验证参数配置
        public const int MAX_TICK_DELTA = 30;      // 允许的最大tick偏差
        public const long TIMESTAMP_TOLERANCE = 5000; // 5秒时间容差（毫秒）

        public static NetworkCommandHeader CreateCommand(CommandType commandType, int tick,
            long timeStamp, CommandAuthority authority = CommandAuthority.Client)
        {
            return new NetworkCommandHeader
            {
                ConnectionId = NetworkServer.localConnection.connectionId,
                Tick = tick,
                CommandType = commandType,
                Timestamp = timeStamp,
                Authority = authority
            };
        }

        public static CommandValidationResult ValidateCommand(this INetworkCommand command)
        {
            var result = ObjectPoolManager<CommandValidationResult>.Instance.Get(50);
            var header = command.GetHeader();

            // 1. Tick验证
            if (header.Tick <= 0)
            {
                result.AddError($"{command.GetCommandType().ToString()}Invalid tick value {header.Tick}");
            }

            // 2. 时间戳验证
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(currentTime - header.Timestamp) > TIMESTAMP_TOLERANCE)
            {
                result.AddError($"Timestamp out of sync: {currentTime - header.Timestamp}ms");
            }

            // 3. 命令类型验证
            if (header.CommandType < 0 || header.CommandType > CommandType.Shop)
            {
                result.AddError($"Unknown command type: {header.CommandType}");
            }

            // 4. 基础有效性验证
            if (!command.IsValid())
            {
                result.AddError("Command specific validation failed");
            }

            return result;
        }
        
        public static BaseSyncSystem GetSyncSystem(this CommandType syncNetworkData)
        {
            switch (syncNetworkData)
            {
                case CommandType.Property:
                    return new PlayerPropertySyncSystem();
                case CommandType.Input:
                    return new PlayerInputSyncSystem();
                case CommandType.Item:
                    return new PlayerItemSyncSystem();
                case CommandType.Equipment:
                    return new PlayerEquipmentSystem();
                case CommandType.Shop:
                    return new ShopSyncSystem();
                case CommandType.Skill:
                    return new PlayerSkillSyncSystem();
                case CommandType.Interact:
                    //Debug.LogWarning("Not implemented yet");
                    return null;
                // case CommandType.UI:
                //     return new PlayerCombatSyncSystem();
            }   
            return null;
        }
    }
}