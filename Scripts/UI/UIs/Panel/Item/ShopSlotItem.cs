using System;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class ShopSlotItem : ItemBase
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image qualityFrame;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Slider quantitySlider;
        [SerializeField] private Button buyButton;
        private RandomShopItemData _shopItemData;
        private Subject<int> _onBuySubject = new Subject<int>();
        private Subject<PointerEventData> _onClickSubject = new Subject<PointerEventData>();
        private CompositeDisposable _disposable = new CompositeDisposable();

        public IObservable<PointerEventData> OnClick => _onClickSubject;
        public IObservable<int> OnBuy => _onBuySubject;
        public RandomShopItemData ShopItemData => _shopItemData;
        
        public override void SetData<T>(T data)
        {
            if (data is RandomShopItemData shopItemData)
            {
                _shopItemData = shopItemData;
                icon.OnPointerClickAsObservable()
                    .Subscribe(p => _onClickSubject.OnNext(p))
                    .AddTo(_disposable);
                buyButton.OnClickAsObservable()
                    .Subscribe(_ =>
                    {
                        var count = (int) quantitySlider.value;
                        _onBuySubject.OnNext(count);
                    })
                    .AddTo(_disposable);
        
                icon.sprite = shopItemData.Icon;
                qualityFrame.sprite = shopItemData.QualityIcon;
                priceText.text = $"{shopItemData.Price}G";
                quantitySlider.maxValue = shopItemData.RemainingCount;
                quantitySlider.minValue = 1;
                quantitySlider.wholeNumbers = true;
            }
        }

        public override void Clear()
        {
            icon.sprite = null;
            qualityFrame.sprite = null;
            priceText.text = "";
            quantitySlider.value = 0;
            _shopItemData = default;
            _disposable.Dispose();
            _disposable.Clear();
        }
    }
}