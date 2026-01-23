using System.Collections;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class MaterialTransparencyController : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        [Header("透明度设置")]
        [Range(0, 1)] public float targetAlpha = 1f;
        public float transitionDuration = 0.5f;
        
        private Renderer _objectRenderer;
        private Material[] _originalMaterials;
        private Material[] _transparentMaterials;
        private Coroutine _fadeCoroutine;
        
        void Start()
        {
            _objectRenderer = GetComponent<Renderer>();
            if(_objectRenderer)
            {
                // 保存原始材质
                _originalMaterials = _objectRenderer.materials;
                
                // 创建透明材质实例
                CreateTransparentMaterials();
            }
        }
        
        void CreateTransparentMaterials()
        {
            _transparentMaterials = new Material[_originalMaterials.Length];
            
            for(int i = 0; i < _originalMaterials.Length; i++)
            {
                // 复制原始材质
                _transparentMaterials[i] = new Material(_originalMaterials[i]);
                
                // 尝试设置透明度
                SetMaterial(_transparentMaterials[i], _originalMaterials[i].color, targetAlpha);
            }
        }
        
        private static readonly string[] AlphaPropertyNames = {
            "_Color", "_BaseColor", "_MainColor", "_TintColor",
            "_Alpha", "_Transparency", "_Opacity"
        };
        
        void SetMaterial(Material material, Color targetColor, float alpha)
        {
            // 尝试颜色属性中的alpha
            foreach(string propertyName in AlphaPropertyNames)
            {
                if(material.HasProperty(propertyName))
                {
                    if(material.GetColor(propertyName) != default)
                    {
                        targetColor = targetColor == default ? material.GetColor(propertyName) : targetColor;
                        targetColor.a = alpha;
                        material.SetColor(propertyName, targetColor);
                        
                        // 如果设置了透明度，启用透明渲染模式
                        SetupTransparentRendering(material);
                        return;
                    }
                }
            }
            
            // 尝试单独的alpha属性
            foreach(string propertyName in AlphaPropertyNames)
            {
                if(material.HasProperty(propertyName))
                {
                    material.SetFloat(propertyName, alpha);
                    SetupTransparentRendering(material);
                    return;
                }
            }
            
            // 如果以上都不行，使用默认的颜色属性
            if(material.HasProperty(ColorProperty))
            {
                Color color = material.GetColor(ColorProperty);
                color = targetColor == default ? color : targetColor;
                color.a = alpha;
                material.SetColor(ColorProperty, color);
                SetupTransparentRendering(material);
            }
        }
        
        void SetupTransparentRendering(Material material)
        {
            // 设置渲染模式为透明
            material.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt(ZWrite, 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        
        public void SetColor(Color targetColor = default, float alpha = 1f)
        {
            if (!this)
            {
                return;
            }
            if(_fadeCoroutine != null || !this)
                StopCoroutine(_fadeCoroutine);
            
            _fadeCoroutine = StartCoroutine(FadeToAlpha(targetColor, alpha));
        }
        
        IEnumerator FadeToAlpha(Color targetColor, float alpha)
        {
            float startAlpha = GetCurrentAlpha();
            float elapsedTime = 0f;
            
            while(elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(startAlpha, alpha, elapsedTime / transitionDuration);
                
                // 更新所有材质的透明度
                foreach(Material material in _transparentMaterials)
                {
                    SetMaterial(material, targetColor, currentAlpha);
                }
                
                // 应用透明材质
                _objectRenderer.materials = _transparentMaterials;
                
                yield return null;
            }
            
            // 最终设置
            foreach(Material material in _transparentMaterials)
            {
                SetMaterial(material, targetColor, alpha);
            }
            _objectRenderer.materials = _transparentMaterials;
        }
        
        float GetCurrentAlpha()
        {
            if(_transparentMaterials == null || _transparentMaterials.Length == 0)
                return 1f;
            
            // 从第一个材质获取当前alpha
            Material firstMaterial = _transparentMaterials[0];
            
            // 尝试获取颜色属性的alpha
            string[] colorProperties = { "_Color", "_BaseColor", "_MainColor" };
            foreach(string property in colorProperties)
            {
                if(firstMaterial.HasProperty(property))
                {
                    return firstMaterial.GetColor(property).a;
                }
            }
            
            return 1f;
        }
        
        // 恢复原始材质
        public void RestoreOriginalMaterials()
        {
            if(_objectRenderer && _originalMaterials != null)
            {
                _objectRenderer.materials = _originalMaterials;
            }
        }
        
        public void SetEnabled(bool isEnabled)
        {
            if(_objectRenderer)
            {
                _objectRenderer.enabled = isEnabled;
            }
        }
        
        void OnDestroy()
        {
            if(_fadeCoroutine != null || !this)
                StopCoroutine(_fadeCoroutine);
            // 清理创建的材质实例
            if(_transparentMaterials != null)
            {
                foreach(Material material in _transparentMaterials)
                {
                    if(material)
                        Destroy(material);
                }
            }
        }
    }
}