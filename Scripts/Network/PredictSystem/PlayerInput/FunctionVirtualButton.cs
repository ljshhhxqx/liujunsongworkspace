using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class FunctionVirtualButton : MonoBehaviour
    {
        [SerializeField]
        private KeyFunction keyFunction;
        [SerializeField]
        private Button button;
        private KeyFunctionConfig _keyFunctionConfig;
        private GameEventManager _gameEventManager;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _keyFunctionConfig = configProvider.GetConfig<KeyFunctionConfig>();
            button.OnClickAsObservable().Subscribe(_ => OnClick()).AddTo(this);
        }

        private void OnClick()
        {
            var uiType = _keyFunctionConfig.GetUIType(keyFunction);
            if (uiType != UIType.None)
            {
                _gameEventManager.Publish(new GameFunctionUIShowEvent(uiType));
                return;
            }

            if (keyFunction == KeyFunction.Reset)
            {
                _gameEventManager.Publish(new TouchResetCameraEvent());
                return;
            }
            Debug.LogWarning($"No UIType found for {keyFunction}");
        }
    }
}
