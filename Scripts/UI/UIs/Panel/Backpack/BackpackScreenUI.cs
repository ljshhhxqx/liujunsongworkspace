﻿using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel.Backpack
{
    public class BackpackScreenUI : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList contentItemList;

        private List<BagSlotItem> _bagSlotItems = new List<BagSlotItem>(); // 存储格子引用
        private List<BagItemData> _bagItemData = new List<BagItemData>();  // 存储物品
        
        private GameObject _dragIcon; // 拖拽临时图标
        private BagSlotItem _draggedSlot; // 当前被拖拽的格子

        public static BagCommonData BagCommonData { get; private set; }

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            BagCommonData = jsonConfig.BagCommonData;
        }

        // 初始化格子
        public void InitializeSlots(IEnumerable<BagItemData> bagItemData)
        {
            var bagItemDatas = new List<BagItemData>();
            for (var i = 0; i < BagCommonData.maxBagCount; i++)
            {
                var data = bagItemDatas.FirstOrDefault(x => x.Index == i) ?? new BagItemData();
                bagItemDatas.Add(data);
            }
            contentItemList.SetItemList(bagItemDatas.ToArray());
            foreach (var item in contentItemList.ItemBases)
            {
                var slot = item as BagSlotItem;
                if (!slot) continue;
                slot.OnPointerEnterObserver
                    .Subscribe(x => OnSlotBeginDrag(slot, x))
                    .AddTo(this);
                slot.OnEndDragObserver
                    .Subscribe(x => OnSlotEndDrag(slot, x))
                    .AddTo(this);
                slot.OnPointerEnterObserver
                    .Subscribe(x => OnSlotPointerEnter(slot, x))
                    .AddTo(this);
                _bagSlotItems.Add(slot);
            }

            _bagItemData = bagItemDatas.ToList();
        }
        
        // 处理拖拽开始
        private void OnSlotBeginDrag(BagSlotItem slot, PointerEventData eventData)
        {
            if (!slot.HasItem()) return;

            // 创建拖拽图标（由Inventory统一管理）
            _draggedSlot = slot;
            _dragIcon = new GameObject("DragIcon");
            var dragImage = _dragIcon.AddComponent<Image>();
            dragImage.sprite = slot.Item.Icon;
            dragImage.raycastTarget = false;
            _dragIcon.transform.SetParent(transform.root, false);
            _dragIcon.transform.position = eventData.position;
        }

        // 处理拖拽结束
        private void OnSlotEndDrag(BagSlotItem sourceSlot, PointerEventData eventData)
        {
            Destroy(_dragIcon);

            // 获取目标格子
            var targetSlot = eventData.pointerEnter?.GetComponent<BagSlotItem>();
            if (targetSlot && targetSlot != sourceSlot)
            {
                // 在Inventory中交换物品数据
                SwapItemsBetweenSlots(sourceSlot, targetSlot);
            }
        }

        // 统一管理物品交换逻辑
        private void SwapItemsBetweenSlots(BagSlotItem source, BagSlotItem target)
        {
            // 交换数据
            var tempItem = target.Item;
            var tempCount = target.MaxStack;

            target.SetItem(source.Item, source.MaxStack);
            if (tempItem != null)
            {
                source.SetItem(tempItem, tempCount);
            }
            else
            {
                source.SetItem(null, 0);
            }
        }

        // 处理悬停提示
        private void OnSlotPointerEnter(BagSlotItem slot, PointerEventData eventData)
        {
            if (slot.HasItem())
            {
                Debug.Log($"显示物品详情: {slot.Item.ItemName}");
                // 这里可以在Inventory中统一控制UI面板
            }
        }

        // 添加物品到背包
        public bool AddItem(BagItemData newItem)
        {
            // 先检查是否可以堆叠
            foreach (var slot in _bagSlotItems)
            {
                if (slot.HasItem() && slot.Item.ItemName == newItem.ItemName && slot.MaxStack < slot.Item.MaxStack)
                {
                    slot.AddToStack(1); // 增加堆叠数量
                    return true;
                }
            }

            // 如果没有可堆叠的格子，找一个空格子
            foreach (var slot in _bagSlotItems)
            {
                if (!slot.HasItem())
                {
                    slot.SetItem(newItem, 1); // 设置新物品，初始数量为1
                    return true;
                }
            }

            Debug.Log("背包已满！");
            return false;
        }

        public override UIType Type => UIType.Backpack;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        // // 测试添加物品（可以在Inspector中调用）
        // [ContextMenu("Test Add Item")]
        // public void TestAddItem()
        // {
        //     Sprite testSprite = Resources.Load<Sprite>("TestIcon"); // 假设有一个测试图标
        //     var testItem = new BagItemData("Sword", testSprite);
        //     AddItem(testItem);
        // }
    }
}