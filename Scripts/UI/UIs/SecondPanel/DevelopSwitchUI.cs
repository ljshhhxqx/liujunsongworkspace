using Common;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.SecondPanel
{
    public class DevelopSwitchUI : MonoBehaviour
    {
        [SerializeField]
        private Button switchButton;
        private int _clickCount;
        private UIManager _uiManager;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            switchButton.BindDebouncedListener(OnSwitchButtonClick, 0.1f);
        }

        private void OnSwitchButtonClick()
        {
            _clickCount++;
            if (_clickCount != 5) return;
            _clickCount = 0;
            _uiManager.SwitchUI<DevelopScreenUI>();
        }
    }
}
