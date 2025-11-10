using HotUpdate.Scripts.Game.Map;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class MoveCollectItem : CollectBehaviour
    {
        [Header("运动设置")]
        public float moveSpeed = 3f;
        public float rotationSpeed = 5f;
        
        [Header("碰撞检测")]
        public float collisionCheckDistance = 0.5f;
        public float sphereCheckRadius = 0.3f;
        
        private Vector3 currentDirection;
        private Vector3 currentVelocity;
        private bool isOnSurface = false;
        private Vector3 patternOrigin;
        private float patternTimer;
        private Vector3 linearTarget;
        public MoveType movementPattern = MoveType.Linear;
        public float patternAmplitude = 2f;
        public float patternFrequency = 1f;
        private LayerMask _sceneLayer;
        
        private void Start()
        {
            patternOrigin = transform.position;
            patternTimer = 0f;
            
            // 设置线性运动的目标点
            linearTarget = patternOrigin + new Vector3(
                Random.Range(-patternAmplitude, patternAmplitude),
                0,
                Random.Range(-patternAmplitude, patternAmplitude)
            );
            
            // 初始方向指向目标
            currentDirection = (linearTarget - patternOrigin).normalized;
            currentVelocity = currentDirection * moveSpeed;
            // 随机初始方向
            currentDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            currentVelocity = currentDirection * moveSpeed;
        }

        private void FixedUpdate()
        {
            if (!ServerHandler) 
                return;
            // 只在非表面状态下应用模式运动
            if(!isOnSurface)
            {
                ApplyMovementPattern();
            }
            
            // 碰撞检测和基础运动
            if(CheckCollisionAhead())
            {
                HandleCollision();
            }
            else
            {
                isOnSurface = false;
            }
            
            ApplyMovement();
            UpdateRotation();
            
            patternTimer += Time.deltaTime;
        }
        
        private bool CheckCollisionAhead()
        {
            if (!GameObjectContainer.Instance.IsIntersect(transform.position, ColliderConfig))
            {
                return false;
            }
            // 使用球形检测前方碰撞
            Vector3 checkPosition = transform.position + currentDirection * collisionCheckDistance;
            return Physics.CheckSphere(checkPosition, sphereCheckRadius, _sceneLayer);
        }
        
        private void HandleCollision()
        {
            // 发射射线获取碰撞点法线
            if(Physics.Raycast(transform.position, currentDirection, out var hit, collisionCheckDistance * 1.5f, _sceneLayer))
            {
                Vector3 surfaceNormal = hit.normal;
                
                // 计算新的移动方向：沿着表面切线
                Vector3 newDirection = Vector3.ProjectOnPlane(currentDirection, surfaceNormal).normalized;
                
                // 如果投影为零（垂直碰撞），选择随机切线方向
                if(newDirection == Vector3.zero)
                {
                    newDirection = GetRandomTangent(surfaceNormal);
                }
                
                currentDirection = newDirection;
                currentVelocity = currentDirection * moveSpeed;
                isOnSurface = true;
            }
        }
        
        private void ApplyMovement()
        {
            // 直接设置位置
            transform.position += currentVelocity * Time.deltaTime;
        }
        
        private void UpdateRotation()
        {
            if(currentDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(currentDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        private Vector3 GetRandomTangent(Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if(tangent == Vector3.zero)
                tangent = Vector3.Cross(normal, Vector3.forward);
            return tangent.normalized;
        }
        
        // // 可视化调试
        // private void OnDrawGizmosSelected()
        // {
        //     Gizmos.color = Color.blue;
        //     Gizmos.DrawRay(transform.position, currentDirection * collisionCheckDistance);
        //     
        //     Gizmos.color = Color.red;
        //     Vector3 spherePos = transform.position + currentDirection * collisionCheckDistance;
        //     Gizmos.DrawWireSphere(spherePos, sphereCheckRadius);
        // }
        
        private void ApplyMovementPattern()
        {
            switch(movementPattern)
            {
                case MoveType.Linear:
                    ApplyLinearPattern();
                    break;
                case MoveType.Circular:
                    ApplyCircularPattern();
                    break;
                case MoveType.SineWave:
                    ApplySinePattern();
                    break;
                case MoveType.Bounce:
                    ApplyBouncePattern();
                    break;
            }
        }
        
        private void ApplyLinearPattern()
        {
            // 线性往返运动
            float pingPong = Mathf.PingPong(patternTimer * moveSpeed * 0.3f, 1f);
            Vector3 targetPos = Vector3.Lerp(patternOrigin, linearTarget, pingPong);
            
            currentDirection = (targetPos - transform.position).normalized;
            currentVelocity = currentDirection * moveSpeed;
        }
        
        private void ApplyCircularPattern()
        {
            // 圆周运动
            float x = Mathf.Cos(patternTimer * patternFrequency) * patternAmplitude;
            float z = Mathf.Sin(patternTimer * patternFrequency) * patternAmplitude;
            Vector3 targetPos = patternOrigin + new Vector3(x, 0, z);
            
            currentDirection = (targetPos - transform.position).normalized;
            currentVelocity = currentDirection * moveSpeed;
        }
        
        private void ApplySinePattern()
        {
            // 正弦波运动
            float y = Mathf.Sin(patternTimer * patternFrequency) * patternAmplitude;
            Vector3 targetPos = patternOrigin + new Vector3(0, y, 0);
            
            currentDirection = (targetPos - transform.position).normalized;
            currentVelocity = currentDirection * moveSpeed;
        }
        
        private void ApplyBouncePattern()
        {
            // 简单的反弹模式
            // 如果检测到碰撞，方向已经在HandleCollision中处理
            // 这里只需保持当前速度
            currentVelocity = currentDirection * moveSpeed;
        }

        protected override void OnInitialize()
        {
            _sceneLayer = GameConfigData.stairSceneLayer;
        }
    }

    public enum MoveType
    {
        Linear,
        Circular, 
        SineWave, 
        Bounce
    }
}