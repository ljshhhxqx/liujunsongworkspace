using UI.UIBase;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Popup
{
    public class TipsPopup : CommonTipsPopup
    {
        private UIManager _uiManager;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            onClose = () => _uiManager.CloseUI(UIType.TipsPopup);
            print("TipsPopup Init");
        }
    }
}