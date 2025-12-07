using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.UIFollow
{
    [System.Serializable]
    public class UIFollowConfig
    {
        public FollowUIType uiPrefabName = FollowUIType.None;  // UI预设名称
        public Vector3 worldOffset = Vector3.up * 1.5f;
        public Vector2 screenOffset = Vector2.zero;
        public FollowMode followMode = FollowMode.WorldSpace;
        public bool faceCamera = true;
        public bool smoothFollow = true;
        public float smoothSpeed = 5f;
        public float maxDistance = 50f;
        public bool hideWhenOccluded = false;
        public LayerMask occlusionLayers = -1;
    }

    public enum FollowUIType
    {
        None,
        CollectItem,
        Player,
    }

    public enum FollowMode
    {
        WorldSpace,        // 世界空间跟随
        ScreenProjection,  // 屏幕投影
        Adaptive           // 自适应（根据距离切换）
    }

    public class UIFollowInstance : MonoBehaviour
    {
        // 公共属性（可在外部动态修改）
        public Transform Transform { get; private set; }
        public UIFollowConfig UIFollowConfig { get; private set; }
        public bool IsActive { get; private set; }
    
        // 内部组件引用
        private Canvas _worldCanvas;
        private Canvas _screenCanvas;
        private RectTransform _screenRect;
        private Camera _mainCamera;
        private Vector3 _velocity;
    
        // 状态变量
        private bool _isInitialized = false;
        private float _currentDistance;
        private bool _isVisible = true;
        private Camera _camera;
        
        public Canvas WorldCanvas => _worldCanvas;
        public Canvas ScreenCanvas => _screenCanvas;

        #region 公开方法

        private void Start()
        {
            _camera = Camera.main;
        }

        /// <summary>
        /// 初始化跟随实例
        /// </summary>
        public void Initialize(Transform targetTransform, UIFollowConfig followConfig = null)
        {
            if (!targetTransform)
            {
                Debug.LogError("UIFollowInstance: Target is null!");
                return;
            }
        
            Transform = targetTransform;
            UIFollowConfig = followConfig ?? new UIFollowConfig();
            _mainCamera = Camera.main;
        
            // 清理旧实例
            Cleanup();
        
            // 创建UI实例
            CreateUIInstance();
        
            _isInitialized = true;
            IsActive = true;
        
            // 注册到管理器
            if (!UIFollowManager.Instance.UIFollowInstances.ContainsKey(Transform))
            {
                UIFollowManager.Instance.UIFollowInstances.Add(Transform, this);
            }
        }
    
        /// <summary>
        /// 动态更新配置
        /// </summary>
        public void UpdateConfig(UIFollowConfig newConfig)
        {
            if (newConfig != null)
            {
                UIFollowConfig = newConfig;
            }
        }
    
        /// <summary>
        /// 设置UI显示状态
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            UpdateUIVisibility();
        }
    
        /// <summary>
        /// 销毁此跟随实例
        /// </summary>
        public void Dispose()
        {
            IsActive = false;
            Cleanup();
        
            if (Transform && UIFollowManager.Instance.UIFollowInstances.ContainsKey(Transform))
            {
                UIFollowManager.Instance.UIFollowInstances.Remove(Transform);
            }
        
            Destroy(gameObject);
        }
    
        #endregion
    
        #region 核心逻辑
    
        /// <summary>
        /// 每帧更新位置
        /// </summary>
        public void UpdatePosition()
        {
            if (!_isInitialized || !IsActive || !Transform)
                return;
        
            // 检查相机有效性
            if (!_mainCamera)
            {
                _mainCamera = _camera;
                if (!_mainCamera) return;
            }
        
            // 计算目标世界位置
            Vector3 targetWorldPos = Transform.position + UIFollowConfig.worldOffset;
            _currentDistance = Vector3.Distance(_mainCamera.transform.position, targetWorldPos);
        
            // 距离检查
            if (_currentDistance > UIFollowConfig.maxDistance)
            {
                SetUIActive(false);
                return;
            }
        
            // 根据模式更新UI
            switch (UIFollowConfig.followMode)
            {
                case FollowMode.WorldSpace:
                    UpdateWorldSpaceUI(targetWorldPos);
                    break;
                case FollowMode.ScreenProjection:
                    UpdateScreenSpaceUI(targetWorldPos);
                    break;
                case FollowMode.Adaptive:
                    UpdateAdaptiveUI(targetWorldPos);
                    break;
            }
        }
    
        /// <summary>
        /// 更新世界空间UI
        /// </summary>
        private void UpdateWorldSpaceUI(Vector3 worldPosition)
        {
            if (!_worldCanvas) return;
        
            // 遮挡检测
            if (UIFollowConfig.hideWhenOccluded && CheckOcclusion(worldPosition))
            {
                SetUIActive(false);
                return;
            }
        
            SetUIActive(true);
        
            // 位置更新
            Vector3 newPosition = UIFollowConfig.smoothFollow
                ? Vector3.SmoothDamp(_worldCanvas.transform.position, worldPosition, ref _velocity, UIFollowConfig.smoothSpeed * Time.deltaTime)
                : worldPosition;
        
            _worldCanvas.transform.position = newPosition;
        
            // 旋转更新（始终面向相机）
            if (UIFollowConfig.faceCamera)
            {
                _worldCanvas.transform.LookAt(_worldCanvas.transform.position + _mainCamera.transform.rotation * Vector3.forward,
                    _mainCamera.transform.rotation * Vector3.up);
            }
        }
    
        /// <summary>
        /// 更新屏幕空间UI
        /// </summary>
        private void UpdateScreenSpaceUI(Vector3 worldPosition)
        {
            if (!_screenCanvas || !_screenRect) return;
        
            // 世界坐标转屏幕坐标
            Vector3 screenPoint = _mainCamera.WorldToScreenPoint(worldPosition);
        
            // 检查是否在屏幕内
            bool isOnScreen = screenPoint.z > 0 && 
                              screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
                              screenPoint.y >= 0 && screenPoint.y <= Screen.height;
        
            if (!isOnScreen)
            {
                SetUIActive(false);
                return;
            }
        
            SetUIActive(true);
        
            // 应用屏幕偏移
            screenPoint.x += UIFollowConfig.screenOffset.x;
            screenPoint.y += UIFollowConfig.screenOffset.y;
        
            // 平滑过渡
            Vector3 currentPos = _screenRect.position;
            Vector3 targetPos = screenPoint;
        
            _screenRect.position = UIFollowConfig.smoothFollow
                ? Vector3.SmoothDamp(currentPos, targetPos, ref _velocity, UIFollowConfig.smoothSpeed * Time.deltaTime)
                : targetPos;
        
            // 根据距离调整大小（透视效果）
            float scaleFactor = Mathf.Clamp(10f / screenPoint.z, 0.5f, 2f);
            _screenRect.localScale = Vector3.one * scaleFactor;
        }
    
        /// <summary>
        /// 自适应UI更新（根据距离切换模式）
        /// </summary>
        private void UpdateAdaptiveUI(Vector3 worldPosition)
        {
            // 近距离使用世界空间，远距离使用屏幕投影
            if (_currentDistance < 15f)
            {
                if (!_worldCanvas || !_worldCanvas.gameObject.activeSelf)
                {
                    SetUIActive(false, _worldCanvas);
                    SetUIActive(true, _screenCanvas);
                }
                UpdateWorldSpaceUI(worldPosition);
            }
            else
            {
                if (!_screenCanvas || !_screenCanvas.gameObject.activeSelf)
                {
                    SetUIActive(false, _screenCanvas);
                    SetUIActive(true, _worldCanvas);
                }
                UpdateScreenSpaceUI(worldPosition);
            }
        }
    
        #endregion
    
        #region 辅助方法
    
        /// <summary>
        /// 创建UI实例
        /// </summary>
        private void CreateUIInstance()
        {
            // 获取UI预设
            GameObject uiPrefab = null;
            if (UIFollowManager.Instance.UIPrefabDict.TryGetValue(UIFollowConfig.uiPrefabName, out uiPrefab))
            {
                // 创建世界空间UI
                _worldCanvas = CreateCanvas(RenderMode.WorldSpace, uiPrefab);
                if (_worldCanvas)
                {
                    _worldCanvas.transform.SetParent(transform);
                    _worldCanvas.transform.localPosition = Vector3.zero;
                }
            
                // 创建屏幕空间UI
                _screenCanvas = CreateCanvas(RenderMode.ScreenSpaceOverlay, uiPrefab);
                if (_screenCanvas)
                {
                    _screenCanvas.transform.SetParent(transform);
                    _screenRect = _screenCanvas.GetComponent<RectTransform>();
                }
            }
            else
            {
                Debug.LogError($"UIFollowInstance: UI prefab '{UIFollowConfig.uiPrefabName}' not found!");
            }
        }
    
        /// <summary>
        /// 创建Canvas实例
        /// </summary>
        // 在 UIFollowInstance 类中更新这个方法
        private Canvas CreateCanvas(RenderMode renderMode, GameObject uiContent)
        {
            // 从管理器获取Canvas
            Canvas canvas = UIFollowManager.Instance.GetCanvasFromPool(renderMode);
    
            if (!canvas)
            {
                Debug.LogError($"Failed to get canvas for render mode: {renderMode}");
                return null;
            }
    
            // 设置Canvas父级
            canvas.transform.SetParent(transform);
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one;
    
            // 清理旧内容
            foreach (Transform child in canvas.transform)
            {
                Destroy(child.gameObject);
            }
    
            // 实例化新的UI内容
            if (uiContent)
            {
                GameObject uiInstance = Instantiate(uiContent, canvas.transform);
                uiInstance.transform.localPosition = Vector3.zero;
                uiInstance.transform.localRotation = Quaternion.identity;
                uiInstance.transform.localScale = Vector3.one;
                uiInstance.name = $"{uiContent.name}_Instance";
            }
    
            return canvas;
        }
    
        /// <summary>
        /// 遮挡检测
        /// </summary>
        private bool CheckOcclusion(Vector3 worldPosition)
        {
            if (UIFollowConfig.occlusionLayers == 0) return false;
        
            RaycastHit hit;
            Vector3 direction = worldPosition - _mainCamera.transform.position;
            float distance = direction.magnitude;
        
            if (Physics.Raycast(_mainCamera.transform.position, direction.normalized, 
                    out hit, distance, UIFollowConfig.occlusionLayers))
            {
                return hit.transform != Transform && hit.transform != transform;
            }
        
            return false;
        }
    
        /// <summary>
        /// 设置UI激活状态
        /// </summary>
        private void SetUIActive(bool active, Canvas specificCanvas = null)
        {
            if (!_isVisible) return;
        
            if (specificCanvas)
            {
                specificCanvas.gameObject.SetActive(active);
            }
            else
            {
                if (_worldCanvas ) _worldCanvas.gameObject.SetActive(active);
                if (_screenCanvas ) _screenCanvas.gameObject.SetActive(active);
            }
        }
    
        /// <summary>
        /// 更新UI可见性
        /// </summary>
        private void UpdateUIVisibility()
        {
            if (_worldCanvas) _worldCanvas.gameObject.SetActive(_isVisible);
            if (_screenCanvas ) _screenCanvas.gameObject.SetActive(_isVisible);
        }
    
        /// <summary>
        /// 清理资源
        /// </summary>
        private void Cleanup()
        {
            // 返回Canvas到对象池
            if (_worldCanvas)
            {
                UIFollowManager.Instance.ReturnCanvasToPool(_worldCanvas);
                _worldCanvas = null;
            }
    
            if (_screenCanvas)
            {
                UIFollowManager.Instance.ReturnCanvasToPool(_screenCanvas);
                _screenCanvas = null;
            }
        }
    
        #endregion
    
        void OnDestroy()
        {
            Cleanup();
        }
    }
}