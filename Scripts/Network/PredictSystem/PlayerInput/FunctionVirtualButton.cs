using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class FunctionVirtualButton : MonoBehaviour
    {
        [SerializeField]
        private KeyFunction keyFunction;
        private Button _button;
        private KeyFunctionConfig _keyFunctionConfig;
        private UIManager _uiManager;
        private GameEventManager _gameEventManager;

        [Inject]
        private void Init(UIManager uiManager, IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            _uiManager = uiManager;
            _gameEventManager = gameEventManager;
            _keyFunctionConfig = configProvider.GetConfig<KeyFunctionConfig>();
            _button.OnClickAsObservable().Subscribe(_ => OnClick()).AddTo(this);
        }

        private void OnClick()
        {
            var uiType = _keyFunctionConfig.GetUIType(keyFunction);
            if (uiType != UIType.None)
            {
                _gameEventManager.Publish(new GameFunctionUIShowEvent(uiType));
                _uiManager.OpenUI(uiType);
                return;
            }
            Debug.LogWarning($"No UIType found for {keyFunction}");
        }
    }
}
