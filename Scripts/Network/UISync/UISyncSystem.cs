using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using Mirror;
using VContainer;

namespace HotUpdate.Scripts.Network.UISync
{
    public class UISyncSystem : NetworkBehaviour
    {
        // 双通道队列
        private readonly ConcurrentQueue<UISyncCommand> _immediateQueue = new ConcurrentQueue<UISyncCommand>();
        private readonly ConcurrentQueue<UISyncCommand> _timedQueue = new ConcurrentQueue<UISyncCommand>();
        // UniTask取消令牌
        private CancellationTokenSource _cts;
        private GameConfigData _gameConfigData;
        public Dictionary<int, UIDataBroker> UIDataBroker { get; } = new Dictionary<int, UIDataBroker>();

        [Inject]
        private void Init(IConfigProvider configProvider, GameSyncManager gameSyncManager)
        {
            var config = configProvider.GetConfig<JsonDataConfig>();
            _gameConfigData = config.GameConfig;
            // 统一启动双通道处理
            if (isServer)
            {
                _cts = new CancellationTokenSource();
                ProcessImmediateChannel(_cts.Token).Forget();
                ProcessTimedChannel(_cts.Token).Forget();
            }
            gameSyncManager.OnPlayerConnected += OnPlayerConnect;
            gameSyncManager.OnPlayerDisconnected += OnPlayerDisconnect;
            RegisterNetworkReaderWriter();
        }

        private void OnPlayerDisconnect(int connectionId)
        {
            UIDataBroker.Remove(connectionId);
        }

        private void OnPlayerConnect(int connectionId, uint playerNetId, NetworkIdentity connection)
        {
            UIDataBroker.Add(connectionId, new UIDataBroker(connectionId));
        }

        // 客户端发起UI更新请求
        [Command]
        public void CmdUpdateUI(UISyncDataHeader header, byte[] data, UISyncDataType type)
        {
            var uiData = ObjectPool<UISyncCommand>.Get();
            uiData.Header = header;
            uiData.CommandData = data;
            uiData.SyncDataType = type;
            switch (header.SyncMode)
            {
                case SyncMode.Immediate:
                    _immediateQueue.Enqueue(uiData);
                    break;
                case SyncMode.Timed:
                    _timedQueue.Enqueue(uiData);
                    break;
            }
        }

        [Server]
        private async UniTaskVoid ProcessImmediateChannel(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => !_immediateQueue.IsEmpty, 
                    cancellationToken: ct);

                while (_immediateQueue.TryDequeue(out var data))
                {
                    HandleUICommand(data);
                    //RpcSyncUI(data.Header.CommandHeader.ConnectionId, MemoryPackSerializer.Serialize(data));
                }
            }
        }

        private void HandleUICommand(UISyncCommand command)
        {
            var commandData = MemoryPackSerializer.Deserialize<IUISyncCommandData>(command.CommandData);
            switch (commandData)
            {
                case PlayerUseItemData playerUseItemData:
                    //GameEventManager.Instance.Publish(new PlayerUseItemEvent(playerUseItemData));
                    break;
                case PlayerExchangeItemData playerExchangeItemData:
                    //GameEventManager.Instance.Publish(new PlayerExchangeItemEvent(playerExchangeItemData));
                    break;
                default:
                    break;
            }
        }

        [Server]
        // 定时通道处理（UniTask版本）
        private async UniTaskVoid ProcessTimedChannel(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay((int)(_gameConfigData.uiUpdateInterval * 1000), 
                    false, 
                    PlayerLoopTiming.Update, 
                    ct);

                if (_timedQueue.IsEmpty) continue;

                //var batch = new List<byte[]>();
                while (_timedQueue.TryDequeue(out var data))
                {
                    HandleUICommand(data);
                    //batch.Add(MemoryPackSerializer.Serialize(data));
                }

                //RpcBatchSyncUI(batch.ToArray());
            }
        }

        [ClientRpc]
        private void RpcSyncUI(int connectionId, byte[] data)
        {
            var command = MemoryPackSerializer.Deserialize<UISyncCommand>(data);
            UpdateData(connectionId, command);
        }

        [Client]
        public void UpdateData(int connectionId, UISyncCommand command)
        {
            var uiBroker = UIDataBroker[connectionId];
            uiBroker?.UpdateData(command);
        }

        [ClientRpc]
        private void RpcBatchSyncUI(byte[][] batch)
        {
            foreach (var data in batch)
            {
                var command = MemoryPackSerializer.Deserialize<UISyncCommand>(data);
                UpdateData(command.Header.CommandHeader.ConnectionId, command);
            }
        }

        public IObservable<T> RegisterUIEvent<T>(int connectionId, UISyncDataType key) where T : IUIData
        {
            return UIDataBroker[connectionId]?.GetObservable<T>(key);
        }

        public void SetLocalData(UISyncDataHeader header, byte[] data, UISyncDataType type)
        {
            if (isLocalPlayer)
                UIDataBroker[header.CommandHeader.ConnectionId]?.SetLocalData(header, data, type);
        }

        public static UISyncCommand CreateUISyncCommand(UISyncDataHeader header, IUISyncCommandData commandData,
            UISyncDataType type)
        {
            var uiData = ObjectPool<UISyncCommand>.Get();
            var data = MemoryPackSerializer.Serialize(commandData);
            uiData.Header = header;
            uiData.CommandData = data;
            uiData.SyncDataType = type;
            return uiData;
        }

        public static byte[] Serialize<T>(T data) where T : IUIData
        {
            return MemoryPackSerializer.Serialize<IUIData>(data);
        }
        
        public static T Deserialize<T>(byte[] bytes) where T : IUIData
        {
            return MemoryPackSerializer.Deserialize<T>(bytes);
        }

        private void OnDestroy()
        {
            _cts?.Cancel(); // 统一取消任务
            _cts?.Dispose();
        }

        private void RegisterNetworkReaderWriter()
        {
            Reader<UISyncCommand>.read = Read;
            Writer<UISyncCommand>.write = Write;
            Reader<UISyncDataHeader>.read = ReadHeader;
            Writer<UISyncDataHeader>.write = WriteHeader;
            Reader<NetworkCommandHeader>.read = ReadNetworkCommandHeader;
            Writer<NetworkCommandHeader>.write = WriteNetworkCommandHeader;
        }

        private void WriteNetworkCommandHeader(NetworkWriter writer, NetworkCommandHeader networkCommandHeader)
        {
            writer.Write(networkCommandHeader.ConnectionId);
            writer.Write(networkCommandHeader.CommandType);
            writer.Write(networkCommandHeader.CommandId);
            writer.Write(networkCommandHeader.Authority);
            writer.Write(networkCommandHeader.Tick);
            writer.Write(networkCommandHeader.Timestamp);
        }

        private NetworkCommandHeader ReadNetworkCommandHeader(NetworkReader reader)
        {
            return new NetworkCommandHeader
            {
                ConnectionId = reader.ReadInt(),
                CommandType = (CommandType)reader.ReadInt(),
                CommandId = reader.ReadUInt(),
                Authority = (CommandAuthority)reader.ReadByte(),
                Tick = reader.ReadInt(),
                Timestamp = reader.ReadLong()
            };
        }

        private void WriteHeader(NetworkWriter writer, UISyncDataHeader header)
        {
            writer.Write(header.CommandHeader);
            writer.Write((byte)header.SyncMode);
        }

        private UISyncDataHeader ReadHeader(NetworkReader reader)
        {
            return new UISyncDataHeader
            {
                CommandHeader = reader.Read<NetworkCommandHeader>(),
                SyncMode = (SyncMode)reader.ReadByte()
            };
        }

        private void Write(NetworkWriter writer, UISyncCommand command)
        {
            writer.Write(command.Header);
            writer.Write(command.CommandData);
            writer.Write((int)command.SyncDataType);
        }

        private UISyncCommand Read(NetworkReader reader)
        {
            return new UISyncCommand
            {
                Header = reader.Read<UISyncDataHeader>(),
                CommandData = reader.ReadBytesAndSize(),    
                SyncDataType = (UISyncDataType)reader.ReadInt()
            };
        }
    }
}