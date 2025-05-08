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
        private ReactiveDictionary<int, PlayerHpItemData> _playerHpItemDatas;
        private FollowTargetParams _defaultFollowTargetParams;

        public void BindPlayersHp(ReactiveDictionary<int, PlayerHpItemData> playerHpItemDatas, FollowTargetParams defaultFollowTargetParams)
        {
            _defaultFollowTargetParams = new FollowTargetParams();
            _defaultFollowTargetParams = defaultFollowTargetParams; 
            _playerHpItemDatas = playerHpItemDatas;
            _playerHpItemDatas.ObserveAdd().Subscribe(x =>
            {
                SetItemDataAndShow(_playerHpItemDatas.Values.ToArray());
            }).AddTo(this);
            _playerHpItemDatas.ObserveRemove().Subscribe(x =>
            {
                SetItemDataAndShow(_playerHpItemDatas.Values.ToArray());
            }).AddTo(this); 
            _playerHpItemDatas.ObserveReplace().Subscribe(x =>
            {
                SetItemDataAndShow(_playerHpItemDatas.Values.ToArray());
            }).AddTo(this);
            _playerHpItemDatas.ObserveReset().Subscribe(_ =>
            {
                SetItemDataAndShow(Array.Empty<PlayerHpItemData>());
            }).AddTo(this);
        }

        private void SetItemDataAndShow(PlayerHpItemData[] playerHpItemDatas)
        {
            contentItemList.SetItemList(playerHpItemDatas);
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