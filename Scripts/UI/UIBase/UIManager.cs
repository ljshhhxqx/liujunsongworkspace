using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Popup;
using Resource;
using UI.UIBase;
using UI.UIs.Exception;
using UI.UIs.Popup;
using UI.UIs.SecondPanel;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.UI.UIBase
{
    public class UIManager
    {
        private List<ScreenUIBase> _uIPrefabs = new List<ScreenUIBase>();
        private ScreenUIBase _currentActiveScreenUI1;
        private ScreenUIBase _currentActiveScreenUI2;
        private ScreenUIBase _currentActiveScreenUI3;
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
        
        public void InitMapUIs(string mapName)
        {
            var mapUIs = ResourceManager.Instance.GetResources<GameObject>(x => x.resourceData.Address.StartsWith($"/Map/{mapName}") && x.resourceInfo.Resource is GameObject).ToList();
            if (mapUIs.Count > 0)
            {
                foreach (var mapUI in mapUIs)
                {
                    if (mapUI.TryGetComponent(out ScreenUIBase screenUI))
                    {
                        _uIPrefabs.Add(screenUI);
                    }
                }
            }
        }

        public void InitMapSprites(string mapName)
        {
            var mapSprites = ResourceManager.Instance.GetMapSprite(mapName);
            var spriteInfo = new SpriteInfo[mapSprites.Count];
            for (int i = 0; i < mapSprites.Count; i++)
            {
                spriteInfo[i] = new SpriteInfo
                {
                    Sprite = mapSprites[i],
                    Name = mapSprites[i].name
                };  
            }
            UISpriteContainer.InitUISprites(spriteInfo);
        }

        private void GetUIResources(IReadOnlyCollection<GameObject> uiObjects)
        {
            if (uiObjects.Count > 0)
            {
                foreach (var t in uiObjects)
                {
                    var ui = t.GetComponent<ScreenUIBase>();
                    if (ui && _uIPrefabs.All(t1 => t1.Type != ui.Type))
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
            if (_uiDict.Remove(uIType, out var ui))
            {
                Object.Destroy(ui.gameObject);
                return;
            }
            Debug.Log($"UI名有误{uIType}");
        }

        public bool IsUIOpen(UIType uIType)
        {
            return _uiDict.ContainsKey(uIType);
        }
        
        public T GetUI<T>() where T : ScreenUIBase
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

        public T SwitchUI<T>(Action<T> onShow = null) where T : ScreenUIBase, new()
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
                    if (_currentActiveScreenUI1 != ui && _currentActiveScreenUI1)
                    {
                        Object.Destroy(_currentActiveScreenUI2.gameObject);
                        _currentActiveScreenUI1 = ui;
                    }
                }
                else if (ui.CanvasType == UICanvasType.SecondPanel)
                {
                    if (_currentActiveScreenUI2 != ui && _currentActiveScreenUI2)
                    {
                        Object.Destroy(_currentActiveScreenUI2.gameObject);
                        _currentActiveScreenUI2 = ui;
                    }
                }
                else if (ui.CanvasType == UICanvasType.ThirdPanel)
                {
                    if (_currentActiveScreenUI3 != ui && _currentActiveScreenUI3)
                    {
                        Object.Destroy(_currentActiveScreenUI3.gameObject);
                    }
                    _currentActiveScreenUI3 = ui;
                }
                onShow?.Invoke(ui);
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
            if (tipsUI)
            {
                tipsUI.ShowTips(message, confirmCallback, cancelCallback);
            }
            
        }

        public static void ShowHelp(this UIManager uiManager, string message)
        {
            var tipsUI = uiManager.SwitchUI<HelpPopup>();
            if (tipsUI)
            {
                tipsUI.ShowHelp(message);
            }
        }
        
        public static void ShowPasswordInput(this UIManager uiManager, string password, Action<bool> confirmCallback)
        {
            var passwordUI = uiManager.SwitchUI<PasswordUI>();
            if (passwordUI)
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

        public static void ShowTipsOverlay(this UIManager uiManager, string message)
        {
            var tipsUI = uiManager.SwitchUI<TipsOverlay>();
            if (tipsUI)
            {
                tipsUI.ShowTips(message);
            }
        }
    }
}
