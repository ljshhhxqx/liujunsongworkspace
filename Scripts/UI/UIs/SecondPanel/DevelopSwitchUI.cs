using AOTScripts.Tool;
using AOTScripts.Tool.Resource;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.UI.UIBase;
using UI.UIBase;
using UI.UIs.SecondPanel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    public class DevelopSwitchUI : ScreenUIBase
    {
        [SerializeField]
        private Button switchButton;
        [SerializeField]
        private Button functionButton;
        private int _clickCount;
        private int _switchCount;
        private UIManager _uiManager;
        private bool _isDevelop;
        private GameConfigData _gameConfigData;
        private IConfigProvider _configProvider;

        [Inject]
        private void Init(UIManager uiManager, IConfigProvider configProvider)
        {
            _uiManager = uiManager;
            _configProvider = configProvider;
            switchButton.BindDebouncedListener(OnSwitchButtonClick, 0.1f);
            functionButton.BindDebouncedListener(OnFunctionButtonClick, 0.1f);
        }

        private void OnFunctionButtonClick()
        {
            if (_gameConfigData.developKeyValue == null)
            {
                var gameConfig = _configProvider.GetConfig<JsonDataConfig>();
                var value = PlayerPrefs.GetInt(gameConfig.GameConfig.developKey);
                _isDevelop = value == 1;
            }
            if (_isDevelop)
            {
                _clickCount++;
                if (_clickCount != 5) return;
                _clickCount = 0;
                _uiManager.SwitchUI<DevelopFunctionUI>();
            }
        }

        private void OnSwitchButtonClick()
        {
            _switchCount++;
            if (_switchCount != 5) return;
            _switchCount = 0;
            _uiManager.SwitchUI<DevelopScreenUI>();
        }

        public override UIType Type => UIType.DevelopSwitch;
        public override UICanvasType CanvasType => UICanvasType.Popup;
    }
}
