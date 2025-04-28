using System;
using TMPro;
using UniRx;
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
        private TextMeshProUGUI stackText;         // 显示堆叠数量的Text组件
        [SerializeField]
        private GameObject lockIcon;    // 锁定图标的GameObject组件
        [SerializeField]
        private Button sellBtn;        // 出售按钮
        [SerializeField]
        private Button lockBtn;        // 锁定按钮

        private BagItemData _currentItem;              // 当前格子的物品
        private Subject<int> _onSellSubject = new Subject<int>();
        private Subject<bool> _onLockSubject = new Subject<bool>();
        private Subject<PointerEventData> _onClickSubject = new Subject<PointerEventData>();
        public IObservable<int> OnSellObservable => _onSellSubject;
        public IObservable<bool> OnLockObservable => _onLockSubject;
        public IObservable<PointerEventData> OnClickObservable => _onClickSubject;
        public BagItemData CurrentItem => _currentItem;
        public override void SetData<T>(T data)
        {
            if (data is BagItemData itemData)
            {
                lockBtn.onClick.RemoveAllListeners();
                sellBtn.onClick.RemoveAllListeners();
                lockBtn.onClick.AddListener(() =>
                {
                    _onLockSubject.OnNext(!itemData.IsLock);
                });
                sellBtn.onClick.AddListener(() =>
                {
                    _onSellSubject.OnNext(itemData.Index);
                });
                _currentItem = itemData;
                itemImage.sprite = itemData.Icon;
                qualityImage.sprite = itemData.QualityIcon;
                stackText.text = itemData.Stack.ToString();
                lockIcon.SetActive(itemData.IsLock);
            }
        }

        public override void Clear()
        {
            
        }
    }
}