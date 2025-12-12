using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Effect
{
    [System.Serializable]
    public class AttackEffectConfig
    {
        [Header("攻击力等级映射")]
        public AttackPowerLevel powerLevel = AttackPowerLevel.Normal;
        public float minPower = 0f;
        public float maxPower = 20f;
        
        [Header("扭曲效果配置")]
        public float baseDistortion = 0.1f;
        public float normalDistortion = 0.2f;
        public float strongDistortion = 0.4f;
        public float superDistortion = 0.7f;
        
        [Header("肢解效果配置")]
        public float baseDisintegration = 0f;
        public float normalDisintegration = 0.3f;
        public float strongDisintegration = 0.6f;
        public float superDisintegration = 1f;
        
        [Header("闪光效果配置")]
        public float baseFlashIntensity = 0f;
        public float normalFlashIntensity = 3f;
        public float strongFlashIntensity = 6f;
        public float superFlashIntensity = 10f;
        
        [Header("闪光颜色池")]
        public List<Color> flashColorPool = new List<Color>
        {
            Color.white,
            Color.yellow,
            Color.red,
            Color.blue,
            Color.cyan,
            Color.green
        };
    }

    [System.Serializable]
    public class AttackSpeedConfig
    {
        [Header("攻击频率等级映射")]
        public AttackSpeedLevel speedLevel = AttackSpeedLevel.Normal;
        public float minAttackInterval = 0.1f;
        public float maxAttackInterval = 2f;
        
        [Header("动画速度配置")]
        public float normalSpeed = 1f;
        public float fastSpeed = 1.5f;
        public float superFastSpeed = 2f;
        
        [Header("攻击持续时间配置")]
        public float normalAttackDuration = 1f;
        public float fastAttackDuration = 0.5f;
        public float superFastAttackDuration = 0.25f;
    }

    public enum AttackPowerLevel { Normal, Strong, Super }
    public enum AttackSpeedLevel { Normal, Fast, SuperFast }

    public class AttackEffectMapper : MonoBehaviour
    {
        private static readonly int Distortion = Shader.PropertyToID("_Distortion");
        private static readonly int Disintegration = Shader.PropertyToID("_Disintegration");
        private static readonly int FlashIntensity = Shader.PropertyToID("_FlashIntensity");
        private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        [Header("配置")]
        public AttackEffectConfig powerConfig = new AttackEffectConfig();
        public AttackSpeedConfig speedConfig = new AttackSpeedConfig();
        
        [Header("运行时参数")]
        public float currentAttackPower;
        public float currentAttackInterval;
        
        [Header("当前等级")]
        [SerializeField] public AttackPowerLevel currentPowerLevel;
        [SerializeField] public AttackSpeedLevel currentSpeedLevel;
        
        [Header("输出参数")]
        public float distortionIntensity = 0f;
        public float disintegrationIntensity = 0f;
        public float flashIntensity = 0f;
        public Color flashColor = Color.white;
        public float animationSpeed = 1f;
        public float attackDuration = 1f;
        
        void Start()
        {
            InitializeConfig();
        }
        
        void Update()
        {
            UpdateEffectParameters();
        }
        
        private void InitializeConfig()
        {
            // 确保颜色池有默认颜色
            if (powerConfig.flashColorPool.Count == 0)
            {
                powerConfig.flashColorPool = new List<Color>
                {
                    Color.white,
                    Color.yellow,
                    Color.red,
                    Color.blue,
                    Color.cyan,
                    Color.green
                };
            }
        }
        
        public void SetAttackParameters(float power, float interval)
        {
            currentAttackPower = Mathf.Clamp(power, 0f, 100f);
            currentAttackInterval = Mathf.Clamp(interval, 
                speedConfig.minAttackInterval, 
                speedConfig.maxAttackInterval);
            
            UpdateEffectParameters();
        }
        
        private void UpdateEffectParameters()
        {
            // 1. 计算攻击力等级
            UpdatePowerLevel();
            
            // 2. 计算攻击频率等级
            UpdateSpeedLevel();
            
            // 3. 映射效果强度
            MapPowerToEffects();
            
            // 4. 映射动画速度
            MapSpeedToAnimation();
            
            // 5. 随机选择闪光颜色
            SelectRandomFlashColor();
        }
        
        private void UpdatePowerLevel()
        {
            float powerRange = powerConfig.maxPower - powerConfig.minPower;
            float normalizedPower = (currentAttackPower - powerConfig.minPower) / powerRange;
            
            if (normalizedPower < 0.33f)
                currentPowerLevel = AttackPowerLevel.Normal;
            else if (normalizedPower < 0.66f)
                currentPowerLevel = AttackPowerLevel.Strong;
            else
                currentPowerLevel = AttackPowerLevel.Super;
        }
        
        private void UpdateSpeedLevel()
        {
            float intervalRange = speedConfig.maxAttackInterval - speedConfig.minAttackInterval;
            float normalizedInterval = (currentAttackInterval - speedConfig.minAttackInterval) / intervalRange;
            
            // 攻击间隔越短，速度越快
            if (normalizedInterval > 0.66f) // 间隔时间长，速度慢
                currentSpeedLevel = AttackSpeedLevel.Normal;
            else if (normalizedInterval > 0.33f)
                currentSpeedLevel = AttackSpeedLevel.Fast;
            else
                currentSpeedLevel = AttackSpeedLevel.SuperFast;
        }
        
        private void MapPowerToEffects()
        {
            switch (currentPowerLevel)
            {
                case AttackPowerLevel.Normal:
                    distortionIntensity = powerConfig.normalDistortion;
                    disintegrationIntensity = powerConfig.normalDisintegration;
                    flashIntensity = powerConfig.normalFlashIntensity;
                    break;
                    
                case AttackPowerLevel.Strong:
                    distortionIntensity = powerConfig.strongDistortion;
                    disintegrationIntensity = powerConfig.strongDisintegration;
                    flashIntensity = powerConfig.strongFlashIntensity;
                    break;
                    
                case AttackPowerLevel.Super:
                    distortionIntensity = powerConfig.superDistortion;
                    disintegrationIntensity = powerConfig.superDisintegration;
                    flashIntensity = powerConfig.superFlashIntensity;
                    break;
            }
        }
        
        private void MapSpeedToAnimation()
        {
            switch (currentSpeedLevel)
            {
                case AttackSpeedLevel.Normal:
                    animationSpeed = speedConfig.normalSpeed;
                    attackDuration = speedConfig.normalAttackDuration;
                    break;
                    
                case AttackSpeedLevel.Fast:
                    animationSpeed = speedConfig.fastSpeed;
                    attackDuration = speedConfig.fastAttackDuration;
                    break;
                    
                case AttackSpeedLevel.SuperFast:
                    animationSpeed = speedConfig.superFastSpeed;
                    attackDuration = speedConfig.superFastAttackDuration;
                    break;
            }
        }
        
        private void SelectRandomFlashColor()
        {
            if (powerConfig.flashColorPool.Count > 0)
            {
                int randomIndex = Random.Range(0, powerConfig.flashColorPool.Count);
                flashColor = powerConfig.flashColorPool[randomIndex];
            }
            else
            {
                flashColor = Color.white;
            }
        }
        
        public void ApplyToMaterial(Material material)
        {
            if (!material) return;
            
            material.SetFloat(Distortion, distortionIntensity);
            material.SetFloat(Disintegration, disintegrationIntensity);
            material.SetFloat(FlashIntensity, flashIntensity);
            material.SetColor(FlashColor, flashColor);
        }
        
        // 调试方法
        public void LogCurrentState()
        {
            Debug.Log($"Power Level: {currentPowerLevel}, " +
                      $"Speed Level: {currentSpeedLevel}, " +
                      $"Distortion: {distortionIntensity}, " +
                      $"Disintegration: {disintegrationIntensity}, " +
                      $"Flash: {flashIntensity}, " +
                      $"Animation Speed: {animationSpeed}, " +
                      $"Attack Duration: {attackDuration}");
        }
    }
}