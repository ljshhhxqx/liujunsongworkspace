using System.Collections.Generic;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class ShopScreenUI : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList shopItemList;
        [SerializeField]
        private ContentItemList bagItemList;
        private UIManager _uiManager;
        private List<ShopSlotItem> _shopSlotItems = new List<ShopSlotItem>();
        private List<BagSlotItem> _bagSlotItems = new List<BagSlotItem>();
        private Dictionary<int, RandomShopItemData> _shopItemData = new Dictionary<int, RandomShopItemData>();
        private Dictionary<int, BagItemData> _bagItemData = new Dictionary<int, BagItemData>();

        public void BindShopItemData(ReactiveDictionary<int, RandomShopItemData> shopItemData)
        {
            shopItemData.ObserveAdd().Subscribe(x =>
            {
                
            }).AddTo(this);
            shopItemData.ObserveRemove().Subscribe(x =>
            {
            }).AddTo(this);
            shopItemData.ObserveReplace().Subscribe(x =>
            {
            }).AddTo(this);
            shopItemData.ObserveReset().Subscribe(x =>
            {
                
            }).AddTo(this);
        }

        public void BindBagItemData(ReactiveDictionary<int, BagItemData> bagItemData)
        {
            bagItemData.ObserveAdd().Subscribe(x =>
            {
                
            }).AddTo(this);
            bagItemData.ObserveRemove().Subscribe(x =>
            {
            }).AddTo(this);
            bagItemData.ObserveReplace().Subscribe(x =>
            {
            }).AddTo(this);
            bagItemData.ObserveReset().Subscribe(x =>
            {
            }).AddTo(this);
        }
        
        public override UIType Type => UIType.Shop;
        public override UICanvasType CanvasType => UICanvasType.Panel;
    }
}