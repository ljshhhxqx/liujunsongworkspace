using Network.Server;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs
{
    public class LocalConnectScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        [SerializeField]
        private Button hostButton;
        [SerializeField]
        private Button clientButton;
        [SerializeField]
        private Button serverButton;
        [SerializeField]
        private Button helpButton;
        
        public override UIType Type => UIType.LocalConnect;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
        
        [Inject]
        private void Init(UIManager uiManager, NetworkManagerCustom networkManager)
        {
            _uiManager = uiManager;
            hostButton.BindDebouncedListener(() => 
            {
                networkManager.StartHost();
                uiManager.CloseAll();
            });
            clientButton.BindDebouncedListener(() =>
            {
                networkManager.StartClient();
                uiManager.CloseAll();
            });
            serverButton.BindDebouncedListener(() =>
            {
                networkManager.StartServer();
                uiManager.CloseAll();
            });
            helpButton.BindDebouncedListener(() => _uiManager.ShowHelp(""));
        }
    }
}
