using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public static class UIPropertyBinder
    {
        private static readonly Dictionary<UIPropertyDefine, Type> KeyTypeMap = new Dictionary<UIPropertyDefine, Type>();
        private static readonly Dictionary<BindingKey, object> KeyPropertyMap = 
            new Dictionary<BindingKey, object>();
        private static readonly Dictionary<UIPropertyDefine, List<BindingKey>> KeyIndex = 
            new Dictionary<UIPropertyDefine, List<BindingKey>>();
        #region 数据层接口
        // 设置全局数据
        public static void SetGlobalValue<T>(UIPropertyDefine key, T value)
        {
            var bindingKey = new BindingKey(key, DataScope.Global);
            UpdateValue(bindingKey, value);
        }

        public static void SetGlobalCollectionValue<T>(UIPropertyDefine key, T value)
        {
            
        }

        // 设置指定玩家数据
        public static void SetPlayerValue<T>(UIPropertyDefine key, int playerId, T value)
        {
            var bindingKey = new BindingKey(key, DataScope.SpecificPlayer, playerId);
            UpdateValue(bindingKey, value);
        }

        // 设置本地玩家数据（快捷方式）
        public static void SetLocalValue<T>(UIPropertyDefine key, T value)
        {
            var bindingKey = new BindingKey(key);
            UpdateValue(bindingKey, value);
        }
        #endregion
        #region UI层接口
        // 绑定全局数据
        public static void BindGlobalProperty<T>(
            UIPropertyDefine key, 
            Action<T> onUpdate, 
            Component context, 
            IEqualityComparer<T> customComparer = null)
            where T : class
        {
            BindInternalProperty(
                new BindingKey(key, DataScope.Global),
                onUpdate,
                context,
                customComparer
            );
        }

        // 绑定本地玩家数据
        public static void BindLocalProperty<T>(UIPropertyDefine key, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer = null)
            where T : class
        {
            BindInternalProperty(new BindingKey(key), onUpdate, context, customComparer);
        }
        

        // 绑定指定玩家数据
        public static void BindPlayerProperty<T>(UIPropertyDefine key, int playerId, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer = null)
            where T : class
        {
            BindInternalProperty(new BindingKey(key, DataScope.SpecificPlayer, playerId), onUpdate, context, customComparer);
        }

        private static void BindInternalProperty<T>(BindingKey bindingKey, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer) where T : class
        {
            
        }

        #endregion

        #region 核心实现
        private static void UpdateValue<T>(BindingKey key, T value)
        {
            try
            {
                if (!KeyIndex.TryGetValue(key.PropertyKey, out var list))
                {
                    list = new List<BindingKey>();
                    KeyIndex[key.PropertyKey] = list;
                }
                list.Add(key);
                ValidateType(key, typeof(T));

                if (!KeyPropertyMap.TryGetValue(key, out var property))
                {
                    property = new ReactiveProperty<T>(value);
                    KeyPropertyMap[key] = property;
                    return;
                }

                if (property is ReactiveProperty<T> reactiveProp)
                    reactiveProp.Value = value;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void UpdateCollectionValue<T>(BindingKey key, int index, T newValue)
        {
            try
            {
                if (!KeyIndex.TryGetValue(key.PropertyKey, out var list))
                {
                    list = new List<BindingKey>();
                    KeyIndex[key.PropertyKey] = list;
                }
                list.Add(key);
                ValidateType(key, typeof(T));

                if (!KeyPropertyMap.TryGetValue(key, out var property))
                {
                    property = new ReactiveCollection<T>();
                    KeyPropertyMap[key] = property;
                }

                if (property is ReactiveCollection<T> reactiveCollection)
                {
                    if (index >= reactiveCollection.Count)
                        reactiveCollection.Add(newValue);
                    else
                        reactiveCollection[index] = newValue;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void UpdateDictionaryValue<T1, T2>(BindingKey bindingKey, T1 key, T2 value)
        {
            try
            {
                if (!KeyIndex.TryGetValue(bindingKey.PropertyKey, out var list))
                {
                    list = new List<BindingKey>();
                    KeyIndex[bindingKey.PropertyKey] = list;
                }
                list.Add(bindingKey);
                ValidateType(bindingKey, typeof(T2));

                if (!KeyPropertyMap.TryGetValue(bindingKey, out var property))
                {
                    property = new ReactiveDictionary<T1, T2>();
                    KeyPropertyMap[bindingKey] = property;
                }

                if (property is ReactiveDictionary<T1, T2> reactiveDictionary)
                    reactiveDictionary[key] = value;    
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static IObservable<T> GetObservable<T>(BindingKey key, Component context)
        {
            ValidateType(key, typeof(T));
        
            var property = GetOrCreateProperty<T>(key);
            if (property is ReactiveProperty<T> reactiveProp)
                return reactiveProp.AsObservable()
                   .TakeUntilDestroy(context.gameObject);
            return null;
        }

        private static object GetOrCreateProperty<T>(BindingKey key)
        {
            try
            {
                ValidateType(key, typeof(T));
                if (!KeyPropertyMap.TryGetValue(key, out var property))
                {
                    property = new ReactiveProperty<T>();
                    KeyPropertyMap[key] = property;
                }
                return property;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        #endregion
        #region 高级功能
        // 观察任意玩家数据变化（用于GM工具等）
        public static IObservable<T> ObserveAnyPlayer<T>(UIPropertyDefine key)
        {
            return Observable.Create<T>(observer =>
            {
                var disposables = new CompositeDisposable();
            
                foreach (var kv in KeyPropertyMap)
                {
                    if (kv.Key.PropertyKey == key && kv.Value is ReactiveProperty<T> prop)
                    {
                        prop.AsObservable()
                            .Subscribe(observer.OnNext)
                            .AddTo(disposables);
                    }
                }
            
                return disposables;
            });
        }
        // 添加数据清理接口
        public static void CleanupData(DataScope? scopeFilter = null, 
            Predicate<BindingKey>? customFilter = null)
        {
            var keysToRemove = KeyPropertyMap.Keys
                .Where(k => 
                    (scopeFilter == null || k.Scope == scopeFilter) &&
                    (customFilter == null || customFilter(k)))
                .ToList();

            foreach(var key in keysToRemove)
            {
                KeyPropertyMap.Remove(key);
            }
        }

        // 场景卸载时自动清理
        [RuntimeInitializeOnLoadMethod]
        static void OnSceneUnload()
        {
            SceneManager.sceneUnloaded += scene => 
            {
                CleanupData(customFilter: k => 
                    k.Scope != DataScope.Global);
            };
        }

        // 批量更新玩家数据
        public static void BatchUpdatePlayers<T>(UIPropertyDefine key, IEnumerable<int> playerIds, T value)
        {
            foreach (var playerId in playerIds)
            {
                SetPlayerValue(key, playerId, value);
            }
        }

        // 数据版本控制（解决网络延迟导致的数据顺序问题）
        public static IDisposable BindWithVersion<T>(
            BindingKey key, 
            Action<T, int> onUpdate, 
            Component context)
        {
            var version = 0;
            var property = GetOrCreateProperty<T>(key);
            if (property is ReactiveProperty<T> reactiveProp)
            {
                return reactiveProp.ObserveEveryValueChanged(x => x.Value)
                    .Select(x => (Value: x, Version: ++version))
                    .TakeUntilDestroy(context.gameObject)
                    .Subscribe(t => onUpdate(t.Value, t.Version))
                    .AddTo(context);;
            }
            return null;
        }
        #endregion
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            // 自动扫描枚举类型定义
            var enumType = typeof(UIPropertyDefine);
            foreach (UIPropertyDefine key in Enum.GetValues(enumType))
            {
                var fieldInfo = enumType.GetField(Enum.GetName(enumType, key));
                var attribute = fieldInfo.GetCustomAttributes(typeof(UIPropertyTypeAttribute), false)[0] 
                    as UIPropertyTypeAttribute;
            
                KeyTypeMap[key] = attribute?.ValueType ?? 
                                  throw new InvalidOperationException($"Missing UIPropertyTypeAttribute for {key}");
            }
        }

        private static void ValidateType(BindingKey key, Type requestedType)
        {
            if (!KeyTypeMap.TryGetValue(key.PropertyKey, out var definedType))
                throw new KeyNotRegisteredException(key.PropertyKey);

            if (definedType != requestedType)
                throw new TypeMismatchException(key.PropertyKey, definedType, requestedType);
        }
        // 添加本地玩家管理模块
        private static int _currentLocalPlayerId = 0;

        public static int CurrentLocalPlayerId
        {
            get => _currentLocalPlayerId;
            set
            {
                if(_currentLocalPlayerId != value)
                {
                    // 触发本地玩家数据迁移逻辑
                    MigrateLocalPlayerData(_currentLocalPlayerId, value);
                    _currentLocalPlayerId = value;
                }
            }
        }

        private static void MigrateLocalPlayerData(int oldId, int newId)
        {
            var oldKeys = KeyPropertyMap.Keys
                .Where(k => k.Scope == DataScope.LocalPlayer)
                .ToList();

            foreach(var key in oldKeys)
            {
                var newKey = new BindingKey(key.PropertyKey, DataScope.LocalPlayer, newId);
                KeyPropertyMap[newKey] = KeyPropertyMap[key];
                KeyPropertyMap.Remove(key);
            }
        }
        
        // 异常定义
        private class TypeMismatchException : InvalidOperationException
        {
            public TypeMismatchException(UIPropertyDefine key, Type defined, Type requested)
                : base($"[{key}] Type mismatch! Defined: {defined.Name}, Requested: {requested.Name}") {}
        }

        private class KeyNotRegisteredException : InvalidOperationException
        {
            public KeyNotRegisteredException(UIPropertyDefine key)
                : base($"[{key}] is not registered with UIPropertyTypeAttribute") {}
        }
    }
}