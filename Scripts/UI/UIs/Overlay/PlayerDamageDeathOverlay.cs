using System;
using AOTScripts.Tool.Resource;
using Coffee.UIEffects;
using DG.Tweening;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
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
        private HReactiveProperty<ValuePropertyData> _goldObservable;
        private ValuePropertyData _valuePropertyData;
        private float _hpRatioToWarning;
        
        private Sequence _damageSequence;
        private Sequence _deathSequence;
        private Tween _countDownTween;
        private Tween _sliderTween;
        
        public override UIType Type => UIType.PlayerDamageDeathOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
        public override bool IsGameUI => true;

        [Inject]
        private void Init(UIManager uiManager, IConfigProvider configProvider)
        {
            _uiManager = uiManager;
            var gameConfig = configProvider.GetConfig<JsonDataConfig>().GameConfig;
            deathRoot.gameObject.SetActive(false);
            damageRoot.gameObject.SetActive(false);
            _hpRatioToWarning = gameConfig.playerHpRatioToWarning;
        }

        public void BindGold(HReactiveProperty<ValuePropertyData> goldObservable)
        {
            _goldObservable = goldObservable;
            _goldObservable.Subscribe(OnGoldChanged).AddTo(this);
            damageRoot.SetActive(false);
            Debug.Log("PlayerDamageDeathOverlay BindGold called");
        }

        private void OnGoldChanged(ValuePropertyData valuePropertyData)
        {
            if (valuePropertyData.Equals(default) || Mathf.Approximately(valuePropertyData.Health, _valuePropertyData.Health)) return;
            if (!_isDeathCountDownStarted)
            {
                deathRoot.SetActive(false);
            }
            PlayDamageEffect(_valuePropertyData.Health, valuePropertyData.Health, valuePropertyData.MaxHealth);
            _valuePropertyData = valuePropertyData;
        }

        public void PlayDamageEffect(float oldHealth, float newHealth, float maxHealth)
        {
            if (_isDeathCountDownStarted || newHealth <= 0f || newHealth >= oldHealth ) return;
            damageRoot.SetActive(newHealth < oldHealth);
//            Debug.Log($"PlayerDamageDeathOverlay PlayDamageEffect called  oldHealth: {oldHealth}, newHealth: {newHealth}, maxHealth: {maxHealth}");
            deathRoot.SetActive(false);
            _damageSequence?.Kill();
            _deathSequence?.Kill();
            _countDownTween?.Kill();
            damageImage.color = new Color(damageImage.color.r, damageImage.color.g, damageImage.color.b, 0f);
            _damageSequence = DOTween.Sequence();
            _damageSequence.Append(damageImage.DOFade(0.5f, 0.05f).SetEase(Ease.Linear));
            _damageSequence.Append(damageImage.DOFade(0, 0.05f).SetEase(Ease.Linear));
            _damageSequence.OnComplete(() =>
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
        
        private bool _isDeathCountDownStarted;

        public void PlayDeathEffect(float deathCountDown)
        {
            _isDeathCountDownStarted = true;
            // 1. 激活相关对象
            deathRoot.SetActive(true);
            damageRoot.SetActive(false);
            
            // 2. 重置相关组件状态（关键修复点）
            countDownSlider.gameObject.SetActive(false);
            countDownSlider.value = 0f;
            
            // 3. 重置Image的透明度
            var damageColor = damageImage.color;
            damageImage.color = new Color(damageColor.r, damageColor.g, damageColor.b, 0f);
            
            // 4. 重置deathImage透明度（关键：确保初始状态正确）
            var deathColor = deathImage.color;
            deathImage.color = new Color(deathColor.r, deathColor.g, deathColor.b, 0f);
            
            // 5. 重置文本透明度
            var textColor = countDownText.color;
            countDownText.color = new Color(textColor.r, textColor.g, textColor.b, 0.7f);
            
            // 6. 重置text effect
            deathTextEffect.transitionRate = 1f;
            
            // 7. 停止之前的动画
            _damageSequence?.Kill();
            _deathSequence?.Kill();
            _countDownTween?.Kill();
            _sliderTween?.Kill();
            
            // 8. 创建新的动画（修复后的版本）
            
            // 文字闪烁效果
            _countDownTween = countDownText
                .DOFade(0.2f, 0.75f)
                .SetLoops(int.MaxValue, LoopType.Yoyo)
                .SetEase(Ease.Linear)
                .OnStart(() => {
                    // 确保text是激活的
                    countDownText.gameObject.SetActive(true);
                });
            
            // 倒计时进度条
            _sliderTween = countDownSlider
                .DOValue(1f, deathCountDown)
                .SetEase(Ease.Linear)
                .OnStart(() => {
                    // 在动画开始时激活slider
                    countDownSlider.gameObject.SetActive(true);
                    countDownSlider.value = 0f; // 再次确保初始值为0
                });
            
            // 死亡动画序列
            _deathSequence = DOTween.Sequence();
            
            // 阶段1：图片淡入
            _deathSequence.Append(
                deathImage.DOFade(0.2f, 1f)
                    .SetEase(Ease.Linear)
                    .OnStart(() => {
                        // 确保deathImage是激活的
                        deathImage.gameObject.SetActive(true);
                    })
            );
            
            // 阶段2：显示进度条（在淡入完成后）
            _deathSequence.AppendCallback(() => {
                countDownSlider.gameObject.SetActive(true);
            });
            
            // 阶段3：文字效果渐变
            _deathSequence.Append(
                DOTween.To(
                    () => deathTextEffect.transitionRate,
                    x => deathTextEffect.transitionRate = x,
                    0f,
                    2f
                ).SetEase(Ease.Linear)
            );
            
            // 设置序列的回调
            _deathSequence.OnComplete(() => {
                Debug.Log("死亡动画播放完成");
                _isDeathCountDownStarted = false;
                // 可以在这里添加动画完成后的逻辑
            });
        }
    }
}