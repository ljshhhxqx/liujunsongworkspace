using System;
using Data;
using Network.Server.PlayFab;
using TMPro;
using Tool.Coroutine;
using UI.UIBase;
using UI.UIs.SecondPanel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.Panel
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
        private TextMeshProUGUI timerText;
        [SerializeField] 
        private TextMeshProUGUI infoText;
        private float _timer;
        private RepeatedTask _repeatedTask;
        private TimeSpan _timeSpan;
        
        public override UIType Type => UIType.Main;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager, RepeatedTask repeatedTask, PlayFabAccountManager playFabAccountManager)
        {
            _uiManager = uiManager;
            _playFabRoomManager = playFabRoomManager;
            _repeatedTask = repeatedTask;
            _playFabAccountManager = playFabAccountManager;
            _playFabRoomManager.OnMatchmakingChanged += OnMatchmakingChanged;
            matchButton.BindDebouncedListener(OnMatchButtonClick);
            createRoomButton.BindDebouncedListener(OnCreateRoomClick);
            helpButton.BindDebouncedListener(OnHelpButtonClick);
            joinRoomButton.BindDebouncedListener(OnJoinRoomClick);
            infoButton.BindDebouncedListener(OnInfoButtonClick);
            logoutButton.BindDebouncedListener(OnLogoutButtonClick);
            quitButton.BindDebouncedListener(OnQuitButtonClick);
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
        
        private void OnDestroy()
        {
            _playFabRoomManager.OnMatchmakingChanged -= OnMatchmakingChanged;
        }
    }
}