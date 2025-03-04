using System;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class BagSlotItem : ItemBase, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private Image itemImage;        // 显示物品图标的Image组件
        [SerializeField]
        private Text stackText;         // 显示堆叠数量的Text组件
        private BagItemData item;              // 当前格子的物品
        private int stackCount = 0;     // 物品堆叠数量
        private int slotIndex;         // 格子索引
        public int MaxStack => item.MaxStack;
        private Subject<PointerEventData> OnPointerEnterObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnPointerExitObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnBeginDragObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnDragObservable = new Subject<PointerEventData>();
        private Subject<PointerEventData> OnEndDragObservable = new Subject<PointerEventData>();
        
        public IObservable<PointerEventData> OnPointerEnterObserver => OnPointerEnterObservable;
        public IObservable<PointerEventData> OnPointerExitObserver => OnPointerExitObservable;
        public IObservable<PointerEventData> OnBeginDragObserver => OnBeginDragObservable;
        public IObservable<PointerEventData> OnDragObserver => OnDragObservable;
        public IObservable<PointerEventData> OnEndDragObserver => OnEndDragObservable;
        public BagItemData Item => item;
        
        public override void SetData<T>(T data)
        {
            if (data is BagItemData bagItemData)
            {
                item = bagItemData;
                slotIndex = item.Index;
                UpdateSlotUI();
            }
            
        }

        public void SetSlotIndex(int index)
        {
            slotIndex = index;
        }

        // 设置物品到格子
        public void SetItem(BagItemData newItem, int count)
        {
            item = newItem;
            stackCount = count;
            UpdateSlotUI();
        }

        // 添加到堆叠
        public void AddToStack(int count)
        {
            stackCount += count;
            UpdateSlotUI();
        }

        // 是否有物品
        public bool HasItem()
        {
            return item != null;
        }

        // 更新格子UI
        private void UpdateSlotUI()
        {
            itemImage.sprite = item?.Icon;
            itemImage.enabled = item != null;
            stackText.text = item != null && stackCount > 1 ? stackCount.ToString() : "";
        }

        // 鼠标悬停显示信息（可选）
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterObservable.OnNext(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 隐藏物品详情面板（如果有）
            OnPointerExitObservable.OnNext(eventData);
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
        private void SwapItems(BagSlotItem targetSlot)
        {
            var tempItem = targetSlot.item;
            var tempCount = targetSlot.stackCount;

            targetSlot.SetItem(item, stackCount);
            if (tempItem != null)
            {
                SetItem(tempItem, tempCount);
            }
            else
            {
                item = null;
                stackCount = 0;
                UpdateSlotUI();
            }
        }
    }
}