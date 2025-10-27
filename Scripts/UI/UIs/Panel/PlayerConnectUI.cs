using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
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
            Debug.Log($"OnGameInfoChanged {info}");
            var dict = new Dictionary<int, PlayerConnectionData>();
            for (int i = 0; i < info.playersInfo.Length; i++)
            {
                var playerInfo = info.playersInfo[i];
                if (playerInfo.playerId == PlayFabData.PlayFabId.Value)
                {
                    hostBtn.interactable = playerInfo.playerDuty != PlayerGameDuty.Host.ToString() || playerInfo.playerDuty == PlayerGameDuty.None.ToString();// || playerInfo.playerDuty == ..ToString();
                    serverBtn.interactable = playerInfo.playerDuty != PlayerGameDuty.Server.ToString()|| playerInfo.playerDuty == PlayerGameDuty.None.ToString();
                    clientBtn.interactable = playerInfo.playerDuty != PlayerGameDuty.Client.ToString()|| playerInfo.playerDuty == PlayerGameDuty.None.ToString();
                }
                var data = new PlayerConnectionData
                {
                    PlayerId = playerInfo.playerId,
                    Name = playerInfo.playerName,
                    Duty = (PlayerGameDuty)Enum.Parse(typeof(PlayerGameDuty), playerInfo.playerDuty),
                    Level = playerInfo.playerLevel,
                    Status =  (PlayerGameStatus)Enum.Parse(typeof(PlayerGameStatus), playerInfo.playerStatus),
                };
                dict.Add(playerInfo.id, data);
            }
            contentItemList.SetItemList(dict);
        }

        private void OnPlayerInfoChanged(string player, GamePlayerInfo playerInfo)
        {
            if (player == PlayFabData.PlayFabId.Value)
            {
                hostBtn.interactable = playerInfo.playerDuty == PlayerGameDuty.Host.ToString()|| playerInfo.playerDuty == PlayerGameDuty.None.ToString();
                serverBtn.interactable = playerInfo.playerDuty == PlayerGameDuty.Server.ToString()|| playerInfo.playerDuty == PlayerGameDuty.None.ToString();
                clientBtn.interactable = playerInfo.playerDuty == PlayerGameDuty.Client.ToString()|| playerInfo.playerDuty == PlayerGameDuty.None.ToString();
            }

            var key = 0;
            foreach (var kvp in contentItemList.ItemBaseDatas)
            {
                if (kvp.Value is PlayerConnectionData connectionData && connectionData.PlayerId == player)
                {
                    key = kvp.Key;
                    break;
                }
            }
            if (key == 0)
            {
                return;
            }
            var dict = contentItemList.ItemBaseDatas;
            dict[key] = new PlayerConnectionData
            {
                PlayerId = player,
                Name = playerInfo.playerName,
                Duty = (PlayerGameDuty)Enum.Parse(typeof(PlayerGameDuty), playerInfo.playerDuty),
                Level = playerInfo.playerLevel,
                Status =  (PlayerGameStatus)Enum.Parse(typeof(PlayerGameStatus), playerInfo.playerStatus),
            };
            contentItemList.SetItemList(dict);
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
