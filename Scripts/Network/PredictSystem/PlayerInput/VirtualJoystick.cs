using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Joystick Components")]
        [SerializeField]
        private RectTransform background;
        [SerializeField]
        private RectTransform handle;
        [SerializeField]
        private CanvasGroup visualGroup; // 新增：控制显示隐藏

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
            
            // 初始隐藏摇杆视觉（但保持交互区域激活）
            HideVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            
            // 显示摇杆
            ShowVisual();
            
            // 设置摇杆位置为点击位置
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, 
                    eventData.position, 
                    _camera, 
                    out var localPoint))
            {
                background.localPosition = localPoint;
                handle.localPosition = Vector2.zero;
            }
            
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActive) return;
            
            Vector2 position = RectTransformUtility.WorldToScreenPoint(_camera, background.position);
            Vector2 radius = background.sizeDelta / 2;
            
            // 计算摇杆输入
            Vector2 input = (eventData.position - position) / (radius * _canvas.scaleFactor);
            InputVector = NormalizeInput(input);
            
            // 更新手柄位置
            Vector2 handlePosition = InputVector * radius * handleRange;
            handle.localPosition = handlePosition;
            
            OnInputChanged?.Invoke(InputVector);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            InputVector = Vector2.zero;
            handle.localPosition = Vector2.zero;
            
            // 隐藏摇杆视觉（但不禁用 GameObject）
            HideVisual();
            
            OnJoystickReleased?.Invoke();
        }

        private Vector2 NormalizeInput(Vector2 input)
        {
            if (input.magnitude < deadZone)
                return Vector2.zero;
            
            if (snapToCenter)
                return input.normalized * Mathf.Clamp01((input.magnitude - deadZone) / (1 - deadZone));
            else
                return Vector2.ClampMagnitude(input, 1f);
        }
        
        /// <summary>
        /// 显示摇杆（使用 CanvasGroup）
        /// </summary>
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
        
        /// <summary>
        /// 隐藏摇杆（使用 CanvasGroup，保持交互）
        /// </summary>
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