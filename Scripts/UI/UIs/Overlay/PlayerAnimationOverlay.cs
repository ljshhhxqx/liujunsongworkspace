using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
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
        [SerializeField]
        private ProgressItem progressItem;
        private Sequence _progressTween;
        private Dictionary<int, AnimationStateData> _playerAnimiationDatas;
        public override bool IsGameUI => true;

        public void BindPlayerAnimationData(HReactiveDictionary<int, AnimationStateData> playerAnimationDatas)
        {
            progressItem.transform.localScale = Vector3.zero;
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
        
        public void StartProgress(string description, float countdown, Action onComplete = null, Func<bool> condition = null)
        {
            if (countdown <= 0)
            {
                onComplete?.Invoke();
                //progressItem.SetProgress(description, countdown, onComplete, condition);
                return;
            }
            Debug.Log("[PlayerPropertiesOverlay] StartProgress: " + description + " " + countdown);
            progressItem.SetProgress(description, countdown, onComplete, condition);

        }
    }
}