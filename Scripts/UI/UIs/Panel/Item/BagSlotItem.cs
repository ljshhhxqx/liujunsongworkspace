using System;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class BagSlotItem : ItemBase, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private Image itemImage;        // 显示物品图标的Image组件
        [SerializeField]
        private Text stackText;         // 显示堆叠数量的Text组件
        private BagItemData _currentItem;              // 当前格子的物品
        private int _stackCount;     // 物品堆叠数量
        private int _slotIndex;         // 格子索引
        private Subject<PointerEventData> _pointerClickObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnBeginDragObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnDragObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnEndDragObservable = new Subject<PointerEventData>();
        
        public IObservable<PointerEventData> OnPointerClickObserver => _pointerClickObservable;
        public IObservable<PointerEventData> OnBeginDragObserver => OnBeginDragObservable;
        public IObservable<PointerEventData> OnDragObserver => OnDragObservable;
        public IObservable<PointerEventData> OnEndDragObserver => OnEndDragObservable;
        public BagItemData CurrentItem => _currentItem;
        public int SlotIndex => _slotIndex;
        public int MaxStack => _currentItem.MaxStack;
        
        public override void SetData<T>(T data)
        {
            if (data is BagItemData bagItemData)
            {
                _currentItem = bagItemData;
                _slotIndex = _currentItem.Index;
                UpdateSlotUI();
            }
            
        }

        // public void SetSlotIndex(int index)
        // {
        //     _slotIndex = index;
        // }

        // 设置物品到格子
        public void SetItem(BagItemData newItem, int count)
        {
            _currentItem = newItem;
            _stackCount = count;
            UpdateSlotUI();
        }

        // 添加到堆叠
        public void AddToStack(int count)
        {
            _stackCount += count;
            UpdateSlotUI();
        }

        // 是否有物品
        public bool HasItem()
        {
            return _currentItem.ItemName != null;
        }

        // 更新格子UI
        private void UpdateSlotUI()
        {
            var itemIsNull = _currentItem.ItemName == null;
            itemImage.sprite = itemIsNull ? null : _currentItem.Icon;
            itemImage.enabled = !itemIsNull;
            stackText.text = !itemIsNull && _stackCount > 1 ? _stackCount.ToString() : "";
        }

        // 拖拽开始
        public void OnBeginDrag(PointerEventData eventData)
        {
            OnBeginDragObservable.OnNext(eventData);
        }

        // 拖拽中
        public void OnDrag(PointerEventData eventData)
        {
            OnDragObservable.OnNext(eventData);
        }

        // 拖拽结束
        public void OnEndDrag(PointerEventData eventData)
        {
            OnEndDragObservable.OnNext(eventData);
        }

        // 交换物品
        // private void SwapItems(BagSlotItem targetSlot)
        // {
        //     var tempItem = targetSlot._currentItem;
        //     var tempCount = targetSlot._stackCount;
        //
        //     targetSlot.SetItem(_currentItem, _stackCount);
        //     if (tempItem.ItemName != null)
        //     {
        //         SetItem(tempItem, tempCount);
        //     }
        //     else
        //     {
        //         _currentItem = default;
        //         _stackCount = 0;
        //         UpdateSlotUI();
        //     }
        // }

        public void OnPointerClick(PointerEventData eventData)
        {
            _pointerClickObservable.OnNext(eventData);
        }
    }
}