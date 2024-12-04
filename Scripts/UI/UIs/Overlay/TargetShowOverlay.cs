using TMPro;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class TargetShowOverlay : ScreenUIBase
    {
        private Transform _target; // 要追踪的目标物品
        private Transform _player; // 玩家角色
        [SerializeField] private RectTransform indicatorUI; // UI指示器
        [SerializeField] private TextMeshProUGUI distanceText; // 显示距离的Text组件
        [SerializeField] private float screenBorderOffset = 50f; // 距离屏幕边界的偏移量
        
        private Camera _mainCamera;
        private RectTransform _canvasRect;

        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            gameEventManager.Subscribe<TargetShowEvent>(OnTargetShow);

            _mainCamera = Camera.main;
            _canvasRect = indicatorUI.parent.GetComponent<RectTransform>();
            indicatorUI.gameObject.SetActive(_target);
        }

        private void OnTargetShow(TargetShowEvent targetShowEvent)
        {
            _target = targetShowEvent.Target ? targetShowEvent.Target.transform : null;
            indicatorUI.gameObject.SetActive(_target);
            _player ??= GameObject.FindGameObjectWithTag("Player").transform;
            if (_player == null)
            {
                Debug.LogError("Player not found!");
            }
        }

        private void Update()
        {
            if (!_target || !_player) return;

            // 计算距离
            var distance = Vector3.Distance(_player.position, _target.position);
            distanceText.text = $"{distance:F1}m";

            // 将目标世界坐标转换为屏幕坐标
            var screenPos = _mainCamera.WorldToScreenPoint(_target.position);

            // 检查目标是否在相机前方
            var isBehind = screenPos.z < 0;
            screenPos.z = 0;

            // 如果目标在相机后方，将指示器翻转到屏幕另一边
            if (isBehind)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            // 将屏幕坐标转换为Canvas坐标
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPos, null, out var localPos);
            
            // 确保指示器在屏幕边界内
            var clampedPos = ClampToScreen(localPos);
            indicatorUI.localPosition = clampedPos;

            // 计算指示器的旋转（指向目标方向）
            var direction = localPos - (Vector2)indicatorUI.localPosition;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            indicatorUI.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private Vector2 ClampToScreen(Vector2 position)
        {
            // 获取Canvas的一半大小
            var halfSize = _canvasRect.rect.size * 0.5f;
            halfSize -= new Vector2(screenBorderOffset, screenBorderOffset);

            // 限制位置在屏幕边界内
            var clampedX = Mathf.Clamp(position.x, -halfSize.x, halfSize.x);
            var clampedY = Mathf.Clamp(position.y, -halfSize.y, halfSize.y);

            return new Vector2(clampedX, clampedY);
        }

        public override UIType Type => UIType.TargetShowOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}
