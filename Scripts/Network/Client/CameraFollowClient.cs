using System;
using Game.Map;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Client
{
    public class CameraFollowClient : MonoBehaviour, IInjectableObject
    {
        private GameEventManager _gameEventManager;
        private JsonDataConfig _jsonDataConfig;
        private PlayerConfigData _playerDataConfig;
        private Transform _target; // 角色的 Transform
        private Vector3 _offset; // 初始偏移量
        private float _lastHorizontal;
        private bool _isControlling = true;
        private LayerMask _groundSceneLayer;
        private UIManager _uiManager;
        private bool _isWindowsApplication;
        private bool _isMobile;
        private int _cameraControlTouchId = -1;
        [SerializeField] 
        [Range(0.3f, 0.7f)] 
        private float screenDivideRatio = 0.5f;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, UIManager uiManager)
        {
            _gameEventManager = gameEventManager;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _playerDataConfig = _jsonDataConfig.PlayerConfig;
            _uiManager = uiManager;
            _uiManager.IsUnlockMouse+= OnUnlockMouse;
            _gameEventManager.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            _gameEventManager.Subscribe<TouchResetCameraEvent>(OnTouchResetCamera);
            _isWindowsApplication = PlayerPlatformDefine.IsWindowsPlatform();
            _isMobile = PlayerPlatformDefine.IsJoystickPlatform();
            Debug.Log("CameraFollowClient init");
        }

        private void OnTouchResetCamera(TouchResetCameraEvent cameraEvent)
        {
            _isControlling = true;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnUnlockMouse(bool isUnlock)
        {
            Cursor.lockState = isUnlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isUnlock;
        }

        private void OnPlayerSpawned(PlayerSpawnedEvent playerSpawnedEvent)
        {
            if (!playerSpawnedEvent.Target)
            {
                Debug.LogError("player has no tag!");
                return;
            }

            _target = playerSpawnedEvent.CameraFollowTarget;
            _offset = _jsonDataConfig.PlayerConfig.Offset;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Input.GetButtonDown("Exit"))
            {
                _isControlling = !_isControlling;
                Cursor.lockState = _isControlling ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_isControlling;
            }
        }
       
        private bool CanTouchControlCamera(Touch touch)
        {
            // 情况 1：摇杆未激活 → 全屏都可以
            if (!JoystickStatic.TouchedJoystick.Value)
            {
                return true;
            }
        
            // 情况 2：摇杆激活中 → 只允许右半边屏幕
            float divideX = Screen.width * screenDivideRatio;
            return touch.position.x >= divideX;
        }

        private void LateUpdate()
        {
            if (!_target || !_isControlling || Cursor.visible) return;
            float horizontal = 0;
            float vertical = 0;
            float rawVertical = 0;
            float angleWithGround = 0;
            float maxVerticalAngle = 0;
            
            if (_isWindowsApplication)
            {
                // PC平台的输入逻辑
                horizontal = Mathf.Lerp(_lastHorizontal, Input.GetAxis("Mouse X") * _jsonDataConfig.PlayerConfig.TurnSpeed, Time.deltaTime * 10);
                rawVertical = Mathf.Clamp(Input.GetAxis("Mouse Y") * _jsonDataConfig.PlayerConfig.TurnSpeed, -10, 10);
                _lastHorizontal = horizontal;
            }
            else if (_isMobile)
            {
                if (Input.touchCount > 0)
                {
                    foreach (var touch in Input.touches)
                    {
                        switch (touch.phase)
                        {
                            case TouchPhase.Began:
                                // ⭐ 核心逻辑：判断是否允许这个触摸点控制摄像机
                                if (_cameraControlTouchId == -1 && CanTouchControlCamera(touch))
                                {
                                    _cameraControlTouchId = touch.fingerId;
                                }
                                break;

                            case TouchPhase.Moved:
                                if (touch.fingerId == _cameraControlTouchId)
                                {
                                    horizontal = touch.deltaPosition.x * _playerDataConfig.TurnSpeed * Time.deltaTime * 2;
                                    vertical = touch.deltaPosition.y * _playerDataConfig.TurnSpeed * Time.deltaTime * 2;

                                    // 计算摄像机与水平面的角度
                                    angleWithGround = Vector3.Angle(Vector3.down, _offset.normalized) - 90; // 减去90是因为原点是向下的
                                    maxVerticalAngle = 90 - Mathf.Abs(angleWithGround);
                                    vertical = Mathf.Clamp(vertical, -maxVerticalAngle, maxVerticalAngle);

                                    _offset = Quaternion.AngleAxis(horizontal, Vector3.up) * _offset;
                                    _offset = Quaternion.AngleAxis(vertical, Vector3.right) * _offset;
                                }
                                break;

                            case TouchPhase.Ended:
                            case TouchPhase.Canceled:
                                if (touch.fingerId == _cameraControlTouchId)
                                {
                                    _cameraControlTouchId = -1;
                                }
                                break;
                        }
                    }
                }
            }

            // 计算摄像机与水平面的角度
            angleWithGround = Vector3.Angle(Vector3.down, _offset.normalized) - 90; // 减去90是因为原点是向下的
            maxVerticalAngle = 90 - Mathf.Abs(angleWithGround);
            vertical = Mathf.Clamp(rawVertical, -maxVerticalAngle, maxVerticalAngle);

            _offset = Quaternion.AngleAxis(horizontal, Vector3.up) * _offset;
            _offset = Quaternion.AngleAxis(vertical, Vector3.right) * _offset;
            var desiredPosition = _target.position + _offset;
            Vector3 smoothedPosition;
            if (Physics.Raycast(_target.position, desiredPosition - _target.position, out var hit, _offset.magnitude, _jsonDataConfig.GameConfig.groundSceneLayer))
            {
                smoothedPosition = Vector3.Lerp(transform.position, hit.point, _jsonDataConfig.PlayerConfig.MouseSpeed);
            }
            else
            {
                smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _jsonDataConfig.PlayerConfig.MouseSpeed);
            }

            transform.position = smoothedPosition;
            transform.LookAt(_target);
        }

        private void OnDestroy()
        {
            _uiManager.IsUnlockMouse-= OnUnlockMouse;
            _gameEventManager.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("CameraFollowClient OnDestroy");
        }
    }
}