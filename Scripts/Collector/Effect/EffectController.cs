using System.Collections;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Effect
{
    [RequireComponent(typeof(Renderer))]
    public class EffectController : MonoBehaviour
    {
        [Header("材质引用")]
        private Material material;
        private Material originalMaterial;
    
        [Header("效果参数")]
        [SerializeField] private float distortionIntensity = 0f;
        [SerializeField] private float disintegrationIntensity = 0f;
        [SerializeField] private float flashIntensity = 0f;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float distortionSpeed = 1f;
    
        [Header("动画控制")]
        public AnimationCurve disintegrationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve flashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        private Coroutine activeAnimation;
    
        [Header("状态")]
        public bool isInitialized = false;
    
        void Start()
        {
            Initialize();
        }
    
        void Update()
        {
            // 如果需要每帧更新材质参数（例如扭曲动画）
            if (isInitialized && material)
            {
                // 更新扭曲相关的时间参数
                UpdateDistortionTime();
            }
        }
    
        private void Initialize()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer)
            {
                // 复制材质以确保不影响其他使用相同材质的物体
                originalMaterial = renderer.material;
                material = new Material(originalMaterial);
                renderer.material = material;
            
                // 初始化Shader参数
                ResetAllEffects();
                isInitialized = true;
            
                Debug.Log($"EffectController initialized for {gameObject.name}");
            }
            else
            {
                Debug.LogError($"No renderer found on {gameObject.name}");
            }
        }
    
        #region 基础设置方法
    
        public void SetDistortionIntensity(float intensity)
        {
            distortionIntensity = Mathf.Clamp01(intensity);
            UpdateMaterialFloat("_Distortion", distortionIntensity);
        }
    
        public void SetDisintegrationIntensity(float intensity)
        {
            disintegrationIntensity = Mathf.Clamp01(intensity);
            UpdateMaterialFloat("_Disintegration", disintegrationIntensity);
        }
    
        public void SetFlashIntensity(float intensity)
        {
            flashIntensity = Mathf.Clamp(intensity, 0f, 10f);
            UpdateMaterialFloat("_FlashIntensity", flashIntensity);
        }
    
        public void SetFlashColor(Color color)
        {
            flashColor = color;
            UpdateMaterialColor("_FlashColor", flashColor);
        }
    
        public void SetDistortionSpeed(float speed)
        {
            distortionSpeed = Mathf.Max(0.1f, speed);
            UpdateMaterialFloat("_DistortionSpeed", distortionSpeed);
        }
    
        public void SetDisintegrationAmount(float amount)
        {
            UpdateMaterialFloat("_DisintegrationAmount", amount);
        }
    
        public void SetDisintegrationSpeed(float speed)
        {
            UpdateMaterialFloat("_DisintegrationSpeed", speed);
        }
    
        public void SetEdgeWidth(float width)
        {
            UpdateMaterialFloat("_EdgeWidth", width);
        }
    
        public void SetEdgeColor(Color color)
        {
            UpdateMaterialColor("_EdgeColor", color);
        }
    
        #endregion
    
        #region 获取当前值方法
    
        public float GetDistortionIntensity()
        {
            return distortionIntensity;
        }
    
        public float GetDisintegrationIntensity()
        {
            return disintegrationIntensity;
        }
    
        public float GetFlashIntensity()
        {
            return flashIntensity;
        }
    
        public Color GetFlashColor()
        {
            return flashColor;
        }
    
        #endregion
    
        #region 动画控制
    
        public void PlayDisintegrationAnimation(float targetIntensity, float duration, AnimationCurve curve = null)
        {
            if (!isInitialized) return;
        
            // 停止正在进行的动画
            if (activeAnimation != null)
            {
                StopCoroutine(activeAnimation);
            }
        
            activeAnimation = StartCoroutine(AnimateDisintegration(targetIntensity, duration, curve));
        }
    
        public void PlayFlashAnimation(float targetIntensity, float duration, Color flashColor, AnimationCurve curve = null)
        {
            if (!isInitialized) return;
        
            // 停止正在进行的动画
            if (activeAnimation != null)
            {
                StopCoroutine(activeAnimation);
            }
        
            activeAnimation = StartCoroutine(AnimateFlash(targetIntensity, duration, flashColor, curve));
        }
    
        public void PlayAttackAnimation(float disintegrationIntensity, float flashIntensity, float duration, Color flashColor)
        {
            if (!isInitialized) return;
        
            // 停止正在进行的动画
            if (activeAnimation != null)
            {
                StopCoroutine(activeAnimation);
            }
        
            activeAnimation = StartCoroutine(AnimateAttack(disintegrationIntensity, flashIntensity, duration, flashColor));
        }
    
        public void StopAllAnimations()
        {
            if (activeAnimation != null)
            {
                StopCoroutine(activeAnimation);
                activeAnimation = null;
            }
        
            ResetAllEffects();
        }
    
        private IEnumerator AnimateDisintegration(float targetIntensity, float duration, AnimationCurve curve)
        {
            float startIntensity = disintegrationIntensity;
            float elapsedTime = 0f;
        
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
            
                if (curve != null)
                {
                    t = curve.Evaluate(t);
                }
            
                float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
                SetDisintegrationIntensity(currentIntensity);
            
                yield return null;
            }
        
            SetDisintegrationIntensity(targetIntensity);
            activeAnimation = null;
        }
    
        private IEnumerator AnimateFlash(float targetIntensity, float duration, Color color, AnimationCurve curve)
        {
            float startIntensity = flashIntensity;
            Color startColor = flashColor;
            float elapsedTime = 0f;
        
            SetFlashColor(color);
        
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
            
                if (curve != null)
                {
                    t = curve.Evaluate(t);
                }
            
                float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
                SetFlashIntensity(currentIntensity);
            
                yield return null;
            }
        
            SetFlashIntensity(targetIntensity);
            activeAnimation = null;
        }
    
        private IEnumerator AnimateAttack(float disintegrationTarget, float flashTarget, float duration, Color flashColor)
        {
            float startDisintegration = disintegrationIntensity;
            float startFlash = flashIntensity;
            Color startFlashColor = flashColor;
        
            float elapsedTime = 0f;
        
            // 设置目标颜色
            SetFlashColor(flashColor);
        
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
            
                // 肢解效果：快速上升到目标值，然后保持，最后快速下降
                float disintegrationT;
                if (t < 0.3f) // 上升阶段
                {
                    disintegrationT = t / 0.3f;
                    disintegrationT = disintegrationCurve.Evaluate(disintegrationT);
                }
                else if (t < 0.7f) // 保持阶段
                {
                    disintegrationT = 1f;
                }
                else // 下降阶段
                {
                    disintegrationT = (1f - ((t - 0.7f) / 0.3f));
                    disintegrationT = disintegrationCurve.Evaluate(disintegrationT);
                }
            
                // 闪光效果：脉冲效果
                float flashT = Mathf.Sin(t * Mathf.PI * 4) * 0.5f + 0.5f;
            
                float currentDisintegration = Mathf.Lerp(startDisintegration, disintegrationTarget, disintegrationT);
                float currentFlash = Mathf.Lerp(startFlash, flashTarget, flashT);
            
                SetDisintegrationIntensity(currentDisintegration);
                SetFlashIntensity(currentFlash);
            
                yield return null;
            }
        
            // 确保最后回到初始状态
            SetDisintegrationIntensity(0f);
            SetFlashIntensity(0f);
            activeAnimation = null;
        }
    
        #endregion
    
        #region 辅助方法
    
        private void UpdateMaterialFloat(string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
            else
            {
                Debug.LogWarning($"Material does not have property: {propertyName}");
            }
        }
    
        private void UpdateMaterialColor(string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
            else
            {
                Debug.LogWarning($"Material does not have property: {propertyName}");
            }
        }
    
        private void UpdateDistortionTime()
        {
            // 如果需要每帧更新扭曲时间，可以在这里处理
            // 例如：material.SetFloat("_DistortionTime", Time.time * distortionSpeed);
        }
    
        public void ResetAllEffects()
        {
            SetDistortionIntensity(0f);
            SetDisintegrationIntensity(0f);
            SetFlashIntensity(0f);
            SetFlashColor(Color.white);
            SetDistortionSpeed(1f);
        
            // 重置其他可能的效果参数
            UpdateMaterialFloat("_DisintegrationAmount", 1f);
            UpdateMaterialFloat("_DisintegrationSpeed", 1f);
            UpdateMaterialFloat("_EdgeWidth", 0.05f);
            UpdateMaterialColor("_EdgeColor", Color.red);
        }
    
        public void ApplyAttackEffectSettings(float distortion, float disintegration, float flash, Color flashCol)
        {
            SetDistortionIntensity(distortion);
            SetDisintegrationIntensity(disintegration);
            SetFlashIntensity(flash);
            SetFlashColor(flashCol);
        }
    
        #endregion
    
        void OnDestroy()
        {
            // 清理材质实例
            if (material != null)
            {
                Destroy(material);
            }
        }
    
        void OnValidate()
        {
            // 在编辑器模式下即时更新效果
            if (Application.isPlaying && isInitialized)
            {
                UpdateMaterialFloat("_Distortion", distortionIntensity);
                UpdateMaterialFloat("_Disintegration", disintegrationIntensity);
                UpdateMaterialFloat("_FlashIntensity", flashIntensity);
                UpdateMaterialColor("_FlashColor", flashColor);
                UpdateMaterialFloat("_DistortionSpeed", distortionSpeed);
            }
        }
    
        #region 调试方法
    
        [ContextMenu("测试肢解效果")]
        public void TestDisintegration()
        {
            if (!isInitialized) Initialize();
            PlayDisintegrationAnimation(1f, 2f);
        }
    
        [ContextMenu("测试闪光效果")]
        public void TestFlash()
        {
            if (!isInitialized) Initialize();
        
            Color[] colors = { Color.white, Color.yellow, Color.red, Color.blue, Color.cyan, Color.green };
            Color randomColor = colors[Random.Range(0, colors.Length)];
        
            PlayFlashAnimation(5f, 1f, randomColor);
        }
    
        [ContextMenu("测试攻击效果")]
        public void TestAttack()
        {
            if (!isInitialized) Initialize();
        
            Color[] colors = { Color.white, Color.yellow, Color.red, Color.blue, Color.cyan, Color.green };
            Color randomColor = colors[Random.Range(0, colors.Length)];
        
            PlayAttackAnimation(0.8f, 8f, 1f, randomColor);
        }
    
        [ContextMenu("重置所有效果")]
        public void ResetEffects()
        {
            ResetAllEffects();
        }
    
        #endregion
    }
}