using System;
using AOTScripts.Data;
using UnityEngine;
using Random = UnityEngine.Random;
using HotUpdate.Scripts.Config.ArrayConfig;

namespace HotUpdate.Scripts.Collector.Collects.Move
{
    public class PeriodicMovement : IItemMovement
    {
        private readonly PeriodicMovementConfig _periodicMovementConfig;
        private Transform _transform;
        private Func<Vector3, bool> _checkInsideMap;
        private Func<Vector3, IColliderConfig, bool> _checkObstacle;
        private IColliderConfig _colliderConfig;

        private readonly AnimationCurve _animationCurve = AnimationCurve.Linear(0, 1, 1, 1);
        private float _timeCounter;
        private Vector3 _startPosition;
        private Vector3 _lastValidPosition;
        private int _currentWaypointIndex;
        private float _waypointProgress;
        private Vector3[] _waypoints;

        public PeriodicMovement(PeriodicMovementConfig periodicMovementConfig)
        {
            _periodicMovementConfig = periodicMovementConfig;
        }

        public void Initialize(Transform ts,
            IColliderConfig colliderConfig,
            Func<Vector3, bool> insideMapCheck,
            Func<Vector3, IColliderConfig, bool> obstacleCheck)
        {
            _transform = ts;
            _checkInsideMap = insideMapCheck;
            _checkObstacle = obstacleCheck;

            _startPosition = ts.position;
            _lastValidPosition = _startPosition;
            _timeCounter = Random.Range(0f, Mathf.PI * 2f); // 随机起始相位
            _currentWaypointIndex = 0;
            _waypoints = _periodicMovementConfig.waypoints ?? MapBoundDefiner.Instance.GetWaypointPositions(ts.position);
        }

        public void UpdateMovement(float deltaTime)
        {
            _timeCounter += deltaTime * _periodicMovementConfig.frequency;

            Vector3 targetPosition = CalculateTargetPosition();

            // 平滑移动
            Vector3 newPosition = Vector3.Lerp(
                _transform.position,
                targetPosition,
                _periodicMovementConfig.moveSpeed * deltaTime * _animationCurve.Evaluate(_timeCounter % 1f)
            );

            if (ValidatePosition(newPosition))
            {
                _transform.position = newPosition;
                _lastValidPosition = newPosition;
            }
            else
            {
                // 遇到障碍，反向或调整
                HandleObstacle();
            }
        }

        private Vector3 CalculateTargetPosition()
        {
            Vector3 offset = Vector3.zero;

            switch (_periodicMovementConfig.pathType)
            {
                case PathType.Horizontal:
                    offset.x = Mathf.Sin(_timeCounter) * _periodicMovementConfig.amplitude;
                    break;

                case PathType.Vertical:
                    offset.y = Mathf.Sin(_timeCounter) * _periodicMovementConfig.amplitude;
                    break;

                case PathType.Circular:
                    offset.x = Mathf.Sin(_timeCounter) * _periodicMovementConfig.amplitude;
                    offset.z = Mathf.Cos(_timeCounter) * _periodicMovementConfig.amplitude;
                    break;

                case PathType.Lissajous:
                    offset.x = Mathf.Sin(_timeCounter) * _periodicMovementConfig.amplitude;
                    offset.z = Mathf.Sin(_timeCounter * 1.5f + Mathf.PI / 2) * _periodicMovementConfig.amplitude * 0.8f;
                    offset.y = Mathf.Sin(_timeCounter * 2f) * _periodicMovementConfig.amplitude * 0.3f;
                    break;

                case PathType.Square:
                    offset = CalculateSquarePath(_timeCounter);
                    break;

                case PathType.CustomWaypoints:
                    return CalculateWaypointPosition();
            }

            offset.Scale(_periodicMovementConfig.axisMultiplier);
            return _startPosition + offset;
        }

        private Vector3 CalculateSquarePath(float time)
        {
            float period = Mathf.PI * 2f;
            float t = time % period;
            float sideLength = _periodicMovementConfig.amplitude * 2f;

            float segment = period / 4f;
            int side = Mathf.FloorToInt(t / segment);
            float progress = (t % segment) / segment;

            return side switch
            {
                0 => new Vector3(-_periodicMovementConfig.amplitude + sideLength * progress, 0,
                    -_periodicMovementConfig.amplitude),
                1 => new Vector3(_periodicMovementConfig.amplitude, 0,
                    -_periodicMovementConfig.amplitude + sideLength * progress),
                2 => new Vector3(_periodicMovementConfig.amplitude - sideLength * progress, 0,
                    _periodicMovementConfig.amplitude),
                _ => new Vector3(-_periodicMovementConfig.amplitude, 0,
                    _periodicMovementConfig.amplitude - sideLength * progress)
            };
        }

        private Vector3 CalculateWaypointPosition()
        {
            if (_waypoints == null || _waypoints.Length == 0)
                return _startPosition;

            int nextIndex = (_currentWaypointIndex + 1) % _waypoints.Length;

            if (!_periodicMovementConfig.loopWaypoints && nextIndex < _currentWaypointIndex)
                nextIndex = _currentWaypointIndex;

            Vector3 currentWaypoint = _waypoints[_currentWaypointIndex];
            Vector3 nextWaypoint = _waypoints[nextIndex];

            _waypointProgress += _periodicMovementConfig.moveSpeed * Time.deltaTime;
            float distance = Vector3.Distance(currentWaypoint, nextWaypoint);

            if (_waypointProgress >= 1f)
            {
                _waypointProgress = 0f;
                _currentWaypointIndex = nextIndex;

                if (!_periodicMovementConfig.loopWaypoints &&
                    _currentWaypointIndex == _waypoints.Length - 1)
                {
                    // 到达终点，反向
                    Array.Reverse(_waypoints);
                    _currentWaypointIndex = 0;
                }
            }

            return Vector3.Lerp(currentWaypoint, nextWaypoint, _waypointProgress);
        }

        private bool ValidatePosition(Vector3 position)
        {
            return _checkInsideMap(position) && !_checkObstacle(position, _colliderConfig);
        }

        private void HandleObstacle()
        {
            // 反向运动
            _timeCounter += Mathf.PI;

            // 尝试轻微调整方向
            _startPosition = _lastValidPosition + new Vector3(
                Random.Range(-0.5f, 0.5f),
                0,
                Random.Range(-0.5f, 0.5f)
            );
        }

        public void ResetMovement()
        {
            _timeCounter = Random.Range(0f, Mathf.PI * 2f);
            _startPosition = _transform.position;
            _lastValidPosition = _startPosition;
            _currentWaypointIndex = 0;
            _waypointProgress = 0f;
        }

        public Vector3 GetPredictedPosition(float timeAhead)
        {
            float predictedTime = _timeCounter + timeAhead * _periodicMovementConfig.frequency;
            return CalculateTargetPositionFromTime(predictedTime);
        }

        private Vector3 CalculateTargetPositionFromTime(float time)
        {
            // 简化计算，实际应根据具体路径类型计算
            Vector3 offset = Vector3.zero;

            if (_periodicMovementConfig.pathType == PathType.Circular)
            {
                offset.x = Mathf.Sin(time) * _periodicMovementConfig.amplitude;
                offset.z = Mathf.Cos(time) * _periodicMovementConfig.amplitude;
            }

            offset.Scale(_periodicMovementConfig.axisMultiplier);
            return _startPosition + offset;
        }
    }
}