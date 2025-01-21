using System;
using Mirror;

namespace HotUpdate.Scripts.Network.Data.PredictableObject
{
    public abstract class PredictableSyncObject : IPredictableSyncObject
    {
        public bool IsDirty { get; protected set; }
        public bool AllowClientPredict { get; set; } = true;

        public abstract void OnSerializeAll(NetworkWriter writer);
        public abstract void OnDeserializeAll(NetworkReader reader);
        public abstract void OnSerializeDelta(NetworkWriter writer);
        public abstract void OnDeserializeDelta(NetworkReader reader);
        public abstract void ResetSyncObjects();
    }
    
    public interface IPredictableSyncObject
    {
        public bool IsDirty { get; }
        public bool AllowClientPredict { get; set; }
        void OnSerializeAll(NetworkWriter writer);
        void OnDeserializeAll(NetworkReader reader);
        void OnSerializeDelta(NetworkWriter writer);
        void OnDeserializeDelta(NetworkReader reader);
        void ResetSyncObjects();
    }
    
    // 同步对象的事件接口
    public interface IPredictableSyncEvents<TKey, TValue>
    {
        event Action<TKey, TValue> OnAdd;
        event Action<TKey, TValue> OnRemove;
        event Action<TKey, TValue, TValue> OnValueChanged;
        event Action OnClear;
    }

    // 列表的事件接口
    public interface IPredictableListEvents<T>
    {
        event Action<int, T> OnInsert;
        event Action<int, T> OnRemoveAt;
        event Action<int, T, T> OnSet;
        event Action OnClear;
    }

    // 单值的事件接口
    public interface IPredictableValueEvents<T>
    {
        event Action<T, T> OnValueChanged; // oldValue, newValue
        event Action<T, T> OnServerValueChanged;
    }
}