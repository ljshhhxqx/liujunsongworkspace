
using UnityEngine;
using Mirror;
using System.Collections.Generic;
namespace HotUpdate.Scripts
{

    public class NetworkedVelocityController : NetworkBehaviour
    {
        [Header("Physics Settings")]
        public Rigidbody rb;
        public float moveSpeed = 8f;
        public float jumpSpeed = 12f;
        public float groundCheckDistance = 0.2f;
        public LayerMask groundLayer;

        [Header("Network Settings")]
        [SerializeField] private float positionErrorThreshold = 0.15f;
        [SerializeField] private float positionCorrectionStrength = 10f;
        [SerializeField] private float syncInterval = 0.1f;
        
        // 输入队列
        private struct PlayerInput
        {
            public uint inputId;
            public Vector2 moveDirection;
            public bool jumpPressed;
            public float timestamp;
        }
        
        private Queue<PlayerInput> inputQueue = new Queue<PlayerInput>();
        private uint nextInputId = 1;
        private uint lastProcessedInputId = 0;
        
        // 服务器状态
        [SyncVar(hook = nameof(OnServerStateReceived))]
        private ServerState serverState;
        private Vector3 pendingCorrection;
        
        private bool isGrounded;
        private float lastSyncTime;
        private Vector3 lastServerPosition;

        private struct ServerState
        {
            public Vector3 position;
            public Vector3 velocity;
            public uint lastInputId;
            public float timestamp;
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            
            if (isServer)
            {
                ServerFixedUpdate();
            }
            
            if (isLocalPlayer)
            {
                ClientFixedUpdate();
            }
            else
            {
                InterpolatePosition();
            }
        }
        
        private void CheckGrounded()
        {
            isGrounded = Physics.Raycast(transform.position, Vector3.down, 
                groundCheckDistance, groundLayer);
        }
        
        // ================== 服务器端逻辑 ==================
        [Server]
        private void ServerFixedUpdate()
        {
            // 处理输入队列
            while (inputQueue.Count > 0)
            {
                PlayerInput input = inputQueue.Dequeue();
                ApplyMovement(input);
                lastProcessedInputId = input.inputId;
            }
            
            // 定期同步状态
            if (Time.time - lastSyncTime >= syncInterval)
            {
                SyncStateToClients();
                lastSyncTime = Time.time;
            }
        }
        
        [Server]
        private void ApplyMovement(PlayerInput input)
        {
            // 计算水平速度
            Vector3 horizontalVelocity = new Vector3(input.moveDirection.x, 0, input.moveDirection.y) * moveSpeed;
            
            // 保持当前垂直速度（重力）
            float verticalVelocity = rb.velocity.y;
            
            // 跳跃处理
            if (input.jumpPressed && isGrounded)
            {
                verticalVelocity = jumpSpeed;
            }
            
            // 设置最终速度
            rb.velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            
            // 防作弊验证
            ValidateMovement(input);
        }
        
        [Server]
        private void ValidateMovement(PlayerInput input)
        {
            // 1. 速度检查
            if (rb.velocity.magnitude > moveSpeed * 1.5f + jumpSpeed)
            {
                Debug.LogWarning($"可疑速度: {rb.velocity.magnitude}");
                rb.velocity = rb.velocity.normalized * (moveSpeed + jumpSpeed);
            }
            
            // 2. 位置突变检查
            float positionDelta = Vector3.Distance(transform.position, lastServerPosition);
            if (positionDelta > moveSpeed * syncInterval * 2f)
            {
                Debug.LogWarning($"可疑位置变化: {positionDelta}");
                transform.position = lastServerPosition;
            }
            lastServerPosition = transform.position;
        }
        
        [Server]
        private void SyncStateToClients()
        {
            serverState = new ServerState
            {
                position = rb.position,
                velocity = rb.velocity,
                lastInputId = lastProcessedInputId,
                timestamp = Time.time
            };
        }
        
        // ================== 客户端逻辑 ==================
        private void ClientFixedUpdate()
        {
            if (!isLocalPlayer) return;
            
            // 收集输入
            Vector2 moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );
            bool jumpInput = Input.GetButtonDown("Jump");
            
            // 本地预测：设置速度
            Vector3 horizontalVelocity = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed;
            float verticalVelocity = rb.velocity.y;
            
            if (jumpInput && isGrounded)
            {
                verticalVelocity = jumpSpeed;
            }
            
            rb.velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            
            // 发送输入到服务器
            SendInputToServer(moveInput, jumpInput);
            
            // 应用位置校正
            ApplyPositionCorrection();
        }
        
        private void SendInputToServer(Vector2 moveInput, bool jumpInput)
        {
            PlayerInput input = new PlayerInput
            {
                inputId = nextInputId++,
                moveDirection = moveInput,
                jumpPressed = jumpInput,
                timestamp = Time.time
            };
            
            CmdSendPlayerInput(input);
            inputQueue.Enqueue(input);
        }
        
        [Command(channel = Channels.Unreliable)]
        private void CmdSendPlayerInput(PlayerInput input)
        {
            inputQueue.Enqueue(input);
        }
        
        private void OnServerStateReceived(ServerState oldState, ServerState newState)
        {
            if (!isLocalPlayer) return;
            
            // 计算位置误差
            Vector3 positionError = newState.position - rb.position;
            float errorMagnitude = positionError.magnitude;
            
            // 超过阈值则校正
            if (errorMagnitude > positionErrorThreshold)
            {
                pendingCorrection = positionError * positionCorrectionStrength;
                
                // 重放未处理的输入
                ReconcileInputs(newState.lastInputId);
            }
        }
        
        private void ApplyPositionCorrection()
        {
            if (pendingCorrection.magnitude > 0.01f)
            {
                rb.position += pendingCorrection * Time.fixedDeltaTime;
                pendingCorrection = Vector3.Lerp(pendingCorrection, Vector3.zero, 5f * Time.fixedDeltaTime);
            }
        }
        
        private void ReconcileInputs(uint lastServerInputId)
        {
            // 移除已处理的输入
            while (inputQueue.Count > 0 && inputQueue.Peek().inputId <= lastServerInputId)
            {
                inputQueue.Dequeue();
            }
            
            // 保存当前状态
            Vector3 savedPosition = rb.position;
            Vector3 savedVelocity = rb.velocity;
            
            // 重置到服务器状态
            rb.position = serverState.position;
            rb.velocity = serverState.velocity;
            
            // 重放未处理的输入
            foreach (var input in inputQueue)
            {
                // 计算速度
                Vector3 horizontalVelocity = new Vector3(input.moveDirection.x, 0, input.moveDirection.y) * moveSpeed;
                float verticalVelocity = rb.velocity.y;
                
                if (input.jumpPressed && isGrounded)
                {
                    verticalVelocity = jumpSpeed;
                }
                
                rb.velocity = horizontalVelocity + Vector3.up * verticalVelocity;
                
                // 模拟物理步进
                Physics.Simulate(Time.fixedDeltaTime);
            }
            
            // 恢复状态（预测结果）
            rb.position = savedPosition;
            rb.velocity = savedVelocity;
        }
        
        // ================== 其他玩家插值 ==================
        private void InterpolatePosition()
        {
            float t = Mathf.Clamp01(Time.fixedDeltaTime / syncInterval);
            rb.position = Vector3.Lerp(rb.position, serverState.position, t);
            rb.velocity = Vector3.Lerp(rb.velocity, serverState.velocity, t);
        }
        
        // ================== 调试工具 ==================
        void OnGUI()
        {
            if (isLocalPlayer)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, 200));
                GUILayout.Label($"位置误差: {pendingCorrection.magnitude:F3}m");
                GUILayout.Label($"输入队列: {inputQueue.Count}");
                GUILayout.Label($"最后处理ID: {lastProcessedInputId}");
                GUILayout.Label($"速度: {rb.velocity.magnitude:F1}m/s");
                GUILayout.EndArea();
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if (isLocalPlayer)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(serverState.position, 0.5f);
                Gizmos.DrawLine(transform.position, serverState.position);
            }
        }
    }
}