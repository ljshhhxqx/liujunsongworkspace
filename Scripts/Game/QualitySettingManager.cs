using System;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UnityEngine;

namespace HotUpdate.Scripts.Game
{
    /// <summary>
    /// 画质设置管理器
    /// 提供游戏画质设置的应用和本地存档功能
    /// </summary>
    public class QualitySettingManager : Singleton<QualitySettingManager>
    {
        private const string KEY_QUALITY_DATA = "QualitySettingData";

        public HReactiveProperty<QualitySettingData> QualitySetting { get; } =
            new HReactiveProperty<QualitySettingData>();
        
        public QualitySettingManager()
        { 
            QualitySetting.Value = LoadFromPlayerPrefs() ?? QualitySettingData.GetDefault();
        }

        /// <summary>
        /// 应用画质设置到Unity引擎
        /// </summary>
        public void ApplySettings(QualitySettingData settings)
        {
            if (settings == null) return;
            
            settings.Validate();
            QualitySetting.Value = settings;

            ApplyResolutionSettings(settings);
            ApplyQualitySettings(settings);
            ApplyShadowSettings(settings);
            ApplyAntiAliasing(settings);
            ApplyVSyncAndFrameRate(settings);
            
            Debug.Log($"[QualitySettingManager] 画质设置已应用: {settings.qualityLevel}");
        }

        /// <summary>
        /// 应用分辨率设置
        /// </summary>
        private void ApplyResolutionSettings(QualitySettingData settings)
        {
            if (settings.useCustomResolution)
            {
                Screen.SetResolution(settings.customWidth, settings.customHeight, settings.isFullscreen);
            }
            else
            {
                Screen.SetResolution(Screen.width, Screen.height, settings.isFullscreen);
            }
        }

        /// <summary>
        /// 应用基础质量设置
        /// </summary>
        private void ApplyQualitySettings(QualitySettingData settings)
        {
            QualitySettings.globalTextureMipmapLimit = 2 - settings.materialQuality;
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)settings.anisotropicFiltering;
            QualitySettings.softParticles = settings.softParticles;
            QualitySettings.streamingMipmapsActive = settings.enableTextureStreaming;
        }

        /// <summary>
        /// 应用阴影设置
        /// </summary>
        private void ApplyShadowSettings(QualitySettingData settings)
        {
            QualitySettings.shadows = settings.shadowsEnabled 
                ? settings.shadowQuality switch
                {
                    1 => ShadowQuality.HardOnly,
                    2 => ShadowQuality.All,
                    _ => ShadowQuality.Disable
                }
                : ShadowQuality.Disable;

            QualitySettings.shadowResolution = (ShadowResolution)settings.shadowResolution;
            QualitySettings.shadowDistance = settings.shadowDistance;
        }

        /// <summary>
        /// 应用抗锯齿设置
        /// </summary>
        private void ApplyAntiAliasing(QualitySettingData settings)
        {
            QualitySettings.antiAliasing = (int)settings.antiAliasing;
        }

        /// <summary>
        /// 应用垂直同步和帧率设置
        /// </summary>
        private void ApplyVSyncAndFrameRate(QualitySettingData settings)
        {
            QualitySettings.vSyncCount = settings.enableVSync ? 1 : 0;
            Application.targetFrameRate = settings.targetFrameRate;
        }

        /// <summary>
        /// 快速应用预设等级
        /// </summary>
        public void ApplyPreset(QualityLevel level)
        {
            var preset = QualitySettingData.GetRecommended(level);
            ApplySettings(preset);
            SaveToPlayerPrefs();
        }

        /// <summary>
        /// 保存当前设置到本地
        /// </summary>
        public void SaveToPlayerPrefs()
        {
            var json = JsonUtility.ToJson(QualitySetting.Value);
            PlayerPrefs.SetString(KEY_QUALITY_DATA, json);
            PlayerPrefs.Save();
            Debug.Log("[QualitySettingManager] 画质设置已保存到本地");
        }

        /// <summary>
        /// 从本地加载设置
        /// </summary>
        public QualitySettingData LoadFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(KEY_QUALITY_DATA))
            {
                return null;
            }

            try
            {
                var json = PlayerPrefs.GetString(KEY_QUALITY_DATA);
                var data = JsonUtility.FromJson<QualitySettingData>(json);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QualitySettingManager] 加载画质设置失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefault()
        {
            ApplySettings(QualitySettingData.GetDefault());
            SaveToPlayerPrefs();
        }

        /// <summary>
        /// 获取当前预设等级
        /// </summary>
        public QualityLevel GetCurrentQualityLevel()
        {
            return QualitySetting.Value?.qualityLevel ?? QualityLevel.High;
        }

        /// <summary>
        /// 检查是否为默认设置
        /// </summary>
        public bool IsDefaultSettings()
        {
            var defaultSettings = QualitySettingData.GetDefault();
            return QualitySetting.Value.qualityLevel == defaultSettings.qualityLevel &&
                   QualitySetting.Value.useCustomResolution == defaultSettings.useCustomResolution &&
                   QualitySetting.Value.enableVSync == defaultSettings.enableVSync &&
                   QualitySetting.Value.targetFrameRate == defaultSettings.targetFrameRate;
        }
    }
}
