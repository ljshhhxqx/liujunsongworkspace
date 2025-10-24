using System.Collections.Generic;
using AOTScripts.Data;
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

        public void BindPlayerAnimationData(ReactiveDictionary<int, AnimationStateData> playerAnimationDatas)
        {
            _playerAnimiationDatas = new Dictionary<int, AnimationStateData>();
            foreach (var (key, animationStateData) in playerAnimationDatas)
            {
                _playerAnimiationDatas.Add(key, animationStateData);
            }
            contentItemList.SetItemList(_playerAnimiationDatas);

            playerAnimationDatas.ObserveAdd()
                .Subscribe(addEvent =>
                {
                    if (_playerAnimiationDatas.ContainsKey(addEvent.Key))
                    {
                        return;
                    }

                    _playerAnimiationDatas.Add(addEvent.Key, addEvent.Value);
                    contentItemList.AddItem<AnimationStateData, AnimationItem>(addEvent.Key, addEvent.Value);
                })
                .AddTo(this);
            playerAnimationDatas.ObserveReplace()
                .Subscribe(replaceEvent =>
                {
                    if (!replaceEvent.OldValue.Equals(replaceEvent.NewValue))
                    {
                        if (_playerAnimiationDatas.ContainsKey(replaceEvent.Key))
                        {
                            _playerAnimiationDatas[replaceEvent.Key] = replaceEvent.NewValue;
                            contentItemList.ReplaceItem<AnimationStateData, AnimationItem>(replaceEvent.Key, replaceEvent.NewValue);
                        }
                    }
                })
                .AddTo(this);
            playerAnimationDatas.ObserveRemove()
                .Subscribe(removeEvent =>
                {
                    if (_playerAnimiationDatas.ContainsKey(removeEvent.Key))
                    {
                        _playerAnimiationDatas.Remove(removeEvent.Key);
                        contentItemList.RemoveItem(removeEvent.Key);
                    }
                })
                .AddTo(this);
            playerAnimationDatas.ObserveReset()
                .Subscribe(_ =>
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