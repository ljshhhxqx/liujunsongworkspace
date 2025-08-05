using System;
using System.Globalization;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
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
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private TextMeshProUGUI nameText;
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
                nameText.text = shopItemData.Name;
                Debug.Log($"[ShopSlotItem] SetData {_shopItemData.Name}");
                qualityFrame.sprite = shopItemData.QualityIcon;
                quantitySlider.maxValue = shopItemData.RemainingCount;
                quantitySlider.minValue = 1;
                quantitySlider.wholeNumbers = true;
                quantitySlider.OnValueChangedAsObservable()
                   .Subscribe(value =>
                   {
                       var quantity = (int) value;
                       quantityText.text = quantity.ToString(CultureInfo.InvariantCulture);
                       var total = quantity * shopItemData.Price;
                       priceText.text = $"{total}G";
                   } )
                   .AddTo(_disposable);
            }
        }

        private void Update()
        {
            var canUseShop = PlayerShopCalculator.CanUseShop(PlayerInGameManager.LocalPlayerId);
            buyButton.interactable = canUseShop;
            
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