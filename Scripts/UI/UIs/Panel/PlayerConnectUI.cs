using System;
using HotUpdate.Scripts.Network.Server;
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
        private void Init(NetworkManagerCustom networkManager)
        {
            hostBtn.onClick.AddListener(networkManager.StartHost);
            serverBtn.onClick.AddListener(networkManager.StartServer);
            clientBtn.onClick.AddListener(networkManager.StartClient);
        }

        private void OnDestroy()
        {
            hostBtn.onClick.RemoveAllListeners();
            serverBtn.onClick.RemoveAllListeners();
            clientBtn.onClick.RemoveAllListeners();
        }
    }
}
