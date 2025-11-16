using System.Collections.Generic;
using AOTScripts.Tool;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;

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

        private GameEventManager _gameEventManager;
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<FollowTargetTextEvent>(OnFollowTargetEvent);
            
        }

        private void OnFollowTargetEvent(FollowTargetTextEvent followTargetTextEvent)
        {
            //contentItemList.AddItem<PlayerHpItemData, PlayerHpItem>(100, y);
        }

        public void BindPlayersHp(HReactiveDictionary<int, PlayerHpItemData> playerHpItemDatas, FollowTargetParams defaultFollowTargetParams)
        {
            _defaultFollowTargetParams = defaultFollowTargetParams; 
            _defaultFollowTargetParams.CanvasRect = canvasRect;
            _playerHpItemDatas = new Dictionary<int, PlayerHpItemData>();
            foreach (var key in playerHpItemDatas.Keys)
            {
                var data = playerHpItemDatas[key];
                _playerHpItemDatas.Add(key, data);
            }

            playerHpItemDatas.ObserveAdd((x,y) =>
            {
                if (_playerHpItemDatas.ContainsKey(x))
                {
                    return;
                }
                _playerHpItemDatas.Add(x, y);
                contentItemList.AddItem<PlayerHpItemData, PlayerHpItem>(x, y);
                SetItemDataAndShow(_playerHpItemDatas);
            }).AddTo(this);
            playerHpItemDatas.ObserveRemove((x,y) =>
            {
                if (!_playerHpItemDatas.ContainsKey(x))
                    return;
                _playerHpItemDatas.Remove(x);
                contentItemList.RemoveItem(x);
            }).AddTo(this); 
            playerHpItemDatas.ObserveUpdate((x,y, z)  =>
            {
                if (!z.Equals(default) && !z.Equals(y))
                {
                    _playerHpItemDatas[x] = z;
                    contentItemList.ReplaceItem<PlayerHpItemData, PlayerHpItem>(x, z);
                    var item = contentItemList.GetItem<PlayerHpItem>(x);
                    _defaultFollowTargetParams.Target = z.TargetPosition;
                    _defaultFollowTargetParams.Player = z.PlayerPosition;
                    item.Show(_defaultFollowTargetParams);
                    item.DataChanged(z);
                }
            }).AddTo(this);
            playerHpItemDatas.ObserveClear(_ =>
            {
                _playerHpItemDatas.Clear();
                contentItemList.Clear();
                //SetItemDataAndShow(_playerHpItemDatas);
            }).AddTo(this);
        }

        private void SetItemDataAndShow(IDictionary<int, PlayerHpItemData> playerHpItemDatas)
        {
            //contentItemList.SetItemList(playerHpItemDatas);
            Show();
        }

        public void Show()
        {
            foreach (var keyValuePair in contentItemList.ItemBases)
            {
                if (keyValuePair.Value is not PlayerHpItem playerHpItem)
                {
                    Debug.LogError($"PlayerHpItem {keyValuePair.Value.name} is not a PlayerHpItem");
                    continue;
                }
                playerHpItem.Show(_defaultFollowTargetParams);
            }
        }

        public override UIType Type => UIType.PlayerHpShowOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}