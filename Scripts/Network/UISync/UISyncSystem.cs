using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Tool.GameEvent;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.UISync
{
    public class UISyncSystem : NetworkBehaviour
    {
        // 双通道队列
        private ConcurrentQueue<UISyncData> _immediateQueue = new ConcurrentQueue<UISyncData>();
        private ConcurrentQueue<UISyncData> _timedQueue = new ConcurrentQueue<UISyncData>();
        // UniTask取消令牌
        private CancellationTokenSource _cts;
        public Dictionary<int, UIDataBroker> UIDataBroker { get; } = new Dictionary<int, UIDataBroker>();

        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            // 统一启动双通道处理
            if (isServer)
            {
                _cts = new CancellationTokenSource();
                ProcessImmediateChannel(_cts.Token).Forget();
                ProcessTimedChannel(_cts.Token).Forget();
                gameEventManager.Subscribe<PlayerConnectEvent>(OnPlayerConnect);
                gameEventManager.Subscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
            }
        }

        private void OnPlayerDisconnect(PlayerDisconnectEvent disconnectEvent)
        {
            UIDataBroker.Remove(disconnectEvent.ConnectionId);
        }

        private void OnPlayerConnect(PlayerConnectEvent connectEvent)
        {
            UIDataBroker.Add(connectEvent.ConnectionId, new UIDataBroker(connectEvent.ConnectionId));
        }

        // 客户端发起UI更新请求
        [Command]
        public void CmdUpdateUI(UISyncData data)
        {
            var header = data.Header;

            switch (header.SyncMode)
            {
                case SyncMode.Immediate:
                    _immediateQueue.Enqueue(data);
                    break;
                case SyncMode.Timed:
                    _timedQueue.Enqueue(data);
                    break;
                case SyncMode.Hybrid:
                    // if (Mathf.Abs(data.value - GetCurrentValue(data.key)) > profile.DeltaThreshold)
                    //     _immediateQueue.Enqueue(data);
                    // else
                    //     _timedQueue.Enqueue(data);
                    break;
            }
        }

        async UniTaskVoid ProcessImmediateChannel(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => !_immediateQueue.IsEmpty, 
                    cancellationToken: ct);

                while (_immediateQueue.TryDequeue(out var data))
                {
                    RpcSyncUI(data.Header.ConnectionId, data);
                }
            }
        }

        // 定时通道处理（UniTask版本）
        async UniTaskVoid ProcessTimedChannel(CancellationToken ct)
        {
            const float interval = 1f;
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay((int)(interval * 1000), 
                    delayTiming: PlayerLoopTiming.Update, 
                    cancellationToken: ct);

                if (_timedQueue.IsEmpty) continue;

                var batch = new List<UISyncData>();
                while (_timedQueue.TryDequeue(out var data))
                {
                    batch.Add(data);
                }

                RpcBatchSyncUI(batch);
            }
        }

        [ClientRpc]
        private void RpcSyncUI(int connectionId, UISyncData data)
        {
            UpdateData(connectionId, data);
        }

        public void UpdateData(int connectionId, UISyncData data)
        {
            var uiBroker = UIDataBroker[connectionId];
            uiBroker?.UpdateData(data);
        }

        [ClientRpc]
        private void RpcBatchSyncUI(IEnumerable<UISyncData> batch)
        {
            foreach (var data in batch)
            {
                UpdateData(data.Header.ConnectionId, data);
            }
        }

        public IObservable<T> RegisterUIEvent<T>(int connectionId, UISyncDataType key) where T : IUIData
        {
            return UIDataBroker[connectionId]?.GetObservable<T>(key);
        }

        public void SetLocalData<T>(int connectionId, UISyncDataType key, T value) where T : IUIData
        {
            if (isLocalPlayer)
                UIDataBroker[connectionId]?.SetLocalData(key, value);
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
    }
}