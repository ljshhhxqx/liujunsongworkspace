using System;
using Coffee.UIEffects;
using DG.Tweening;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.UI.UIBase;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerDamageDeathOverlay : ScreenUIBase
    {
        [SerializeField] 
        private GameObject damageRoot;
        [SerializeField]
        private GameObject deathRoot;
        [SerializeField]
        private Image deathImage;
        [SerializeField]
        private Image damageImage;
        [SerializeField]
        private UIEffect deathTextEffect;
        [SerializeField]
        private Slider countDownSlider;
        [SerializeField]
        private TextMeshProUGUI countDownText;
        private UIManager _uiManager;
        private IObservable<GoldData> _goldObservable;
        private GoldData _goldData;
        private BindingKey _goldBindKey;
        private float _hpRatioToWarning;
        
        private Sequence _damageSequence;
        
        public override UIType Type => UIType.PlayerDamageDeathOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        [Inject]
        private void Init(UIManager uiManager, IConfigProvider configProvider)
        {
            _uiManager = uiManager;
            var gameConfig = configProvider.GetConfig<JsonDataConfig>().GameConfig;
            _hpRatioToWarning = gameConfig.playerHpRatioToWarning;
            _goldBindKey = new BindingKey(UIPropertyDefine.PlayerBaseData, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
            _goldObservable = UIPropertyBinder.ObserveProperty<GoldData>(_goldBindKey);
            _goldObservable.Subscribe(OnGoldChanged).AddTo(this);
        }

        private void OnGoldChanged(GoldData goldData)
        {
            if (goldData.Equals(default) || Mathf.Approximately(goldData.Health, _goldData.Health)) return;
            _damageSequence?.Kill();
            _damageSequence = DOTween.Sequence();
            //_damageSequence.Append(damageImage.CrossFadeAlpha(1, 0.1f, true));
            
        }

        public void PlayDamageEffect()
        {
            
        }
    }
}