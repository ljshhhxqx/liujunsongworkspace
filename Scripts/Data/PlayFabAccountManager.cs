using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel;
using Network.Data;
using Newtonsoft.Json;
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
        
        public event Action<List<FriendData>> OnRefreshFriendList;
        
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
        public event Action<int, FriendStatus> OnFriendStatusChanged;
    
        // 更改好友状态
        public void ChangeFriendStatus(int id, string friendId, FriendStatus status)
        {
            var request = new ExecuteEntityCloudScriptRequest()
            {
                FunctionName = "ChangeFriendStatus",
                FunctionParameter = new { 
                    friendId = friendId,
                    status = status.ToString(),
                    playerId = PlayFabData.PlayFabId.Value 
                }
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, result =>
            {
                Debug.Log("好友状态更改成功: " + result.FunctionResult.ToString());
                RefreshFriendList(); // 刷新好友列表
                GetNonFriendOnlinePlayers();// 刷新非好友列表
                OnFriendStatusChanged?.Invoke(id, status);
            }, error =>
            {
                Debug.LogError("更改好友状态失败: " + error.ErrorMessage);
            });
        }
        
        // 同意好友请求
        public void AcceptFriendRequest(int id, string friendId)
        {
            ChangeFriendStatus(id, friendId, FriendStatus.Friends);
        }
        
        // 拒绝好友请求
        public void RejectFriendRequest(int id, string friendId)
        {
            ChangeFriendStatus(id, friendId, FriendStatus.Rejected);
        }
        
        // 删除好友
        public void RemoveFriend(int id, string friendId)
        {
            ChangeFriendStatus(id, friendId, FriendStatus.Removed);
        }
        
        // 获取好友列表
        public void RefreshFriendList( bool showLoading = true)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "GetFriendList",
                FunctionParameter = new { 
                    playerId = PlayFabData.PlayFabId.Value   }
            };
            
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, result =>
            {
                try
                {
                    // 解析好友列表
                    var json = result.FunctionResult.ToString();
                    var friendList = JsonConvert.DeserializeObject<FriendList>(json);
                    _friendDatas = friendList.Friends;
                    
                    // 更新UI或处理好友列表
                    UpdateFriendUI();

                    OnRefreshFriendList?.Invoke(_friendDatas);
                }
                catch (Exception e)
                {
                    Debug.LogError("解析好友列表失败: " + e.Message);
                }
            }, error =>
            {
                Debug.LogError("获取好友列表失败: " + error.ErrorMessage);
            }, showLoading);
        }
        
        // 更新好友UI
        private void UpdateFriendUI()
        {
            // 这里可以更新UI显示好友列表
            // 例如：根据好友状态显示不同的UI元素
            // foreach (var friend in _friendDatas)
            // {
            //     Debug.Log($"好友: {friend.Username}, 状态: {friend.FriendStatus}, 在线: {friend.PlayerStatus == PlayerStatus.Online}");
            // }
        }
        // 发送好友请求
        public void SendFriendRequest(int id, string playFabId)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "SendFriendRequest",
                FunctionParameter = new 
                { 
                    recipientId = playFabId,
                    playerId = PlayFabData.PlayFabId.Value  
                }
            };
        
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, result =>
            {
                Debug.Log("好友请求发送成功");
                OnFriendStatusChanged?.Invoke(id, FriendStatus.RequestSent);
            }, error =>
            {
                Debug.LogError("发送好友请求失败: " + error.ErrorMessage);
            });
        }
        
        private List<PlayerReadOnlyData> _nonFriendData = new List<PlayerReadOnlyData>();
        
        public event Action<List<PlayerReadOnlyData>> OnGetNonFriendList;
        
        public void GetNonFriendOnlinePlayers(int maxResults = 50, bool showLoading = true)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "GetNonFriendOnlinePlayers",
                FunctionParameter = new { maxResults = maxResults,
                    playerId = PlayFabData.PlayFabId.Value   }
            };
        
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, result =>
            {
                try
                {
                    // 解析结果
                    var json = result.FunctionResult.ToString();
                    var apiResult = JsonConvert.DeserializeObject<NonFriendOnlinePlayersResult>(json);
                
                    if (!string.IsNullOrEmpty(apiResult.error))
                    {
                        _uiManager.ShowTips("获取非好友在线玩家失败: " + apiResult.error);
                        return;
                    }
                    Debug.Log($"获取到: {apiResult.count}个可以添加的好友");
                    _nonFriendData = apiResult.players;
                    OnGetNonFriendList?.Invoke(_nonFriendData);
                }
                catch (Exception e)
                {
                    Debug.LogError("解析非好友在线玩家结果失败: " + e.Message);
                }
            }, error =>
            {
                _uiManager.ShowTips("获取非好友在线玩家失败: " + error.ErrorMessage);
            }, showLoading);
        }
        public IEnumerable<FriendData> GetFilteredFriend(string inputText)
        {
            return _friendDatas.Where(room => FilterByIdOrName(room.PlayFabId, room.Username, inputText));
        }
        
        public IEnumerable<PlayerReadOnlyData> GetFilteredPlayer(string inputText)
        {
            return _nonFriendData.Where(room => FilterByIdOrName(room.PlayerId, room.Nickname, inputText));
        }

        private bool FilterByIdOrName(string id, string name, string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                return true;
            }
            var isNumeric = int.TryParse(inputText, out _);
            return isNumeric ? id.Contains(inputText) : name.Contains(inputText);
        }
    }
}
