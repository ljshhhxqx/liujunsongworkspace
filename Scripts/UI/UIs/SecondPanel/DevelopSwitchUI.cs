using AOTScripts.Tool;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
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
                
                _isDevelop = PlayerPrefs.GetInt(gameConfig.GameConfig.developKeyValue) == 1;
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
        
    }
}
