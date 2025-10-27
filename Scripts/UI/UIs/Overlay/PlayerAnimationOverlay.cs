using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerAnimationOverlay : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList contentItemList;
        private Dictionary<int, AnimationStateData> _playerAnimiationDatas;

        public void BindPlayerAnimationData(HReactiveDictionary<int, AnimationStateData> playerAnimationDatas)
        {
            _playerAnimiationDatas = new Dictionary<int, AnimationStateData>();
            foreach (var (key, animationStateData) in playerAnimationDatas)
            {
                _playerAnimiationDatas.Add(key, animationStateData);
            }
            contentItemList.SetItemList(_playerAnimiationDatas);

            playerAnimationDatas.ObserveAdd((x,y) =>
                {
                    if (_playerAnimiationDatas.ContainsKey(x))
                    {
                        return;
                    }

                    _playerAnimiationDatas.Add(x, y);
                    contentItemList.AddItem<AnimationStateData, AnimationItem>(x, y);
                })
                .AddTo(this);
            playerAnimationDatas.ObserveUpdate((x, y, z) =>
                {
                    if (!y.Equals(z))
                    {
                        if (_playerAnimiationDatas.ContainsKey(x))
                        {
                            _playerAnimiationDatas[x] = z;
                            contentItemList.ReplaceItem<AnimationStateData, AnimationItem>(x, z);
                        }
                    }
                })
                .AddTo(this);
            playerAnimationDatas.ObserveRemove((x, y) =>
                {
                    if (_playerAnimiationDatas.ContainsKey(x))
                    {
                        _playerAnimiationDatas.Remove(x);
                        contentItemList.RemoveItem(x);
                    }
                })
                .AddTo(this);
            playerAnimationDatas.ObserveClear(_ =>
                {
                    _playerAnimiationDatas.Clear();
                    contentItemList.Clear();
                })
                .AddTo(this);
        }

        public override UIType Type => UIType.PlayerAnimationOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}