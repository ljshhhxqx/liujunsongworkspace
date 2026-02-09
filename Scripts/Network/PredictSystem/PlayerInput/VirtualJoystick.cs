using System;
using System.Collections;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Joystick Components")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;

        [Header("Settings")]
        [SerializeField] private float handleRange = 1f;

        [Tooltip("死区（0~1），例如 0.2 表示摇杆偏移 20% 内输出为 0")]
        [Range(0f, 1f)]
        [SerializeField] private float deadZone = 0.2f;

        [Tooltip("是否使用 InputSystem 风格的 radial deadzone 重映射（推荐开启）")]
        [SerializeField] private bool useInputSystemDeadzone = true;

        [Tooltip("是否启用轻微曲线（更接近真实摇杆的推感）。1=线性，>1 更迟钝，<1 更灵敏")]
        [Range(0.2f, 4f)]
        [SerializeField] private float responseCurvePower = 1.0f;

        [Header("Visual Feedback")]
        [SerializeField] private float returnSpeed = 10f;

        private Canvas _canvas;
        private Camera _camera;

        private float _radius;

        /// <summary>
        /// 输入向量（等同于键盘 Horizontal/Vertical）
        /// X: 左右 (-1 到 1)，Z: 前后 (-1 到 1)
        /// </summary>
        public Vector3 InputVector { get; private set; }

        /// <summary>
        /// 2D 输入（用于 UI 显示）
        /// </summary>
        public Vector2 InputVector2D { get; private set; }

        public bool IsActive { get; private set; }
        
        public bool IsInputOverload { get; private set; }

        public event Action<Vector3> OnInputChanged;
        public event Action OnJoystickReleased;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();

            // Overlay 模式 camera 传 null
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _camera = _canvas.worldCamera;
            else
                _camera = null;

            UpdateRadius();
            ResetJoystickImmediate();
        }

        private void UpdateRadius()
        {
            if (background == null) return;

            // 用最小边保证圆形范围正确（防止 background 非正方形）
            _radius = Mathf.Min(background.sizeDelta.x, background.sizeDelta.y) * 0.5f;
            if (_radius <= 0.001f) _radius = 1f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsActive = true;

            StopAllCoroutines();

            UpdateRadius();
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActive) return;
            if (background == null || handle == null) return;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background,
                    eventData.position,
                    _camera,
                    out localPoint))
                return;

            // 转成 [-1,1] 的输入（rawInput magnitude 理论上可 > 1）
            Vector2 rawInput = localPoint / _radius;

            // Clamp 到圆形范围
            Vector2 clampedInput = Vector2.ClampMagnitude(rawInput, 1f);
            IsInputOverload = rawInput.magnitude > 1.2f;
            // 应用 deadzone + remap（Input System 风格）
            Vector2 finalInput = useInputSystemDeadzone
                ? ApplyRadialDeadzone(clampedInput, deadZone, responseCurvePower)
                : ApplySimpleDeadzone(clampedInput, deadZone);

            InputVector2D = finalInput;

            // 输出等价于 Input.GetAxis
            InputVector = new Vector3(finalInput.x, 0, finalInput.y);

            // UI handle 使用 clampedInput（保持视觉连续）
            handle.anchoredPosition = clampedInput * _radius * handleRange;

            OnInputChanged?.Invoke(InputVector);
            JoystickStatic.TouchedJoystick.Value = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsActive = false;

            InputVector2D = Vector2.zero;
            InputVector = Vector3.zero;
            IsInputOverload = false;

            OnInputChanged?.Invoke(InputVector);
            OnJoystickReleased?.Invoke();
            JoystickStatic.TouchedJoystick.Value = false;

            if (returnSpeed > 0)
            {
                StopAllCoroutines();
                StartCoroutine(ReturnHandleToCenter());
            }
            else
            {
                ResetJoystickImmediate();
            }
        }

        private void ResetJoystickImmediate()
        {
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Input System 风格：Radial Deadzone + deadzone 外重新归一化
        /// 并且可以加一个 responseCurvePower 调节手感
        /// </summary>
        private Vector2 ApplyRadialDeadzone(Vector2 input, float deadZone, float curvePower)
        {
            float magnitude = input.magnitude;

            // 死区内直接 0
            if (magnitude <= deadZone)
                return Vector2.zero;

            // deadzone 外重新映射到 0~1
            // 这一步就是 Input System 摇杆手感的核心：保证最大仍为 1
            float normalizedMagnitude = Mathf.Clamp01((magnitude - deadZone) / (1f - deadZone));

            // 曲线控制（1=线性，>1 更迟钝，<1 更灵敏）
            if (!Mathf.Approximately(curvePower, 1f))
            {
                normalizedMagnitude = Mathf.Pow(normalizedMagnitude, curvePower);
            }

            return input.normalized * normalizedMagnitude;
        }

        /// <summary>
        /// 简单死区（非 InputSystem 风格）
        /// </summary>
        private Vector2 ApplySimpleDeadzone(Vector2 input, float deadZone)
        {
            if (input.magnitude < deadZone)
                return Vector2.zero;

            return input;
        }

        private IEnumerator ReturnHandleToCenter()
        {
            while (handle != null && handle.anchoredPosition.magnitude > 0.01f)
            {
                handle.anchoredPosition = Vector2.Lerp(
                    handle.anchoredPosition,
                    Vector2.zero,
                    Time.deltaTime * returnSpeed
                );

                yield return null;
            }

            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
        }

        private void OnRectTransformDimensionsChange()
        {
            // UI 改尺寸时自动更新半径
            UpdateRadius();
        }

        private void OnDestroy()
        {
            JoystickStatic.TouchedJoystick.Value = false;
        }
        
        private Vector2 ApplyDigital8Direction(Vector2 input, float threshold)
        {
            if (input.magnitude < threshold)
                return Vector2.zero;

            float x = 0;
            float y = 0;

            if (input.x > 0.0f) x = 1;
            else if (input.x < 0.0f) x = -1;

            if (input.y > 0.0f) y = 1;
            else if (input.y < 0.0f) y = -1;

            return new Vector2(x, y);
        }

    }

    public static class JoystickStatic
    {
        public static HReactiveProperty<bool> TouchedJoystick { get; } = new HReactiveProperty<bool>();
    }
}
