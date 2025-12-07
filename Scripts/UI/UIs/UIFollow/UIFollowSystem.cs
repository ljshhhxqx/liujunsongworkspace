using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.UIFollow
{
    public static class UIFollowSystem
    {
        #region 快速调用方法
    
        /// <summary>
        /// 为指定对象创建跟随UI（最简单调用）
        /// </summary>
        public static UIFollowInstance CreateFollowUI(GameObject target, FollowUIType uiPrefabName)
        {
            if (!target)
            {
                Debug.LogError("CreateFollowUI: Target is null!");
                return null;
            }
        
            return UIFollowManager.Instance.CreateFollowUI(target.transform, uiPrefabName);
        }
    
        /// <summary>
        /// 为指定对象创建跟随UI（自定义配置）
        /// </summary>
        public static UIFollowInstance CreateFollowUI(GameObject target, UIFollowConfig config)
        {
            if (!target)
            {
                Debug.LogError("CreateFollowUI: Target is null!");
                return null;
            }
        
            return UIFollowManager.Instance.CreateFollowUI(target.transform, config);
        }
    
        /// <summary>
        /// 移除指定对象的跟随UI
        /// </summary>
        public static void RemoveFollowUI(GameObject target)
        {
            if (!target) return;
            UIFollowManager.Instance.RemoveFollowUI(target.transform);
        }
    
        /// <summary>
        /// 获取指定对象的跟随UI实例
        /// </summary>
        public static UIFollowInstance GetFollowUI(GameObject target)
        {
            if (!target) return null;
            return UIFollowManager.Instance.GetFollowUI(target.transform);
        }
    
        /// <summary>
        /// 检查对象是否有跟随UI
        /// </summary>
        public static bool HasFollowUI(GameObject target)
        {
            if (!target) return false;
            return UIFollowManager.Instance.HasFollowUI(target.transform);
        }
    
        /// <summary>
        /// 设置跟随UI可见性
        /// </summary>
        public static void SetFollowUIVisible(GameObject target, bool visible)
        {
            var instance = GetFollowUI(target);
            if (instance)
            {
                instance.SetVisible(visible);
            }
        }
    
        /// <summary>
        /// 批量为列表中的对象创建跟随UI
        /// </summary>
        public static List<UIFollowInstance> CreateFollowUIBatch(List<GameObject> targets, FollowUIType uiPrefabName)
        {
            List<UIFollowInstance> instances = new List<UIFollowInstance>();
        
            foreach (var target in targets)
            {
                if (target)
                {
                    var instance = CreateFollowUI(target, uiPrefabName);
                    if (instance)
                    {
                        instances.Add(instance);
                    }
                }
            }
        
            return instances;
        }
    
        #endregion
    
        #region 预定义配置快捷方法
    
        public static UIFollowInstance CreateWorldSpaceUI(GameObject target, FollowUIType uiPrefabName, 
            float offsetY = 1.5f, bool faceCamera = true)
        {
            var config = new UIFollowConfig
            {
                uiPrefabName = uiPrefabName,
                followMode = FollowMode.WorldSpace,
                worldOffset = Vector3.up * offsetY,
                faceCamera = faceCamera
            };
        
            return CreateFollowUI(target, config);
        }
    
        public static UIFollowInstance CreateScreenSpaceUI(GameObject target, FollowUIType uiPrefabName,
            Vector2 screenOffset = default)
        {
            var config = new UIFollowConfig
            {
                uiPrefabName = uiPrefabName,
                followMode = FollowMode.ScreenProjection,
                screenOffset = screenOffset
            };
        
            return CreateFollowUI(target, config);
        }
    
        public static UIFollowInstance CreateAdaptiveUI(GameObject target, FollowUIType uiPrefabName,
            float closeDistance = 15f, float farDistance = 50f)
        {
            var config = new UIFollowConfig
            {
                uiPrefabName = uiPrefabName,
                followMode = FollowMode.Adaptive,
                maxDistance = farDistance
            };
        
            return CreateFollowUI(target, config);
        }
    
        #endregion
    
        #region 批量操作
    
        /// <summary>
        /// 批量移除跟随UI
        /// </summary>
        public static void RemoveFollowUIBatch(List<GameObject> targets)
        {
            foreach (var target in targets)
            {
                if (target)
                {
                    RemoveFollowUI(target);
                }
            }
        }
    
        /// <summary>
        /// 根据标签移除所有跟随UI
        /// </summary>
        public static void RemoveFollowUIByTag(string tag)
        {
            UIFollowManager.Instance.RemoveFollowUIByTag(tag);
        }
    
        /// <summary>
        /// 移除所有跟随UI
        /// </summary>
        public static void RemoveAllFollowUI()
        {
            UIFollowManager.Instance.RemoveAllFollowUI();
        }
    
        /// <summary>
        /// 设置所有跟随UI可见性
        /// </summary>
        public static void SetAllFollowUIVisible(bool visible)
        {
            UIFollowManager.Instance.SetAllFollowUIVisible(visible);
        }
    
        #endregion
    
        #region 调试信息
    
        /// <summary>
        /// 获取系统状态
        /// </summary>
        public static string GetSystemStatus()
        {
            if (!UIFollowManager.Instance)
                return "UIFollowManager not initialized";
        
            return $"Instances: {UIFollowManager.Instance.GetActiveInstanceCount()}, {UIFollowManager.Instance.GetPoolStatus()}";
        }
    
        /// <summary>
        /// 获取所有实例的详细信息
        /// </summary>
        public static string GetAllInstanceDetails()
        {
            if (!UIFollowManager.Instance)
                return "UIFollowManager not initialized";
        
            return UIFollowManager.Instance.GetAllInstanceInfo();
        }
    
        #endregion
    }
}