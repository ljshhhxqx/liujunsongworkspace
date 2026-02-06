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
        private float handleRange = 1f;          // Handle 可移动的最大距离（相对于 Background 半径的倍数）
        [SerializeField]
        private float deadZone = 0.2f;           // 死区（小于此值时输入为 0）
        [SerializeField]
        private bool snapToCenter = true;        // 是否平滑过渡死区
    
        [Header("Visual Feedback")]
        [SerializeField]
        private float returnSpeed = 10f;         // Handle 回正速度（0 表示瞬间回正）

        private Canvas _canvas;
        private Camera _camera;
        private Vector2 _backgroundCenter;       // Background 的屏幕坐标中心点（固定）
        private float _backgroundRadius;         // Background 的半径（像素）

        public Vector2 InputVector { get; private set; }
        public bool IsActive { get; private set; }
    
        public event Action<Vector2> OnInputChanged;
        public event Action OnJoystickReleased;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
                _camera = _canvas.worldCamera;
        
            // 计算 Background 的固定中心点（屏幕坐标）
            _backgroundCenter = RectTransformUtility.WorldToScreenPoint(_camera, background.position);
            _backgroundRadius = background.sizeDelta.x / 2 * _canvas.scaleFactor;
        
            // 确保 Handle 初始在中心
            handle.anchoredPosition = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            OnDrag(eventData); // 立即响应第一次触摸
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActive) return;
        
            // 计算触摸点相对于 Background 中心的偏移（屏幕坐标）
            Vector2 touchOffset = eventData.position - _backgroundCenter;
        
            // 归一化输入（-1 到 1）
            Vector2 rawInput = touchOffset / _backgroundRadius;
        
            // 限制最大距离
            Vector2 clampedInput = Vector2.ClampMagnitude(rawInput, 1f);
        
            // 应用死区和归一化
            InputVector = NormalizeInput(clampedInput);
        
            // 更新 Handle 位置（本地坐标，相对于 Background）
            handle.anchoredPosition = clampedInput * (background.sizeDelta.x / 2) * handleRange;
        
            OnInputChanged?.Invoke(InputVector);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            InputVector = Vector2.zero;
        
            // Handle 回正（根据 returnSpeed 选择瞬间或平滑）
            if (returnSpeed > 0)
            {
                StopAllCoroutines();
                StartCoroutine(ReturnHandleToCenter());
            }
            else
            {
                handle.anchoredPosition = Vector2.zero;
            }
        
            OnJoystickReleased?.Invoke();
        }

        private void Update()
        {
            // 实时更新 Background 的屏幕坐标（处理屏幕旋转/缩放等情况）
            if (!IsActive)
            {
                _backgroundCenter = RectTransformUtility.WorldToScreenPoint(_camera, background.position);
            }
        }

        private Vector2 NormalizeInput(Vector2 input)
        {
            float magnitude = input.magnitude;
        
            // 应用死区
            if (magnitude < deadZone)
                return Vector2.zero;
        
            // 平滑过渡死区（可选）
            if (snapToCenter)
            {
                float normalizedMagnitude = Mathf.Clamp01((magnitude - deadZone) / (1 - deadZone));
                return input.normalized * normalizedMagnitude;
            }
        
            return input;
        }

        private System.Collections.IEnumerator ReturnHandleToCenter()
        {
            while (handle.anchoredPosition.magnitude > 0.01f)
            {
                handle.anchoredPosition = Vector2.Lerp(
                    handle.anchoredPosition, 
                    Vector2.zero, 
                    Time.deltaTime * returnSpeed
                );
                yield return null;
            }
            handle.anchoredPosition = Vector2.zero;
        }

        // 调试用：绘制摇杆范围
        private void OnDrawGizmos()
        {
            if (background == null) return;
        
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(background.position, background.sizeDelta.x / 2);
        
            if (handle != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(background.position, handle.position);
            }
        }
    }
}
