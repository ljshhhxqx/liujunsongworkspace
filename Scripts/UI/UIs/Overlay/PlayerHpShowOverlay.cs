using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Tool.Static;
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

        [SerializeField] 
        private RectTransform canvasRect;
        private Dictionary<int, PlayerHpItemData> _playerHpItemDatas;
        private FollowTargetParams _defaultFollowTargetParams;

        public void BindPlayersHp(ReactiveDictionary<int, PlayerHpItemData> playerHpItemDatas, FollowTargetParams defaultFollowTargetParams)
        {
            _defaultFollowTargetParams = defaultFollowTargetParams; 
            _defaultFollowTargetParams.CanvasRect = canvasRect;
            _playerHpItemDatas = new Dictionary<int, PlayerHpItemData>();
            foreach (var key in playerHpItemDatas.Keys)
            {
                var data = playerHpItemDatas[key];
                _playerHpItemDatas.Add(key, data);
            }

            playerHpItemDatas.ObserveAdd().Subscribe(x =>
            {
                _playerHpItemDatas.Add(x.Key, x.Value);
                contentItemList.AddItem(x.Key, x.Value);
                SetItemDataAndShow(_playerHpItemDatas);
            }).AddTo(this);
            playerHpItemDatas.ObserveRemove().Subscribe(x =>
            {
                _playerHpItemDatas.Remove(x.Key);
                contentItemList.RemoveItem(x.Key);
                SetItemDataAndShow(_playerHpItemDatas);
            }).AddTo(this); 
            playerHpItemDatas.ObserveReplace().Subscribe(x =>
            {
                _playerHpItemDatas[x.Key] = x.NewValue;
                contentItemList.ReplaceItem(x.Key, x.NewValue);
                var item = contentItemList.GetItem<PlayerHpItem>(x.Key);
                item.DataChanged(x.NewValue);
                item.Show(_defaultFollowTargetParams);
            }).AddTo(this);
            playerHpItemDatas.ObserveReset().Subscribe(_ =>
            {
                _playerHpItemDatas.Clear();
                contentItemList.Clear();
                SetItemDataAndShow(_playerHpItemDatas);
            }).AddTo(this);
        }

        private void SetItemDataAndShow(IDictionary<int, PlayerHpItemData> playerHpItemDatas)
        {
            //contentItemList.SetItemList(playerHpItemDatas);
            Show();
        }

        public void Show()
        {
            for (int i = 0; i < contentItemList.ItemBases.Count; i++)
            {
                var item = contentItemList.ItemBases[i];
                if (item is not PlayerHpItem playerHpItem)
                {
                    Debug.LogError($"PlayerHpItem {item.name} is not a PlayerHpItem");
                    continue;
                }
                playerHpItem.Show(_defaultFollowTargetParams);
            }
        }

        public override UIType Type => UIType.PlayerHpShowOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}