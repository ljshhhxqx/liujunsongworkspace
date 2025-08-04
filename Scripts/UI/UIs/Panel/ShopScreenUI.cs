using System;
using System.Collections.Generic;
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
    public class ShopScreenUI : ScreenUIBase, IUnlockMouse
    {
        [SerializeField]
        private ContentItemList shopItemList;
        [SerializeField]
        private ContentItemList bagItemList;
        [SerializeField]
        private Button refreshButton;
        [SerializeField]
        private Button closeButton;
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
            
        }

        private void Start()
        {
            closeButton.OnClickAsObservable()
                .Subscribe(_ => _uiManager.CloseUI(Type))
                .AddTo(this);
            refreshButton.OnClickAsObservable()
                .Subscribe(_ => _refreshSubject.OnNext(Unit.Default))
                .AddTo(this);
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
            shopItemList.SetItemList(_shopItemData);
            InitShopItems();
            shopItemData.ObserveAdd().Subscribe(x =>
            {
                _shopItemData.Add(x.Key, x.Value);
                shopItemList.AddItem(x.Key, x.Value);
                //shopItemList.SetItemList(_shopItemData);
            }).AddTo(this);
            shopItemData.ObserveRemove().Subscribe(x =>
            {
                _shopItemData.Remove(x.Key);
                shopItemList.RemoveItem(x.Key);
            }).AddTo(this);
            shopItemData.ObserveReplace().Subscribe(x =>
            {
                if (x.OldValue.Equals(x.NewValue))
                    return;
                _shopItemData[x.Key] = x.NewValue;
                shopItemList.ReplaceItem(x.Key, x.NewValue);
            }).AddTo(this);
            shopItemData.ObserveReset().Subscribe(x =>
            {
                _shopItemData.Clear();
                shopItemList.Clear();
            }).AddTo(this);
        }

        public void BindBagItemData(ReactiveDictionary<int, BagItemData> bagItemData)
        {
            foreach (var keyValue in bagItemData)
            {
                _bagItemData.Add(keyValue.Key, keyValue.Value);
            }
            bagItemList.SetItemList(_bagItemData);
            InitBagItems();
            bagItemData.ObserveAdd().Subscribe(x =>
            {
                if (!_bagItemData.ContainsKey(x.Key))
                {
                    _bagItemData.Add(x.Key, x.Value);
                    bagItemList.AddItem(x.Key, x.Value);
                }
                //bagItemList.SetItemList(_bagItemData);
            }).AddTo(this);
            bagItemData.ObserveRemove().Subscribe(x =>
            {
                if (!_bagItemData.ContainsKey(x.Key))
                {
                    return;
                }
                _bagItemData.Remove(x.Key);
                bagItemList.RemoveItem(x.Key);
            }).AddTo(this);
            bagItemData.ObserveReplace().Subscribe(x =>
            {
                if (x.OldValue.Equals(x.NewValue))
                    return;
                _bagItemData[x.Key] = x.NewValue;
                bagItemList.ReplaceItem(x.Key, x.NewValue);
            }).AddTo(this);
            bagItemData.ObserveReset().Subscribe(x =>
            {
                _bagItemData.Clear();
                bagItemList.Clear();
            }).AddTo(this);
        }

        private void InitShopItems()
        {
            foreach (var key in shopItemList.ItemBases.Keys)
            {
                var slot = shopItemList.ItemBases[key] as ShopSlotItem;
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
            foreach (var key in bagItemList.ItemBases.Keys)
            {
                var slot = bagItemList.ItemBases[key] as ShopBagSlotItem;
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