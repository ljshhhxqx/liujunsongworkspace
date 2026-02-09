using System;
using System.Collections.Generic;
using AOTScripts.Tool.Resource;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UniRx;
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
        private Dictionary<int, AnimationStateData> _playerAnimiationDatas = new Dictionary<int, AnimationStateData>();
    
        [SerializeField]
        private ProgressItem progressItem;
        [Header("References")]
        [SerializeField]
        private VirtualJoystick movementJoystick;
        private readonly List<VirtualButton> _virtualButtons = new List<VirtualButton>();

        [SerializeField] private ContentItemList contentItemList;
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
            for (int i = 0; i < functionButtons.Count; i++)
            {
                var functionButton = functionButtons[i];
                objectResolver.Inject(functionButton);
            }
        }
        
        private void InitializeButton(VirtualButton button)
        {
            var i = _virtualButtons.IndexOf(button);
            if (i != -1)
            {
                _virtualButtons[i] = button;
                button.ButtonReleased -= button.ButtonReleased;
                button.ButtonPressed -= button.ButtonPressed;
                return;
            }
            _virtualButtons.Add(button);
            
            foreach (var btn in _virtualButtons)
            {
                btn.ButtonPressed += OnButtonPressed;
                btn.ButtonReleased += OnButtonReleased;
            
                buttonStates[btn.ButtonName] = false;
                buttonDownStates[btn.ButtonName] = false;
            }
            
        }

        private void InitializeInputSystem()
        {
            // 注册摇杆事件
            movementJoystick.OnInputChanged += OnJoystickInput;
            movementJoystick.OnJoystickReleased += OnJoystickReleased;
            
            // 注册按钮事件
            foreach (var button in _virtualButtons)
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
        public Vector3 GetMovementInput()
        {
            if (movementJoystick)
            {
                if (Mathf.Approximately(movementJoystick.InputVector.magnitude, 1))
                {
                    Debug.Log($"[virtualInput] GetMovementInput: {movementJoystick.InputVector}");
                }
                return movementJoystick.InputVector;
            }
            return Vector3.zero;
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
            //AdaptJoystickToScreen();
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
        
            foreach (var button in _virtualButtons)
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
            for (int i = 0; i < _virtualButtons.Count; i++)
            {
                RectTransform buttonRect = _virtualButtons[i].GetComponent<RectTransform>();
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

        public bool IsProgressing()
        {
            return progressItem.IsProgressing();
        }

        public override UIType Type => UIType.VirtualInput;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        
        public void StartProgress(string description, float countdown, Action onComplete = null, Func<bool> condition = null)
        {
            if (countdown <= 0)
            {
                onComplete?.Invoke();
                //progressItem.SetProgress(description, countdown, onComplete, condition);
                return;
            }
            Debug.Log("[PlayerPropertiesOverlay] StartProgress: " + description + " " + countdown);
            progressItem.SetProgress(description, countdown, onComplete, condition);

        }

        public void BindPlayerAnimationData(HReactiveDictionary<int, AnimationStateData> animationStateDataDict)
        {
            progressItem.transform.localScale = Vector3.zero;
            _playerAnimiationDatas = new Dictionary<int, AnimationStateData>();
            foreach (var (key, animationStateData) in animationStateDataDict)
            {
                _playerAnimiationDatas.Add(key, animationStateData);
            }
            contentItemList.SetItemList(_playerAnimiationDatas);
            for (int i = 0; i < contentItemList.ItemBases.Count; i++)
            {
                var item = contentItemList.ItemBases[i];
                if (item is AnimationItem animationItem)
                {
                    if (animationItem.TryGetComponent<VirtualButton>(out var virtualButton))
                    {
                        _virtualButtons.Add(virtualButton);
                    }
                }
            }

            animationStateDataDict.ObserveAdd((x,y) =>
                {
                    if (_playerAnimiationDatas.ContainsKey(x))
                    {
                        return;
                    }

                    _playerAnimiationDatas.Add(x, y);
                    var item = contentItemList.AddItem<AnimationStateData, AnimationItem>(x, y);
                    if (item.TryGetComponent<VirtualButton>(out var virtualButton))
                    {
                        _virtualButtons.Add(virtualButton);
                        virtualButton.ButtonPressed += OnButtonPressed;
                        virtualButton.ButtonReleased += OnButtonReleased;
            
                        buttonStates[virtualButton.ButtonName] = false;
                        buttonDownStates[virtualButton.ButtonName] = false;
                    }
                })
                .AddTo(this);
            animationStateDataDict.ObserveUpdate((x, y, z) =>
                {
                    if (!y.Equals(z))
                    {
                        if (_playerAnimiationDatas.ContainsKey(x))
                        {
                            _playerAnimiationDatas[x] = z;
                            var item = contentItemList.ReplaceItem<AnimationStateData, AnimationItem>(x, z);
                        }
                    }
                })
                .AddTo(this);
            animationStateDataDict.ObserveRemove((x, y) =>
                {
                    if (_playerAnimiationDatas.ContainsKey(x))
                    {
                        _playerAnimiationDatas.Remove(x);
                        var item = contentItemList.GetItem<AnimationItem>(x);
                        if (item)
                        {
                            if (item.TryGetComponent<VirtualButton>(out var virtualButton))
                            {
                                virtualButton.ButtonPressed -= OnButtonPressed;
                                virtualButton.ButtonReleased -= OnButtonReleased;
                                buttonStates.Remove(virtualButton.ButtonName);
                                buttonDownStates.Remove(virtualButton.ButtonName);
                                _virtualButtons.Remove(virtualButton);
                            }
                        }
                        contentItemList.RemoveItem(x);
                    }
                })
                .AddTo(this);
            animationStateDataDict.ObserveClear(_ =>
                {
                    _playerAnimiationDatas.Clear();
                    contentItemList.Clear();
                    buttonStates.Clear();
                    buttonDownStates.Clear();
                    _virtualButtons.Clear();
                })
                .AddTo(this);
            InitializeInputSystem();
        }

        public bool IsSprinting()
        {
            return movementJoystick.IsInputOverload;
        }
    }
}