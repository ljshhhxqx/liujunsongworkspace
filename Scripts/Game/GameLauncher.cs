using System;
using Collector;
using Common;
using Config;
using Cysharp.Threading.Tasks;
using Data;
using Game.Inject;
using Game.Map;
using HotUpdate.Scripts.Collector;
using Network.Data;
using Network.Server.Edgegap;
using Network.Server.PlayFab;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using Sirenix.OdinInspector;
using Tool.GameEvent;
using UI.UIBase;
using UI.UIs.Exception;
using UI.UIs.SecondPanel;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using ExecuteCloudScriptResult = PlayFab.ClientModels.ExecuteCloudScriptResult;

namespace Game
{
    public class GameLauncher : IStartable
    {
        [Inject] private LocationManager _locationManager;
        [Inject] private IObjectInjector _injector;
        [Inject] private PlayFabRoomManager _playFabRoomManager;
        [Inject] private PlayFabMessageHandler _playFabEventHandler;
        [Inject] private UIManager _uiManager;
        [Inject] private PlayFabAccountManager _playFabAccountManager;
        [Inject] private GameEventManager _gameEventManager;
        [Inject] private ConfigManager _configManager;
        [Inject] private CollectItemSpawner _collectItemSpawner;
        [Inject] private GameSceneManager _gameSceneManager;
        
        public async void Start()
        {
            await LoadResources(); 
            await ResourcesLoadedCallback();
        }

        private async UniTask ResourcesLoadedCallback()
        {
            _gameEventManager.Publish(new GameResourceLoadedEvent());
            await _gameSceneManager.LoadScene("MainGame");
            // async UniTask
            // var prefab = await _resourceManager.GetGameMap();
            // Instantiate(prefab);
            //_uiManager.SwitchUI<LoginScreenUI>();
        }
        

        private async UniTask LoadResources()
        {
            try
            {
                await ResourceManager.Instance.LoadPermanentResources();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            try
            {
                _uiManager.InitPermanentUI();
                _uiManager.SwitchUI<LoadingScreenUI>();
                await ResourceManager.Instance.PreloadResources();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            _gameEventManager.Subscribe<GameMessageListeningEvent>(OnGameMessageListening);
            _uiManager.CloseUI(UIType.Loading);
            //blackboard.gameObject.SetActive(false);
            _configManager.InitConfigs(ResourceManager.Instance.GetAllScriptableObjects());
            _uiManager.InitUIs();
        }

        private void OnGameMessageListening(GameMessageListeningEvent e)
        {
            // var request = new ExecuteEntityCloudScriptRequest
            // {
            //     FunctionName = "TestSendMessage",
            //     FunctionParameter = new
            //     {
            //         playerId = PlayFabData.PlayFabId.Value
            //     },
            //     Entity = PlayFabData.EntityKey.Value,
            //     GeneratePlayStreamEvent = true
            // };
            //
            // PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, results =>
            // {
            //     results.DebugEntityCloudScriptResult();
            //     if (results.Error == null)
            //     {
            //         Debug.Log($"Test message sent successfully");
            //     }
            // }, playFabError =>
            // {
            //     Debug.LogError($"Failed to send test message: {playFabError.GenerateErrorReport()}");
            // });
        }
        
        //[Button("Test Get Object")]
        private void TestGetObject()
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "TestGetObject",
                FunctionParameter = new
                {
                    playerId = PlayFabData.PlayFabId.Value
                },
                Entity = PlayFabData.EntityKey.Value,
                GeneratePlayStreamEvent = true
            };

            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, results =>
            {
                results.DebugEntityCloudScriptResult();
            }, playFabError =>
            {
                Debug.LogError($"Failed to get object: {playFabError.GenerateErrorReport()}");
            });
        }

        //[Button]
        private void TestSpawn()
        { 
            _collectItemSpawner.SpawnItems(_collectItemSpawner.GenerateRandomWeight(), _collectItemSpawner.GenerateRandomSpawnMethod());
        }
        
        //[Button("Test Resource Load")]
        public void TestResourceLoad()
        {
            //var resourceManager = FindObjectOfType<GameMainLifetimeScope>().Container.Resolve<ResourceManager>();
            //var resource = resourceManager.GetResource<GameObject>(new ResourceData{UID =  400000});
        }

        //[Button("Test Edgegap Server")]
        private void TestEdgegapServer()
        {
            _locationManager.GetPlayerLocationOrIP().Forget();
        }
        
        [Button("Test PlayFab Login")]
        private void TestLogin()
        {
            var login = new LoginWithCustomIDRequest
            {
                CustomId = targetPlayerId,
                CreateAccount = true
            };
            PlayFabClientAPI.LoginWithCustomID(login, OnLoginSuccess, OnLoginFailure);
            
        }
        
        private void OnLoginSuccess(LoginResult result)
        {
            Debug.Log($"Login successful!{PlayFabSettings.TitleId}");
            
            playerAccountId = result.PlayFabId;
            
            // PlayFabClientCloudScriptExtensions.ExecuteCloudScript(new ExecuteCloudScriptRequest
            // {
            //     FunctionName = "LoginWithCustomIDRegister",
            //     FunctionParameter = new { playerId = playerAccountId },
            //     GeneratePlayStreamEvent = true,
            // }, OnPlayerDataSuccess, OnCloudScriptError);
            // _playFabRoomManager.InvitePlayer(playerAccountId, ticketId);
            // _playFabEventHandler.StartListening();

            // var request = new ExecuteCloudScriptRequest
            // {
            //     FunctionName = "helloWorld",
            //     FunctionParameter = new { inputValue = "------------MyValue-------------" },
            //     GeneratePlayStreamEvent = true
            // };
            //PlayFabClientAPI.ExecuteCloudScript(request, OnCloudScriptSuccess, OnCloudScriptError);
        }

        private void OnPlayerDataSuccess(ExecuteCloudScriptResult result)
        {
            PlayerInternalData internalData;
            PlayerReadOnlyData readOnlyData;
            var data = result.FunctionResult.ParseCloudScriptResultToDic();
            foreach (var key in data.Keys)
            {
                if (key == "internalData")
                {
                     internalData = data[key].ParseCloudScriptResultToData<PlayerInternalData>();
                }
                else if (key == "readOnlyData")
                {
                     readOnlyData = data[key].ParseCloudScriptResultToData<PlayerReadOnlyData>();
                }
            }
            Debug.Log("Player Data Success");
        }

        private void OnLoginFailure(PlayFabError error)
        {
            Debug.LogError($"Login failed: {error.GenerateErrorReport()}");
        }

        private void OnCloudScriptSuccess(ExecuteCloudScriptResult result)
        {
            result.FunctionResult.DebugCloudScriptResult();
        }

        private void OnCloudScriptError(PlayFabError error)
        {
            Debug.LogError($"Failed to send invitation: {error.GenerateErrorReport()}");
        }

        [SerializeField]
        private string targetPlayerId;
        public string ticketId = "TEST_TICKET_ID";
        public string playerAccountId;
        
        //[Button("Test Title")]
        private void TestTitleData()
        {
            TestLogin();
            //_playFabRoomManager.TestTitleData();
        }
        
        //[Button("Test UITips")]
        private void TestUITips()
        {
            _uiManager.ShowTips("测试提示");
        }
 
        //[Button("Test Create Room")]
        private void TestCreateRoom()
        {
            _uiManager.SwitchUI<CreateRoomScreenUI>();
            
        }

        //[Button("Test Filter")]
        private void TestFilter(string id, string name, string inputText)
        {
            // var filter =_playFabRoomManager.FilterByIdOrName(id, name, inputText);
            // print(filter);
        }
        
        [Button("Test loading")]
        private void TestLoading()
        {
            _uiManager.SwitchUI<LoadingScreenUI>();
        }
    }
}