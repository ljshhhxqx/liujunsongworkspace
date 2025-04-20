using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel.Backpack
{
    public class BackpackScreenUI : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList equipmentItemList;
        [SerializeField]
        private ContentItemList bagItemList;
        [SerializeField]
        [Header("拖拽临时图标")]
        private GameObject dragIcon;
        private UIManager _uiManager;

        private List<BagSlotItem> _bagSlotItems; // 存储格子引用
        private List<EquipmentSlotItem> _slotItems;
        private List<BagItemData> _bagItemData = new List<BagItemData>();  // 存储物品
        private List<EquipItemData> _slotEquipItemData = new List<EquipItemData>();
        
        private BagSlotItem _draggedSlot; // 当前被拖拽的格子

        public static BagCommonData BagCommonData { get; private set; }

        [Inject]
        private void Init(IConfigProvider configProvider, UIManager uiManager)
        {
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            _uiManager = uiManager;
            BagCommonData = jsonConfig.BagCommonData;
        }

        public void BindEquipItemData(ReactiveDictionary<int, EquipItemData> slotEquipItemData)
        {
            _slotEquipItemData ??= slotEquipItemData.Values.ToList();
            _slotItems = new List<EquipmentSlotItem>();
            slotEquipItemData.ObserveAdd()
                .Subscribe(x =>
                {
                    _slotEquipItemData.Add(x.Value);
                    equipmentItemList.SetItemList(_slotEquipItemData.ToArray());
                })
                .AddTo(this);
            slotEquipItemData.ObserveRemove()
                .Subscribe(x =>
                {
                    _slotEquipItemData.RemoveAll(y => (int)y.EquipmentPartType == x.Key);
                    equipmentItemList.SetItemList(_slotEquipItemData.ToArray());
                })
                .AddTo(this);
            slotEquipItemData.ObserveReplace()
                .Subscribe(x =>
                {
                    var index = _slotEquipItemData.FindIndex(y => (int)y.EquipmentPartType == x.Key);
                    _slotEquipItemData[index] = x.NewValue;
                    equipmentItemList.SetItemList(_slotEquipItemData.ToArray());
                })
                .AddTo(this);
            slotEquipItemData.ObserveReset()
                .Subscribe(x =>
                {
                    _slotEquipItemData.Clear();
                    equipmentItemList.SetItemList(Array.Empty<EquipItemData>());
                })
                .AddTo(this);
            RefreshEquip(_slotEquipItemData);
        }

        private void RefreshEquip(List<EquipItemData> slotEquipItemData)
        {
            equipmentItemList.SetItemList(slotEquipItemData.ToArray());
            InitializeEquipSlots();
        }

        public void BindBagItemData(ReactiveDictionary<int, BagItemData> bagItemData)
        {
            _bagItemData ??= bagItemData.Values.ToList();
            _bagSlotItems = new List<BagSlotItem>();
            var dragImage = dragIcon.GetComponent<Image>();
            dragImage.raycastTarget = false;
            dragImage.transform.SetParent(transform.root, false);
            dragImage.gameObject.SetActive(false);
            RefreshBag(_bagItemData);
            InitializeSlots();
            bagItemData.ObserveAdd()
                .Subscribe(x =>
                {
                    _bagItemData.Add(x.Value);
                    bagItemList.SetItemList(_bagItemData.ToArray());
                })
                .AddTo(this);
            bagItemData.ObserveRemove()
                .Subscribe(x =>
                {
                    _bagItemData.RemoveAll(y => y.Index == x.Key);
                    bagItemList.SetItemList(_bagItemData.ToArray());
                })
                .AddTo(this);
            bagItemData.ObserveReplace()
                .Subscribe(x =>
                {
                    var index = _bagItemData.FindIndex(y => y.Index == x.Key);
                    _bagItemData[index] = x.NewValue;
                    bagItemList.SetItemList(_bagItemData.ToArray());
                })
                .AddTo(this);
        }

        private void RefreshBag(List<BagItemData> bagItemData)
        {
            // for (var i = 0; i < BagCommonData.maxBagCount; i++)
            // {
            //     var originalData = bagItemData.FirstOrDefault(x => x.Index == i);
            //     var data = originalData.ItemName != null ? originalData : new BagItemData();
            //     _bagItemData.Add(data);
            // }
            bagItemList.SetItemList(bagItemData.ToArray());
        }
        
        private void InitializeEquipSlots()
        {
            foreach (var item in equipmentItemList.ItemBases)
            {
                var slot = item as EquipmentSlotItem;
                if (!slot) continue;
                slot.OnPointerClickObservable
                    .Subscribe(x => OnEquipClick(slot, x))
                    .AddTo(this);
                _slotItems.Add(slot);
            }
        }

        private void OnEquipClick(EquipmentSlotItem slot, PointerEventData pointerEventData)
        {
            if (slot.IsEmpty()) return;
            // 这里可以弹出物品详情面板
            Debug.Log($"显示物品详情: {slot.name}");
            _uiManager.SwitchUI<ItemDetailsScreenUI>(ui => ui.Open(slot.CurrentItem.ToBagItemData()));
        }

        // 初始化格子
        private void InitializeSlots()
        {
            foreach (var item in bagItemList.ItemBases)
            {
                var slot = item as BagSlotItem;
                if (!slot) continue;
                slot.OnBeginDragObserver
                    .Subscribe(x => OnSlotBeginDrag(slot, x))
                    .AddTo(this);
                slot.OnDragObserver
                    .Subscribe(x => OnSlotDrag(slot, x))
                    .AddTo(this);
                slot.OnEndDragObserver
                    .Subscribe(x => OnSlotEndDrag(slot, x))
                    .AddTo(this);
                slot.OnPointerClickObserver
                    .Subscribe(x => OnSlotClick(slot, x))
                    .AddTo(this);
                _bagSlotItems.Add(slot);
            }
        }

        private void OnSlotDrag(BagSlotItem slot, PointerEventData pointerEventData)
        {
            dragIcon.transform.position = pointerEventData.position; 
        }

        // 处理拖拽开始
        private void OnSlotBeginDrag(BagSlotItem slot, PointerEventData eventData)
        {
            if (!slot.HasItem()) return;

            // 创建拖拽图标（由Inventory统一管理）
            _draggedSlot = slot;
            var dragImage = dragIcon.GetComponent<Image>();
            dragImage.sprite = slot.CurrentItem.Icon;
            dragIcon.SetActive(true);
            dragIcon.transform.position = eventData.position;
        }

        // 处理拖拽结束
        private void OnSlotEndDrag(BagSlotItem sourceSlot, PointerEventData eventData)
        {
            Destroy(dragIcon);

            // 获取目标格子
            var targetSlot = eventData.pointerEnter?.GetComponent<BagSlotItem>();
            if (targetSlot && targetSlot != sourceSlot)
            {
                SwapItemsBetweenSlots(sourceSlot, targetSlot);
            }
        }

        private void SwapItemsBetweenSlots(BagSlotItem source, BagSlotItem target)
        {
            // 交换数据
            var tempItem = target.CurrentItem;
            var tempCount = target.MaxStack;

            target.SetItem(source.CurrentItem, source.MaxStack);
            if (tempItem.ItemName != null)
            {
                source.SetItem(tempItem, tempCount);
            }
            else
            {
                source.SetItem(default, 0);
            }
            target.CurrentItem.OnExchangeItem?.Invoke(source.SlotIndex, target.SlotIndex);
        }

        // 处理悬停提示
        private void OnSlotClick(BagSlotItem slot, PointerEventData eventData)
        {
            if (slot.HasItem())
            {
                Debug.Log($"显示物品详情: {slot.CurrentItem.ItemName}");
                // 这里可以在Inventory中统一控制UI面板
                _uiManager.SwitchUI<ItemDetailsScreenUI>(ui => ui.Open(slot.CurrentItem));
            }
        }

        // 添加物品到背包
        // public bool AddItem(BagItemData newItem)
        // {
        //     // 先检查是否可以堆叠
        //     foreach (var slot in _bagSlotItems)
        //     {
        //         if (slot.HasItem() && slot.CurrentItem.ItemName == newItem.ItemName && slot.MaxStack < slot.CurrentItem.MaxStack)
        //         {
        //             slot.AddToStack(1); // 增加堆叠数量
        //             return true;
        //         }
        //     }
        //
        //     // 如果没有可堆叠的格子，找一个空格子
        //     foreach (var slot in _bagSlotItems)
        //     {
        //         if (!slot.HasItem())
        //         {
        //             slot.SetItem(newItem, 1); // 设置新物品，初始数量为1
        //             return true;
        //         }
        //     }
        //
        //     Debug.Log("背包已满！");
        //     return false;
        // }

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