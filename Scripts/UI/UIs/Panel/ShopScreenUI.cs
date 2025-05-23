﻿using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class ShopScreenUI : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList shopItemList;
        [SerializeField]
        private ContentItemList bagItemList;
        [SerializeField]
        private Button refreshButton;
        private UIManager _uiManager;
        private readonly List<ShopSlotItem> _shopSlotItems = new List<ShopSlotItem>();
        private readonly List<ShopBagSlotItem> _bagSlotItems = new List<ShopBagSlotItem>();
        private readonly Dictionary<int, RandomShopItemData> _shopItemData = new Dictionary<int, RandomShopItemData>();
        private readonly Dictionary<int, BagItemData> _bagItemData = new Dictionary<int, BagItemData>();
        private IObservable<ValuePropertyData> _goldObservable;
        private Subject<Unit> _refreshSubject = new Subject<Unit>();
        public IObservable<Unit> OnRefresh => _refreshSubject;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.BindDebouncedListener(() =>
            {
                _refreshSubject.OnNext(Unit.Default);
            });
        }

        public void BindPlayerGold(IObservable<ValuePropertyData> playerGold)
        {
            _goldObservable = playerGold;
        }

        public void BindShopItemData(ReactiveDictionary<int, RandomShopItemData> shopItemData)
        {
            foreach (var keyValue in shopItemData)
            {
                _shopItemData.Add(keyValue.Key, keyValue.Value);
            }
            shopItemList.SetItemList(_shopItemData.Values.ToArray());
            InitShopItems();
            shopItemData.ObserveAdd().Subscribe(x =>
            {
                _shopItemData.Add(x.Key, x.Value);
                shopItemList.SetItemList(_shopItemData.Values.ToArray());
            }).AddTo(this);
            shopItemData.ObserveRemove().Subscribe(x =>
            {
                _shopItemData.Remove(x.Key);
                shopItemList.SetItemList(_shopItemData.Values.ToArray());
            }).AddTo(this);
            shopItemData.ObserveReplace().Subscribe(x =>
            {
                _shopItemData[x.Key] = x.NewValue;
                shopItemList.SetItemList(_shopItemData.Values.ToArray());
            }).AddTo(this);
            shopItemData.ObserveReset().Subscribe(x =>
            {
                _shopItemData.Clear();
                shopItemList.SetItemList(Array.Empty<RandomShopItemData>());
            }).AddTo(this);
        }

        public void BindBagItemData(ReactiveDictionary<int, BagItemData> bagItemData)
        {
            foreach (var keyValue in bagItemData)
            {
                _bagItemData.Add(keyValue.Key, keyValue.Value);
            }
            bagItemList.SetItemList(_bagItemData.Values.ToArray());
            InitBagItems();
            bagItemData.ObserveAdd().Subscribe(x =>
            {
                _bagItemData.Add(x.Key, x.Value);
                bagItemList.SetItemList(_bagItemData.Values.ToArray());
            }).AddTo(this);
            bagItemData.ObserveRemove().Subscribe(x =>
            {
                _bagItemData.Remove(x.Key);
                bagItemList.SetItemList(_bagItemData.Values.ToArray());
            }).AddTo(this);
            bagItemData.ObserveReplace().Subscribe(x =>
            {
                _bagItemData[x.Key] = x.NewValue;
                bagItemList.SetItemList(_bagItemData.Values.ToArray());
            }).AddTo(this);
            bagItemData.ObserveReset().Subscribe(x =>
            {
                _bagItemData.Clear();
                bagItemList.SetItemList(Array.Empty<BagItemData>());
            }).AddTo(this);
        }

        private void InitShopItems()
        {
            foreach (var shopSlotItem in shopItemList.ItemBases)
            {
                var slot = shopSlotItem as ShopSlotItem;
                if (!slot) continue;
                slot.OnBuy.Subscribe(count =>
                {
                    OnBuyItem(slot, count);
                }).AddTo(slot.gameObject);
                slot.OnClick.Subscribe(_ =>
                {
                    OnShowItemInfo(slot);
                }).AddTo(slot.gameObject);
                _shopSlotItems.Add(slot);
            }
        }

        private void InitBagItems()
        {
            foreach (var bagSlotItem in bagItemList.ItemBases)
            {
                var slot = bagSlotItem as ShopBagSlotItem;
                if (!slot) continue;
                slot.OnSellObservable.Subscribe(count =>
                {
                    OnSellItem(slot, count);
                }).AddTo(slot.gameObject);
                slot.OnClickObservable.Subscribe(_ =>
                {
                    OnShowItemInfo(slot);
                }).AddTo(slot.gameObject);
                slot.OnLockObservable.Subscribe(locked =>
                {
                    OnLockItem(slot, locked);
                }).AddTo(slot.gameObject);
                _bagSlotItems.Add(slot);
            }
        }

        private void OnShowItemInfo(ShopSlotItem shopSlotItem)
        {
            var itemData = shopSlotItem.ShopItemData;
            _uiManager.SwitchUI<ItemDetailsScreenUI>(onShow: x =>
            {
                x.BindPlayerGold(_goldObservable);
                x.OpenShop(itemData);
            });
        }
        
        private void OnShowItemInfo(ShopBagSlotItem shopBagSlotItem)
        {
            var itemData = shopBagSlotItem.CurrentItem;
            _uiManager.SwitchUI<ItemDetailsScreenUI>(onShow: x =>
            {
                x.BindPlayerGold(_goldObservable);
                x.OpenBag(itemData);
            });
        }

        private void OnBuyItem(ShopSlotItem shopSlotItem, int count = 1)
        {
            var itemData = shopSlotItem.ShopItemData;
            itemData.OnBuyItem?.Invoke(itemData.ShopId, count);
        }
        
        private void OnSellItem(ShopBagSlotItem shopBagSlotItem, int count = 1)
        {
            var itemData = shopBagSlotItem.CurrentItem;
            itemData.OnSellItem?.Invoke(itemData.Index, count);
        }

        private void OnLockItem(ShopBagSlotItem shopBagSlotItem, bool locked)
        {
            var itemData = shopBagSlotItem.CurrentItem;
            itemData.OnLockItem?.Invoke(itemData.Index, locked);
        }

        public override UIType Type => UIType.Shop;
        public override UICanvasType CanvasType => UICanvasType.Panel;
    }
}