using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Static;
using TMPro;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class TargetShowOverlay : ScreenUIBase
    {
        private readonly List<Transform> _targets = new List<Transform>(); // 要追踪的目标物品们
        private Transform _player; // 玩家角色
        [SerializeField] private RectTransform indicatorUI; // UI指示器
        [SerializeField] private TextMeshProUGUI distanceText; // 显示距离的Text组件
        private FollowTargetParams _followTargetParams;
        
        private Camera _mainCamera;
        private RectTransform _canvasRect;
        private bool IsTargetNotNull => _targets != null && _targets.Count > 0;

        [Inject]
        private void Init(GameEventManager gameEventManager, ConfigProvider configProvider)
        {
            gameEventManager.Subscribe<TargetShowEvent>(OnTargetShow);
            var gameConfig = configProvider.GetConfig<JsonDataConfig>().GameConfig;

            _mainCamera = Camera.main;
            _canvasRect = indicatorUI.parent.GetComponent<RectTransform>();
            indicatorUI.gameObject.SetActive(IsTargetNotNull);
            _followTargetParams??= new FollowTargetParams();
            _followTargetParams.MainCamera = _mainCamera;
            _followTargetParams.CanvasRect = _canvasRect;
            _followTargetParams.ScreenBorderOffset = gameConfig.screenBorderOffset;
            _followTargetParams.IndicatorUI = indicatorUI;
            _followTargetParams.DistanceText = distanceText;
        }

        private void OnTargetShow(TargetShowEvent targetShowEvent)
        {
            if (targetShowEvent.Target)
            {
                _targets.Add(targetShowEvent.Target);
            }
            indicatorUI.gameObject.SetActive(IsTargetNotNull);
            _player ??= targetShowEvent.Player;
            if (!_player)
            {
                Debug.LogError("Player not found!");
            }
        }

        private void LateUpdate()
        {
            if (!IsTargetNotNull || !_player) return;

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                _followTargetParams.Target = target.position;
                _followTargetParams.Player = _player.position;
                GameStaticExtensions.FollowTarget(_followTargetParams);
            }
        }

        public override UIType Type => UIType.TargetShowOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}
