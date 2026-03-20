using System.Collections.Generic;
using AOTScripts.Tool.Resource;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.UI.UIBase;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using QualityLevel = HotUpdate.Scripts.Game.QualityLevel;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    /// <summary>
    /// 画质设置UI面板
    /// </summary>
    public class QualitySettingUI : ScreenUIBase
    {
        public override UIType Type => UIType.QualitySetting;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;

        [Header("=== 画质等级 ===")]
        [SerializeField]
        private TMP_Dropdown qualityLevelDropdown;

        [Header("=== 分辨率 ===")]
        [SerializeField]
        private Toggle useCustomResolutionToggle;
        [SerializeField]
        private TMP_InputField resolutionWidthInput;
        [SerializeField]
        private TMP_InputField resolutionHeightInput;
        [SerializeField]
        private Toggle fullscreenToggle;

        [Header("=== 帧率 ===")]
        [SerializeField]
        private Toggle vsyncToggle;
        [SerializeField]
        private TMP_Dropdown targetFrameRateDropdown;

        [Header("=== 贴图质量 ===")]
        [SerializeField]
        private TMP_Dropdown materialQualityDropdown;

        [Header("=== 抗锯齿 ===")]
        [SerializeField]
        private TMP_Dropdown antiAliasingDropdown;

        [Header("=== 阴影 ===")]
        [SerializeField]
        private Toggle shadowsToggle;
        
        [Header("=== 粒子效果 ===")]
        [SerializeField]
        private Toggle softParticlesToggle;
        
        [Header("=== 操作按钮 ===")]
        [SerializeField]
        private Button applyButton;
        [SerializeField]
        private Button resetButton;
        [SerializeField]
        private Button closeButton;

        private QualitySettingData _pendingSettings;
        private bool _isUpdatingUI;
        private QualitySettingData _originSettings;
        private UIManager _uiManager;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            Debug.Log("QualitySettingUI Init");
            InitializeDropdowns();
            BindEvents();
            QualitySettingManager.Instance.QualitySetting
                .Subscribe(OnQualitySettingChanged)
                .AddTo(this);
            LoadCurrentSettings();
        }

        private void InitializeDropdowns()
        {
            qualityLevelDropdown.ClearOptions();
            qualityLevelDropdown.AddOptions(new List<TMP_Dropdown.OptionData>() { new TMP_Dropdown.OptionData("低"), new TMP_Dropdown.OptionData("中"), new TMP_Dropdown.OptionData("高"), new TMP_Dropdown.OptionData("极致") });

            targetFrameRateDropdown.ClearOptions();
            targetFrameRateDropdown.AddOptions(new List<TMP_Dropdown.OptionData>() { new TMP_Dropdown.OptionData("无限制"), new TMP_Dropdown.OptionData("30"), new TMP_Dropdown.OptionData("60"), new TMP_Dropdown.OptionData("120"), new TMP_Dropdown.OptionData("144"), new TMP_Dropdown.OptionData("240") });
            

            materialQualityDropdown.ClearOptions();
            materialQualityDropdown.AddOptions(new List<TMP_Dropdown.OptionData>() { new TMP_Dropdown.OptionData("低 (0)"), new TMP_Dropdown.OptionData("中 (1)"), new TMP_Dropdown.OptionData("高 (2)") });

            antiAliasingDropdown.ClearOptions();
            antiAliasingDropdown.AddOptions(new List<TMP_Dropdown.OptionData>() { new TMP_Dropdown.OptionData("关闭"), new TMP_Dropdown.OptionData("FXAA"), new TMP_Dropdown.OptionData("MSAA 2x"), new TMP_Dropdown.OptionData("MSAA 4x") });
        }

        private void BindEvents()
        {
            qualityLevelDropdown.onValueChanged.AddListener(OnQualityLevelChanged);
            useCustomResolutionToggle.onValueChanged.AddListener(OnUseCustomResolutionChanged);
            resolutionWidthInput.onValueChanged.AddListener(OnResolutionWidthChanged);
            resolutionHeightInput.onValueChanged.AddListener(OnResolutionHeightChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            targetFrameRateDropdown.onValueChanged.AddListener(OnTargetFrameRateChanged);
            materialQualityDropdown.onValueChanged.AddListener(OnMaterialQualityChanged);
            antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
            shadowsToggle.onValueChanged.AddListener(OnShadowsChanged);
            softParticlesToggle.onValueChanged.AddListener(OnSoftParticlesChanged);

            applyButton.onClick.AddListener(OnApplyClicked);
            resetButton.onClick.AddListener(OnResetClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void LoadCurrentSettings()
        {
            var settings = QualitySettingManager.Instance.QualitySetting.Value;
            _pendingSettings = new QualitySettingData();
            CopySettings(settings, _pendingSettings);
            UpdateUIFromSettings(_pendingSettings);
            _originSettings = settings;
        }

        private void CopySettings(QualitySettingData source, QualitySettingData target)
        {
            target.qualityLevel = source.qualityLevel;
            target.useCustomResolution = source.useCustomResolution;
            target.customWidth = source.customWidth;
            target.customHeight = source.customHeight;
            target.isFullscreen = source.isFullscreen;
            target.enableVSync = source.enableVSync;
            target.targetFrameRate = source.targetFrameRate;
            target.materialQuality = source.materialQuality;
            target.anisotropicFiltering = source.anisotropicFiltering;
            target.enableTextureStreaming = source.enableTextureStreaming;
            target.antiAliasing = source.antiAliasing;
            target.shadowsEnabled = source.shadowsEnabled;
            target.shadowQuality = source.shadowQuality;
            target.shadowResolution = source.shadowResolution;
            target.shadowDistance = source.shadowDistance;
            target.softParticles = source.softParticles;
            target.particleDrawDistance = source.particleDrawDistance;
        }

        private void OnQualitySettingChanged(QualitySettingData settings)
        {
            if (_isUpdatingUI) return;
            _pendingSettings = new QualitySettingData();
            CopySettings(settings, _pendingSettings);
            UpdateUIFromSettings(_pendingSettings);
        }

        private void UpdateUIFromSettings(QualitySettingData settings)
        {
            _isUpdatingUI = true;

            qualityLevelDropdown.value = (int)settings.qualityLevel;
            useCustomResolutionToggle.isOn = settings.useCustomResolution;
            resolutionWidthInput.text = settings.customWidth.ToString();
            resolutionHeightInput.text = settings.customHeight.ToString();
            fullscreenToggle.isOn = settings.isFullscreen;
            vsyncToggle.isOn = settings.enableVSync;
            targetFrameRateDropdown.value = GetFrameRateIndex(settings.targetFrameRate);
            materialQualityDropdown.value = settings.materialQuality;
            antiAliasingDropdown.value = (int)settings.antiAliasing;
            shadowsToggle.isOn = settings.shadowsEnabled;
            softParticlesToggle.isOn = settings.softParticles;

            UpdateResolutionInputInteractable(settings.useCustomResolution);
            UpdateShadowsInteractable(settings.shadowsEnabled);

            _isUpdatingUI = false;
        }

        private int GetFrameRateIndex(int frameRate)
        {
            return frameRate switch
            {
                -1 => 0,
                30 => 1,
                60 => 2,
                120 => 3,
                144 => 4,
                240 => 5,
                _ => 0
            };
        }

        private int GetFrameRateValue(int index)
        {
            return index switch
            {
                0 => -1,
                1 => 30,
                2 => 60,
                3 => 120,
                4 => 144,
                5 => 240,
                _ => -1
            };
        }

        private void UpdateResolutionInputInteractable(bool interactable)
        {
            resolutionWidthInput.interactable = interactable;
            resolutionHeightInput.interactable = interactable;
        }

        private void UpdateShadowsInteractable(bool interactable)
        {
        }

        private void OnQualityLevelChanged(int index)
        {
            if (_isUpdatingUI) return;
            var preset = QualitySettingData.GetRecommended((QualityLevel)index);
            _pendingSettings = preset;
            UpdateUIFromSettings(_pendingSettings);
        }

        private void OnUseCustomResolutionChanged(bool value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.useCustomResolution = value;
            UpdateResolutionInputInteractable(value);
        }

        private void OnResolutionWidthChanged(string value)
        {
            if (_isUpdatingUI) return;
            if (int.TryParse(value, out var width))
            {
                _pendingSettings.customWidth = Mathf.Clamp(width, 640, 7680);
            }
        }

        private void OnResolutionHeightChanged(string value)
        {
            if (_isUpdatingUI) return;
            if (int.TryParse(value, out var height))
            {
                _pendingSettings.customHeight = Mathf.Clamp(height, 480, 4320);
            }
        }

        private void OnFullscreenChanged(bool value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.isFullscreen = value;
        }

        private void OnVSyncChanged(bool value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.enableVSync = value;
        }

        private void OnTargetFrameRateChanged(int index)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.targetFrameRate = GetFrameRateValue(index);
        }

        private void OnMaterialQualityChanged(int value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.materialQuality = value;
        }

        private void OnAnisotropicFilteringChanged(int value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.anisotropicFiltering = value;
        }

        private void OnTextureStreamingChanged(bool value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.enableTextureStreaming = value;
        }

        private void OnAntiAliasingChanged(int value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.antiAliasing = (AntiAliasingLevel)value;
        }

        private void OnShadowsChanged(bool value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.shadowsEnabled = value;
            UpdateShadowsInteractable(value);
        }

        private void OnShadowQualityChanged(int value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.shadowQuality = value;
        }

        private void OnShadowResolutionChanged(int value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.shadowResolution = value;
        }

        private void OnShadowDistanceChanged(float value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.shadowDistance = value;
        }

        private void OnSoftParticlesChanged(bool value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.softParticles = value;
        }

        private void OnParticleDrawDistanceChanged(float value)
        {
            if (_isUpdatingUI) return;
            _pendingSettings.particleDrawDistance = value;
        }

        private void OnApplyClicked()
        {
            _pendingSettings.Validate();
            _originSettings = _pendingSettings;
            QualitySettingManager.Instance.ApplySettings(_pendingSettings);
            QualitySettingManager.Instance.SaveToPlayerPrefs();
            Debug.Log("[QualitySettingUI] 画质设置已应用并保存");
        }

        private void OnResetClicked()
        {
            var defaultSettings = QualitySettingData.GetDefault();
            QualitySettingManager.Instance.ApplySettings(defaultSettings);
            QualitySettingManager.Instance.SaveToPlayerPrefs();
            Debug.Log("[QualitySettingUI] 已重置为默认画质设置");
        }

        private void OnCloseClicked()
        {
            if (!_originSettings.Equals(_pendingSettings))
            {
                _uiManager.ShowTips("画质有改动，需要保存吗？", OnApplyClicked);
                return;
            }
            _uiManager.CloseUI(Type);
        }

        private void OnDestroy()
        {
            qualityLevelDropdown.onValueChanged.RemoveAllListeners();
            useCustomResolutionToggle.onValueChanged.RemoveAllListeners();
            resolutionWidthInput.onValueChanged.RemoveAllListeners();
            resolutionHeightInput.onValueChanged.RemoveAllListeners();
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            vsyncToggle.onValueChanged.RemoveAllListeners();
            targetFrameRateDropdown.onValueChanged.RemoveAllListeners();
            materialQualityDropdown.onValueChanged.RemoveAllListeners();
            antiAliasingDropdown.onValueChanged.RemoveAllListeners();
            shadowsToggle.onValueChanged.RemoveAllListeners();
            softParticlesToggle.onValueChanged.RemoveAllListeners();
            applyButton.onClick.RemoveAllListeners();
            resetButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
        }
    }
}
