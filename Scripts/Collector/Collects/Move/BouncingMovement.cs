using System;
using UnityEngine;
using Random = UnityEngine.Random;
using HotUpdate.Scripts.Config.ArrayConfig;

namespace HotUpdate.Scripts.Collector.Collects.Move
{
    public class BouncingMovement : IItemMovement
    {
        private readonly BouncingMovementConfig _movementConfig;
        private Transform _transform;
        private Func<Vector3, bool> _checkInsideMap;
        private Func<Vector3, IColliderConfig, bool> _checkObstacle;
        private IColliderConfig _colliderConfig;
    
        private float _currentHeight;
        private float _currentVelocity;
        private float _currentBounceHeight;
        private Vector3 _currentPosition;
        private Vector3 _horizontalVelocity;
        private float _lastBounceTime;
        private Vector3 _lastSafePosition;
    
        public BouncingMovement(BouncingMovementConfig movementConfig)
        {
            _movementConfig = movementConfig;
            _currentBounceHeight = movementConfig.bounceHeight;
        }
    
        public void Initialize(Transform ts, 
            IColliderConfig colliderConfig, 
            Func<Vector3, bool> insideMapCheck, 
            Func<Vector3, IColliderConfig, bool> obstacleCheck)
        {
            _transform = ts;
            _checkInsideMap = insideMapCheck;
            _checkObstacle = obstacleCheck;
            _colliderConfig = colliderConfig;
        
            _currentPosition = ts.position;
            _currentHeight = _movementConfig.groundLevel;
            _lastSafePosition = _currentPosition;
        
            // 初始水平速度
            _horizontalVelocity = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized * (_movementConfig.bounceSpeed * 0.5f);
        }
    
        public void UpdateMovement(float deltaTime)
        {
            // 垂直运动计算（模拟物理）
            _currentVelocity -= Physics.gravity.magnitude * deltaTime; // 重力加速度
            _currentHeight += _currentVelocity * deltaTime;
        
            // 触地反弹
            if (_currentHeight <= _movementConfig.groundLevel)
            {
                _currentHeight = _movementConfig.groundLevel;
                _currentVelocity = -_currentVelocity * _movementConfig.bounceDecay; // 反弹
            
                // 更新弹跳高度
                _currentBounceHeight *= _movementConfig.bounceDecay;
                if (_currentBounceHeight < _movementConfig.minBounceHeight)
                {
                    _currentBounceHeight = _movementConfig.minBounceHeight;
                }
            
                // 随机改变水平方向
                if (Random.value > 0.7f)
                {
                    _horizontalVelocity = Quaternion.Euler(0, Random.Range(-45f, 45f), 0) * _horizontalVelocity;
                }
            
                _lastBounceTime = Time.time;
            }
        
            // 水平运动
            Vector3 targetPosition = _currentPosition + _horizontalVelocity * deltaTime;
            Vector3 verticalOffset = (Vector3)_movementConfig.bounceDirection * _currentHeight;
        
            Vector3 newPosition = targetPosition + verticalOffset;
        
            // 边界和障碍检测
            if (ValidatePosition(newPosition, targetPosition))
            {
                _currentPosition = targetPosition;
                _transform.position = newPosition;
                _lastSafePosition = newPosition;
            }
            else
            {
                // 撞到边界或障碍，反弹
                HandleCollision(newPosition);
            }
        }
    
        private bool ValidatePosition(Vector3 testPosition, Vector3 horizontalPos)
        {
            // 检查边界
            if (!_checkInsideMap(testPosition) || !_checkInsideMap(horizontalPos))
                return false;
        
            // 检查垂直方向的障碍（使用射线检测）
            if (_checkObstacle(testPosition, _colliderConfig))
                return false;
        
            // 检查水平方向的障碍
            if (_checkObstacle(horizontalPos, _colliderConfig))
                return false;
        
            return true;
        }
    
        private void HandleCollision(Vector3 collisionPoint)
        {
            // 计算反射方向
            Vector3 hitNormal = (collisionPoint - _lastSafePosition).normalized;
            hitNormal.y = 0; // 保持水平反射
        
            if (hitNormal.magnitude > 0.1f)
            {
                // 反射水平速度
                _horizontalVelocity = Vector3.Reflect(_horizontalVelocity, hitNormal) * _movementConfig.bounceDecay;
            
                // 稍微随机化反射方向
                _horizontalVelocity = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * _horizontalVelocity;
            }
        
            // 回到安全位置
            _currentPosition = _lastSafePosition;
            _transform.position = _currentPosition + (Vector3)_movementConfig.bounceDirection * _currentHeight;
        
            // 增加一点垂直弹跳
            _currentVelocity = Mathf.Abs(_currentVelocity) * 0.5f;
        }
    
        public void ResetMovement()
        {
            _currentHeight = _movementConfig.groundLevel;
            _currentVelocity = 0;
            _currentBounceHeight = _movementConfig.bounceHeight;
            _currentPosition = _transform.position;
            _lastSafePosition = _currentPosition;
        
            // 随机初始水平速度
            _horizontalVelocity = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized * (_movementConfig.bounceSpeed * 0.5f);
        }
    
        public Vector3 GetPredictedPosition(float timeAhead)
        {
            // 简单的线性预测（实际需要更复杂的物理预测）
            Vector3 predictedPos = _currentPosition + _horizontalVelocity * timeAhead;
        
            // 预测垂直运动（简谐运动近似）
            float predictedHeight = Mathf.Max(_movementConfig.groundLevel, 
                _currentHeight + _currentVelocity * timeAhead - 4.9f * timeAhead * timeAhead);
        
            return predictedPos + (Vector3)_movementConfig.bounceDirection * predictedHeight;
        }
    }
}