using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using UI.UIBase;
using UnityEngine;
using VContainer;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class VirtualInputOverlay : ScreenUIBase
    {
        [Header("Input Settings")]
        [SerializeField]
        private bool autoDetectControls = true;
        [SerializeField]
        private float buttonSizeMultiplier = 0.15f; // 按钮大小占屏幕比例
        public override bool IsGameUI => true;
    
        [Header("References")]
        [SerializeField]
        private VirtualJoystick movementJoystick;
        [SerializeField]
        private List<VirtualButton> actionButtons;
        [SerializeField]
        private List<FunctionVirtualButton> functionButtons;
        private HashSet<AnimationState> _activeButtons = new HashSet<AnimationState>();
        public HashSet<AnimationState> ActiveButtons => _activeButtons;
    
        private Dictionary<AnimationState, bool> buttonStates = new Dictionary<AnimationState, bool>();
        private Dictionary<AnimationState, bool> buttonDownStates = new Dictionary<AnimationState, bool>();

        private HashSet<AnimationState> _animationStates = new HashSet<AnimationState>();

        [Inject]
        private void Init(IObjectResolver objectResolver)
        {
            InitializeInputSystem();
            for (int i = 0; i < functionButtons.Count; i++)
            {
                var functionButton = functionButtons[i];
                objectResolver.Inject(functionButton);
            }
        }

        private void InitializeInputSystem()
        {
            // 注册摇杆事件
            movementJoystick.OnInputChanged += OnJoystickInput;
            movementJoystick.OnJoystickReleased += OnJoystickReleased;
            
            // 注册按钮事件
            foreach (var button in actionButtons)
            {
                button.ButtonPressed += OnButtonPressed;
                button.ButtonReleased += OnButtonReleased;
            
                buttonStates[button.ButtonName] = false;
                buttonDownStates[button.ButtonName] = false;
            }
        
            // 自动适配控件布局
            if (autoDetectControls)
            {
                AdaptControlsToScreen();
            }
        }
    
        private void Update()
        {
            // 重置按钮按下状态（每帧重置）
            foreach (var key in _animationStates)
            {
                buttonDownStates[key] = false;
            }
        }
    
        // 摇杆输入处理
        private void OnJoystickInput(Vector3 input)
        {
            // 这里可以添加额外的输入处理逻辑
            Debug.Log($"Joystick Input: {input}");
        }
    
        private void OnJoystickReleased()
        {
            // 摇杆释放处理
            //Debug.Log("Joystick Released");
        }
    
        // 按钮输入处理
        private void OnButtonPressed(AnimationState buttonName)
        {
            buttonStates[buttonName] = true;
            buttonDownStates[buttonName] = true;
            _animationStates.Add(buttonName);
            _activeButtons.Add(buttonName);
        }
    
        private void OnButtonReleased(AnimationState buttonName)
        {
            buttonStates[buttonName] = false;
            if (_activeButtons.Contains(buttonName))
            {
                _activeButtons.Remove(buttonName);
            }
        }
    
        // 公共输入接口
        public Vector2 GetMovementInput()
        {
            return movementJoystick?.InputVector ?? Vector2.zero;
        }

        public List<AnimationState> GetActiveButtons()
        {
            List<AnimationState> activeButtons = new List<AnimationState>();
            foreach (var key in buttonStates.Keys)
            {
                if (buttonStates[key])
                {
                    activeButtons.Add(key);
                }
            }
            return activeButtons;
        }

        public bool GetButton(AnimationState buttonName)
        {
            return buttonStates.ContainsKey(buttonName) && buttonStates[buttonName];
        }
    
        public bool GetButtonDown(AnimationState buttonName)
        {
            return buttonDownStates.ContainsKey(buttonName) && buttonDownStates[buttonName];
        }
    
        // 屏幕适配
        private void AdaptControlsToScreen()
        {
            AdaptJoystickToScreen();
            //AdaptButtonsToScreen();
        }
    
        private void AdaptJoystickToScreen()
        {
            if (movementJoystick == null) return;
        
            RectTransform joystickRect = movementJoystick.GetComponent<RectTransform>();
            float screenMin = Mathf.Min(Screen.width, Screen.height);
            float joystickSize = screenMin * 0.2f; // 摇杆大小为屏幕最小尺寸的20%
        
            joystickRect.sizeDelta = new Vector2(joystickSize, joystickSize);
        
            // 设置摇杆位置（左下角）
            joystickRect.anchorMin = new Vector2(0, 0);
            joystickRect.anchorMax = new Vector2(0, 0);
            joystickRect.pivot = new Vector2(0, 0);
            joystickRect.anchoredPosition = new Vector2(joystickSize * 0.5f, joystickSize * 0.5f);
        }
    
        private void AdaptButtonsToScreen()
        {
            float screenMin = Mathf.Min(Screen.width, Screen.height);
            float buttonSize = screenMin * buttonSizeMultiplier;
        
            foreach (var button in actionButtons)
            {
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
            }
        
            // 排列按钮位置（右下角）
            ArrangeButtonsInLayout();
        }
    
        private void ArrangeButtonsInLayout()
        {
            // 根据按钮数量和屏幕尺寸自动排列按钮
            // 这里可以实现更复杂的布局逻辑
            for (int i = 0; i < actionButtons.Count; i++)
            {
                RectTransform buttonRect = actionButtons[i].GetComponent<RectTransform>();
                float buttonSize = buttonRect.sizeDelta.x;
                float spacing = buttonSize * 0.2f;
            
                // 从右下角开始排列
                float xPos = Screen.width - buttonSize - spacing;
                float yPos = spacing + (buttonSize + spacing) * i;
            
                buttonRect.anchorMin = new Vector2(1, 0);
                buttonRect.anchorMax = new Vector2(1, 0);
                buttonRect.pivot = new Vector2(1, 0);
                buttonRect.anchoredPosition = new Vector2(-xPos, yPos);
            }
        }
    
        // 动态添加按钮
        public void AddButton(string buttonName, Action<string> onPressed = null)
        {
            // 这里可以实现动态创建按钮的逻辑
        }
    
        // 设备旋转处理
        private void OnRectTransformDimensionsChange()
        {
            if (autoDetectControls)
            {
                AdaptControlsToScreen();
            }
        }

        public override UIType Type => UIType.VirtualInput;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}