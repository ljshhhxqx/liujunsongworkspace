using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using HotUpdate.Scripts.UI.UIs.Popup;
using Resource;
using UI.UIs.Exception;
using UI.UIs.Popup;
using UI.UIs.SecondPanel;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace UI.UIBase
{
    public class UIManager
    {
        private List<ScreenUIBase> _uIPrefabs = new List<ScreenUIBase>();
        private ScreenUIBase _currentActiveScreenUI1;
        private ScreenUIBase _currentActiveScreenUI2;
        private UICanvasRoot[] _roots;
        private IObjectInjector _injector;
        private readonly Dictionary<UIType, ScreenUIBase> _uiDict = new Dictionary<UIType, ScreenUIBase>();

        [Inject]
        private void Init(IObjectInjector injector)
        {
            _injector = injector;
            _roots = Object.FindObjectsOfType<UICanvasRoot>();
        }

        public void InitPermanentUI()
        {
            GetUIResources(ResourceManager.Instance.GetPermanentUI());
        }

        public void InitUIs()
        {
            GetUIResources(ResourceManager.Instance.GetAllUIObjects());
        }

        private void GetUIResources(IReadOnlyCollection<GameObject> uiObjects)
        {
            if (uiObjects.Count > 0)
            {
                foreach (var t in uiObjects)
                {
                    var ui = t.GetComponent<ScreenUIBase>();
                    if (ui != null && _uIPrefabs.All(t1 => t1.Type != ui.Type))
                    {
                        _uIPrefabs.Add(ui);
                    }
                    else
                    {
                        Debug.Log($"UI对象{t.gameObject.name}没有ScreenUIBase组件");
                    }
                }
                return;
            }
            Debug.Log("资源管理器中没有找到UI对象");
        }

        public void CloseAll()
        {
            foreach (var t in _uiDict.Values)
            {
                Object.Destroy(t.gameObject);
                _uiDict.Remove(t.Type);
            }
        }
        
        public void CloseUI(UIType uIType)
        {
            if (_uiDict.TryGetValue(uIType, out var ui))
            {
                Object.Destroy(ui.gameObject);
                _uiDict.Remove(uIType);
                return;
            }
            Debug.Log($"UI名有误{uIType}");
        }

        public bool IsUIOpen(UIType uIType)
        {
            return _uiDict.ContainsKey(uIType);
        }
        
        private T GetUI<T>() where T : ScreenUIBase
        {
            foreach (var uiBase in _uIPrefabs)
            {
                if (uiBase.GetType() == typeof(T))
                {
                    return uiBase as T;
                }
            }
            Debug.Log($"UI类型有误{typeof(T).Name}");
            return null;
        }

        public T SwitchUI<T>(Action onShow = null) where T : ScreenUIBase, new()
        {
            var ui = GetUI<T>();
            if (ui) 
            {
                var uIType = ui.Type;
                var root = _roots.FirstOrDefault(t => t.CanvasType == ui.CanvasType)?.transform;
                var go = Object.Instantiate(ui.gameObject, root);
                ui = go.GetComponent<T>();
                if (!ui)
                {
                    throw new Exception($"UI对象{uIType}没有{typeof(T).Name}组件");
                }

                if (ui.TryGetComponent<BlockUIComponent>(out var resourceComponent))
                {
                    resourceComponent.SetUIType(uIType);
                    _injector.Inject(resourceComponent);
                }
                _injector.Inject(ui);
                _uiDict.TryAdd(uIType, ui);
                if (ui.CanvasType == UICanvasType.Panel)
                {
                    if (_currentActiveScreenUI1 != ui)
                    {
                        if (_currentActiveScreenUI1)
                        {
                            Object.Destroy(_currentActiveScreenUI1.gameObject);
                        }
                        _currentActiveScreenUI1 = ui;
                    }
                }
                else if (ui.CanvasType == UICanvasType.SecondPanel)
                {
                    if (_currentActiveScreenUI2 != ui)
                    {
                        if (_currentActiveScreenUI2)
                        {
                            Object.Destroy(_currentActiveScreenUI2.gameObject);
                        }
                        _currentActiveScreenUI2 = ui;
                    }
                }
                onShow?.Invoke();
            }

            return ui;
        }

        public void UnloadAll()
        {
            for (int i = 0; i < _uIPrefabs.Count; i++)
            {
                if (_uIPrefabs[i].TryGetComponent<ResourceComponent>(out var resourceComponent))
                {
                    ResourceManager.Instance.UnloadResource(resourceComponent.ResourceData);
                }
            }
        }
    }
    
    public static class UIManagerExtension
    {
        public static void ShowTips(this UIManager uiManager, string message, Action confirmCallback = null, Action cancelCallback = null)
        {
            var tipsUI = uiManager.SwitchUI<TipsPopup>();
            if (tipsUI != null)
            {
                tipsUI.ShowTips(message, confirmCallback, cancelCallback);
            }
            
        }

        public static void ShowHelp(this UIManager uiManager, string message)
        {
            var tipsUI = uiManager.SwitchUI<HelpPopup>();
            if (tipsUI != null)
            {
                tipsUI.ShowHelp(message);
            }
        }
        
        public static void ShowPasswordInput(this UIManager uiManager, string password, Action<bool> confirmCallback)
        {
            var passwordUI = uiManager.SwitchUI<PasswordUI>();
            if (passwordUI != null)
            {
                passwordUI.ShowPasswordUI(password, confirmCallback);
            }
        }

        public static void SwitchLoadingPanel(this UIManager uiManager, bool isShow)
        {
            if (isShow)
            {
                uiManager.SwitchUI<LoadingScreenUI>();
            }
            else
            {
                uiManager.CloseUI(UIType.Loading);
            }

            // var loadingUi = uiManager.GetUI(UIType.Loading);
            // if (loadingUi != null)
            // {
            //     uiManager.CloseUI(UIType.Loading);
            //     return;
            // }
            // uiManager.SwitchUI<LoadingScreenUI>(UIType.Loading);
        }
    }
}
