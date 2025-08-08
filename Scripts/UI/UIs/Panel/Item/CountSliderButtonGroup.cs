using System;
using AOTScripts.Tool;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class CountSliderButtonGroup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Slider slider;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI buttonText;
        private Subject<int> _sliderSubject = new Subject<int>();
        public IObservable<int> OnSliderChanged => _sliderSubject;

        private CountSliderButtonGroupData _countSliderButtonGroupData;
        private void Start()
        {
            slider.wholeNumbers = true;
        }

        public void SetPlayerGold(float gold)
        {
            _countSliderButtonGroupData.CurrentGold = gold;
            SetCount((int)slider.value, _countSliderButtonGroupData.MaxCount);
        }

        public void Init(CountSliderButtonGroupData countSliderButtonGroupData)
        {
            _countSliderButtonGroupData = countSliderButtonGroupData;
            slider.onValueChanged.RemoveAllListeners();
            buttonText.text = EnumHeaderParser.GetHeader(_countSliderButtonGroupData.ButtonType);
            
            button.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromMilliseconds(0.5f))
                .Subscribe(_ =>
                {
                    countSliderButtonGroupData.MaxCount -= (int)slider.value;
                    _countSliderButtonGroupData.Callback?.Invoke((int)slider.value);
                })
                .AddTo(this);
            slider.minValue = Mathf.Min(_countSliderButtonGroupData.MinCount, 1);
            slider.maxValue = _countSliderButtonGroupData.MaxCount;
            slider.onValueChanged.AddListener(x =>
            {
                _sliderSubject.OnNext((int)x);
                SetCount((int)x, _countSliderButtonGroupData.MaxCount);
            });
            SetCount(1, _countSliderButtonGroupData.MaxCount);
        }

        private void SetCount(int count, int maxCount)
        {
            var totalPrice = count * _countSliderButtonGroupData.PricePerItem;
            countText.text = $"{count}/{maxCount}";
            priceText.text = _countSliderButtonGroupData.ShowPrice ? 
                $"价格: {totalPrice}G (当前金币: {_countSliderButtonGroupData.CurrentGold}G)" : "";
            switch (_countSliderButtonGroupData.ButtonType)
            {
                case ButtonType.Use:
                case ButtonType.Drop:
                case ButtonType.Sell:
                    button.interactable = count > 0;
                    break;
                case ButtonType.Buy:
                    button.interactable = count > 0 && _countSliderButtonGroupData.CurrentGold >= totalPrice;
                    break;
            }
            slider.gameObject.SetActive(_countSliderButtonGroupData.PlayerItemType == PlayerItemType.Consume || _countSliderButtonGroupData.PlayerItemType == PlayerItemType.Item);
        }
    }
}