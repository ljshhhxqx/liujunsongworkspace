using System.Collections.Generic;
using HotUpdate.Scripts.Game.Map;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class MoveCollectItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        private MoveInfo _moveInfo;
        
        private Vector3 _currentDirection;
        private Vector3 _currentVelocity;
        private bool _isOnSurface = false;
        private Vector3 _patternOrigin;
        private float _patternTimer;
        private LayerMask _sceneLayer;
        private float collisionCheckDistance = 1f;
        private HashSet<GameObjectData> _collectedItems = new HashSet<GameObjectData>();

        private void FixedUpdate()
        {
            if (!ServerHandler) 
                return;
            // 只在非表面状态下应用模式运动
            if(!_isOnSurface)
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
                _isOnSurface = false;
            }
            
            ApplyMovement();
            UpdateRotation();
            
            _patternTimer += Time.deltaTime;
        }
        
        private bool CheckCollisionAhead()
        {
            if (!GameObjectContainer.Instance.IsIntersect(transform.position, ColliderConfig, _collectedItems))
            {
                return false;
            }
            return _collectedItems.Count > 0;
        }
        
        private void HandleCollision()
        {
            // 发射射线获取碰撞点法线
            if(Physics.Raycast(transform.position, _currentDirection, out var hit, collisionCheckDistance * 1.5f, _sceneLayer))
            {
                Vector3 surfaceNormal = hit.normal;
                
                // 计算新的移动方向：沿着表面切线
                Vector3 newDirection = Vector3.ProjectOnPlane(_currentDirection, surfaceNormal).normalized;
                
                // 如果投影为零（垂直碰撞），选择随机切线方向
                if(newDirection == Vector3.zero)
                {
                    newDirection = GetRandomTangent(surfaceNormal);
                }
                
                _currentDirection = newDirection;
                _currentVelocity = _currentDirection * _moveInfo.speed;
                _isOnSurface = true;
            }
        }
        
        private void ApplyMovement()
        {
            // 直接设置位置
            transform.position += _currentVelocity * Time.fixedDeltaTime;
        }
        
        private void UpdateRotation()
        {
            if(_currentDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_currentDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _moveInfo.rotateSpeed * Time.fixedDeltaTime);
            }
        }
        
        private Vector3 GetRandomTangent(Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if(tangent == Vector3.zero)
                tangent = Vector3.Cross(normal, Vector3.forward);
            return tangent.normalized;
        }
        
        private void ApplyMovementPattern()
        {
            switch(_moveInfo.moveType)
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
            }
        }
        
        private void ApplyLinearPattern()
        {
            // 线性往返运动
            float pingPong = Mathf.PingPong(_patternTimer * _moveInfo.speed * 0.3f, 1f);
            Vector3 targetPos = Vector3.Lerp(_patternOrigin, _moveInfo.TargetPosition, pingPong);
            
            _currentDirection = (targetPos - transform.position).normalized;
            _currentVelocity = _currentDirection * _moveInfo.speed;
        }
        
        private void ApplyCircularPattern()
        {
            // 圆周运动
            float x = Mathf.Cos(_patternTimer * _moveInfo.patternFrequency) * _moveInfo.patternAmplitude;
            float z = Mathf.Sin(_patternTimer * _moveInfo.patternFrequency) * _moveInfo.patternAmplitude;
            Vector3 targetPos = _patternOrigin + new Vector3(x, 0, z);
            
            _currentDirection = (targetPos - transform.position).normalized;
            _currentVelocity = _currentDirection * _moveInfo.speed;
        }
        
        private void ApplySinePattern()
        {
            // 正弦波运动
            float y = Mathf.Sin(_patternTimer * _moveInfo.patternFrequency) * _moveInfo.patternAmplitude;
            Vector3 targetPos = _patternOrigin + new Vector3(0, y, 0);
            
            _currentDirection = (targetPos - transform.position).normalized;
            _currentVelocity = _currentDirection * _moveInfo.speed;
        }

        protected override void OnInitialize()
        {
            _sceneLayer = GameConfigData.stairSceneLayer;
        }

        public void OnSelfSpawn()
        {
            _patternOrigin = transform.position;
            _patternTimer = 0f;
            
            // 初始方向指向目标
            _currentDirection = (_moveInfo.TargetPosition - _patternOrigin).normalized;
            _currentVelocity = _currentDirection * _moveInfo.speed;
        }

        public void Init(MoveInfo moveInfo)
        {
            _moveInfo = moveInfo;
        }

        public void OnSelfDespawn()
        {
            
        }
    }
}