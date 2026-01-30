using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server
{
    public class NetworkEndHandler : NetworkBehaviour
    {
        [SyncVar]
        private int _cleanupConfirmedClients;
    
        [SyncVar]
        private int _cleanupCompletedClients;
    
        [Header("超时设置")]
        [SerializeField] private float clientConfirmationTimeout = 7f;
        [SerializeField] private float clientCleanupTimeout = 10f;
    
        private List<int> _confirmedClientIds = new List<int>();
        private List<int> _completedClientIds = new List<int>();
        public event Action OnCleanup;
        public event Action OnDisconnected;
        [SyncVar]
        private int _totalClients = 0;
        private PlayerComponentController _playerComponentController;
        private GameSyncManager _gameSyncManager;
        private PlayerInGameManager _playerInGameManager;
        
        [Inject]
        private void Init(PlayerInGameManager playerInGameManager)
        {
            _playerInGameManager = playerInGameManager;
            _gameSyncManager = FindObjectOfType<GameSyncManager>();
        }
    
        // 服务器开始结束流程
        [Server]
        public void BeginGameEndProcedure()
        {
            var isHost = NetworkServer.active && NetworkClient.isConnected;
            _totalClients = isHost ? NetworkServer.connections.Count : NetworkServer.connections.Count - 1;
            _cleanupConfirmedClients = 0;
            _cleanupCompletedClients = 0;
            _confirmedClientIds.Clear();
            _completedClientIds.Clear();
            // 启动超时监控
            //CleanupTimeoutMonitor().Forget();
        }

        public void CmdConfirmCleanup()
        {
            _playerComponentController ??= _gameSyncManager.GetLocalPlayerConnection();
            _playerComponentController.CmdEndGame(_playerInGameManager.LocalPlayerId);
        }

        public void ConfirmCleanup(int connectionId)
        {
            if (_confirmedClientIds.Contains(connectionId))
                return;
            
            _confirmedClientIds.Add(connectionId);
            _cleanupConfirmedClients++;
        
            Debug.Log($"服务器：客户端 {connectionId} 已确认，当前：{_cleanupConfirmedClients}/{_totalClients}");
        
            // 所有客户端确认后，开始第二阶段
            if (_cleanupConfirmedClients >= _totalClients)
            {
                StartClientCleanupPhase();
            }
        }
    
        // 阶段2：客户端开始清理
        [Server]
        private void StartClientCleanupPhase()
        {
            CleanupClientAsync().Forget();
        }

        private async UniTask CleanupClientAsync()
        {
            Debug.Log("服务器：所有客户端已确认，开始清理阶段");
        
            // 给客户端一点准备时间
            await UniTask.Delay(1000);
        
            // 通知客户端开始清理
            RpcBeginClientCleanup();
        
            // 启动清理超时
            //StartCoroutine(WaitForClientCleanup());
        }

        [ClientRpc]
        private void RpcBeginClientCleanup()
        {
            Debug.Log("客户端：开始清理流程");
        
            // 在断开连接前完成清理
            CleanupBeforeDisconnect().Forget();
        }
    
        private async UniTask CleanupBeforeDisconnect()
        {
            // 如果是Host，不要断开客户端部分
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                Debug.Log("Host：只清理客户端部分，不断开");
                _playerComponentController.CmdCleanupClient(_playerInGameManager.LocalPlayerId);
                // Host只需清理，不断开网络
                return;
            }
            OnCleanup?.Invoke();
            _playerComponentController.CmdCleanupClient(_playerInGameManager.LocalPlayerId);
        
            await UniTask.Yield();
            
            DisconnectClient();
            OnDisconnected?.Invoke();
        }
    
        private void DisconnectClient()
        {
            // 如果是Host，不要断开客户端部分
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                Debug.Log("Host：只清理客户端部分，不断开");
                // Host只需清理，不断开网络
                return;
            }
        
            // 普通客户端断开连接
            NetworkManager.singleton.StopClient();
            Debug.Log("客户端：已断开连接");
        }
    
        public void CmdReportCleanupCompleted(int connectionId)
        {
            if (_completedClientIds.Contains(connectionId))
                return;
            
            _completedClientIds.Add(connectionId);
            _cleanupCompletedClients++;
        
            Debug.Log($"服务器：客户端 {connectionId} 清理完成，当前：{_cleanupCompletedClients}/{_totalClients}");
        
            // 检查是否所有客户端都已完成
            CheckAllClientsCleaned();
        }
    
        [Server]
        private void CheckAllClientsCleaned()
        {
            if (_cleanupCompletedClients >= _totalClients)
            {
                Debug.Log("服务器：所有客户端清理完成，开始服务器清理");
                CleanupServer().Forget();
            }
        }
    
        // 阶段3：服务器清理
        [Server]
        private async UniTask CleanupServer()
        {
            // 1. 保存服务器数据
            // yield return SaveServerData();
            //
            // // 2. 清理服务器对象
            // CleanupServerObjects();
        
            OnCleanup?.Invoke();
            await UniTask.Yield();
            if (NetworkServer.active)
            {
                if (NetworkClient.isConnected)
                {
                    NetworkManager.singleton.StopHost();
                }
                else
                {
                    NetworkManager.singleton.StopServer();
                }
            }
            await UniTask.Yield();
            OnDisconnected?.Invoke();
            // 3. 停止服务器
        
            Debug.Log("服务器：清理完成，已停止");
        }
    
        // 超时监控
        private async UniTask CleanupTimeoutMonitor()
        {
            float startTime = Time.time;
            bool phase1Complete = false;
            bool phase2Complete = false;
        
            // 阶段1超时
            while (!phase1Complete && Time.time - startTime < clientConfirmationTimeout)
            {
                if (_cleanupConfirmedClients >= _totalClients)
                {
                    phase1Complete = true;
                    Debug.Log("阶段1：所有客户端确认（在规定时间内）");
                }
                await UniTask.Yield();
            }
        
            if (!phase1Complete)
            {
                Debug.LogWarning($"阶段1超时：只有 {_cleanupConfirmedClients}/{_totalClients} 客户端确认");
                // 强制进入阶段2
                StartClientCleanupPhase();
            }
        
            // 阶段2超时
            startTime = Time.time;
            while (!phase2Complete && Time.time - startTime < clientCleanupTimeout)
            {
                if (_cleanupCompletedClients >= _totalClients)
                {
                    phase2Complete = true;
                    Debug.Log("阶段2：所有客户端清理完成（在规定时间内）");
                }
                await UniTask.Yield();
            }
        
            if (!phase2Complete)
            {
                Debug.LogWarning($"阶段2超时：只有 {_cleanupCompletedClients}/{_totalClients} 客户端完成清理");
                // 强制清理服务器
                ForceCleanupServer();
            }
        }
    
        [Server]
        private void ForceCleanupServer()
        {
            Debug.Log("服务器：强制清理（超时或客户端异常）");
        
            // 1. 踢掉所有未响应的客户端
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != null && !_completedClientIds.Contains(conn.connectionId))
                {
                    Debug.Log($"强制断开客户端：{conn.connectionId}");
                    conn.Disconnect();
                }
            }
        
            // 2. 清理服务器
            CleanupServer().Forget();
        }
    }
}