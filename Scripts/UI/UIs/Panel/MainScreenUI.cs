using System;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.UniRxTool;
using Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using TMPro;
using UI.UIBase;
using UI.UIs;
using UI.UIs.SecondPanel;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class MainScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabRoomManager _playFabRoomManager;
        private PlayFabAccountManager _playFabAccountManager;
        [SerializeField]
        private Button matchButton;
        [SerializeField]
        private Button createRoomButton;
        [SerializeField]
        private Button joinRoomButton;
        [SerializeField]
        private Button helpButton;
        [SerializeField]
        private Button logoutButton;
        [SerializeField]
        private Button quitButton;
        [SerializeField]
        private Button infoButton;
        [SerializeField]
        private Button friendButton;
        [SerializeField] 
        private TextMeshProUGUI timerText;
        [SerializeField] 
        private TextMeshProUGUI infoText;
        [SerializeField] 
        private TextMeshProUGUI idText;
        [SerializeField] 
        private TextMeshProUGUI nameText;
        private float _timer;
        private RepeatedTask _repeatedTask;
        private TimeSpan _timeSpan;
        private string _idTitle;
        private string _nameTitle;
        public override UIType Type => UIType.Main;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager, PlayFabAccountManager playFabAccountManager)
        {
            _idTitle = idText.text;
            _nameTitle = nameText.text;
            _uiManager = uiManager;
            _playFabRoomManager = playFabRoomManager;
            _repeatedTask = RepeatedTask.Instance;
            _playFabAccountManager = playFabAccountManager;
            _playFabRoomManager.OnMatchmakingChanged += OnMatchmakingChanged;
            matchButton.BindDebouncedListener(OnMatchButtonClick);
            createRoomButton.BindDebouncedListener(OnCreateRoomClick);
            helpButton.BindDebouncedListener(OnHelpButtonClick);
            joinRoomButton.BindDebouncedListener(OnJoinRoomClick);
            infoButton.BindDebouncedListener(OnInfoButtonClick);
            logoutButton.BindDebouncedListener(OnLogoutButtonClick);
            quitButton.BindDebouncedListener(OnQuitButtonClick);
            friendButton.BindDebouncedListener(OnFriendButtonClick);
            Debug.Log("MainScreenUI Init");
            
            HReactiveProperty<int> test = new HReactiveProperty<int>();
            test.Subscribe(value =>
            {
                Debug.Log($"Test: {value}");
            });
            test.Value = 10;
            Debug.Log("testData Init");
            HReactiveProperty<PlayerInternalData> internalData = new HReactiveProperty<PlayerInternalData>();
            internalData.Subscribe(value =>
            {
                Debug.Log($"PlayerId: {value.PlayerId}");
            });
            internalData.Value = new PlayerInternalData() { PlayerId = "123456"};
            Debug.Log("PlayerInternalData Init");
            PlayFabData.PlayerReadOnlyData.Subscribe(value =>
            {
                Debug.Log($"PlayerId: {value.PlayerId}, Nickname: {value.Nickname}");
                idText.text = _idTitle + value.PlayerId;
                nameText.text = _nameTitle + value.Nickname;
            })
            .AddTo(this);
        }

        private void OnFriendButtonClick()
        {
            _uiManager.SwitchUI<FriendScreenUI>();
        }

        private void OnInfoButtonClick()
        {
            _uiManager.SwitchUI<PlayerInfoScreenUI>();
        }

        private void OnQuitButtonClick()
        {
            _playFabAccountManager.Logout(Application.Quit);
        }

        private void OnLogoutButtonClick()
        {
            _playFabAccountManager.Logout(() =>
            {
                _uiManager.SwitchUI<LoginScreenUI>();
            });
        }

        private void OnMatchmakingChanged(bool obj)
        {
            if (!_playFabRoomManager.IsMatchmaking)
            {
                infoText.text = "取消匹配";
                timerText.gameObject.SetActive(true);
                _repeatedTask.StartRepeatingTask(UpdateMatchmakingInfo, 1f);
            }
            else
            {
                _repeatedTask.StopRepeatingTask(UpdateMatchmakingInfo);             
                infoText.text = "匹配对战";
                timerText.text = _timeSpan.ToString("00:00");
                timerText.gameObject.SetActive(false);
            }
            _timer = 0;
        }

        private void OnJoinRoomClick()
        {
            _uiManager.SwitchUI<RoomListScreenUI>();
        }

        private void OnHelpButtonClick()
        {
            _uiManager.ShowHelp("<b>匹配对战：</b>将默认使用远程服务器，系统匹配与您实力相匹配的玩家作为对手进行游戏。\n\n<b>创建房间：</b>允许自定义房间名、玩家人数、密码，并可以使用本地服务器(也可以使用远程服务器邀请不在同一局域网下的玩家)进行游戏。\n\n<b>加入房间：</b>允许加入别的自定义房间进行游戏。");
        }
        
        private void OnCreateRoomClick()
        {
            _uiManager.SwitchUI<CreateRoomScreenUI>();
        }

        private void OnMatchButtonClick()
        {
            _uiManager.ShowTips("敬请期待！");
            return;
            if (!_playFabRoomManager.IsMatchmaking)
            {
                _playFabRoomManager.CreateOrJoinMatchingRoom();
            }
            else
            {
                _playFabRoomManager.CancelMatchmaking();   
            }
        }
        
        private void UpdateMatchmakingInfo()
        {
            _timeSpan = TimeSpan.FromSeconds(_timer);
            timerText.text = _timeSpan.ToString(@"mm\\:ss");
            _timer += 1f;
            Debug.Log($"Matchmaking: {_timer}");
        }
    }
}