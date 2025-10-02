using System;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.PlayFab;
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
        public override UIType Type => UIType.PlayerConnect;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init(PlayFabRoomManager playFabRoomManager)
        {
            hostBtn.BindDebouncedListener(() => playFabRoomManager.TryChangePlayerGameInfo(PlayerGameDuty.Host), 2f);
            serverBtn.BindDebouncedListener(() => playFabRoomManager.TryChangePlayerGameInfo(PlayerGameDuty.Server), 2f);
            clientBtn.BindDebouncedListener(() => playFabRoomManager.TryChangePlayerGameInfo(PlayerGameDuty.Client), 2f);
        }

        private void OnDestroy()
        {
            hostBtn.onClick.RemoveAllListeners();
            serverBtn.onClick.RemoveAllListeners();
            clientBtn.onClick.RemoveAllListeners();
        }
    }
}
