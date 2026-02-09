using UnityEngine;
using UnityEngine.EventSystems;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Button Settings")]
        [SerializeField]
        private AnimationState buttonName;
        [SerializeField]
        private float pressScale = 0.9f;
    
        private Vector3 _originalScale;
        private bool _isPressed;
    
        // 按钮事件
        public System.Action<AnimationState> ButtonPressed;
        public System.Action<AnimationState> ButtonReleased;
        public AnimationState ButtonName => buttonName;
    
        private void Start()
        {
            _originalScale = transform.localScale;
        }
        
        public void SetButtonName(AnimationState btnName)
        {
            buttonName = btnName;
        }
    
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isPressed) return;
        
            _isPressed = true;
            transform.localScale = _originalScale * pressScale;
        
            ButtonPressed?.Invoke(buttonName);
        }
    
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed) return;
        
            _isPressed = false;
            transform.localScale = _originalScale;
        
            ButtonReleased?.Invoke(buttonName);
        }
    
        public bool GetButton()
        {
            return _isPressed;
        }
    
        public bool GetButtonDown()
        {
            // 注意：这个需要在帧间检测，实际使用需要结合Update逻辑
            return _isPressed;
        }
    }
}