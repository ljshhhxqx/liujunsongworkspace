using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class ShopBagSlotItem : ItemBase
    {
        [SerializeField]
        private Image itemImage;        // 显示物品图标的Image组件
        [SerializeField]
        private Image qualityImage;        // 显示物品图标的Image组件
        [SerializeField]
        private Slider qualitySlider;
        [SerializeField]
        private TextMeshProUGUI sellCountText;         // 显示物品名称的Text组件
        [SerializeField]
        private TextMeshProUGUI stackText;         // 显示堆叠数量的Text组件
        [SerializeField]
        private TextMeshProUGUI sellPriceText;         // 显示总价的Text组件
        [SerializeField]
        private GameObject lockIcon;    // 锁定图标的GameObject组件
        [SerializeField]
        private GameObject equipIcon;    // 锁定图标的GameObject组件
        [SerializeField]
        private Button sellBtn;        // 出售按钮
        [SerializeField]
        private Button lockBtn;        // 锁定按钮

        private BagItemData _currentItem;              // 当前格子的物品
        private string _priceString;
        private Subject<int> _onSellSubject = new Subject<int>();
        private Subject<bool> _onLockSubject = new Subject<bool>();
        private Subject<PointerEventData> _onClickSubject = new Subject<PointerEventData>();
        private CompositeDisposable _disposable = new CompositeDisposable();
        public IObservable<int> OnSellObservable => _onSellSubject;
        public IObservable<bool> OnLockObservable => _onLockSubject;
        public IObservable<PointerEventData> OnClickObservable => _onClickSubject;
        public BagItemData CurrentItem => _currentItem;
        public override void SetData<T>(T data)
        {
            if (data is BagItemData itemData)
            {
                _disposable?.Dispose();
                _disposable = new CompositeDisposable();
                _priceString??= sellPriceText.text;
                lockBtn.OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                    .Subscribe(_ =>
                    {
                        _onLockSubject.OnNext(!itemData.IsLock);
                    })
                    .AddTo(_disposable);
                sellBtn.OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                    .Subscribe(_ =>
                    {
                        _onSellSubject.OnNext((int)qualitySlider.value);
                    })
                    .AddTo(_disposable);
                _currentItem = itemData;
                itemImage.sprite = itemData.Icon;
                itemImage.OnPointerClickAsObservable()
                    .Subscribe(x =>
                     {
                         _onClickSubject.OnNext(x);
                     })
                    .AddTo(_disposable);
                qualityImage.sprite = itemData.QualityIcon;
                stackText.text = itemData.Stack.ToString();
                lockIcon.SetActive(itemData.IsLock);
                equipIcon.SetActive(itemData.IsEquip);
                qualitySlider.value = 1;
                qualitySlider.minValue = 1;
                qualitySlider.maxValue = itemData.Stack;
                qualitySlider.wholeNumbers = true;
                qualitySlider.OnValueChangedAsObservable()
                    .Subscribe(value =>
                    {
                        sellCountText.text = value.ToString("0");
                        sellPriceText.text = _priceString + (itemData.Price *itemData.SellRatio * value).ToString("0") + "G";
                    })
                    .AddTo(_disposable);
            }
        }

        public override void Clear()
        {
            
        }
    }
}