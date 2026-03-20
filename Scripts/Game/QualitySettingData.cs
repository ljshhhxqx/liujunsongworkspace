using System;
using UnityEngine;

namespace HotUpdate.Scripts.Game
{
    /// <summary>
    /// 画质预设等级
    /// </summary>
    public enum QualityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// 抗锯齿等级
    /// </summary>
    public enum AntiAliasingLevel
    {
        None = 0,
        FXAA = 2,
        MSAA2x = 4,
        MSAA4x = 8
    }

    /// <summary>
    /// 画质设置数据（本地存档用）
    /// </summary>
    [Serializable]
    public class QualitySettingData
    {
        [Header("=== 画质等级 ===")]
        [Tooltip("画质预设等级：0=低 1=中 2=高 3=极致")]
        public QualityLevel qualityLevel = QualityLevel.Medium;

        [Header("=== 分辨率 ===")]
        [Tooltip("是否使用自定义分辨率")]
        public bool useCustomResolution = false;
        [Tooltip("自定义分辨率宽度")]
        public int customWidth = 1920;
        [Tooltip("自定义分辨率高度")]
        public int customHeight = 1080;
        [Tooltip("是否全屏")]
        public bool isFullscreen = true;

        [Header("=== 帧率 ===")]
        [Tooltip("是否启用垂直同步 (VSync)")]
        public bool enableVSync = true;
        [Tooltip("目标帧率上限 (-1 为无限制)")]
        public int targetFrameRate = 60;

        [Header("=== 贴图质量 ===")]
        [Tooltip("材质质量等级 (0=低 1=中 2=高)")]
        [Range(0, 2)]
        public int materialQuality = 2;
        [Tooltip("各向异性过滤 (0=关闭 1=2x 2=4x 3=8x)")]
        [Range(0, 3)]
        public int anisotropicFiltering = 3;
        [Tooltip("纹理流是否启用")]
        public bool enableTextureStreaming = true;

        [Header("=== 抗锯齿 ===")]
        [Tooltip("抗锯齿等级：0=关闭 2=FXAA 4=MSAA 2x 8=MSAA 4x")]
        public AntiAliasingLevel antiAliasing = AntiAliasingLevel.FXAA;

        [Header("=== 阴影 ===")]
        [Tooltip("是否启用实时阴影")]
        public bool shadowsEnabled = true;
        [Tooltip("阴影质量 (0=禁用 1=硬阴影 2=软阴影)")]
        [Range(0, 2)]
        public int shadowQuality = 2;
        [Tooltip("阴影分辨率 (0=256 1=512 2=1024 3=2048 4=4096)")]
        [Range(0, 4)]
        public int shadowResolution = 1;
        [Tooltip("阴影渲染距离")]
        [Range(10f, 500f)]
        public float shadowDistance = 100f;

        [Header("=== 粒子效果 ===")]
        [Tooltip("是否启用柔和粒子")]
        public bool softParticles = true;
        [Tooltip("粒子渲染距离")]
        [Range(50f, 300f)]
        public float particleDrawDistance = 200f;

        /// <summary>
        /// 获取默认画质设置
        /// </summary>
        public static QualitySettingData GetDefault()
        {
            return new QualitySettingData();
        }

        /// <summary>
        /// 根据预设等级获取推荐设置
        /// </summary>
        public static QualitySettingData GetRecommended(QualityLevel level)
        {
            var settings = new QualitySettingData
            {
                qualityLevel = level
            };

            switch (level)
            {
                case QualityLevel.Low:
                    settings.materialQuality = 0;
                    settings.anisotropicFiltering = 0;
                    settings.antiAliasing = AntiAliasingLevel.None;
                    settings.softParticles = false;
                    settings.shadowsEnabled = false;
                    settings.enableTextureStreaming = true;
                    settings.particleDrawDistance = 100f;
                    settings.targetFrameRate = 30;
                    break;

                case QualityLevel.Medium:
                    settings.materialQuality = 1;
                    settings.anisotropicFiltering = 1;
                    settings.antiAliasing = AntiAliasingLevel.FXAA;
                    settings.softParticles = true;
                    settings.shadowsEnabled = true;
                    settings.shadowQuality = 1;
                    settings.shadowResolution = 2;
                    settings.shadowDistance = 80f;
                    settings.enableTextureStreaming = true;
                    settings.particleDrawDistance = 150f;
                    settings.targetFrameRate = 60;
                    break;

                case QualityLevel.High:
                    settings.materialQuality = 2;
                    settings.anisotropicFiltering = 2;
                    settings.antiAliasing = AntiAliasingLevel.FXAA;
                    settings.softParticles = true;
                    settings.shadowsEnabled = true;
                    settings.shadowQuality = 2;
                    settings.shadowResolution = 3;
                    settings.shadowDistance = 150f;
                    settings.enableTextureStreaming = true;
                    settings.particleDrawDistance = 200f;
                    settings.targetFrameRate = 60;
                    break;

                case QualityLevel.Ultra:
                    settings.materialQuality = 2;
                    settings.anisotropicFiltering = 3;
                    settings.antiAliasing = AntiAliasingLevel.MSAA4x;
                    settings.softParticles = true;
                    settings.shadowsEnabled = true;
                    settings.shadowQuality = 2;
                    settings.shadowResolution = 4;
                    settings.shadowDistance = 200f;
                    settings.enableTextureStreaming = true;
                    settings.particleDrawDistance = 300f;
                    settings.targetFrameRate = 90;
                    break;
            }

            return settings;
        }

        /// <summary>
        /// 验证并修正设置值的合法性
        /// </summary>
        public void Validate()
        {
            customWidth = Mathf.Clamp(customWidth, 640, 7680);
            customHeight = Mathf.Clamp(customHeight, 480, 4320);
            targetFrameRate = Mathf.Clamp(targetFrameRate, -1, 300);
            materialQuality = Mathf.Clamp(materialQuality, 0, 2);
            shadowQuality = Mathf.Clamp(shadowQuality, 0, 2);
            shadowResolution = Mathf.Clamp(shadowResolution, 0, 4);
            shadowDistance = Mathf.Clamp(shadowDistance, 10f, 500f);
            particleDrawDistance = Mathf.Clamp(particleDrawDistance, 50f, 300f);
        }

        public bool Equals(QualitySettingData other)
        {
            return other != null &&
                   qualityLevel == other.qualityLevel &&
                   customWidth == other.customWidth &&
                   customHeight == other.customHeight &&
                   targetFrameRate == other.targetFrameRate &&
                   materialQuality == other.materialQuality &&
                   anisotropicFiltering == other.anisotropicFiltering &&
                   enableTextureStreaming == other.enableTextureStreaming &&
                   antiAliasing == other.antiAliasing &&
                   shadowsEnabled == other.shadowsEnabled &&
                   shadowQuality == other.shadowQuality &&
                   shadowResolution == other.shadowResolution &&
                   Mathf.Approximately(shadowDistance, other.shadowDistance) &&
                   Mathf.Approximately(particleDrawDistance, other.particleDrawDistance);
        }
    }
}
