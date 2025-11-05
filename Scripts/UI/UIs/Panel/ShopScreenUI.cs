using System;
using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using HotUpdate.Scripts.UI.UIs.Popup;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using TMPro;
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
        [SerializeField]
        private TextMeshProUGUI goldText;

        private string _goldDesc;
        private UIManager _uiManager;
        private readonly List<ShopSlotItem> _shopSlotItems = new List<ShopSlotItem>();
        private readonly List<ShopBagSlotItem> _bagSlotItems = new List<ShopBagSlotItem>();
        private readonly Dictionary<int, RandomShopItemData> _shopItemData = new Dictionary<int, RandomShopItemData>();
        private readonly Dictionary<int, BagItemData> _bagItemData = new Dictionary<int, BagItemData>();
        private HReactiveProperty<ValuePropertyData> _goldObservable;
        private Subject<Unit> _refreshSubject = new Subject<Unit>();
        public IObservable<Unit> OnRefresh => _refreshSubject;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            _goldDesc = goldText.text;
            closeButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => _uiManager.CloseUI(Type))
                .AddTo(this);
            refreshButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => _refreshSubject.OnNext(Unit.Default))
                .AddTo(this);
        }

        public void BindPlayerGold(HReactiveProperty<ValuePropertyData> playerGold)
        {
            _goldObservable = playerGold;
            _goldObservable.Subscribe(x =>
            {
                goldText.text = _goldDesc + x.Gold.ToString("0");
            }).AddTo(this);
        }

        public void BindShopItemData(HReactiveDictionary<int, RandomShopItemData> shopItemData)
        {
            foreach (var keyValue in shopItemData)
            {
                _shopItemData.Add(keyValue.Key, keyValue.Value);
            }
            shopItemList.SetItemList(_shopItemData);
            InitShopItems();
            shopItemData.ObserveAdd((x, y) =>
            {
                if (!_shopItemData.ContainsKey(x))
                {
                    _shopItemData.Add(x, y);
                    shopItemList.AddItem<RandomShopItemData, ShopSlotItem>(x, y, OnSpawnShopItem);
                }
                //shopItemList.SetItemList(_shopItemData);
            }).AddTo(this);
            shopItemData.ObserveRemove((x, y) =>
            {
                if (_shopItemData.ContainsKey(x))
                {
                    _shopItemData.Remove(x);
                    shopItemList.RemoveItem(x);
                }
            }).AddTo(this);
            shopItemData.ObserveUpdate((x,y,z) => 
            {
                if (y.Equals(z))
                    return;
                _shopItemData[x] = z;
                shopItemList.ReplaceItem<RandomShopItemData, ShopSlotItem>(x, z);
            }).AddTo(this);
            shopItemData.ObserveClear(x =>
            {
                _shopItemData.Clear();
                shopItemList.Clear();
            }).AddTo(this);
        }

        public void BindBagItemData(HReactiveDictionary<int, BagItemData> bagItemData)
        {
            foreach (var keyValue in bagItemData)
            {
                _bagItemData.Add(keyValue.Key, keyValue.Value);
            }
            bagItemList.SetItemList(_bagItemData);
            InitBagItems();
            bagItemData.ObserveAdd((x, y) =>
            {
                if (!_bagItemData.ContainsKey(x))
                {
                    _bagItemData.Add(x, y);
                    bagItemList.AddItem<BagItemData, ShopBagSlotItem>(x, y, OnSpawnBagItem);
                }
                //bagItemList.SetItemList(_bagItemData);
            }).AddTo(this);
            bagItemData.ObserveRemove((x,y) =>
            {
                if (!_bagItemData.ContainsKey(x))
                {
                    return;
                }
                _bagItemData.Remove(x);
                bagItemList.RemoveItem(x);
            }).AddTo(this);
            bagItemData.ObserveUpdate((x,y,z) =>
            {
                if (y.Equals(z))
                    return;
                _bagItemData[x] = z;
                bagItemList.ReplaceItem<BagItemData, ShopBagSlotItem>(x, z);
            }).AddTo(this);
            bagItemData.ObserveClear(x =>
            {
                _bagItemData.Clear();
                bagItemList.Clear();
            }).AddTo(this);
        }

        private void OnSpawnShopItem(RandomShopItemData shopItem, ShopSlotItem slot)
        {
            slot.OnBuy.Subscribe(count =>
            {
                _uiManager.ShowTips($"确定购买{count}个{slot.ShopItemData.Name}吗？", () =>
                {
                    OnBuyItem(slot, count);
                });
            }).AddTo(slot.gameObject);
            slot.OnClick.Subscribe(_ =>
            {
                OnShowItemInfo(slot);
            }).AddTo(slot.gameObject);
        }

        private void OnSpawnBagItem(BagItemData bagItem, ShopBagSlotItem slot)
        {
            slot.OnSellObservable.Subscribe(count =>
            {
                _uiManager.ShowTips($"确定出售{count}个{bagItem.ItemName}吗？", () =>
                {
                    OnSellItem(slot, count);
                });
            }).AddTo(slot.gameObject);
            slot.OnClickObservable.Subscribe(_ =>
            {
                OnShowItemInfo(slot);
            }).AddTo(slot.gameObject);
            slot.OnLockObservable.Subscribe(locked =>
            {
                OnLockItem(slot, locked);
            }).AddTo(slot.gameObject);
        }

        private void InitShopItems()
        {
            foreach (var key in shopItemList.ItemBases.Keys)
            {
                var slot = shopItemList.ItemBases[key] as ShopSlotItem;
                if (!slot) continue;
                OnSpawnShopItem(slot.ShopItemData, slot);
                _shopSlotItems.Add(slot);
            }
        }

        private void InitBagItems()
        {
            foreach (var key in bagItemList.ItemBases.Keys)
            {
                var slot = bagItemList.ItemBases[key] as ShopBagSlotItem;
                if (!slot) continue;
                OnSpawnBagItem(slot.CurrentItem, slot);
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