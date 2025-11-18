using System;
using System.Collections;
using System.Collections.Generic;
using AOTScripts.Tool;
using DG.Tweening;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Tool.GameEvent;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class TrainController : NetworkAutoInjectHandlerBehaviour
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");

        [Header("路径设置")]
        private Vector3 _origin = Vector3.zero;
        private Vector3[] _targets;
        private Vector3 _target;
        [SerializeField]
        private float movementDuration = 5f;
    
        [Header("火车部件")]
        [SerializeField]
        private List<TrainPart> carriages = new List<TrainPart>();
        [SerializeField]
        private List<WheelConfig> wheels = new List<WheelConfig>();
        [SerializeField]
        private List<SmokeConfig> smokeConfigs = new List<SmokeConfig>();

        [SerializeField] private Collider deathCollider;
        private IColliderConfig _colliderConfig;
        [Header("烟雾强度设置")]
        [SerializeField]
        private AnimationCurve smokeIntensityCurve = AnimationCurve.Linear(0, 1, 1, 1);
    
        // DOTween相关
        private Sequence _trainSequence;
        private bool isMovingToTarget = true;
    
        // 烟雾管理
        private List<GameObject> _activeSmokeParticles = new List<GameObject>();
        private List<Tween> _smokeTweens = new List<Tween>();
        
        private GameEventManager _gameEventManager;
        private HashSet<DynamicObjectData> _dynamicObjects = new HashSet<DynamicObjectData>();

        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<StartGameTrainEvent>(OnStartTrain);
            _origin = _targets.RandomSelect();
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(deathCollider);
            //GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Train, gameObject.layer, gameObject.tag);
        }

        private void FixedUpdate()
        {
            if (!ServerHandler)
            {
                return;
            }

            if (GameObjectContainer.Instance.DynamicObjectIntersects(netId, deathCollider.transform.position, _colliderConfig, _dynamicObjects))
            {
                foreach (var dynamicObject in _dynamicObjects)
                {
                    if (dynamicObject.Type == ObjectType.Player)
                    {
                        _gameEventManager.Publish(new PlayerDieEvent(dynamicObject.NetId, dynamicObject.Position));
                    }
                }
            }
        }

        private void OnStartTrain(StartGameTrainEvent startEvent)
        {
            if (!ServerHandler)
            {
                return;
            }
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
                isMovingToTarget = true;
                SetTrainVisible(true);
                StartWheelRotation();
                StartSmokeEmission();
            });
        
            _trainSequence.Append(transform.DOMove(_target, movementDuration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => UpdateSmokeIntensity())
                .OnComplete(() => {
                    StopWheelRotation();
                    StopSmokeEmission();
                }));
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
            foreach (var wheelConfig in wheels)
            {
                if (wheelConfig.wheelTransform != null)
                {
                    // 计算旋转速度（根据移动时间和轮子周长）
                    float wheelCircumference = 2 * Mathf.PI * wheelConfig.radius;
                    float totalDistance = Vector3.Distance(_origin, _target);
                    float totalRotations = totalDistance / wheelCircumference;
                    float rotationDuration = movementDuration / totalRotations;
                
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
    
        void StartSmokeEmission()
        {
            // 开始烟雾发射协程
            foreach (var smokeConfig in smokeConfigs)
            {
                StartCoroutine(SmokeEmissionCoroutine(smokeConfig));
            }
        }
    
        void StopSmokeEmission()
        {
            // 停止所有烟雾发射协程
            StopAllCoroutines();
        }
    
        IEnumerator SmokeEmissionCoroutine(SmokeConfig config)
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
            
                yield return null;
            }
        }
    
        void EmitSmoke(SmokeConfig config)
        {
            if (config.smokePrefab == null || config.emissionPoint == null) return;
        
            GameObject smoke = Instantiate(config.smokePrefab, 
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
            if (r != null)
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
                if (smoke != null)
                {
                    _activeSmokeParticles.Remove(smoke);
                    Destroy(smoke);
                }
            });
        
            smokeSequence.Play();
        }
    
        void UpdateSmokeIntensity()
        {
            // 这个方法会在移动过程中每帧调用，可以在这里根据移动进度调整烟雾强度
            // 目前已经在协程中处理，这里可以留空或添加其他效果
        }
    
        void SetTrainVisible(bool visible)
        {
            foreach (var carriage in carriages)
            {
                if (carriage.gameObject != null)
                    carriage.gameObject.SetActive(visible);
            }
        
            foreach (var wheel in wheels)
            {
                if (wheel.wheelTransform != null)
                    wheel.wheelTransform.gameObject.SetActive(visible);
            }
        
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