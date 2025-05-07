using System;
using Coffee.UIEffects;
using DG.Tweening;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.UI.UIBase;
using Sirenix.OdinInspector;
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
        private float _hpRatioToWarning;
        
        private Sequence _damageSequence;
        private Sequence _deathSequence;
        private Tween _countDownTween;
        private Tween _sliderTween;
        
        public override UIType Type => UIType.PlayerDamageDeathOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        [Inject]
        private void Init(UIManager uiManager, IConfigProvider configProvider)
        {
            _uiManager = uiManager;
            var gameConfig = configProvider.GetConfig<JsonDataConfig>().GameConfig;
            deathRoot.gameObject.SetActive(false);
            damageRoot.gameObject.SetActive(false);
            _hpRatioToWarning = gameConfig.playerHpRatioToWarning;
            damageImage.color = new Color(damageImage.color.r, damageImage.color.g, damageImage.color.b, 0f);
        }

        public void BindGold(IObservable<GoldData> goldObservable)
        {
            _goldObservable = goldObservable;
            _goldObservable.Subscribe(OnGoldChanged).AddTo(this);
        }

        private void OnGoldChanged(GoldData goldData)
        {
            if (goldData.Equals(default) || Mathf.Approximately(goldData.Health, _goldData.Health)) return;
            deathRoot.SetActive(false);
            _goldData = goldData;
            PlayDamageEffect(_goldData.Health, goldData.Health, _goldData.MaxHealth);
        }

        public void PlayDamageEffect(float oldHealth, float newHealth, float maxHealth)
        {
            damageRoot.SetActive(newHealth < oldHealth);
            deathRoot.SetActive(false);
            _damageSequence?.Kill();
            _deathSequence?.Kill();
            _countDownTween?.Kill();
            damageImage.color = new Color(damageImage.color.r, damageImage.color.g, damageImage.color.b, 0f);
            _damageSequence = DOTween.Sequence();
            _damageSequence.Append(damageImage.DOFade(0.2f, 0.05f).SetEase(Ease.Linear));
            _damageSequence.SetLoops(8, LoopType.Yoyo);
            _damageSequence.AppendCallback(() =>
            {
                if (oldHealth / maxHealth < _hpRatioToWarning)
                {
                    damageRoot.SetActive(true);
                    var color = damageImage.color;
                    damageImage.color = new Color(color.r, color.g, color.b, 0.2f);
                }
                else
                {
                    damageRoot.SetActive(false);
                }
            });
        }
        
        [Button]
        private void TestDeathEffect(float deathCountDown)
        {
            PlayDeathEffect(deathCountDown);
        }
        
        [Button]
        private void TestDamageEffect(float oldHealth, float newHealth, float maxHealth)
        {
            PlayDamageEffect(10f, 5f, 10f);
        }

        public void Clear()
        {
            damageRoot.SetActive(false);
            deathRoot.SetActive(false);
            _damageSequence?.Kill();
            _deathSequence?.Kill();
            _countDownTween?.Kill();
            _sliderTween?.Kill();
        }

        public void PlayDeathEffect(float deathCountDown)
        {
            deathRoot.SetActive(true);
            damageRoot.SetActive(false);
            countDownSlider.gameObject.SetActive(false);
            countDownSlider.value = 0f;
            var color = damageImage.color;
            damageImage.color = new Color(color.r, color.g, color.b, 0f);
            countDownText.color = new Color(countDownText.color.r, countDownText.color.g, countDownText.color.b, 0.7f);
            deathTextEffect.transitionRate = 1f;
            _damageSequence?.Kill();
            _deathSequence?.Kill();
            _countDownTween?.Kill();
            _sliderTween?.Kill();
            _countDownTween = countDownText.DOFade(0.2f, 0.75f).SetLoops(int.MaxValue, LoopType.Yoyo).SetEase(Ease.Linear);
            _sliderTween = DOTween.To(() => countDownSlider.value, x => countDownSlider.value = x, 1, deathCountDown).SetEase(Ease.Linear);
            _deathSequence = DOTween.Sequence();
            _deathSequence.Append(deathImage.DOFade(0.2f, 2f).SetEase(Ease.Linear).OnComplete(() =>
            {
                countDownSlider.gameObject.SetActive(true);
                DOTween.To(() => deathTextEffect.transitionRate, x => deathTextEffect.transitionRate = x, 0f, 2f)
                    .SetEase(Ease.Linear);
            }));
            _deathSequence.SetEase(Ease.Linear);
        }
    }
}