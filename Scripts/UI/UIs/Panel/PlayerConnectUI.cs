using System;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class PlayerConnectUI : ScreenUIBase
    {
        [SerializeField]
        private Button hostBtn;
        [SerializeField]
        private Button serverBtn;
        [SerializeField]
        private Button clientBtn;
        [SerializeField]
        private Button quitBtn;
        [SerializeField]
        private ContentItemList contentItemList;
        private PlayFabRoomManager _playFabRoomManager;
        public override UIType Type => UIType.PlayerConnect;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init(PlayFabRoomManager playFabRoomManager,UIManager uiManager)
        {
            _playFabRoomManager = playFabRoomManager;
            _playFabRoomManager.OnGameInfoChanged += OnGameInfoChanged;
            _playFabRoomManager.OnPlayerInfoChanged += OnPlayerInfoChanged;
            quitBtn.BindDebouncedListener(() =>
            {
                uiManager.CloseUI(UIType.PlayerConnect);
                _playFabRoomManager.LeaveGame();
            }, 2f);
            hostBtn.BindDebouncedListener(() => _playFabRoomManager.TryChangePlayerGameInfo(PlayerGameDuty.Host), 2f);
            serverBtn.BindDebouncedListener(() => _playFabRoomManager.TryChangePlayerGameInfo(PlayerGameDuty.Server), 2f);
            clientBtn.BindDebouncedListener(() => _playFabRoomManager.TryChangePlayerGameInfo(PlayerGameDuty.Client), 2f);
        }

        private void OnGameInfoChanged(MainGameInfo info)
        {
            
        }

        private void OnPlayerInfoChanged(string player, GamePlayerInfo arg2)
        {
            
        }

        private void OnDestroy()
        {
            hostBtn.onClick.RemoveAllListeners();
            serverBtn.onClick.RemoveAllListeners();
            clientBtn.onClick.RemoveAllListeners();
            quitBtn.onClick.RemoveAllListeners();
        }
    }
}
