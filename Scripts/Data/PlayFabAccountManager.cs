using System;
using System.Collections.Generic;
using AOTScripts.Tool;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel;
using Network.Data;
using Network.Server.PlayFab;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using Tool.GameEvent;
using UI.UIs;
using UnityEngine;
using VContainer;
using EntityKey = PlayFab.CloudScriptModels.EntityKey;
using ExecuteCloudScriptResult = PlayFab.CloudScriptModels.ExecuteCloudScriptResult;

namespace Data
{
    public class PlayFabAccountManager
    {
        private readonly UIManager _uiManager;
        private const string AccountKey = "LatestAccount";
        private const string PlayerKey = "PlayerId";
        private RegisterData _latestAccount;
        private JsonDataConfig _jsonDataConfig;
        private readonly IConfigProvider _configProvider;
        private readonly IPlayFabClientCloudScriptCaller _playFabClientCloudScriptCaller;
        private readonly GameEventManager _gameEventManager;
        
        public string PlayerId => PlayerPrefs.GetString(PlayerKey);

        [Inject]
        private PlayFabAccountManager(UIManager uiManager, IConfigProvider configProvider, GameEventManager gameEventManager, IPlayFabClientCloudScriptCaller playFabClientCloudScriptCaller)
        {
            _uiManager = uiManager;
            _configProvider = configProvider;
            _gameEventManager = gameEventManager;
            _playFabClientCloudScriptCaller = playFabClientCloudScriptCaller;
            gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
        }

        private void OnGameResourceLoaded(GameResourceLoadedEvent gameResourceLoadedEvent)
        {
            _jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            PlayFabData.IsDevelopMode.Value = PlayerPrefs.GetInt(_jsonDataConfig.GameConfig.developKey) == 1;
        }

        public void Register(RegisterData data)
        {
            _latestAccount = new RegisterData
            {
                AccountName = data.AccountName,
                Password = data.Password,
                Email = data.Email
            };
            var request = new RegisterPlayFabUserRequest
            {
                Username = data.AccountName,
                Email = data.Email,
                Password = data.Password,
                RequireBothUsernameAndEmail = true
            };
            _uiManager.SwitchLoadingPanel(true);
            PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnRegisterFailure);
            Debug.Log("Register");
        }
        
        private void OnRegisterSuccess(RegisterPlayFabUserResult result)
        {
            _uiManager.SwitchLoadingPanel(false);
            var json = JsonUtility.ToJson(_latestAccount);
            PlayerPrefs.SetString(AccountKey, json);
            PlayerPrefs.Save();
            _uiManager.ShowTips("注册成功！", () =>
            {
                var loginUI = _uiManager.SwitchUI<LoginScreenUI>();
                if (loginUI)
                {
                    loginUI.WriteAccount(_latestAccount);
                }
            });
        }
        
        private void OnRegisterFailure(PlayFabError error)
        {
            _uiManager.SwitchLoadingPanel(false);
            _latestAccount = null;
            _uiManager.ShowTips($"注册失败！错误信息:{error.Error}-{error.ErrorMessage}");
        }
        
        public void Login(AccountData data)
        {
            PlayFabData.IsLoggedIn.Value = false;
            var isDevelop = PlayerPrefs.GetInt(_jsonDataConfig.GameConfig.developKey) == 1;
            _uiManager.SwitchLoadingPanel(true);
            if (!isDevelop)
            {
                _latestAccount = new RegisterData
                {
                    AccountName = data.AccountName,
                    Password = data.Password
                };
                var request = new LoginWithPlayFabRequest
                {
                    Username = data.AccountName,
                    Password = data.Password,
                };
                PlayFabClientAPI.LoginWithPlayFab(request, s =>
                {
                    _uiManager.SwitchLoadingPanel(false);
                    OnLoginSuccess(s);
                }, f => 
                { 
                    _uiManager.SwitchLoadingPanel(false);
                    OnFailure(f);
                });
            }
            else
            {
                _latestAccount = new RegisterData
                {
                    AccountName = data.AccountName,
                };
                var request = new LoginWithCustomIDRequest
                {
                    CustomId = data.AccountName,
                    CreateAccount = true    
                };
                PlayFabClientAPI.LoginWithCustomID(request, s =>
                {
                    _uiManager.SwitchLoadingPanel(false);
                    OnLoginSuccess(s);
                }, f => 
                { 
                    _uiManager.SwitchLoadingPanel(false);
                    OnFailure(f);
                });
            }
            Debug.Log("Login");
        }

        private void OnFailure(PlayFabError error)
        {
            _latestAccount = null;
            _uiManager.ShowTips($"登录失败！错误信息:{error.Error}-{error.ErrorMessage}");
            Debug.Log("Login Failure");
        }

        private void OnLoginSuccess(LoginResult result, bool isDevelop = false)
        {
            var json = JsonUtility.ToJson(_latestAccount);
            PlayerPrefs.SetString(AccountKey, json);
            PlayerPrefs.SetString(PlayerKey, result.PlayFabId);
            PlayerPrefs.Save();
            PlayFabData.Initialize();
            PlayFabData.PlayFabId.Value = result.PlayFabId;
            PlayFabData.IsLoggedIn.Value = true;
            PlayFabData.EntityKey.Value = new EntityKey { Id = PlayFabSettings.staticPlayer.EntityId, Type = "title_player_account" };
            _uiManager.ShowTips("登录成功！");
            _uiManager.SwitchUI<MainScreenUI>();
            _gameEventManager.Publish(new PlayerLoginEvent(PlayFabData.PlayFabId.Value));
            _playFabClientCloudScriptCaller.ExecuteCloudScript(new ExecuteEntityCloudScriptRequest
            {
                FunctionName = isDevelop ? "LoginWithCustomIDRegister" : "LoginWithPlayFabRegister",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { playerId = result.PlayFabId },
                Entity = PlayFabData.EntityKey.Value,
            }, OnPlayerDataSuccess, OnFailure);

            Debug.Log("Login Success");
        }

        private void OnPlayerDataSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.ParseCloudScriptResultToDic();
            foreach (var key in data.Keys)
            {
                if (key == "internalData")
                {
                    PlayFabData.PlayerInternalData.Value = JsonUtility.FromJson<PlayerInternalData>(data[key].ToString());
                    Debug.Log("Player Internal Data Success" + PlayFabData.PlayerInternalData.Value.ToString());
                }
                else if (key == "readOnlyData")
                {
                    PlayFabData.PlayerReadOnlyData.Value = JsonUtility.FromJson<PlayerReadOnlyData>(data[key].ToString());
                    Debug.Log("Player ReadOnly Data Success" + PlayFabData.PlayerReadOnlyData.Value.ToString());
                }
            }
            Debug.Log("Player Data Success");
        }

        public void Logout(Action logoutCallback = null)
        {
            if (!PlayFabData.IsLoggedIn.Value) return; 
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "PlayerLogout",
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
                FunctionParameter = new { PlayFabId = PlayFabData.PlayFabId.Value }
            };

            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, r =>
            {
                OnLogoutSuccess(r);
                logoutCallback?.Invoke();
            }, OnFailure);
        }

        private void OnLogoutSuccess(ExecuteCloudScriptResult result)
        {
            if (result.Error != null)
            {
                throw new Exception($"{result.Error.Error}-${result.Error.Message}-${result.Error.StackTrace}");
            }
            PlayFabData.IsLoggedIn.Value = false;
            _gameEventManager.Publish(new PlayerLogoutEvent(PlayFabData.PlayFabId.Value));
            Debug.Log("Logout Success");
        }

        public void CheckDevelopers(string code)
        {
            int developValue;
#if UNITY_EDITOR
            developValue = 1;
            PlayerPrefs.SetInt(_jsonDataConfig.GameConfig.developKey, developValue);
            _uiManager.ShowTips($"密钥设置成功，当前模式为：开发模式");
            return;
#endif
            if (string.IsNullOrWhiteSpace(code))
            {
                _uiManager.ShowTips("请输入密钥");
                return;
            }

            developValue = code.Equals(_jsonDataConfig.GameConfig.developKeyValue) ? 1 : 0;
            PlayerPrefs.SetInt(_jsonDataConfig.GameConfig.developKey, developValue);
            var mode = developValue == 1 ? "开发模式" : "正式模式";
            _uiManager.ShowTips($"密钥设置成功，当前模式为：{mode}");
        }
        
        public RegisterData GetLatestAccount()
        {
            var json = PlayerPrefs.GetString(AccountKey);
            var account = new RegisterData();
            if (!string.IsNullOrWhiteSpace(json))
            {
                account = JsonUtility.FromJson<RegisterData>(json);
            }
            return account;
        }


        public void ModifyNickName(string nickName)
        {
            if (!PlayFabData.IsLoggedIn.Value) return;
            _playFabClientCloudScriptCaller.ExecuteCloudScript(new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "UpdateNickName",
                FunctionParameter = new { PlayFabId = PlayFabData.PlayFabId.Value, NewNickName = nickName },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            }, OnModifyNickNameSuccess, OnFailure);
        }

        private void OnModifyNickNameSuccess(ExecuteCloudScriptResult result)
        {
            if (result.Error != null)
            {
                throw new Exception($"{result.Error.Error}-${result.Error.Message}-${result.Error.StackTrace}");
            }
            var data = result.ParseCloudScriptResultToDic();
            foreach (var value in data)
            {
                if (value.Key == "readOnlyData")
                {
                    PlayFabData.PlayerReadOnlyData.Value = JsonUtility.FromJson<PlayerReadOnlyData>(value.Value.ToString()); //.ParseCloudScriptResultToData<PlayerReadOnlyData>();
                }
            }
            Debug.Log("Modify NickName Success");
        }
        private List<FriendData> _friendDatas = new List<FriendData>();
    
        // 更改好友状态
        public void ChangeFriendStatus(string friendId, FriendStatus status, Action<bool, string> callback = null)
        {
            ExecuteCloudScriptRequest request = new ExecuteCloudScriptRequest
            {
                FunctionName = "ChangeFriendStatus",
                FunctionParameter = new { 
                    friendId = friendId,
                    status = status.ToString()
                }
            };
            
            PlayFabClientAPI.ExecuteCloudScript(request, result =>
            {
                Debug.Log("好友状态更改成功: " + result.FunctionResult.ToString());
                RefreshFriendList(); // 刷新好友列表

                callback?.Invoke(true, result.FunctionResult.ToString());
            }, error =>
            {
                Debug.LogError("更改好友状态失败: " + error.ErrorMessage);

                callback?.Invoke(false, error.ErrorMessage);
            });
        }
        
        // 同意好友请求
        public void AcceptFriendRequest(string friendId)
        {
            ChangeFriendStatus(friendId, FriendStatus.Friends, (success, message) =>
            {
                if (success)
                {
                    // 更新UI或显示通知
                    Debug.Log("已接受好友请求");
                }
            });
        }
        
        // 拒绝好友请求
        public void RejectFriendRequest(string friendId)
        {
            ChangeFriendStatus(friendId, FriendStatus.Blocked, (success, message) =>
            {
                if (success)
                {
                    // 更新UI或显示通知
                    Debug.Log("已拒绝好友请求");
                }
            });
        }
        
        // 删除好友
        public void RemoveFriend(string friendId)
        {
            ChangeFriendStatus(friendId, FriendStatus.Blocked, (success, message) =>
            {
                if (success)
                {
                    // 更新UI或显示通知
                    Debug.Log("已删除好友");
                }
            });
        }
        
        public event Action<List<FriendData>> OnRefreshFriendList;
        
        // 获取好友列表
        public void RefreshFriendList()
        {
            ExecuteCloudScriptRequest request = new ExecuteCloudScriptRequest
            {
                FunctionName = "GetFriendList"
            };
            
            PlayFabClientAPI.ExecuteCloudScript(request, result =>
            {
                try
                {
                    // 解析好友列表
                    var json = result.FunctionResult.ToString();
                    var friendList = JsonUtility.FromJson<FriendList>(json);
                    _friendDatas = friendList.Friends;
                    
                    // 更新UI或处理好友列表
                    UpdateFriendUI();

                    OnRefreshFriendList?.Invoke(_friendDatas);
                }
                catch (Exception e)
                {
                    Debug.LogError("解析好友列表失败: " + e.Message);
                    
                    OnRefreshFriendList?.Invoke(_friendDatas);
                }
            }, error =>
            {
                Debug.LogError("获取好友列表失败: " + error.ErrorMessage);
                
                OnRefreshFriendList?.Invoke(_friendDatas);
            });
        }
        
        // 更新好友UI
        private void UpdateFriendUI()
        {
            // 这里可以更新UI显示好友列表
            // 例如：根据好友状态显示不同的UI元素
            foreach (var friend in _friendDatas)
            {
                Debug.Log($"好友: {friend.Username}, 状态: {friend.Status}, 在线: {friend.PlayerStatus == PlayerStatus.Online}");
            }
        }
        // 发送好友请求
        public void SendFriendRequest(string playFabId)
        {
            ExecuteCloudScriptRequest request = new ExecuteCloudScriptRequest
            {
                FunctionName = "SendFriendRequest",
                FunctionParameter = new { recipientId = playFabId }
            };
        
            PlayFabClientAPI.ExecuteCloudScript(request, result =>
            {
                Debug.Log("好友请求发送成功");
            }, error =>
            {
                Debug.LogError("发送好友请求失败: " + error.ErrorMessage);
            });
        }
    }
}
