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
    
        [Header("Settings")]
        [SerializeField]
        private float handleRange = 1f;
        [SerializeField]
        private float deadZone = 0.2f;
        [SerializeField]
        private bool snapToCenter = true;
    
        private Canvas _canvas;
        private Camera _camera;
    
        public Vector2 InputVector { get; private set; }
        public bool IsActive { get; private set; }
        
        public event Action<Vector2> OnInputChanged;
        public event Action OnJoystickReleased;
    
        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
                _camera = _canvas.worldCamera;
        
            // 初始隐藏摇杆
            background.gameObject.SetActive(false);
        }
    
        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            background.gameObject.SetActive(true);
        
            // 设置摇杆位置为点击位置
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.GetComponent<RectTransform>(), 
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
            background.gameObject.SetActive(false);
        
            OnJoystickReleased?.Invoke();
        }
    
        private Vector2 NormalizeInput(Vector2 input)
        {
            // 应用死区
            if (input.magnitude < deadZone)
                return Vector2.zero;
        
            // 归一化输入
            if (snapToCenter)
                return input.normalized * ((input.magnitude - deadZone) / (1 - deadZone));
            else
                return Vector2.ClampMagnitude(input, 1f);
        }
    }
}