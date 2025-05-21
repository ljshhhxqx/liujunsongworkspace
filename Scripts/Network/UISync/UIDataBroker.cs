using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using UniRx;

namespace HotUpdate.Scripts.Network.UISync
{
    public class UIDataBroker
    {
        public int ConnectionId { get; private set; }
        
        public UIDataBroker(int connectionId)
        {
            ConnectionId = connectionId;
        }
        // 数据存储
        private readonly Dictionary<UISyncDataType, byte[]> _serverData = new Dictionary<UISyncDataType, byte[]>();
        private readonly Dictionary<UISyncDataType, IUIData> _localData = new Dictionary<UISyncDataType, IUIData>();
        // 在UIDataBroker中记录待验证操作
        private readonly Dictionary<uint, UISyncCommand> _pendingOperations = new Dictionary<uint, UISyncCommand>();

        public void SetLocalData(UISyncDataHeader header, byte[] data, UISyncDataType type)
        {
            var uiSyncCommand = ObjectPool<UISyncCommand>.Get();
            uiSyncCommand.Header = header;
            uiSyncCommand.CommandData = data;
            uiSyncCommand.SyncDataType = type;
            var uiData = MemoryPackSerializer.Deserialize<IUIData>(data);
            _localData[type] = uiData;
            _pendingOperations[header.CommandHeader.CommandId] = uiSyncCommand;
            PublishData(type, uiData);
        }
        
        // 服务器拒绝时回滚
        public void Rollback(UISyncCommand syncCommand) 
        {
            if (_pendingOperations.TryGetValue(syncCommand.Header.CommandHeader.CommandId, out var op)) 
            {
                // var data = MemoryPackSerializer.Deserialize<IUIData>(op.ReadOnlyData);
                // _localData[syncCommand.SyncDataType] = data;
                // PublishData<IUIData>(syncCommand.SyncDataType, syncCommand.CommandData);
            }
        }


        // 服务器验证通过后清除对应操作
        public void ConfirmOperation(uint operationId) 
        {
            _pendingOperations.Remove(operationId);
        }

        // 响应式事件中心
        private readonly Dictionary<UISyncDataType, ISubject<IUIData>> _streams = new Dictionary<UISyncDataType, ISubject<IUIData>>();

        // 注册可观测流（自动取消订阅）
        public IObservable<T> GetObservable<T>(UISyncDataType key) where T : IUIData
        {
            if (!_streams.TryGetValue(key, out var subject))
            {
                // 创建带缓存的Subject
                subject = new BehaviorSubject<IUIData>(GetCurrentData<T>(key));
                _streams[key] = subject;
            }
            return (IObservable<T>)subject.AsObservable();
        }

        // 更新数据入口
        public void UpdateData(UISyncCommand newCommand)
        {
            _serverData[newCommand.SyncDataType] = newCommand.CommandData;
            PublishData<IUIData>(newCommand.SyncDataType, newCommand.CommandData);
        }

        // 获取当前数据（本地优先）
        public T GetCurrentData<T>(UISyncDataType key, bool isServer = false) where T : IUIData
        {
            if (isServer)
            {
                return _serverData.TryGetValue(key, out var data) ? MemoryPackSerializer.Deserialize<T>(data) : CreateUIDefaultData<T>();
            }
            return _localData.TryGetValue(key, out var value) ? (T)value : CreateUIDefaultData<T>();
        }

        private void PublishData<T>(UISyncDataType dataType, byte[] data) where T : IUIData
        {
            var uiData = MemoryPackSerializer.Deserialize<T>(data);
            if (!_streams.TryGetValue(dataType, out var subject))
            {
                subject = new BehaviorSubject<IUIData>(uiData);
            }
            subject.OnNext(uiData);
        }
        
        private void PublishData(UISyncDataType dataType, IUIData data)
        {
            if (!_streams.TryGetValue(dataType, out var subject))
            {
                subject = new BehaviorSubject<IUIData>(data);
            }
            subject.OnNext(data);
        }

        public static T CreateUIDefaultData<T>() where T : IUIData
        {
            var type = typeof(IUIData);
            T data;
            if (type == typeof(InventoryData))
            {
                var propertyData = new InventoryData(new List<SlotData>());
                data = (T)(IUIData)propertyData;
            }
            // TODO: 其他UI数据类型
            else
            {
                return default;
            }
            return data;
        }
    }
}