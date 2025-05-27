using System;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.Tool.GameEvent;
using Network.Data;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace Game
{
    public class GameOnlineDefine : MonoBehaviour
    {
        private const float HEARTBEAT_INTERVAL = 30f; // 30秒发送一次心跳
        private const string HEARTBEAT_FUNCTION = "UpdatePlayerHeartbeat";
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const float RETRY_DELAY = 5f;
        private GameEventManager _gameEventManager;
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<PlayerLoginEvent>(OnPlayerLogin);
            _gameEventManager.Subscribe<PlayerLogoutEvent>(OnPlayerLogout);
        }

        private void OnPlayerLogin(PlayerLoginEvent obj)
        {
            RepeatedTask.Instance.StartUniTaskVoidTask(SendHeartbeat,HEARTBEAT_INTERVAL);
        }

        private void OnPlayerLogout(PlayerLogoutEvent obj)
        {
            RepeatedTask.Instance.StopUniTaskVoidTask(SendHeartbeat);
        }

        private async UniTaskVoid SendHeartbeat()
        {
            int attempts = 0;
            bool success = false;

            while (!success && attempts < MAX_RETRY_ATTEMPTS)
            {
                attempts++;
                success = await TrySendHeartbeat();

                if (!success)
                {
                    Debug.LogWarning($"Heartbeat attempt {attempts} failed. Retrying in {RETRY_DELAY} seconds.");
                    await UniTask.Delay(TimeSpan.FromSeconds(RETRY_DELAY));
                }
            }

            if (!success)
            {
                Debug.LogError("Failed to send heartbeat after maximum retry attempts.");
            }
        }

        private async UniTask<bool> TrySendHeartbeat()
        {
            bool completed = false;
            bool success = false;

            PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
                {
                    FunctionName = HEARTBEAT_FUNCTION,
                    GeneratePlayStreamEvent = true,
                    FunctionParameter = new { PlayFabId = PlayFabData.PlayFabId.Value },
                }, 
                result => 
                {
                    if (result.Error != null)
                    {
                        throw new Exception(result.Error.Error);
                    }
                    success = true;
                    completed = true;
                    PlayFabData.Initialize();
                    Debug.Log("Heartbeat sent successfully");
                }, 
                error => 
                {
                    success = false;
                    completed = true;
                    Debug.LogError($"Error sending heartbeat: {error.ErrorMessage}");
                });

            while (!completed)
            {
                await UniTask.Yield();
            }
            
            return success;
        }

        private void OnApplicationQuit()
        {
            SendLogoutRequest();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SendLogoutRequest();
            }
        }

        private void SendLogoutRequest()
        {
            if (!PlayFabData.IsLoggedIn.Value)
            {
                return;
            }
            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "PlayerLogout",
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
                FunctionParameter = new { PlayFabId = PlayFabData.PlayFabId.Value },
            }, r =>
            {
                if (r.Error != null)
                {
                    throw new Exception($"{r.Error.Error}-${r.Error.Message}-${r.Error.StackTrace}");
                }
                Debug.Log("Logout request sent successfully");
                _gameEventManager.Publish(new PlayerLogoutEvent(PlayFabData.PlayFabId.Value));
                PlayFabData.Dispose();
            }, e =>
            {
                Debug.LogError($"Error sending logout request: {e.ErrorMessage}");
            });
        }
    }
}
