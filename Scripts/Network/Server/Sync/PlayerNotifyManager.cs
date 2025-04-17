using HotUpdate.Scripts.UI.UIBase;
using Mirror;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server.Sync
{
    public class PlayerNotifyManager : NetworkBehaviour
    {
        private UIManager _uiManager;
        
        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
        }
        
        // 客户端通知
        [ClientRpc]
        public void RpcNotifyInsufficientStamina(string message)
        {
            Debug.Log(message);
            _uiManager.ShowTipsOverlay(message);
        }

        // 目标客户端通知
        [TargetRpc]
        public void TargetNotifyInsufficientStamina(NetworkConnection target, string message)
        {
            Debug.Log(message);
            _uiManager.ShowTipsOverlay(message);
        }
    }
}