using System.Collections;
using System.Collections.Generic;
using HotUpdate.Scripts.Collector.Collects;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Effect
{
    [RequireComponent(typeof(Renderer))]
    public class CollectEffectController : MonoBehaviour
    {
        [Header("攻击属性（生成时设置）")]
        [SerializeField] private float attackPower;
        [SerializeField] private float attackSpeed; // 攻击间隔（秒）
    
        [Header("战斗状态")]
        [SerializeField] private bool isAttackingMode = false;
        [SerializeField] private bool hasTarget = false;
    
        [Header("视觉组件（动态生成）")]
        private AttackEffectMapper effectMapper;
        private AttackStateController stateController;
        private SweepParticleSystem sweepSystem;
        private EffectController effectController;
    
        [Header("配置")]
        public bool autoInitialize = true;
        public bool showDebugLogs = true;
    
        [Header("组件状态")]
        public bool isInitialized = false;
        public bool componentsCreated = false;
    
        [Header("材质配置")]
        public Shader effectShader;
        private Material originalMaterial;
        private Material effectMaterial;
    
        // 事件委托
        public delegate void AttackEventHandler(float power, float speed);
        public event AttackEventHandler OnAttackTriggered;
        public event System.Action OnModeChanged;
    
        void Awake()
        {
            if (autoInitialize)
            {
                Initialize();
            }
        }
    
        void Update()
        {
            // 调试输入
            HandleDebugInput();
        }
    
        #region 初始化
    
        public void Initialize()
        {
            if (isInitialized) return;
        
            if (showDebugLogs) Debug.Log($"[CollectObjectController] 初始化 {gameObject.name}");
        
            // 1. 确保有Renderer组件
            Renderer renderer = GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError($"[CollectObjectController] {gameObject.name} 没有Renderer组件!");
                return;
            }
        
            // 2. 创建效果材质
            SetupEffectMaterial(renderer);
        
            // 3. 动态创建所有视觉效果组件
            CreateVisualComponents();
        
            // 4. 初始化组件
            InitializeComponents();
        
            isInitialized = true;
            componentsCreated = true;
        
            if (showDebugLogs) Debug.Log($"[CollectObjectController] {gameObject.name} 初始化完成!");
        }
    
        private void SetupEffectMaterial(Renderer renderer)
        {
            // 保存原始材质
            originalMaterial = renderer.material;
        
            // 使用默认Shader或指定的Shader
            if (effectShader == null)
            {
                // 查找项目中的效果Shader
                effectShader = Shader.Find("Custom/DisintegrationShader");
                if (effectShader == null)
                {
                    // 如果找不到，使用默认Shader
                    effectShader = Shader.Find("Standard");
                    if (showDebugLogs) Debug.LogWarning($"[CollectObjectController] 未找到效果Shader，使用Standard代替");
                }
            }
        
            // 创建新的效果材质
            effectMaterial = new Material(effectShader);
        
            // 复制原始材质的属性（如果可能）
            CopyMaterialProperties(originalMaterial, effectMaterial);
        
            // 应用效果材质
            renderer.material = effectMaterial;
        }
    
        private void CopyMaterialProperties(Material source, Material target)
        {
            // 尝试复制常见属性
            if (source.HasProperty("_Color"))
                target.SetColor("_Color", source.GetColor("_Color"));
        
            if (source.HasProperty("_MainTex"))
                target.SetTexture("_MainTex", source.GetTexture("_MainTex"));
        
            if (source.HasProperty("_EmissionColor"))
                target.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
        
            // 复制其他可能需要的基本属性
            target.name = source.name + "_Effect";
        }
    
        private void CreateVisualComponents()
        {
            // 创建EffectController
            effectController = gameObject.GetComponent<EffectController>();
            if (effectController == null)
            {
                effectController = gameObject.AddComponent<EffectController>();
                if (showDebugLogs) Debug.Log($"[CollectObjectController] 创建EffectController");
            }
        
            // 创建AttackEffectMapper
            effectMapper = gameObject.GetComponent<AttackEffectMapper>();
            if (effectMapper == null)
            {
                effectMapper = gameObject.AddComponent<AttackEffectMapper>();
                if (showDebugLogs) Debug.Log($"[CollectObjectController] 创建AttackEffectMapper");
            
                // 设置默认配置
                SetupDefaultEffectConfig();
            }
        
            // 创建AttackStateController
            stateController = gameObject.GetComponent<AttackStateController>();
            if (stateController == null)
            {
                stateController = gameObject.AddComponent<AttackStateController>();
                if (showDebugLogs) Debug.Log($"[CollectObjectController] 创建AttackStateController");
            }
        
            // 创建SweepParticleSystem（可选）
            sweepSystem = gameObject.GetComponent<SweepParticleSystem>();
            if (sweepSystem == null)
            {
                sweepSystem = gameObject.AddComponent<SweepParticleSystem>();
                if (showDebugLogs) Debug.Log($"[CollectObjectController] 创建SweepParticleSystem");
            
                // 设置默认粒子系统
                SetupDefaultParticleSystem();
            }
        }
    
        private void SetupDefaultEffectConfig()
        {
            if (effectMapper == null) return;
        
            // 配置默认攻击效果映射
            effectMapper.powerConfig.minPower = 0f;
            effectMapper.powerConfig.maxPower = 100f;
        
            effectMapper.powerConfig.normalDistortion = 0.2f;
            effectMapper.powerConfig.strongDistortion = 0.4f;
            effectMapper.powerConfig.superDistortion = 0.7f;
        
            effectMapper.powerConfig.normalDisintegration = 0.3f;
            effectMapper.powerConfig.strongDisintegration = 0.6f;
            effectMapper.powerConfig.superDisintegration = 1f;
        
            effectMapper.powerConfig.normalFlashIntensity = 3f;
            effectMapper.powerConfig.strongFlashIntensity = 6f;
            effectMapper.powerConfig.superFlashIntensity = 10f;
        
            // 设置默认颜色池
            effectMapper.powerConfig.flashColorPool = new List<Color>
            {
                Color.white,
                new Color(1f, 0.9f, 0f), // 金色
                Color.red,
                Color.blue,
                Color.cyan,
                Color.green
            };
        
            // 配置速度映射
            effectMapper.speedConfig.minAttackInterval = 0.1f;
            effectMapper.speedConfig.maxAttackInterval = 2f;
        
            effectMapper.speedConfig.normalSpeed = 1f;
            effectMapper.speedConfig.fastSpeed = 1.5f;
            effectMapper.speedConfig.superFastSpeed = 2f;
        
            effectMapper.speedConfig.normalAttackDuration = 1f;
            effectMapper.speedConfig.fastAttackDuration = 0.5f;
            effectMapper.speedConfig.superFastAttackDuration = 0.25f;
        }
    
        private void SetupDefaultParticleSystem()
        {
            if (sweepSystem == null) return;
        
            // 创建并配置粒子系统
            GameObject particleObject = new GameObject("SweepParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.zero;
        
            // 添加并配置粒子系统
            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        
            // 配置主模块
            var main = particleSystem.main;
            main.startSpeed = 5f;
            main.startSize = 0.5f;
            main.startLifetime = 0.5f;
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        
            // 配置发射模块
            var emission = particleSystem.emission;
            emission.rateOverTime = 0;
            emission.enabled = false;
        
            // 配置形状（弧形）
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 1f;
            shape.arc = 180f;
        
            sweepSystem.sweepParticleSystem = particleSystem;
        
            // 创建命中粒子系统
            GameObject hitParticleObject = new GameObject("HitParticles");
            hitParticleObject.transform.SetParent(transform);
            hitParticleObject.transform.localPosition = Vector3.zero;
        
            ParticleSystem hitParticleSystem = hitParticleObject.AddComponent<ParticleSystem>();
            var hitMain = hitParticleSystem.main;
            hitMain.startSpeed = 2f;
            hitMain.startSize = 0.3f;
            hitMain.startLifetime = 1f;
            hitMain.maxParticles = 50;
        
            var hitEmission = hitParticleSystem.emission;
            hitEmission.rateOverTime = 0;
            hitEmission.enabled = false;
        
            sweepSystem.hitParticleSystem = hitParticleSystem;
        
            if (showDebugLogs) Debug.Log($"[CollectObjectController] 创建默认粒子系统");
        }
    
        private void InitializeComponents()
        {
            // 设置组件间的引用
            if (stateController != null)
            {
                stateController.effectController = effectController;
                stateController.effectMapper = effectMapper;
            }
        
            // 初始化EffectController
            if (effectController != null)
            {
                // EffectController会在Start时自动初始化
            }
        
            // 设置初始攻击参数
            SetAttackParameters(attackPower, attackSpeed);
        }
    
        #endregion
    
        #region 公共API
    
        /// <summary>
        /// 设置攻击参数（生成时调用）
        /// </summary>
        public void SetAttackParameters(float power, float speed)
        {
            if (!isInitialized)
            {
                Debug.LogWarning($"[CollectObjectController] 未初始化，延迟设置参数");
                attackPower = power;
                attackSpeed = speed;
                return;
            }
        
            attackPower = Mathf.Clamp(power, 0f, 100f);
            attackSpeed = Mathf.Max(0.1f, speed);
        
            if (effectMapper )
            {
                effectMapper.SetAttackParameters(attackPower, attackSpeed);
            }
        
            if (showDebugLogs)
            {
                Debug.Log($"[CollectObjectController] 设置攻击参数: 威力={attackPower}, 速度={attackSpeed}");
            }
        }
    
        /// <summary>
        /// 切换到追踪模式
        /// </summary>
        public void SwitchToTrackingMode(bool hasEnemyInSight = false)
        {
            if (!isInitialized) return;
        
            isAttackingMode = false;
            hasTarget = hasEnemyInSight;
        
            if (stateController )
            {
                stateController.SetHasTarget(hasTarget);
            }
        
            OnModeChanged?.Invoke();
        
            if (showDebugLogs)
            {
                string mode = hasTarget ? "追踪模式(发现敌人)" : "追踪模式(搜索中)";
                Debug.Log($"[CollectObjectController] 切换到{mode}");
            }
        }
    
        /// <summary>
        /// 切换到攻击模式
        /// </summary>
        public void SwitchToAttackMode(bool hasEnemy = true)
        {
            if (!isInitialized) return;
        
            isAttackingMode = true;
            hasTarget = hasEnemy;
        
            if (stateController)
            {
                stateController.SetHasTarget(hasTarget);
            }
        
            OnModeChanged?.Invoke();
        
            if (showDebugLogs)
            {
                string targetStatus = hasEnemy ? "有目标" : "无目标";
                Debug.Log($"[CollectObjectController] 切换到攻击模式, {targetStatus}");
            }
        }
    
        /// <summary>
        /// 触发一次攻击
        /// </summary>
        public void TriggerAttack()
        {
            if (!isInitialized) return;
        
            if (stateController)
            {
                stateController.StartAttack(attackPower, attackSpeed);
            }
        
            // 触发横扫粒子效果
            if (sweepSystem  && effectMapper)
            {
                sweepSystem.TriggerSweep(effectMapper.currentPowerLevel, effectMapper.currentSpeedLevel);
            }
        
            OnAttackTriggered?.Invoke(attackPower, attackSpeed);
        
            if (showDebugLogs)
            {
                Debug.Log($"[CollectObjectController] 触发攻击! 威力={attackPower}, 速度={attackSpeed}");
            }
        }
    
        /// <summary>
        /// 强制停止所有效果
        /// </summary>
        public void StopAllEffects()
        {
            if (!isInitialized) return;
        
            if (effectController != null)
            {
                effectController.StopAllAnimations();
            }
        
            if (showDebugLogs) Debug.Log($"[CollectObjectController] 停止所有效果");
        }
    
        /// <summary>
        /// 重置所有状态到初始
        /// </summary>
        public void ResetAll()
        {
            if (!isInitialized) return;
        
            StopAllEffects();
            SwitchToTrackingMode(false);
        
            if (showDebugLogs) Debug.Log($"[CollectObjectController] 重置所有状态");
        }
    
        #endregion
    
        #region 获取状态信息
    
        /// <summary>
        /// 获取当前攻击参数
        /// </summary>
        public (float power, float speed) GetAttackParameters()
        {
            return (attackPower, attackSpeed);
        }
    
        /// <summary>
        /// 获取当前模式
        /// </summary>
        public (bool isAttacking, bool hasTarget) GetCurrentMode()
        {
            return (isAttackingMode, hasTarget);
        }
    
        /// <summary>
        /// 获取效果强度信息
        /// </summary>
        public (float distortion, float disintegration, float flash) GetEffectIntensities()
        {
            if (effectMapper != null)
            {
                return (effectMapper.distortionIntensity, 
                    effectMapper.disintegrationIntensity, 
                    effectMapper.flashIntensity);
            }
            return (0f, 0f, 0f);
        }
    
        /// <summary>
        /// 获取组件引用（高级使用）
        /// </summary>
        public (EffectController effect, AttackEffectMapper mapper, 
            AttackStateController state, SweepParticleSystem sweep) GetComponentReferences()
        {
            return (effectController, effectMapper, stateController, sweepSystem);
        }
    
        #endregion
    
        #region 调试
    
        private void HandleDebugInput()
        {
            if (!showDebugLogs) return;
        
            // 仅在编辑器中使用快捷键
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SwitchToTrackingMode(false);
            }
        
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SwitchToTrackingMode(true);
            }
        
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SwitchToAttackMode(true);
            }
        
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                TriggerAttack();
            }
        
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                StopAllEffects();
            }
        
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                LogCurrentStatus();
            }
#endif
        }
    
        [ContextMenu("日志当前状态")]
        public void LogCurrentStatus()
        {
            if (!isInitialized)
            {
                Debug.Log($"[CollectObjectController] 未初始化");
                return;
            }
        
            (float distortion, float disintegration, float flash) = GetEffectIntensities();
        
            Debug.Log($"[CollectObjectController] 状态报告:\n" +
                      $"初始化: {isInitialized}\n" +
                      $"攻击模式: {isAttackingMode}\n" +
                      $"有目标: {hasTarget}\n" +
                      $"攻击力: {attackPower}\n" +
                      $"攻速: {attackSpeed}\n" +
                      $"扭曲强度: {distortion}\n" +
                      $"肢解强度: {disintegration}\n" +
                      $"闪光强度: {flash}\n" +
                      $"闪光颜色: {effectMapper?.flashColor}");
        }
    
        [ContextMenu("测试攻击序列")]
        public void TestAttackSequence()
        {
            if (!isInitialized)
            {
                Debug.LogWarning($"[CollectObjectController] 需要先初始化!");
                return;
            }
        
            StartCoroutine(TestSequenceRoutine());
        }
    
        private IEnumerator TestSequenceRoutine()
        {
            Debug.Log($"[CollectObjectController] 开始测试序列...");
        
            // 1. 追踪模式（无目标）
            SwitchToTrackingMode(false);
            yield return new WaitForSeconds(2f);
        
            // 2. 追踪模式（发现目标）
            SwitchToTrackingMode(true);
            yield return new WaitForSeconds(2f);
        
            // 3. 攻击模式
            SwitchToAttackMode(true);
            yield return new WaitForSeconds(1f);
        
            // 4. 触发攻击
            TriggerAttack();
            yield return new WaitForSeconds(2f);
        
            // 5. 回到追踪模式
            SwitchToTrackingMode(true);
        
            Debug.Log($"[CollectObjectController] 测试序列完成!");
        }
    
        #endregion
    
        #region 清理
    
        void OnDestroy()
        {
            // 清理动态创建的材质
            if (effectMaterial != null)
            {
                Destroy(effectMaterial);
            }
        
            // 恢复原始材质
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && originalMaterial != null)
            {
                renderer.material = originalMaterial;
            }
        
            // 清理粒子系统
            CleanupParticleSystems();
        }
    
        private void CleanupParticleSystems()
        {
            if (sweepSystem != null)
            {
                if (sweepSystem.sweepParticleSystem != null)
                {
                    Destroy(sweepSystem.sweepParticleSystem.gameObject);
                }
            
                if (sweepSystem.hitParticleSystem != null)
                {
                    Destroy(sweepSystem.hitParticleSystem.gameObject);
                }
            }
        }
    
        #endregion
    
        #region 编辑器支持
    
#if UNITY_EDITOR
        void OnValidate()
        {
            // 在编辑器中限制参数范围
            attackPower = Mathf.Clamp(attackPower, 0f, 100f);
            attackSpeed = Mathf.Max(0.1f, attackSpeed);
        }
#endif
    
        #endregion
    }
}