using System.Collections.Generic;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using DG.Tweening;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Tool.GameEvent;
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
        private FollowTargetParams _followTargetParams;
        private FollowTargetParams _followTextParams;
        
        private Camera _mainCamera;
        private RectTransform _canvasRect;
        private bool IsTargetNotNull => _targets != null && _targets.Count > 0;

        [Inject]
        private void Init(GameEventManager gameEventManager, IConfigProvider configProvider)
        {
            gameEventManager.Subscribe<TargetShowEvent>(OnTargetShow);
            gameEventManager.Subscribe<FollowTargetTextEvent>(OnFollowTarget);
            var gameConfig = configProvider.GetConfig<JsonDataConfig>().GameConfig;

            Debug.Log("TargetShowOverlay Init");
            _mainCamera = Camera.main;
            _canvasRect = indicatorUI.parent.GetComponent<RectTransform>();
            indicatorUI.gameObject.SetActive(IsTargetNotNull);
            _followTargetParams ??= new FollowTargetParams();
            _followTargetParams.MainCamera = _mainCamera;
            _followTargetParams.CanvasRect = _canvasRect;
            _followTargetParams.ScreenBorderOffset = gameConfig.screenBorderOffset;
            _followTargetParams.IndicatorUI = indicatorUI;
            _followTargetParams.DistanceText = distanceText;
            _followTextParams ??= new FollowTargetParams();
            _followTextParams.MainCamera = _mainCamera;
            _followTextParams.CanvasRect = _canvasRect;
            _followTextParams.ScreenBorderOffset = gameConfig.screenBorderOffset;
        }

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
            if (!_targets.ContainsKey(targetShowEvent.TargetId) && targetShowEvent.Target)
            {
                _targets.Add(targetShowEvent.TargetId, targetShowEvent.Target);
            }
            else if (_targets.ContainsKey(targetShowEvent.TargetId) && !targetShowEvent.Target)
            {
                _targets.Remove(targetShowEvent.TargetId);
            }
            indicatorUI?.gameObject.SetActive(_targets != null && _targets.Count > 0);
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
