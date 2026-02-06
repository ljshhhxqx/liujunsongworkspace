using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Joystick Components")]
        [SerializeField]
        private RectTransform joystickRoot; // 新增：摇杆的逻辑根节点（用于计算）
        [SerializeField]
        private RectTransform background;
        [SerializeField]
        private RectTransform handle;
        [SerializeField]
        private CanvasGroup visualGroup; // 控制 background 和 handle 的显示

        [Header("Settings")]
        [SerializeField]
        private float handleRange = 1f;
        [SerializeField]
        private float deadZone = 0.2f;
        [SerializeField]
        private bool snapToCenter = true;

        private Canvas _canvas;
        private Camera _camera;
        private RectTransform _canvasRect;
    
        // 新增：记录摇杆的逻辑中心点（屏幕坐标）
        private Vector2 _joystickCenter;

        public Vector2 InputVector { get; private set; }
        public bool IsActive { get; private set; }
    
        public event Action<Vector2> OnInputChanged;
        public event Action OnJoystickReleased;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas.GetComponent<RectTransform>();
        
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
                _camera = _canvas.worldCamera;
        
            // 初始隐藏摇杆视觉
            //HideVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
        
            // 1. 设置摇杆逻辑中心点（本地坐标）
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, 
                    eventData.position, 
                    _camera, 
                    out var localPoint))
            {
                // 设置逻辑根节点位置
                if (joystickRoot != null)
                {
                    joystickRoot.localPosition = localPoint;
                }
                else
                {
                    // 如果没有单独的 root，直接用 background
                    background.localPosition = localPoint;
                }
            
                // 重置手柄到中心
                handle.localPosition = Vector2.zero;
            }
        
            // 2. 记录摇杆中心点的屏幕坐标（用于后续计算）
            _joystickCenter = eventData.position;
        
            // 3. 显示视觉
            ShowVisual();
        
            // 4. 立即处理第一次输入
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActive) return;
        
            // 关键修复：使用记录的中心点，而不是实时查询 background.position
            Vector2 radius = background.sizeDelta / 2;
        
            // 计算当前触摸点相对于摇杆中心的偏移
            Vector2 input = (eventData.position - _joystickCenter) / (radius * _canvas.scaleFactor);
            InputVector = NormalizeInput(input);
        
            // 更新手柄位置（视觉反馈）
            Vector2 handlePosition = InputVector * radius * handleRange;
            handle.localPosition = handlePosition;
        
            OnInputChanged?.Invoke(InputVector);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            InputVector = Vector2.zero;
            handle.localPosition = Vector2.zero;
        
            // 隐藏摇杆视觉
            //HideVisual();
        
            OnJoystickReleased?.Invoke();
        }

        private Vector2 NormalizeInput(Vector2 input)
        {
            if (input.magnitude < deadZone)
                return Vector2.zero;
        
            if (snapToCenter)
            {
                float normalizedMagnitude = Mathf.Clamp01((input.magnitude - deadZone) / (1 - deadZone));
                return input.normalized * normalizedMagnitude;
            }
            else
            {
                return Vector2.ClampMagnitude(input, 1f);
            }
        }
    
        private void ShowVisual()
        {
            if (visualGroup != null)
            {
                visualGroup.alpha = 1f;
            }
            else
            {
                background.gameObject.SetActive(true);
            }
        }
    
        private void HideVisual()
        {
            if (visualGroup != null)
            {
                visualGroup.alpha = 0f;
            }
            else
            {
                background.gameObject.SetActive(false);
            }
        }
    }
}
