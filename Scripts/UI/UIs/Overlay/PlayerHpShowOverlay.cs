using System;
using System.Linq;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerHpShowOverlay : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList contentItemList;
        private ReactiveDictionary<int, PlayerHpItemData> _playerHpItemDatas;

        public void BindPlayersHp(ReactiveDictionary<int, PlayerHpItemData> playerHpItemDatas)
        {
            _playerHpItemDatas = playerHpItemDatas;
            _playerHpItemDatas.ObserveAdd().Subscribe(x =>
            {
                contentItemList.SetItemList(_playerHpItemDatas.Values.ToArray());
            }).AddTo(this);
            _playerHpItemDatas.ObserveRemove().Subscribe(x =>
            {
                contentItemList.SetItemList(_playerHpItemDatas.Values.ToArray());
            }).AddTo(this); 
            _playerHpItemDatas.ObserveReplace().Subscribe(x =>
            {
                contentItemList.SetItemList(_playerHpItemDatas.Values.ToArray());
            }).AddTo(this);
            _playerHpItemDatas.ObserveReset().Subscribe(_ =>
            {
                contentItemList.SetItemList(Array.Empty<PlayerHpItemData>());
            }).AddTo(this);
        }

        public void Show()
        {
            for (int i = 0; i < contentItemList.ItemBases.Count; i++)
            {
                var item = contentItemList.ItemBases[i];
            }
        }

        public override UIType Type => UIType.PlayerHpShowOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}