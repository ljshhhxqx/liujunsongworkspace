using System;
using Game.Map;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using Tool.GameEvent;
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

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, UIManager uiManager)
        {
            _gameEventManager = gameEventManager;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _playerDataConfig = _jsonDataConfig.PlayerConfig;
            _uiManager = uiManager;
            _uiManager.IsUnlockMouse+= OnUnlockMouse;
            _gameEventManager.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            Debug.Log("CameraFollowClient init");
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

            _target = playerSpawnedEvent.Target;
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

        private void LateUpdate()
        {
            if (!_target || !_isControlling || Cursor.visible) return;

#if UNITY_ANDROID || UNITY_IOS
            // 手机平台的输入逻辑
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    float horizontal = touch.deltaPosition.x * _playerDataConfig.TurnSpeed * Time.deltaTime;
                    float vertical = touch.deltaPosition.y * _playerDataConfig.TurnSpeed * Time.deltaTime;

                    // 计算摄像机与水平面的角度
                    float angleWithGround = Vector3.Angle(Vector3.down, _offset.normalized) - 90; // 减去90是因为原点是向下的
                    float maxVerticalAngle = 90 - Mathf.Abs(angleWithGround);
                    vertical = Mathf.Clamp(vertical, -maxVerticalAngle, maxVerticalAngle);

                    _offset = Quaternion.AngleAxis(horizontal, Vector3.up) * _offset;
                    _offset = Quaternion.AngleAxis(vertical, Vector3.right) * _offset;
                }
            }
#else
            // PC平台的输入逻辑
            var horizontal = Mathf.Lerp(_lastHorizontal, Input.GetAxis("Mouse X") * _jsonDataConfig.PlayerConfig.TurnSpeed, Time.deltaTime * 10);
            var rawVertical = Mathf.Clamp(Input.GetAxis("Mouse Y") * _jsonDataConfig.PlayerConfig.TurnSpeed, -10, 10);
            _lastHorizontal = horizontal;

            // 计算摄像机与水平面的角度
            float angleWithGround = Vector3.Angle(Vector3.down, _offset.normalized) - 90; // 减去90是因为原点是向下的
            float maxVerticalAngle = 90 - Mathf.Abs(angleWithGround);
            var vertical = Mathf.Clamp(rawVertical, -maxVerticalAngle, maxVerticalAngle);

            _offset = Quaternion.AngleAxis(horizontal, Vector3.up) * _offset;
            _offset = Quaternion.AngleAxis(vertical, Vector3.right) * _offset;
#endif
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
            _uiManager.IsUnlockMouse -= OnUnlockMouse;
        }
    }
}