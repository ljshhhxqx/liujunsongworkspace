using Game.Inject;
using Game.Map;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace Network.Client
{
    public class CameraFollowClient : MonoBehaviour, IInjectableObject
    {
        private PlayerDataConfig playerDataConfig;
        private GameDataConfig gameDataConfig;
        private GameEventManager gameEventManager;
        private Transform target; // 角色的 Transform
        private Vector3 offset; // 初始偏移量
        private float lastHorizontal;
        private bool isContorlling = true;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            this.gameEventManager = gameEventManager;
            this.gameEventManager.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            Debug.Log("CameraFollowClient init");
        }
        
        private void OnPlayerSpawned(PlayerSpawnedEvent playerSpawnedEvent)
        {
            if (!playerSpawnedEvent.Target)
            {
                Debug.LogError("player has no tag!");
                return;
            }

            target = playerSpawnedEvent.Target;
            offset = playerDataConfig.PlayerConfigData.Offset;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Input.GetButtonDown("Exit"))
            {
                isContorlling = !isContorlling;
                Cursor.lockState = isContorlling ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !isContorlling;
            }
        }

        private void LateUpdate()
        {
            if (!target || !isContorlling) return;

            var horizontal = Mathf.Lerp(lastHorizontal, Input.GetAxis("Mouse X") * playerDataConfig.PlayerConfigData.TurnSpeed, Time.deltaTime * 10);
            
            // 原始的垂直输入
            var rawVertical = Mathf.Clamp(Input.GetAxis("Mouse Y") * playerDataConfig.PlayerConfigData.TurnSpeed, -10, 10);
            // 计算摄像机与水平面的角度
            float angleWithGround = Vector3.Angle(Vector3.down, offset.normalized) - 90; // 减去90是因为原点是向下的
            // 根据当前角度限制垂直旋转，使总角度不超过45度
            float maxVerticalAngle = 90 - Mathf.Abs(angleWithGround);
            // 通过Clamp确保旋转角度不会使摄像机旋转超过45度与地面的夹角
            var vertical = Mathf.Clamp(rawVertical, -maxVerticalAngle, maxVerticalAngle);

            //var vertical = Mathf.Clamp(Input.GetAxis("Mouse Y") * playerDataConfig.PlayerConfigData.TurnSpeed, -10, 10);
            lastHorizontal = horizontal;

            offset = Quaternion.AngleAxis(horizontal, Vector3.up) * offset;
            offset = Quaternion.AngleAxis(vertical, Vector3.right) * offset;

            var desiredPosition = target.position + offset;
            Vector3 smoothedPosition;
            if (Physics.Raycast(target.position, desiredPosition - target.position, out var hit, offset.magnitude, gameDataConfig.GameConfigData.GroundSceneLayer))
            {
                smoothedPosition = Vector3.Lerp(transform.position, hit.point, playerDataConfig.PlayerConfigData.MouseSpeed);
            }
            else
            {
                smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, playerDataConfig.PlayerConfigData.MouseSpeed);
            }

            transform.position = smoothedPosition;
            transform.LookAt(target);
        }

    }
}