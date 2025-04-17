using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.UI.UIBase;
using UI.UIBase;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public class PlayerUIController : NetworkAutoInjectComponent
    {
        private UIManager _uiManager;
        
        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
        }
    }
}