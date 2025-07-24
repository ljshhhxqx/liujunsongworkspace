using System;
using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Tool.Coroutine;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerPhysicsCalculator : IPlayerStateCalculator
    {
        [Header("Components")]
        private readonly PhysicsComponent _physicsComponent;
        [Header("ConfigData")]
        private static PhysicsDetermineConstant _physicsDetermineConstant;
        
        [Header("Params")]
        private bool _isOnSlope;
        private Vector3 _slopeNormal;
        private float _slopeAngle;
        private Vector3 _stairsNormal;
        private Vector3 _stairsHitNormal;
        private PlayerEnvironmentState _playerEnvironmentState;
        private float _currentSpeed;
        private float _verticalSpeed;
        public float GroundDistance { get; private set; }
        
        public float CurrentSpeed
        {
            get => _currentSpeed;
            set => _currentSpeed = value;
        }

        public PlayerPhysicsCalculator(PhysicsComponent component, bool isClient = true)
        {
            _physicsComponent = component;
            IsClient = isClient;
        }
        
        public static void SetPhysicsDetermineConstant(PhysicsDetermineConstant constant)
        {
            _physicsDetermineConstant = constant;
        }
        
        public PlayerEnvironmentState CheckPlayerState(CheckGroundDistanceParam param)
        {
            CheckGroundDistance(param);
            PlayerEnvironmentState newEnvironmentState; // 默认保持当前状态

            // 检查楼梯状态
            if (CheckStairs(out _stairsNormal, out _stairsHitNormal))
            {
                newEnvironmentState = PlayerEnvironmentState.OnStairs;
            }
            // 如果不在楼梯上，检查是否在地面
            else if (GroundDistance <= _physicsDetermineConstant.GroundMinDistance)
            {
                newEnvironmentState = PlayerEnvironmentState.OnGround;
            }
            // 既不在楼梯也不在地面，则在空中
            else
            {
                newEnvironmentState = PlayerEnvironmentState.InAir;
            }

            // 只有状态发生改变时才更新
            if (newEnvironmentState != _playerEnvironmentState)
            {
                _playerEnvironmentState = newEnvironmentState;

                // 如果新状态是楼梯状态，更新朝向
                if (newEnvironmentState == PlayerEnvironmentState.OnStairs)
                {
                    // 计算垂直于楼梯的方向（使用楼梯的法线）
                    var desiredForward = -_stairsHitNormal;
                    // 保持y轴垂直，只在水平面上旋转
                    desiredForward.y = 0;
                    desiredForward.Normalize();
    
                    // 立即更新玩家朝向
                    _physicsComponent.Transform.rotation = Quaternion.LookRotation(desiredForward);
                }
            }
            return _playerEnvironmentState;
        }

        public void HandlePlayerJump()
        {
            if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                // 清除当前垂直速度
                var vel = _physicsComponent.Rigidbody.velocity;
                vel.y = 0f;
                _physicsComponent.Rigidbody.velocity = vel;
            
                // 应用跳跃力
                var jumpDirection = _isOnSlope ? Vector3.Lerp(Vector3.up, _slopeNormal, 0.5f) : Vector3.up;
                _physicsComponent.Rigidbody.AddForce(jumpDirection * _physicsDetermineConstant.JumpSpeed, ForceMode.Impulse);
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                _physicsComponent.Rigidbody.velocity = Vector3.zero;
                _physicsComponent.Rigidbody.MovePosition(_physicsComponent.Rigidbody.transform.position + _stairsHitNormal.normalized);
                _physicsComponent.Rigidbody.AddForce(_stairsHitNormal.normalized * _physicsDetermineConstant.JumpSpeed / 5f, ForceMode.Impulse);
            }
        }

        public float CheckGroundDistance(CheckGroundDistanceParam param)
        {
            if (_physicsComponent.CapsuleCollider)
            {
                var radius = _physicsComponent.CapsuleCollider.radius * 0.9f;
                var dist = 10f;
                _isOnSlope = false;

                // 向下的射线检测
                var ray2 = new Ray(_physicsComponent.Transform.position + new Vector3(0, _physicsComponent.CapsuleCollider.height / 2, 0), Vector3.down);
                if (Physics.Raycast(ray2, out var groundHit, _physicsComponent.CapsuleCollider.height / 2 + dist,
                        _physicsDetermineConstant.GroundSceneLayer) && !groundHit.collider.isTrigger)
                {
                    dist = _physicsComponent.Transform.position.y - groundHit.point.y;

                    // 检查斜坡
                    _slopeNormal = groundHit.normal;
                    _slopeAngle = Vector3.Angle(Vector3.up, _slopeNormal);
                    _isOnSlope = _slopeAngle != 0f && _slopeAngle <= _physicsDetermineConstant.MaxSlopeAngle;
                }

                // 球形检测
                if (dist >= _physicsDetermineConstant.GroundMinDistance)
                {
                    var forwardOffset = param.InputMovement.magnitude > 0f ? (param.InputMovement.normalized * (radius * 0.5f)) : Vector3.zero;
                    var pos = _physicsComponent.Transform.position + Vector3.up * (_physicsComponent.CapsuleCollider.radius) + forwardOffset;
                    var ray = new Ray(pos, -Vector3.up);

                    if (Physics.SphereCast(ray, radius, out groundHit, _physicsComponent.CapsuleCollider.radius + _physicsDetermineConstant.GroundMaxDistance,
                            _physicsDetermineConstant.GroundSceneLayer) && !groundHit.collider.isTrigger)
                    {
                        Physics.Linecast(groundHit.point + (Vector3.up * 0.1f), groundHit.point + Vector3.down * 0.15f,
                            out groundHit, _physicsDetermineConstant.GroundSceneLayer);
                        var newDist = _physicsComponent.Transform.position.y - groundHit.point.y;
                        if (dist > newDist)
                        {
                            dist = newDist;
                            // 更新斜坡信息
                            _slopeNormal = groundHit.normal;
                            _slopeAngle = Vector3.Angle(Vector3.up, _slopeNormal);
                            _isOnSlope = _slopeAngle != 0f && _slopeAngle <= _physicsDetermineConstant.MaxSlopeAngle;
                        }
                    }
                }

                GroundDistance = Mathf.Clamp((float)Math.Round(dist, 2), 0f, _physicsDetermineConstant.GroundMaxDistance);
                var hasMovementInput = param.InputMovement.magnitude > 0f;
                //_playerAnimationComponent.SetGroundDistance(_groundDistance);

                if (_playerEnvironmentState == PlayerEnvironmentState.InAir)
                {
                    _physicsComponent.Rigidbody.useGravity = true;
                    var inputSmooth = Vector3.zero;
                    inputSmooth = Vector3.Lerp(inputSmooth, param.InputMovement, 6f * param.FixedDeltaTime);
        
                    if (hasMovementInput)
                    {
                        var airMovement = _physicsComponent.Camera.transform.TransformDirection(inputSmooth).normalized * _currentSpeed;
                        airMovement.y = _physicsComponent.Rigidbody.velocity.y;
                        _physicsComponent.Rigidbody.velocity = Vector3.Lerp(_physicsComponent.Rigidbody.velocity, airMovement,  param.FixedDeltaTime * 2f);
                    }

                    // 应用额外重力
                    _physicsComponent.Rigidbody.AddForce(Physics.gravity * param.FixedDeltaTime, ForceMode.VelocityChange);
                    //_verticalSpeed = _rigidbody.velocity.y;
                }
                else
                {
                    _physicsComponent.Rigidbody.velocity = hasMovementInput ? _physicsComponent.Rigidbody.velocity : Vector3.zero;
                    //_verticalSpeed = 0f;
                }
            }
            return GroundDistance;
        }
        
        private bool CheckStairs(out Vector3 direction, out Vector3 hitNormal)
        {
            direction = Vector3.zero;
            hitNormal = Vector3.zero;

            if (Physics.Raycast(_physicsComponent.CheckStairsTransform.position, _physicsComponent.CheckStairsTransform.forward, out var hit, _physicsDetermineConstant.StairsCheckDistance, _physicsDetermineConstant.StairsSceneLayer))
            {
                hitNormal = hit.normal;
                direction = Vector3.Cross(hit.normal, _physicsComponent.CheckStairsTransform.right).normalized;
                return true;
            }
            return false;
        }

        public void HandleMove(MoveParam moveParam, bool isLocalPlayer = true)
        {
            //Debug.Log($"[HandleMove] START  moveParam.InputMovement-> {moveParam.InputMovement}  moveParam.IsClearVelocity-> {moveParam.IsClearVelocity} moveParam.IsMovingState-> {moveParam.IsMovingState}  moveParam.CameraForward-> {moveParam.CameraForward}  moveParam.DeltaTime-> {moveParam.DeltaTime} isLocalPlayer-> {isLocalPlayer}");
            var hasMovementInput = moveParam.InputMovement.magnitude > 0f;
            Vector3 movement;
            if (_playerEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                _physicsComponent.Rigidbody.useGravity = false;
                movement = moveParam.InputMovement.z * -_stairsNormal.normalized + _physicsComponent.Transform.right * moveParam.InputMovement.x;
                if (hasMovementInput)
                {
                    var moveDirection = movement.normalized;
                    var targetVelocity = moveDirection * _currentSpeed;
                    targetVelocity += _stairsHitNormal.normalized * -2f;
                    _physicsComponent.Rigidbody.velocity = moveParam.IsClearVelocity ? Vector3.zero : targetVelocity;
                }
                else
                {
                    _physicsComponent.Rigidbody.velocity = moveParam.IsClearVelocity ? Vector3.zero : _stairsHitNormal.normalized * -2f;
                }
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                _physicsComponent.Rigidbody.useGravity = true;
                if (isLocalPlayer)
                {
                    var camForward = Vector3.Scale(_physicsComponent.Camera.transform.forward, new Vector3(1, 0, 1))
                        .normalized;
                    var moveDir = (moveParam.InputMovement.z * camForward + moveParam.InputMovement.x * _physicsComponent.Camera.transform.right);
                    movement = moveDir.normalized * moveParam.InputMovement.magnitude;
                }
                else
                {
                    var yaw = DecompressYaw(moveParam.CameraForward);
                    var rotation = Quaternion.Euler(0, yaw, 0);
                    movement = rotation * Vector3.forward * moveParam.InputMovement.z
                               + rotation * Vector3.right * moveParam.InputMovement.x;
                }

                movement.y = 0f;
                // 计算目标位置和速度
                var targetVelocity = moveParam.IsClearVelocity ? Vector3.zero : movement.normalized * _currentSpeed;
                // 保持垂直速度
                targetVelocity.y = _physicsComponent.Rigidbody.velocity.y;
                if (_isOnSlope)
                {
                    // 在斜坡上时，调整移动方向
                    var slopeMovementDirection = Vector3.ProjectOnPlane(movement, _slopeNormal).normalized;
                    targetVelocity = slopeMovementDirection * _currentSpeed;
                    targetVelocity.y = _physicsComponent.Rigidbody.velocity.y;

                    if (hasMovementInput)
                    {
                        _physicsComponent.Rigidbody.AddForce(-_slopeNormal * 20f, ForceMode.Force);
                    }
                }
                // 应用速度
                _physicsComponent.Rigidbody.velocity = hasMovementInput ? targetVelocity : Vector3.zero;
                HandlePlayerRotation(moveParam, movement);
            }
        }

        private void HandlePlayerRotation(MoveParam param, Vector3 movement)
        {
            var canRotate = _playerEnvironmentState is PlayerEnvironmentState.OnGround;
            var isMoving = param.IsMovingState;
            if (param.InputMovement.magnitude > 0.1f && canRotate && isMoving)
            {
                //前进方向转化为摄像机面对的方向
                var movementDirection = movement.normalized;
                var targetRotation = Quaternion.LookRotation(movementDirection);
                targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
                _physicsComponent.Transform.rotation = Quaternion.Slerp( _physicsComponent.Transform.rotation, targetRotation, param.DeltaTime * _physicsDetermineConstant.RotateSpeed);
            }
        }
        
        public float DecompressYaw(ushort compressed)
        {
            return compressed * 360f / 65535f;
        }
        
        public ushort CompressYaw(float yaw)
        {
            return (ushort)(yaw % 360f * 65535f / 360f);
        }

        public void HandlePlayerRoll()
        {
            DelayInvoker.DelayInvoke(0.5f, () =>
            {
                _physicsComponent.Rigidbody.AddForce(_physicsComponent.Rigidbody.transform.forward.normalized * _physicsDetermineConstant.RollForce, ForceMode.VelocityChange);
            });
            
        }

        private static readonly RaycastHit[] CachedHits = new RaycastHit[32];
        /// <summary>
        /// 获取屏幕内可见的敌人列表
        /// </summary>
        /// <param name="camera">玩家摄像机</param>
        /// <param name="potentialTargets">需要检测的敌人列表</param>
        /// <param name="playersInScreen">输出的屏幕内可见敌人列表</param>
        /// <param name="layerMask">检测层</param>
        /// <returns>是否找到可见敌人</returns>
        public static bool TryGetPlayersInScreen(
            Camera camera,
            IEnumerable<Transform> potentialTargets,
            out List<int> playersInScreen,
            int layerMask)
        {
            playersInScreen = new List<int>();
            if (!camera) return false;

            var cameraPos = camera.transform.position;
            var cameraForward = camera.transform.forward;

            foreach (var target in potentialTargets)
            {
                if (!target) continue;

                var targetPos = target.position;
                var directionToTarget = targetPos - cameraPos;
                var distanceToTarget = directionToTarget.magnitude;

                // 1. 距离检查
                if (distanceToTarget > _physicsDetermineConstant.MaxDetermineDistance)  continue;

                // 2. 视角检查
                var dot = Vector3.Dot(cameraForward, directionToTarget.normalized);
                if (dot < _physicsDetermineConstant.ViewAngle) continue;

                // 3. 视锥检查
                var viewportPos = camera.WorldToViewportPoint(targetPos);
                if (viewportPos.z <= 0 || 
                    viewportPos.x < 0 || viewportPos.x > 1 || 
                    viewportPos.y < 0 || viewportPos.y > 1)
                {
                    continue;
                }

                // 4. 遮挡检查（使用 SphereCastNonAlloc）
                var hitCount = Physics.SphereCastNonAlloc(
                    cameraPos,
                    _physicsDetermineConstant.ObstructionCheckRadius,
                    directionToTarget.normalized,
                    CachedHits,
                    distanceToTarget,
                    layerMask);

                var isObstructed = false;
                for (var i = 0; i < hitCount; i++)
                {
                    var hit = CachedHits[i];
                    if (hit.transform == target) continue;
                    var layer = hit.collider.gameObject.layer;
                    if (layer == _physicsDetermineConstant.StairsSceneLayer || 
                        layer == _physicsDetermineConstant.GroundSceneLayer)
                    {
                        isObstructed = true;
                        break;
                    }
                }

                if (!isObstructed)
                {
                    var component = target.GetComponent<PlayerComponentController>();
                    playersInScreen.Add(component.connectionToClient.connectionId);
                }
            }

            return playersInScreen.Count > 0;
        }

        public bool IsClient { get; private set; }
    }
    
    public class PhysicsComponent
    {
        public Rigidbody Rigidbody;
        public Transform Transform;
        public Transform CheckStairsTransform;
        public CapsuleCollider CapsuleCollider;
        public Camera Camera;
        
        public PhysicsComponent(Rigidbody rigidbody, Transform transform, Transform checkStairsTransform, CapsuleCollider capsuleCollider, Camera camera)
        {
            Rigidbody = rigidbody;
            Transform = transform;
            CheckStairsTransform = checkStairsTransform;
            CapsuleCollider = capsuleCollider;
            Camera = camera;
        }
    }

    public struct PhysicsDetermineConstant
    {
        public float GroundMinDistance;
        public float GroundMaxDistance;
        public float MaxSlopeAngle;
        public float StairsCheckDistance;
        public LayerMask GroundSceneLayer;
        public LayerMask StairsSceneLayer;
        public float RotateSpeed;
        public float MaxDetermineDistance;
        public float ViewAngle;
        public float ObstructionCheckRadius;
        public bool IsServer;
        public float RollForce;
        public float JumpSpeed;

        public PhysicsDetermineConstant(float groundMinDistance, float groundMaxDistance, float maxSlopeAngle, float stairsCheckDistance, 
            LayerMask groundSceneLayer, LayerMask stairsSceneLayer, float rotateSpeed, float maxDetermineDistance, 
            float viewAngle, float obstructionCheckRadius, float rollForce, float jumpSpeed, bool isServer = false)
        {
            GroundMinDistance = groundMinDistance;
            GroundMaxDistance = groundMaxDistance;
            MaxSlopeAngle = maxSlopeAngle;
            StairsCheckDistance = stairsCheckDistance;
            GroundSceneLayer = groundSceneLayer;
            StairsSceneLayer = stairsSceneLayer;
            RotateSpeed = rotateSpeed;
            IsServer = isServer;
            MaxDetermineDistance = maxDetermineDistance;
            ViewAngle = viewAngle;
            ObstructionCheckRadius = obstructionCheckRadius;
            RollForce = rollForce;
            JumpSpeed = jumpSpeed;
        }
    }

    public struct MoveParam : IPoolObject
    {
        public Vector3 InputMovement;
        public float DeltaTime;
        public bool IsMovingState;
        public ushort CameraForward;
        public bool IsClearVelocity;
        
        public MoveParam(Vector3 inputMovement, float deltaTime, bool isMovingState, ushort cameraForward, bool isClearVelocity)
        {
            InputMovement = inputMovement;
            DeltaTime = deltaTime;
            IsMovingState = isMovingState;
            CameraForward = cameraForward;
            IsClearVelocity = isClearVelocity;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            InputMovement = Vector3.zero;
            DeltaTime = 0;
            IsMovingState = false;
            CameraForward = 0;
            IsClearVelocity = false;
        }
    }
    
    public struct CheckGroundDistanceParam
    {
        public Vector3 InputMovement;
        public float FixedDeltaTime;
        
        public CheckGroundDistanceParam(Vector3 inputMovement, float fixedDeltaTime)
        {
            InputMovement = inputMovement;
            FixedDeltaTime = fixedDeltaTime;
        }
    }
}