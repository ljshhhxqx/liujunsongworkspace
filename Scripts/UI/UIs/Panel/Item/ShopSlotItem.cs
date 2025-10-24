using System;
using System.Globalization;
using AOTScripts.Data;
using AOTScripts.Data.UI;
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
        private float _currentGold;
        private RandomShopItemData _shopItemData;
        private Subject<int> _onBuySubject = new Subject<int>();
        private Subject<PointerEventData> _onClickSubject = new Subject<PointerEventData>();

        public IObservable<PointerEventData> OnClick => _onClickSubject;
        public IObservable<int> OnBuy => _onBuySubject;
        public RandomShopItemData ShopItemData => _shopItemData;
        private CompositeDisposable _disposable;

        public override void SetData<T>(T data)
        {
            if (data is RandomShopItemData shopItemData)
            {
                _disposable?.Dispose();
                _disposable = new CompositeDisposable();
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
                //Debug.Log($"[ShopSlotItem] SetData {_shopItemData.Name}");
                qualityFrame.sprite = shopItemData.QualityIcon;
                quantityText.gameObject.SetActive(shopItemData.RemainingCount > 1);
                quantitySlider.gameObject.SetActive(shopItemData.RemainingCount > 1);
                quantitySlider.maxValue = shopItemData.RemainingCount;
                quantitySlider.minValue = 1;
                quantitySlider.wholeNumbers = true;
                quantitySlider.OnValueChangedAsObservable()
                   .Subscribe(value =>
                   {
                       var quantity = (int) value;
                       quantityText.text = quantity.ToString();
                       var total = quantity * shopItemData.Price;
                       priceText.text = $"{total}G";
                   } )
                   .AddTo(_disposable);
                quantitySlider.value = 1;
                buyButton.interactable = false;
                var valueGold =
                    UIPropertyBinder.ObserveProperty<ValuePropertyData>(new BindingKey(UIPropertyDefine.PlayerBaseData));
                valueGold
                    .Subscribe(x =>
                    {
                        _currentGold = x.Gold;
                    }).AddTo(_disposable);
                Observable.EveryUpdate().Sample(TimeSpan.FromSeconds(0.2f))
                    .Subscribe(_ =>
                    {
                        var goldIsEnough = _currentGold >= (quantitySlider.value * _shopItemData.Price);
                        //Debug.Log($"goldIsEnough = {goldIsEnough}, _currentGold = {_currentGold}, quantitySlider.value = {quantitySlider.value} _shopItemData.Price = {_shopItemData.Price}");
                        var canUseShop = PlayerShopCalculator.CanUseShop(_shopItemData.PlayerId);
                        //Debug.Log($"CanUseShop: {canUseShop}");
                        if (buyButton)
                        {
                            buyButton.interactable = canUseShop && goldIsEnough;
                        }
                    })
                    .AddTo(_disposable);
            }
        }

        public override void Clear()
        {
        }
    }
}