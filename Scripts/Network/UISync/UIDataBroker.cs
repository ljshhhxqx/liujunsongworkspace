using System;
using System.Collections.Generic;
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
        public void UpdateData(UISyncData newData)
        {
            _serverData[newData.SyncDataType] = newData.PayloadData;
            PublishData<IUIData>(newData.SyncDataType, newData.PayloadData);
        }

        // 客户端预测数据
        public void SetLocalData<T>(UISyncDataType key, T value) where T : IUIData
        {
            _localData[key] = value;
            PublishData<T>(key, value);
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
            if (_streams.TryGetValue(dataType, out var subject))
            {
                var uiData = MemoryPackSerializer.Deserialize<T>(data);
                subject.OnNext(uiData);
            }
        }
        
        private void PublishData<T>(UISyncDataType dataType, IUIData data) where T : IUIData
        {
            if (_streams.TryGetValue(dataType, out var subject))
            {
                subject.OnNext(data);
            }
        }

        public static T CreateUIDefaultData<T>() where T : IUIData
        {
            var type = typeof(IUIData);
            T data;
            if (type == typeof(PropertyData))
            {
                var propertyData = new PropertyData();
                data = (T)(IUIData)propertyData;
            }
            // TODO: 其他UI数据类型
            else
            {
                return default;
            }
            return data;
        }

        public static UISyncDataHeader CreateHeader<T>(UISyncDataType dataType, T value)
        {
            return default;
        }
    }
}