using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UI.UIBase;
using UniRx;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class ShopScreenUI : ScreenUIBase
    {
        public override UIType Type => UIType.Shop;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        public void BindShopItemData(ReactiveDictionary<int, RandomShopItemData> getReactiveDictionary)
        {
            
        }

        public void BindBagItemData(ReactiveDictionary<int, BagItemData> getReactiveDictionary)
        {
            
        }
    }
}