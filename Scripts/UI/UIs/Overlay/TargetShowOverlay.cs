using System;
using System.Collections.Generic;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using DG.Tweening;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using Mirror;
using TMPro;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class TargetShowOverlay : ScreenUIBase
    {
        private readonly Dictionary<uint, Transform> _targets = new Dictionary<uint,Transform>(); // 要追踪的目标物品们
        private Transform _player; // 玩家角色
        [SerializeField] private RectTransform indicatorUI; // UI指示器
        [SerializeField] private TextMeshProUGUI distanceText; // 显示距离的Text组件
        [SerializeField] private RectTransform textUI; // UI指示器
        [SerializeField] private ContentItemList itemList;
        private FollowTargetParams _followTargetParams;
        private FollowTargetParams _followTextParams;
        private FollowTargetParams _followCollectItemParams;
        // private readonly Dictionary<uint, CollectItem> _collectItems = new Dictionary<uint, CollectItem>();
        private Camera _uiCamera;
        private Camera _mainCamera;
        private RectTransform _canvasRect;
        private bool IsTargetNotNull => _targets != null && _targets.Count > 0;

        [Inject]
        private void Init(GameEventManager gameEventManager, IConfigProvider configProvider)
        {
            gameEventManager.Subscribe<TargetShowEvent>(OnTargetShow);
            gameEventManager.Subscribe<FollowTargetTextEvent>(OnFollowTarget);
            //gameEventManager.Subscribe<SceneItemInfoChangedEvent>(OnSceneItemInfoChanged);
            var gameConfig = configProvider.GetConfig<JsonDataConfig>().GameConfig;

            Debug.Log("TargetShowOverlay Init");
            _mainCamera = Camera.main;
            _canvasRect = indicatorUI.parent.GetComponent<RectTransform>();
            _uiCamera = _canvasRect.GetComponentInParent<Canvas>().worldCamera;
            indicatorUI.gameObject.SetActive(IsTargetNotNull);
            _followTargetParams ??= new FollowTargetParams();
            _followTargetParams.MainCamera = _mainCamera;
            _followTargetParams.CanvasRect = _canvasRect;
            _followTargetParams.ScreenBorderOffset = gameConfig.screenBorderOffset;
            _followTargetParams.IndicatorUI = indicatorUI;
            _followTargetParams.DistanceText = distanceText;
            _followTargetParams.ShowBehindIndicator = true;
            _followTargetParams.CanvasCamera = _uiCamera;
            _followTextParams ??= new FollowTargetParams();
            _followTextParams.MainCamera = _mainCamera;
            _followTextParams.CanvasRect = _canvasRect;
            _followTextParams.ScreenBorderOffset = gameConfig.screenBorderOffset;
            _followTextParams.CanvasCamera = _uiCamera;
            _followCollectItemParams ??= new FollowTargetParams();
            _followCollectItemParams.MainCamera = _mainCamera;
            _followCollectItemParams.CanvasRect = _canvasRect;
            _followCollectItemParams.ScreenBorderOffset = gameConfig.screenBorderOffset;
            _followCollectItemParams.ShowBehindIndicator = true;
            _followCollectItemParams.CanvasCamera = _uiCamera;
        }

        // private void OnSceneItemInfoChanged(SceneItemInfoChangedEvent itemInfoChangedEvent)
        // {
        //     switch (itemInfoChangedEvent.Operation)
        //     {
        //         case SyncIDictionary<uint, SceneItemInfo>.Operation.OP_ADD:
        //         case SyncIDictionary<uint, SceneItemInfo>.Operation.OP_SET:
        //             var itemData = new CollectItemData();
        //             itemData.ItemId = itemInfoChangedEvent.ItemId;
        //             itemData.CurrentHp = itemInfoChangedEvent.SceneItemInfo.health;
        //             itemData.MaxHp = itemInfoChangedEvent.SceneItemInfo.maxHealth;
        //             itemData.Position = itemInfoChangedEvent.SceneItemInfo.Position;
        //             itemData.PlayerPosition = PlayerInGameManager.Instance.GetLocalPlayerPosition();
        //             var collectItem =  itemList.ReplaceItem<CollectItemData, CollectItem>((int)itemInfoChangedEvent.ItemId, itemData);
        //             _followCollectItemParams.IndicatorUI = collectItem.rectTransform;
        //             collectItem.Show(_followCollectItemParams);
        //             break;
        //         case SyncIDictionary<uint, SceneItemInfo>.Operation.OP_CLEAR:
        //             itemList.Clear();
        //             break;
        //         case SyncIDictionary<uint, SceneItemInfo>.Operation.OP_REMOVE:
        //             itemList.RemoveItem((int)itemInfoChangedEvent.ItemId);
        //             break;
        //     }
        // }

        private void OnFollowTarget(FollowTargetTextEvent followTargetTextEvent)
        {
            _followTextParams.Target = followTargetTextEvent.Position;
            var go = GameObjectPoolManger.Instance.GetObject(textUI.gameObject);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = followTargetTextEvent.Text;
            _followTextParams.IndicatorUI = go.GetComponent<RectTransform>();
            var canvasGroup = go.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            GameStaticExtensions.FollowTarget(_followTextParams);
            var seq = DOTween.Sequence();
            seq.Append(go.transform.DOLocalMoveY(100, 1f));
            seq.Join(go.transform.DOScale(1.3f, 1f));
            seq.AppendInterval(1f);
            seq.Append(go.transform.DOLocalMoveY(100, 1f));
            seq.Join(canvasGroup.DOFade(0f, 1f));
            seq.onComplete += () => GameObjectPoolManger.Instance.ReturnObject(go);
        }

        private void OnTargetShow(TargetShowEvent targetShowEvent)
        {
            if (!indicatorUI)
            {
                return;
            }

            if (!_targets.ContainsKey(targetShowEvent.TargetId) && targetShowEvent.Target)
            {
                _targets.Add(targetShowEvent.TargetId, targetShowEvent.Target);
            }
            else if (_targets.ContainsKey(targetShowEvent.TargetId) && !targetShowEvent.Target)
            {
                _targets.Remove(targetShowEvent.TargetId);
            }
            indicatorUI.gameObject.SetActive(_targets != null && _targets.Count > 0);
            if (!targetShowEvent.Player)
            {
                return;
            }
            _player ??= targetShowEvent.Player;
            if (!_player)
            {
                Debug.LogError("Player not found!");
            }
        }

        private void LateUpdate()
        {
            if (!IsTargetNotNull || !_player) return;
            
            foreach (var target in _targets.Values)
            {
                if (!target) continue;
                _followTargetParams.Target = target.position;
                _followTargetParams.Player = _player.position;
                GameStaticExtensions.FollowTarget(_followTargetParams);
            }
        }

        public override UIType Type => UIType.TargetShowOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}
