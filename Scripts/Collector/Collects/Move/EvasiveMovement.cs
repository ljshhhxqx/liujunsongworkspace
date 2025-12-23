using System;
using UnityEngine;
using HotUpdate.Scripts.Config.ArrayConfig;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector.Collects.Move
{
    public class EvasiveMovement : IItemMovement
    {
        private readonly EvasiveMovementConfig _evasiveMovementConfig;
        private Transform _transform;
        private Func<Vector3, bool> _checkInsideMap;
        private Func<Vector3, IColliderConfig, bool> _checkObstacle;
        private IColliderConfig _colliderConfig;
    
        private Transform _playerTransform;
        private Vector3 _currentVelocity;
        private Vector3 _wanderTarget;
        private float _directionTimer;
        private Vector3 _lastSafePosition;
        private bool _isEscaping;
        private float _jumpTimer;
    
        public EvasiveMovement(EvasiveMovementConfig evasiveMovementConfig)
        {
            _evasiveMovementConfig = evasiveMovementConfig;
        }
    
        public void Initialize(Transform ts, 
            IColliderConfig colliderConfig, 
            Func<Vector3, bool> insideMapCheck, 
            Func<Vector3, IColliderConfig, bool> obstacleCheck)
        {
            _transform = ts;
            _checkInsideMap = insideMapCheck;
            _checkObstacle = obstacleCheck;
        
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            _lastSafePosition = ts.position;
            _wanderTarget = GetRandomWanderTarget();
            _directionTimer = _evasiveMovementConfig.directionChangeInterval;
        }
    
        public void UpdateMovement(float deltaTime)
        {
            if (!_playerTransform)
                return;
        
            Vector3 toPlayer = _playerTransform.position - _transform.position;
            float distanceToPlayer = toPlayer.magnitude;
        
            _isEscaping = distanceToPlayer < _evasiveMovementConfig.detectionRadius && 
                         distanceToPlayer > _evasiveMovementConfig.minSafeDistance;
        
            Vector3 desiredVelocity = _isEscaping ? 
                CalculateEscapeVelocity(toPlayer, distanceToPlayer) : 
                CalculateWanderVelocity();
        
            // 避障
            if (_evasiveMovementConfig.useObstacleAvoidance)
            {
                desiredVelocity += CalculateObstacleAvoidance() * _evasiveMovementConfig.avoidanceWeight;
            }
        
            // 平滑转向
            _currentVelocity = Vector3.Lerp(_currentVelocity, desiredVelocity, 5f * deltaTime);
        
            // 跳跃行为
            if (_evasiveMovementConfig.canJump && _isEscaping)
            {
                _jumpTimer -= deltaTime;
                if (_jumpTimer <= 0 && Random.value < _evasiveMovementConfig.jumpChance * deltaTime)
                {
                    _currentVelocity.y = Mathf.Abs(_currentVelocity.y) + 3f;
                    _jumpTimer = 1f;
                }
            }
        
            // 应用重力
            if (_transform.position.y > 0.1f)
            {
                _currentVelocity.y -= Physics.gravity.magnitude * deltaTime;
            }
            else
            {
                _currentVelocity.y = 0;
                _transform.position = new Vector3(_transform.position.x, 0, _transform.position.z);
            }
        
            // 计算新位置
            Vector3 newPosition = _transform.position + _currentVelocity * deltaTime;
        
            if (ValidatePosition(newPosition))
            {
                _transform.position = newPosition;
                _lastSafePosition = _transform.position;
            }
            else
            {
                // 撞到边界或障碍，调整方向
                HandleCollision();
            }
        
            // 更新方向计时器
            _directionTimer -= deltaTime;
            if (_directionTimer <= 0)
            {
                _wanderTarget = GetRandomWanderTarget();
                _directionTimer = _evasiveMovementConfig.directionChangeInterval;
            }
        }
    
        private Vector3 CalculateEscapeVelocity(Vector3 toPlayer, float distance)
        {
            // 基本逃避方向
            Vector3 escapeDir = -toPlayer.normalized;
        
            // 预测玩家移动
            Rigidbody playerRb = _playerTransform.GetComponent<Rigidbody>();
            if (playerRb)
            {
                Vector3 predictedPlayerPos = _playerTransform.position + playerRb.velocity * _evasiveMovementConfig.playerPredictionFactor;
                escapeDir = -(predictedPlayerPos - _transform.position).normalized;
            }
        
            // 添加随机性使逃避更真实
            float randomness = Mathf.Clamp01(1f - distance / _evasiveMovementConfig.detectionRadius);
            escapeDir = Quaternion.Euler(0, Random.Range(-30f, 30f) * randomness, 0) * escapeDir;
        
            return escapeDir * _evasiveMovementConfig.escapeSpeed;
        }
    
        private Vector3 CalculateWanderVelocity()
        {
            Vector3 toTarget = _wanderTarget - _transform.position;
        
            if (toTarget.magnitude < 0.5f)
            {
                _wanderTarget = GetRandomWanderTarget();
                toTarget = _wanderTarget - _transform.position;
            }
        
            return toTarget.normalized * _evasiveMovementConfig.wanderSpeed;
        }
    
        private Vector3 CalculateObstacleAvoidance()
        {
            Vector3 avoidance = Vector3.zero;
            float[] rayAngles = { 0, 45, -45, 90, -90 };
            float rayDistance = 2f;
        
            foreach (float angle in rayAngles)
            {
                Vector3 rayDir = Quaternion.Euler(0, angle, 0) * _currentVelocity.normalized;
                Vector3 rayStart = _transform.position + Vector3.up * 0.5f;
            
                // 使用你的障碍检测方法
                if (_checkObstacle(rayStart + rayDir * rayDistance, _colliderConfig))
                {
                    // 计算避开方向（向右偏转）
                    Vector3 avoidDir = Quaternion.Euler(0, 90, 0) * rayDir;
                    avoidance += avoidDir * (rayDistance / 2f);
                }
            }
        
            return avoidance.normalized;
        }
    
        private Vector3 GetRandomWanderTarget()
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-5f, 5f),
                0,
                Random.Range(-5f, 5f)
            );
        
            Vector3 target = _transform.position + randomOffset;
        
            // 确保目标位置有效
            int attempts = 0;
            while (!ValidatePosition(target) && attempts < 10)
            {
                randomOffset = Quaternion.Euler(0, 90, 0) * randomOffset;
                target = _transform.position + randomOffset;
                attempts++;
            }
        
            return target;
        }
    
        private bool ValidatePosition(Vector3 position)
        {
            return _checkInsideMap(position) && !_checkObstacle(position, _colliderConfig);
        }
    
        private void HandleCollision()
        {
            // 计算反射方向
            Vector3 reflectDir = Vector3.Reflect(_currentVelocity.normalized, Vector3.up);
            _currentVelocity = reflectDir * (_currentVelocity.magnitude * 0.7f);
        
            // 回到安全位置
            _transform.position = _lastSafePosition;
        
            // 设置新的徘徊目标
            _wanderTarget = GetRandomWanderTarget();
        }
    
        public void ResetMovement()
        {
            _currentVelocity = Vector3.zero;
            _wanderTarget = GetRandomWanderTarget();
            _directionTimer = _evasiveMovementConfig.directionChangeInterval;
            _lastSafePosition = _transform.position;
            _isEscaping = false;
        }
    
        public Vector3 GetPredictedPosition(float timeAhead)
        {
            // 简单线性预测
            return _transform.position + _currentVelocity * timeAhead;
        }
    }
}