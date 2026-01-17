using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.State;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class TrainController : NetworkAutoInjectHandlerBehaviour
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");

        [Header("路径设置")]
        private Vector3 _origin = Vector3.zero;
        private Vector3 _target;
        private float _movementDuration;
    
        [Header("火车部件")]
        [SerializeField]
        private List<TrainPart> carriages = new List<TrainPart>();
        [SerializeField]
        private List<WheelConfig> wheels = new List<WheelConfig>();
        [SerializeField]
        private List<Transform> trainParts = new List<Transform>();

        [SerializeField] private Collider deathCollider;
        [SerializeField] private Collider touchCollider;
        private IColliderConfig _deathColliderConfig;
        private IColliderConfig _touchColliderConfig;
        [Header("烟雾强度设置")]
        [SerializeField]
        private AnimationCurve smokeIntensityCurve = AnimationCurve.Linear(0, 1, 1, 1);

        [SerializeField] 
        private Ease easeType = Ease.Linear;
        
        // DOTween相关
        private Sequence _trainSequence;
        [SerializeField]
        private ObjectType type;
        private GameSyncManager _gameSyncManager;
    
        // 烟雾管理
        private List<GameObject> _activeSmokeParticles = new List<GameObject>();
        private List<Tween> _smokeTweens = new List<Tween>();
        private List<Transform> _cachedTrainParts = new List<Transform>();
        
        private GameEventManager _gameEventManager;
        private InteractSystem _interactSystem;
        
        private SyncHashSet<uint> _trainPlayers = new SyncHashSet<uint>();
        
        
        protected override bool AutoInjectLocalPlayer => true;

        [Inject]
        private void Init(GameEventManager gameEventManager, IObjectResolver objectResolver)
        {
            transform.localScale = Vector3.zero;
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<StartGameTrainEvent>(OnStartTrain);
            _gameEventManager.Subscribe<TakeTrainEvent>(OnTakeTrain);
            _gameEventManager.Subscribe<TrainAttackPlayerEvent>(OnTrainAttackPlayer);
            _gameSyncManager = objectResolver.Resolve<GameSyncManager>();
            _interactSystem = objectResolver.Resolve<InteractSystem>();
            _deathColliderConfig = GamePhysicsSystem.CreateColliderConfig(deathCollider);
            _touchColliderConfig = GamePhysicsSystem.CreateColliderConfig(touchCollider);
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _touchColliderConfig, type, gameObject.layer, gameObject.tag);
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _deathColliderConfig, ObjectType.Death, gameObject.layer, gameObject.tag);

            for (int i = 0; i < trainParts.Count; i++)
            {
                var trainPart = trainParts[i];
                if (trainPart)
                {
                    _cachedTrainParts.Add(trainPart);
                }
            }
        }

        private void OnTrainAttackPlayer(TrainAttackPlayerEvent trainAttackEvent)
        {
            if (!ServerHandler)
            {
                return;
            }
            var playerIdentity = GameStaticExtensions.GetNetworkIdentity(trainAttackEvent.PlayerId);
            if (playerIdentity)
            {
                var playerConnectionId = PlayerInGameManager.Instance.GetPlayerId(trainAttackEvent.PlayerId);
                var rigidBody = playerIdentity.GetComponent<Rigidbody>();
                //向玩家施加一个冲击力，基于火车运动的方向
                Vector3 direction = (rigidBody.position - transform.position).normalized;
                rigidBody.AddForce(direction * 750f, ForceMode.Impulse);
                var command = new PlayerDeathCommand();
                command.Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Property);
                _gameSyncManager.EnqueueServerCommand(command);
            }
        }

        private void OnTakeTrain(TakeTrainEvent takeEvent)
        {
            if (!ServerHandler)
            {
                return;
            }

            if (!NetworkServer.spawned.TryGetValue(takeEvent.PlayerId, out var identity))
            {
                Debug.LogError($"Player {takeEvent.PlayerId} not spawned.");
                return;
            }

            if (!_trainPlayers.Add(takeEvent.PlayerId))
            {
                return;
            }
            
            Debug.Log($"[TrainController] Take Train {takeEvent.PlayerId}");

            var playerConnectionId = PlayerInGameManager.Instance.GetPlayerId(takeEvent.PlayerId);
            var command = new PlayerStateChangedCommand();
            command.NewState = SubjectedStateType.IsCantMoved;
            command.Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Property);
            command.OperationType = OperationType.Add;
            command.EnableRb = true;
            var ts = _cachedTrainParts.RandomSelect();
            identity.transform.SetParent(ts);
            identity.transform.localPosition = Vector3.zero;
            _cachedTrainParts.Remove(ts);
            _gameSyncManager.EnqueueServerCommand(command);
        }

        private void OnStartTrain(StartGameTrainEvent startEvent)
        {
            if (!ServerHandler)
            {
                return;
            }
            if (_trainSequence != null && _trainSequence.IsPlaying())
            {
                return;
            }
            _movementDuration = startEvent.MoveDuration;
            Debug.Log($"[TrainController] Start Game Train {startEvent.TrainId} {startEvent.StartPosition} {startEvent.TargetPosition}");
            transform.localScale = Vector3.one;
            _origin = startEvent.StartPosition;
            _target = startEvent.TargetPosition;
            transform.position = _origin;
            transform.rotation = startEvent.StartRotation;
            ResetTrainState();
        
            // 创建火车移动序列
            CreateTrainSequence();
        
            // 开始移动
            StartTrainMovement();
        }
    
        void OnDestroy()
        {
            // 清理所有DOTween动画
            _trainSequence?.Kill();
        
            foreach (var tween in _smokeTweens)
            {
                tween?.Kill();
            }
        }
    
        void CreateTrainSequence()
        {
            _trainSequence = DOTween.Sequence();
        
            // 移动到目标点
            _trainSequence.AppendCallback(() => {
                SetTrainVisible(true);
                StartWheelRotation();
            });
        
            _trainSequence.Append(transform.DOMove(_target, _movementDuration)
                .SetEase(Ease.Linear)
                .OnComplete(() => {
                    StopWheelRotation();
                    StopSmokeEmission();
                    OnTrainArrived();
                    transform.localScale = Vector3.zero;
                    Debug.Log("[TrainController] Stop Game Train");
                }));
        }

        private void OnTrainArrived()
        {
            if (!ServerHandler)
            {
                return;
            }

            _cachedTrainParts.Clear();
            var trainPlayers = _trainPlayers.ToArray();
            for (var i = 0; i < trainPlayers.Length; i++)
            {
                var player = trainPlayers[i];
                var identity = GameStaticExtensions.GetNetworkIdentity(player);
                if (identity)
                {
                    var playerConnectionId = PlayerInGameManager.Instance.GetPlayerId(player);
                    var stateChangedCommand = new PlayerStateChangedCommand();
                    stateChangedCommand.NewState = SubjectedStateType.IsCantMoved;
                    stateChangedCommand.Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Property);
                    stateChangedCommand.OperationType = OperationType.Subtract;
                    
                    stateChangedCommand.EnableRb = false;
                    identity.transform.SetParent(null);
                    _gameSyncManager.EnqueueServerCommand(stateChangedCommand);
                    var takeTrainCommand = new PlayerTouchObjectCommand();
                    takeTrainCommand.Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Property);
                    takeTrainCommand.ObjectType = type;
                    _gameSyncManager.EnqueueServerCommand(takeTrainCommand);
                }
            }

            for (int i = 0; i < trainParts.Count; i++)
            {
                var trainPart = trainParts[i];
                if (trainPart)
                {
                    _cachedTrainParts.Add(trainPart);
                }
            }
            _trainPlayers.Clear();
            _gameEventManager.Publish(new TrainArrivedEvent(_interactSystem.currentTrainId, trainPlayers, type));
        }

        void StartTrainMovement()
        {
            _trainSequence?.Play();
        }
    
        void StopTrainMovement()
        {
            _trainSequence?.Pause();
        }
    
        void StartWheelRotation()
        {
            if (wheels.Count == 0)
            {
                return;
            }
            foreach (var wheelConfig in wheels)
            {
                if (wheelConfig.wheelTransform)
                {
                    // 计算旋转速度（根据移动时间和轮子周长）
                    float wheelCircumference = 2 * Mathf.PI * wheelConfig.radius;
                    float totalDistance = Vector3.Distance(_origin, _target);
                    float totalRotations = totalDistance / wheelCircumference;
                    float rotationDuration = _movementDuration / totalRotations;
                
                    // 确定旋转方向
                    Vector3 rotationAxis = Vector3.right;
                    float rotationDirection = wheelConfig.isReverse ? -1f : 1f;
                
                    // 创建无限旋转动画
                    var rotationTween = wheelConfig.wheelTransform
                        .DOLocalRotate(new Vector3(360 * rotationDirection, 0, 0), rotationDuration, RotateMode.LocalAxisAdd)
                        .SetEase(Ease.Linear)
                        .SetLoops(-1, LoopType.Incremental);
                
                    // 保存引用以便后续停止
                    _smokeTweens.Add(rotationTween);
                }
            }
        }
    
        void StopWheelRotation()
        {
            // 停止所有轮子旋转动画
            foreach (var tween in _smokeTweens)
            {
                if (tween != null && tween.IsPlaying())
                {
                    tween.Kill();
                }
            }
            _smokeTweens.Clear();
        }
    
        private async UniTaskVoid SmokeEmissionCoroutine(SmokeConfig config)
        {
            while (true)
            {
                // 根据烟雾强度曲线决定发射频率
                float progress = GetMovementProgress();
                float intensity = smokeIntensityCurve.Evaluate(progress);
                float emissionRate = Mathf.Lerp(config.minEmissionRate, 
                    config.maxEmissionRate, 
                    intensity);
            
                // 随机发射烟雾粒子
                if (Random.Range(0f, 1f) < emissionRate * Time.deltaTime)
                {
                    EmitSmoke(config);
                }
            
                await UniTask.Yield();
            }
        }
    
        private void EmitSmoke(SmokeConfig config)
        {
            if (!config.smokePrefab || !config.emissionPoint) return;
        
            var smoke = NetworkGameObjectPoolManager.Instance.Spawn(config.smokePrefab, 
                config.emissionPoint.position, 
                Quaternion.identity);
        
            _activeSmokeParticles.Add(smoke);
        
            // 使用DOTween创建烟雾动画序列
            Sequence smokeSequence = DOTween.Sequence();
        
            // 随机方向
            Vector3 randomDirection = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0.8f, 1.2f),
                Random.Range(-0.5f, 0.5f)
            ).normalized;
        
            // 抛物线运动
            Vector3 endPosition = smoke.transform.position + 
                                  randomDirection * (config.smokeSpeed * config.smokeLifetime) +
                                  Vector3.up * (0.5f * config.smokeSpeed * config.smokeLifetime);
        
            smokeSequence.Append(smoke.transform.DOMove(endPosition, config.smokeLifetime)
                .SetEase(Ease.OutQuad));
        
            // 缩放动画 - 先变大后变小
            smokeSequence.Join(smoke.transform.DOScale(Vector3.one * 1.5f, config.smokeLifetime * 0.3f)
                .SetEase(Ease.OutBack));
            smokeSequence.Append(smoke.transform.DOScale(Vector3.zero, config.smokeLifetime * 0.7f)
                .SetEase(Ease.InBack));
        
            // 淡出效果（如果有Renderer）
            Renderer r = smoke.GetComponent<Renderer>();
            if (r)
            {
                // 假设使用标准材质，可以通过颜色淡出
                Material material = r.material;
                if (material.HasProperty(Color1))
                {
                    Color originalColor = material.color;
                    smokeSequence.Join(DOTween.To(() => material.color, 
                        x => material.color = x, 
                        new Color(originalColor.r, originalColor.g, originalColor.b, 0), 
                        config.smokeLifetime));
                }
            }
        
            // 动画完成后销毁对象
            smokeSequence.OnComplete(() => {
                if (smoke)
                {
                    _activeSmokeParticles.Remove(smoke);
                    Destroy(smoke);
                }
            });
        
            smokeSequence.Play();
        }
    
        void StopSmokeEmission()
        {
            // 停止所有烟雾发射协程
            StopAllCoroutines();
        }
        
        void SetTrainVisible(bool visible)
        {
            RpcSetTrainVisible(visible);
        
            // 使用DOTween实现淡入淡出效果（如果有需要）
            // if (visible)
            // {
            //     // 可以在这里添加淡入效果
            // }
            // else
            // {
            //     // 隐藏前可以添加淡出效果
            // }
        }

        [ClientRpc]
        private void RpcSetTrainVisible(bool visible)
        {
            foreach (var carriage in carriages)
            {
                if (carriage.gameObject)
                    carriage.gameObject.SetActive(visible);
            }
        
            foreach (var wheel in wheels)
            {
                if (wheel.wheelTransform)
                    wheel.wheelTransform.gameObject.SetActive(visible);
            }
        }

        void ResetTrainState()
        {
            foreach (var carriage in carriages)
            {
                if (carriage.gameObject != null)
                    carriage.gameObject.transform.localPosition = carriage.localPosition;
            }
        }
    
        float GetMovementProgress()
        {
            float totalDistance = Vector3.Distance(_origin, _target);
            float currentDistance = Vector3.Distance(_origin, transform.position);
            return Mathf.Clamp01(currentDistance / totalDistance);
        }
    
        // 在Scene视图中绘制路径
        // void OnDrawGizmos()
        // {
        //     Gizmos.color = Color.green;
        //     Gizmos.DrawWireSphere(_origin, 0.5f);
        //     Gizmos.color = Color.red;
        //     Gizmos.DrawWireSphere(_targets, 0.5f);
        //     Gizmos.color = Color.yellow;
        //     Gizmos.DrawLine(_origin, _targets);
        // }
    
        // 公共方法，用于外部控制
        public void PauseTrain()
        {
            StopTrainMovement();
            StopWheelRotation();
            StopSmokeEmission();
        }
    }
    [Serializable]
    public class TrainPart
    {
        public GameObject gameObject;
        public Vector3 localPosition;
    }

    [Serializable]
    public class WheelConfig
    {
        public Transform wheelTransform;
        public float radius = 0.5f;
        public bool isReverse = false;
    }

    [Serializable]
    public class SmokeConfig
    {
        public Transform emissionPoint;
        public GameObject smokePrefab;
        public float minEmissionRate = 0.5f;
        public float maxEmissionRate = 2f;
        public float smokeLifetime = 3f;
        public float smokeSpeed = 2f;
    }
}