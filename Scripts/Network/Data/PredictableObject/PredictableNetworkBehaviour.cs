using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Data.PredictableObject
{
    public abstract class PredictableNetworkBehaviour : NetworkBehaviour
    {
        private readonly List<IPredictableSyncObject> _syncObjects = new List<IPredictableSyncObject>();
        private readonly Dictionary<string, IPredictableSyncObject> _syncObjectMap = new Dictionary<string, IPredictableSyncObject>();
        private float _lastSyncTime;
        private const float SYNC_INTERVAL = 0.1f; // 同步间隔

        // 用于标记需要同步的字段
        [AttributeUsage(AttributeTargets.Field)]
        public class PredictableSyncVarAttribute : System.Attribute { }

        protected virtual void Awake()
        {
            // 自动收集所有标记了 PredictableSyncVar 的字段
            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<PredictableSyncVarAttribute>() != null)
                {
                    if (field.GetValue(this) is IPredictableSyncObject syncObject)
                    {
                        _syncObjects.Add(syncObject);
                        _syncObjectMap[field.Name] = syncObject;
                    }
                }
            }
        }

        protected virtual void Update()
        {
            if (isServer && Time.time - _lastSyncTime >= SYNC_INTERVAL)
            {
                _lastSyncTime = Time.time;
                SyncToClients();
            }
        }

        public override void OnStartServer()
        {
            // 服务器启动时初始化
            foreach (var syncObject in _syncObjects)
            {
                syncObject.ResetSyncObjects();
            }
        }

        public override void OnStartClient()
        {
            // 客户端启动时请求完整状态
            if (!isServer)
            {
                CmdRequestFullState();
            }
        }

        private void SyncToClients()
        {
            var dirtyObjects = _syncObjects.Where(obj => obj.IsDirty).ToList();
            if (dirtyObjects.Count > 0)
            {
                var writer = new NetworkWriter();
                writer.WriteInt(dirtyObjects.Count);

                foreach (var obj in dirtyObjects)
                {
                    var fieldName = _syncObjectMap.First(x => x.Value == obj).Key;
                    writer.WriteString(fieldName);
                    obj.OnSerializeDelta(writer);
                }

                RpcSyncState(writer.ToArray());
            }
        }

        [Command]
        private void CmdRequestFullState()
        {
            var writer = new NetworkWriter();
            writer.WriteInt(_syncObjects.Count);

            foreach (var kvp in _syncObjectMap)
            {
                writer.WriteString(kvp.Key);
                kvp.Value.OnSerializeAll(writer);
            }

            TargetFullState(connectionToClient, writer.ToArray());
        }

        [TargetRpc]
        private void TargetFullState(NetworkConnection target, byte[] data)
        {
            var reader = new NetworkReader(data);
            int count = reader.ReadInt();

            for (int i = 0; i < count; i++)
            {
                string fieldName = reader.ReadString();
                if (_syncObjectMap.TryGetValue(fieldName, out var syncObject))
                {
                    syncObject.OnDeserializeAll(reader);
                }
            }
        }

        [ClientRpc]
        private void RpcSyncState(byte[] data)
        {
            if (isLocalPlayer) return; // 本地玩家使用预测值

            if (data != null)
            {
                var reader = new NetworkReader(data);
                int count = reader.ReadInt();

                for (int i = 0; i < count; i++)
                {
                    string fieldName = reader.ReadString();
                    if (_syncObjectMap.TryGetValue(fieldName, out var syncObject))
                    {
                        syncObject.OnDeserializeDelta(reader);
                    }
                }
            }
        }
    }
}