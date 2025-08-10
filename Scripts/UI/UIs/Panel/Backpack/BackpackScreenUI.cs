using System;
using System.Collections.Generic;
using System.Linq;
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
    public class BackpackScreenUI : ScreenUIBase, IUnlockMouse
    {
        [SerializeField]
        private ContentItemList equipmentItemList;
        [SerializeField]
        private ContentItemList bagItemList;
        [SerializeField]
        [Header("拖拽临时图标")]
        private GameObject dragIcon;
        [SerializeField]
        private Button closeBtn;
        private UIManager _uiManager;

        private Dictionary<int, BagSlotItem> _bagSlotItems; // 存储格子引用
        private Dictionary<int, EquipmentSlotItem> _slotItems;
        private Dictionary<int, BagItemData> _bagItemData;  // 存储物品
        private Dictionary<int, EquipItemData> _slotEquipItemData;
        
        private BagSlotItem _draggedSlot; // 当前被拖拽的格子

        public static BagCommonData BagCommonData { get; private set; }

        [Inject]
        private void Init(IConfigProvider configProvider, UIManager uiManager)
        {
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            _uiManager = uiManager;
            BagCommonData = jsonConfig.BagCommonData;
            
            closeBtn.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => _uiManager.CloseUI(Type))
                .AddTo(this);
            dragIcon.SetActive(false);
        }

        public void BindEquipItemData(ReactiveDictionary<int, EquipItemData> slotEquipItemData)
        {
            _slotEquipItemData = new Dictionary<int, EquipItemData>();
            foreach (var key in slotEquipItemData.Keys)
            {
                var slot = slotEquipItemData[key];
                _slotEquipItemData.Add(key, slot);
            }
            _slotItems = new Dictionary<int, EquipmentSlotItem>();//<EquipmentSlotItem>();
            slotEquipItemData.ObserveAdd()
                .Subscribe(x =>
                {
                    if (!_slotEquipItemData.ContainsKey(x.Key))
                    {
                        _slotEquipItemData.Add(x.Key, x.Value);
                        equipmentItemList.AddItem(x.Key, x.Value);
                    }
                    //equipmentItemList.SetItemList(_slotEquipItemData);
                })
                .AddTo(this);
            slotEquipItemData.ObserveRemove()
                .Subscribe(x =>
                {
                    if (_slotEquipItemData.ContainsKey(x.Key))
                    {

                        _slotEquipItemData.Remove(x.Key);
                        equipmentItemList.RemoveItem(x.Key);
                    }
                    //equipmentItemList.SetItemList(_slotEquipItemData);
                })
                .AddTo(this);
            slotEquipItemData.ObserveReplace()
                .Subscribe(x =>
                {
                    _slotEquipItemData[x.Key] = x.NewValue;
                    equipmentItemList.RemoveItem(x.Key);
                    //equipmentItemList.SetItemList(_slotEquipItemData);
                })
                .AddTo(this);
            slotEquipItemData.ObserveReset()
                .Subscribe(x =>
                {
                    _slotEquipItemData.Clear();
                    equipmentItemList.Clear();
                    //equipmentItemList.SetItemList(_slotEquipItemData);
                })
                .AddTo(this);
            RefreshEquip(_slotEquipItemData);
        }

        private void RefreshEquip(IDictionary<int, EquipItemData> slotEquipItemData)
        {
            equipmentItemList.SetItemList(slotEquipItemData);
            InitializeEquipSlots();
        }

        public void BindBagItemData(ReactiveDictionary<int, BagItemData> bagItemData)
        {
            _bagSlotItems = new Dictionary<int, BagSlotItem>();
            _bagItemData = new Dictionary<int, BagItemData>();
            foreach (var key in bagItemData.Keys)
            {
                var slot = bagItemData[key];
                _bagItemData.Add(key, slot);
            }
            var dragImage = dragIcon.GetComponent<Image>();
            dragImage.raycastTarget = false;
            dragImage.transform.SetParent(transform.root, false);
            dragImage.gameObject.SetActive(false);
            RefreshBag(_bagItemData);
            InitializeSlots();
            bagItemData.ObserveAdd()
                .Subscribe(x =>
                {
                    if (!_bagItemData.ContainsKey(x.Key))
                    {
                        _bagItemData.Add(x.Key, x.Value);
                        bagItemList.AddItem(x.Key, x.Value);
                    }
                })
                .AddTo(this);
            bagItemData.ObserveRemove()
                .Subscribe(x =>
                {
                    if (_bagItemData.ContainsKey(x.Key))
                    {
                        _bagItemData.Remove(x.Key);
                        bagItemList.RemoveItem(x.Key);
                    }
                })
                .AddTo(this);
            bagItemData.ObserveReplace()
                .Subscribe(x =>
                {
                    if (!x.NewValue.Equals(x.OldValue))
                    {
                        _bagItemData[x.Key] = x.NewValue;
                        bagItemList.ReplaceItem(x.Key, x.NewValue);
                    }
                })
                .AddTo(this);
            bagItemData.ObserveReset()
                .Subscribe(x =>
                {
                    _bagItemData.Clear();
                    bagItemList.Clear();
                })
                .AddTo(this);
        }

        private void RefreshBag(IDictionary<int, BagItemData> bagItemData)
        {
            // for (var i = 0; i < BagCommonData.maxBagCount; i++)
            // {
            //     var originalData = bagItemData.FirstOrDefault(x => x.Index == i);
            //     var data = originalData.ItemName != null ? originalData : new BagItemData();
            //     _bagItemData.Add(data);
            // }
            bagItemList.SetItemList(bagItemData);
        }
        
        private void InitializeEquipSlots()
        {
            foreach (var key in equipmentItemList.ItemBases.Keys)
            {
                var slot = equipmentItemList.ItemBases[key] as EquipmentSlotItem;
                if (!slot) continue;
                slot.OnPointerClickObservable
                    .Subscribe(x => OnEquipClick(slot, x))
                    .AddTo(slot.gameObject);
                _slotItems.Add(key, slot);
            }
        }

        private void OnEquipClick(EquipmentSlotItem slot, PointerEventData pointerEventData)
        {
            if (slot.IsEmpty()) return;
            // 这里可以弹出物品详情面板
            Debug.Log($"显示物品详情: {slot.name}");
            _uiManager.SwitchUI<ItemDetailsScreenUI>(ui => ui.OpenBag(slot.CurrentItem.ToBagItemData(), ItemDetailsType.Equipment));
        }

        // 初始化格子
        private void InitializeSlots()
        {
            foreach (var key in bagItemList.ItemBases.Keys)
            {
                var item = bagItemList.ItemBases[key];
                var slot = item as BagSlotItem;
                if (!slot) continue;
                slot.OnBeginDragObserver
                    .Subscribe(x => OnSlotBeginDrag(slot, x))
                    .AddTo(slot.gameObject);
                slot.OnDragObserver
                    .Subscribe(x => OnSlotDrag(slot, x))
                    .AddTo(slot.gameObject);
                slot.OnEndDragObserver
                    .Subscribe(x => OnSlotEndDrag(slot, x))
                    .AddTo(slot.gameObject);
                slot.OnPointerClickObserver
                    .Subscribe(x => OnSlotClick(slot, x))
                    .AddTo(slot.gameObject);
                _bagSlotItems.Add(key, slot);
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
            _draggedSlot.gameObject.SetActive(true);
            var dragImage = dragIcon.GetComponent<Image>();
            dragImage.sprite = slot.CurrentItem.Icon;
            dragIcon.SetActive(true);
            dragIcon.transform.position = eventData.position;
        }

        // 处理拖拽结束
        private void OnSlotEndDrag(BagSlotItem sourceSlot, PointerEventData eventData)
        {
            //Destroy(_draggedSlot.gameObject);
            _draggedSlot.gameObject.SetActive(false);
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
                //Debug.Log($"显示物品详情: {slot.CurrentItem.ItemName}");
                // 这里可以在Inventory中统一控制UI面板
                _uiManager.SwitchUI<ItemDetailsScreenUI>(ui => ui.OpenBag(slot.CurrentItem));
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