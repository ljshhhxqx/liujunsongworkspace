using UnityEngine;

namespace HotUpdate.Scripts.Collector
{
    public class CollectParticlePlayer : MonoBehaviour
    {
        
        public void Play(Color targetColor, float lerpFactor = 0.5f)
        {
            // 获取当前GameObject及其所有子物体上的所有ParticleSystem组件
            var particleSystems = GetComponentsInChildren<ParticleSystem>();

            foreach (var ps in particleSystems)
            {
                // 获取ParticleSystem的MainModule
                var mainModule = ps.main;

                // 获取原始的StartColor
                var originalColor = mainModule.startColor;

                // 创建新的MinMaxGradient
                ParticleSystem.MinMaxGradient newColor;

                if (originalColor.mode == ParticleSystemGradientMode.Color)
                {
                    // 单一颜色模式
                    newColor = new ParticleSystem.MinMaxGradient(Color.Lerp(originalColor.color, targetColor, lerpFactor));
                }
                else if (originalColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    // 两个颜色模式
                    newColor = new ParticleSystem.MinMaxGradient(
                        Color.Lerp(originalColor.colorMin, targetColor, lerpFactor),
                        Color.Lerp(originalColor.colorMax, targetColor, lerpFactor)
                    );
                }
                else if (originalColor.mode == ParticleSystemGradientMode.Gradient)
                {
                    // 梯度颜色模式
                    var gradient = new Gradient();
                    var colorKeys = originalColor.gradient.colorKeys;
                    var alphaKeys = originalColor.gradient.alphaKeys;

                    for (int i = 0; i < colorKeys.Length; i++)
                    {
                        colorKeys[i].color = Color.Lerp(colorKeys[i].color, targetColor, lerpFactor);
                    }

                    gradient.SetKeys(colorKeys, alphaKeys);
                    newColor = new ParticleSystem.MinMaxGradient(gradient);
                }
                else if (originalColor.mode == ParticleSystemGradientMode.TwoGradients)
                {
                    // 两个梯度颜色模式
                    var gradientMin = new Gradient();
                    var gradientMax = new Gradient();

                    var colorKeysMin = originalColor.gradientMin.colorKeys;
                    var colorKeysMax = originalColor.gradientMax.colorKeys;
                    var alphaKeysMin = originalColor.gradientMin.alphaKeys;
                    var alphaKeysMax = originalColor.gradientMax.alphaKeys;

                    for (var i = 0; i < colorKeysMin.Length; i++)
                    {
                        colorKeysMin[i].color = Color.Lerp(colorKeysMin[i].color, targetColor, lerpFactor);
                    }
                    for (var i = 0; i < colorKeysMax.Length; i++)
                    {
                        colorKeysMax[i].color = Color.Lerp(colorKeysMax[i].color, targetColor, lerpFactor);
                    }

                    gradientMin.SetKeys(colorKeysMin, alphaKeysMin);
                    gradientMax.SetKeys(colorKeysMax, alphaKeysMax);

                    newColor = new ParticleSystem.MinMaxGradient(gradientMin, gradientMax);
                }
                else
                {
                    continue; // 其他模式不处理
                }

                // 设置新的StartColor
                mainModule.startColor = newColor;
            }
        
            // 修改完颜色后，立即播放所有粒子系统
            foreach (var ps in particleSystems)
            {
                ps.Play();
            }
        }
    }
}
 