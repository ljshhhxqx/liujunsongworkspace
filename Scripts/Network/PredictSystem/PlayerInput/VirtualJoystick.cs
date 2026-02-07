using System;
using HotUpdate.Scripts.Tool.ReactiveProperty;
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
    
        [Header("Visual Feedback")]
        [SerializeField]
        private float returnSpeed = 10f;

        private Canvas _canvas;
        private Camera _camera;
        private Vector2 _backgroundCenter;
        private float _backgroundRadius;

        /// <summary>
        /// ⭐ 输入向量（等同于键盘的 Horizontal/Vertical）
        /// X: 左右 (-1 到 1)，Z: 前后 (-1 到 1)
        /// </summary>
        public Vector3 InputVector { get; private set; }
        
        /// <summary>
        /// 2D 版本（用于 UI 显示）
        /// </summary>
        public Vector2 InputVector2D { get; private set; }
        
        public bool IsActive { get; private set; }
    
        public event Action<Vector3> OnInputChanged;
        public event Action OnJoystickReleased;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
                _camera = _canvas.worldCamera;
        
            _backgroundCenter = RectTransformUtility.WorldToScreenPoint(_camera, background.position);
            _backgroundRadius = background.sizeDelta.x / 2 * _canvas.scaleFactor;
        
            handle.anchoredPosition = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActive) return;
        
            Vector2 touchOffset = eventData.position - _backgroundCenter;
            Vector2 rawInput = touchOffset / _backgroundRadius;
            Vector2 clampedInput = Vector2.ClampMagnitude(rawInput, 1f);
        
            // ⭐ 应用死区处理
            Vector2 normalizedInput = NormalizeInput(clampedInput);
            InputVector2D = normalizedInput;
            
            // ⭐ 修正：转换为 3D 格式（等同于键盘输入）
            // X = Horizontal（左右），Z = Vertical（前后）
            InputVector = new Vector3(normalizedInput.x, 0, normalizedInput.y);
        
            // 更新 Handle 位置（使用未处理死区的 clampedInput 以保持视觉连贯）
            handle.anchoredPosition = clampedInput * (background.sizeDelta.x / 2) * handleRange;
        
            OnInputChanged?.Invoke(InputVector);
            JoystickStatic.TouchedJoystick.Value = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;
            InputVector2D = Vector2.zero;
            InputVector = Vector3.zero;
        
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
            JoystickStatic.TouchedJoystick.Value = false;
        }

        private void Update()
        {
            if (!IsActive)
            {
                _backgroundCenter = RectTransformUtility.WorldToScreenPoint(_camera, background.position);
            }
        }

        /// <summary>
        /// ⭐ 归一化输入并应用死区
        /// </summary>
        private Vector2 NormalizeInput(Vector2 input)
        {
            float magnitude = input.magnitude;
        
            // 死区过滤
            if (magnitude < deadZone)
                return Vector2.zero;
        
            if (snapToCenter)
            {
                // 重映射：死区外的值映射到 0-1
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

        private void OnDrawGizmos()
        {
            if (background == null) return;
        
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(background.position, background.sizeDelta.x / 2);
        
            if (handle != null && Application.isPlaying && IsActive)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(background.position, handle.position);
                
                // ⭐ 调试：显示输入向量方向
                Gizmos.color = Color.blue;
                Vector3 worldPos = background.position;
                // 将 2D 输入转换为世界空间显示
                Vector3 debugDirection = new Vector3(InputVector2D.x, 0, InputVector2D.y);
                Gizmos.DrawRay(worldPos, debugDirection * 2);
                
                // 显示文本标签
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    worldPos + debugDirection * 2.5f, 
                    $"Input: ({InputVector2D.x:F2}, {InputVector2D.y:F2})"
                );
                #endif
            }
        }

        private void OnDestroy()
        {
            JoystickStatic.TouchedJoystick.Value = false;
        }
    }
    
    public static class JoystickStatic
    {
        public static HReactiveProperty<bool> TouchedJoystick { get; } = new HReactiveProperty<bool>();
    }
}
