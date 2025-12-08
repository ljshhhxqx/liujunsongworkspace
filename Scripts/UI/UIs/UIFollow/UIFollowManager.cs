using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.UIFollow
{
    public class UIFollowManager : SingletonAutoMono<UIFollowManager>
    {
        [Header("Canvas 预设")]
        private GameObject _worldCanvasPrefab;    // 世界空间Canvas预设
        private GameObject _screenCanvasPrefab;   // 屏幕空间Canvas预设
    
        private List<GameObject> _uiPrefabs;      // 可用的UI预设列表
        private List<GameObject> _controllerPrefabs; // 可用的控制器预设列表
    
        [Header("默认设置")]
        public float defaultOffsetY = 1.5f;
        public float defaultFollowSpeed = 5f;
        public float defaultMaxDistance = 50f;
        public bool defaultFaceCamera = true;

        private Dictionary<Transform, UIFollowInstance> _uiFollowInstances = new Dictionary<Transform, UIFollowInstance>();
        private Stack<Canvas> _worldCanvasPool = new Stack<Canvas>();
        private Stack<Canvas> _screenCanvasPool = new Stack<Canvas>();
        private Transform _canvasContainer;
        
        public Dictionary<Transform, UIFollowInstance> UIFollowInstances => _uiFollowInstances;

        public void InitPrefabs(string mapName)
        {
            var mapUIs = ResourceManager.Instance.GetResources<GameObject>(x => x.resourceData.Address.StartsWith($"/Map/{mapName}/WorldUIPrefab") && x.resourceInfo.Resource is GameObject);
            foreach (var go in mapUIs)
            {
                if (go.TryGetComponent<IUIController>(out _))
                {
                    _controllerPrefabs.Add(go);
                }
            }
            Initialize();
        }

        void Initialize()
        {
            // 创建Canvas容器
            _canvasContainer = new GameObject("UI_Follow_Container").transform;
            _canvasContainer.SetParent(transform);
            _canvasContainer.localPosition = Vector3.zero;
        
            // 如果没有预设，创建默认Canvas预制体
            if (!_worldCanvasPrefab)
            {
                _worldCanvasPrefab = CreateDefaultCanvasPrefab(RenderMode.WorldSpace);
            }
        
            if (!_screenCanvasPrefab)
            {
                _screenCanvasPrefab = CreateDefaultCanvasPrefab(RenderMode.ScreenSpaceOverlay);
            }
        }

        void Update()
        {
            // 批量更新所有活跃实例
            foreach (var instance in _uiFollowInstances.Values)
            {
                if (instance && instance.IsActive)
                {
                    instance.UpdatePosition();
                }
            }
        }

        #region 公共方法 - 创建和管理UI实例

        /// <summary>
        /// 创建跟随UI实例（主方法）
        /// </summary>
        public UIFollowInstance CreateFollowUI(Transform target, UIFollowConfig config = null)
        {
            if (!target)
            {
                Debug.LogError("CreateFollowUI: Target is null!");
                return null;
            }
        
            // 检查是否已存在实例
            if (_uiFollowInstances.ContainsKey(target))
            {
                Debug.LogWarning($"Target '{target.name}' already has a follow UI. Returning existing instance.");
                return _uiFollowInstances[target];
            }
        
            // 创建新的跟随实例
            var instanceObj = new GameObject($"UI_Follow_{target.name}");
            instanceObj.transform.SetParent(_canvasContainer);
        
            var instance = instanceObj.AddComponent<UIFollowInstance>();
            instance.Initialize(target, config ?? CreateDefaultConfig());
        
            _uiFollowInstances[target] = instance;
            return instance;
        }

        /// <summary>
        /// 快捷方法：使用预制体名称创建UI
        /// </summary>
        public UIFollowInstance CreateFollowUI(Transform target, FollowUIType uiPrefabName)
        {
            var config = new UIFollowConfig
            {
                uiPrefabName = uiPrefabName,
                worldOffset = Vector3.up * defaultOffsetY,
                smoothSpeed = defaultFollowSpeed,
                maxDistance = defaultMaxDistance,
                faceCamera = defaultFaceCamera
            };
        
            return CreateFollowUI(target, config);
        }

        /// <summary>
        /// 移除指定目标的跟随UI
        /// </summary>
        public void RemoveFollowUI(Transform target)
        {
            if (!target || !_uiFollowInstances.ContainsKey(target))
            {
                return;
            }
        
            var instance = _uiFollowInstances[target];
            
            instance?.Dispose();
            _uiFollowInstances.Remove(target);
        }

        /// <summary>
        /// 获取指定目标的跟随UI实例
        /// </summary>
        public UIFollowInstance GetFollowUI(Transform target)
        {
            if (!target || !_uiFollowInstances.TryGetValue(target, out var ui))
            {
                return null;
            }
        
            return ui;
        }

        /// <summary>
        /// 检查目标是否有跟随UI
        /// </summary>
        public bool HasFollowUI(Transform target)
        {
            return target && _uiFollowInstances.ContainsKey(target);
        }

        /// <summary>
        /// 移除所有跟随UI
        /// </summary>
        public void RemoveAllFollowUI()
        {
            var targets = new List<Transform>(_uiFollowInstances.Keys);
            foreach (var target in targets)
            {
                RemoveFollowUI(target);
            }
        
            _uiFollowInstances.Clear();
        }

        /// <summary>
        /// 根据标签批量移除跟随UI
        /// </summary>
        public void RemoveFollowUIByTag(string t)
        {
            var targetsToRemove = new List<Transform>();
        
            foreach (var kvp in _uiFollowInstances)
            {
                if (kvp.Key && kvp.Key.CompareTag(t))
                {
                    targetsToRemove.Add(kvp.Key);
                }
            }
        
            foreach (var target in targetsToRemove)
            {
                RemoveFollowUI(target);
            }
        }

        /// <summary>
        /// 设置所有跟随UI的可见性
        /// </summary>
        public void SetAllFollowUIVisible(bool visible)
        {
            foreach (var instance in _uiFollowInstances.Values)
            {
                instance?.SetVisible(visible);
            }
        }

        #endregion

        #region 内部方法 - 对象池和资源管理

        /// <summary>
        /// 从池中获取或创建Canvas
        /// </summary>
        public Canvas GetCanvasFromPool(RenderMode renderMode)
        {
            var pool = renderMode == RenderMode.WorldSpace ? _worldCanvasPool : _screenCanvasPool;
            Canvas canvas;
        
            if (pool.Count > 0)
            {
                canvas = pool.Pop();
                canvas.gameObject.SetActive(true);
                return canvas;
            }
        
            // 池为空，创建新的Canvas
            var prefab = renderMode == RenderMode.WorldSpace ? _worldCanvasPrefab : _screenCanvasPrefab;
            if (prefab)
            {
                var canvasObj = Instantiate(prefab, _canvasContainer);
                canvas = canvasObj.GetComponent<Canvas>();
                if (!canvas)
                {
                    canvas = canvasObj.AddComponent<Canvas>();
                }
            
                canvas.renderMode = renderMode;
                return canvas;
            }
        
            Debug.LogError($"Cannot create canvas for render mode: {renderMode}");
            return null;
        }

        /// <summary>
        /// 将Canvas返回到对象池
        /// </summary>
        public void ReturnCanvasToPool(Canvas canvas)
        {
            if (!canvas) return;
        
            canvas.gameObject.SetActive(false);
        
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                _worldCanvasPool.Push(canvas);
            }
            else
            {
                _screenCanvasPool.Push(canvas);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private UIFollowConfig CreateDefaultConfig()
        {
            return new UIFollowConfig
            {
                uiPrefabName = FollowUIType.None,
                worldOffset = Vector3.up * defaultOffsetY,
                followMode = FollowMode.WorldSpace,
                smoothFollow = true,
                smoothSpeed = defaultFollowSpeed,
                maxDistance = defaultMaxDistance,
                faceCamera = defaultFaceCamera
            };
        }

        /// <summary>
        /// 创建默认Canvas预制体
        /// </summary>
        private GameObject CreateDefaultCanvasPrefab(RenderMode renderMode)
        {
            var prefab = new GameObject($"Default{renderMode}Canvas");
        
            var canvas = prefab.AddComponent<Canvas>();
            canvas.renderMode = renderMode;
        
            var scaler = prefab.AddComponent<CanvasScaler>();
            if (renderMode == RenderMode.WorldSpace)
            {
                scaler.dynamicPixelsPerUnit = 100;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
        
            prefab.AddComponent<GraphicRaycaster>();
            prefab.SetActive(false);
        
            return prefab;
        }

        /// <summary>
        /// 清理对象池
        /// </summary>
        public void CleanupPools()
        {
            while (_worldCanvasPool.Count > 0)
            {
                Canvas canvas = _worldCanvasPool.Pop();
                if (canvas && canvas.gameObject)
                {
                    Destroy(canvas.gameObject);
                }
            }
        
            while (_screenCanvasPool.Count > 0)
            {
                Canvas canvas = _screenCanvasPool.Pop();
                if (canvas && canvas.gameObject)
                {
                    Destroy(canvas.gameObject);
                }
            }
        
            _worldCanvasPool.Clear();
            _screenCanvasPool.Clear();
            _uiFollowInstances.Clear();
        }

        #endregion

        #region 调试和状态信息

        /// <summary>
        /// 获取活跃实例数量
        /// </summary>
        public int GetActiveInstanceCount()
        {
            return _uiFollowInstances.Count;
        }

        /// <summary>
        /// 获取对象池状态
        /// </summary>
        public string GetPoolStatus()
        {
            return $"World Canvas Pool: {_worldCanvasPool.Count}, Screen Canvas Pool: {_screenCanvasPool.Count}";
        }

        /// <summary>
        /// 获取所有活跃实例的信息
        /// </summary>
        public string GetAllInstanceInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total Active Instances: {_uiFollowInstances.Count}");
        
            foreach (var kvp in _uiFollowInstances)
            {
                if (kvp.Key && kvp.Value)
                {
                    sb.AppendLine($"- {kvp.Key.name}: {kvp.Value.UIFollowConfig.uiPrefabName}");
                }
            }
        
            return sb.ToString();
        }

        #endregion

        void OnDestroy()
        {
            CleanupPools();
            
        }

        public T GetController<T>() where T : IUIController
        {
            foreach (var controller in _controllerPrefabs)
            {
                if (controller.TryGetComponent<T>(out var c))
                {
                    return c;
                }
            }
            Debug.LogError($"Controller {typeof(T).Name} not found.");
            return default;
        }
    }

    public enum UIFollowType
    {
        None,
        Default,
        Adapt,
    }
}