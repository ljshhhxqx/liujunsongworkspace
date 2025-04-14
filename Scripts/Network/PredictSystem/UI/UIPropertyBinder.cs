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
        private static readonly ReaderWriterLockSlim LockSlim = new ReaderWriterLockSlim();
        private static readonly Dictionary<UIPropertyDefine, List<BindingKey>> KeyIndex = 
            new Dictionary<UIPropertyDefine, List<BindingKey>>();
        #region 数据层接口
        // 设置全局数据
        public static void SetGlobalValue<T>(UIPropertyDefine key, T value)
        {
            var bindingKey = new BindingKey(key, DataScope.Global);
            UpdateValue(bindingKey, value);
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
        public static void BindGlobalClass<T>(
            UIPropertyDefine key, 
            Action<T> onUpdate, 
            Component context, 
            IEqualityComparer<T> customComparer = null)
            where T : class
        {
            BindInternalClass(
                new BindingKey(key, DataScope.Global),
                onUpdate,
                context,
                customComparer
            );
        }
        public static void BindGlobalStruct<T>(
            UIPropertyDefine key, 
            Action<T> onUpdate, 
            Component context, 
            IEqualityComparer<T> customComparer = null)
            where T : struct
        {
            BindInternalStruct(
                new BindingKey(key, DataScope.Global),
                onUpdate,
                context,
                customComparer
            );
        }

        // 绑定本地玩家数据
        public static void BindLocalClass<T>(UIPropertyDefine key, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer = null)
            where T : class
        {
            BindInternalClass(new BindingKey(key), onUpdate, context, customComparer);
        }
        
        public static void BindLocalStruct<T>(UIPropertyDefine key, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer = null)
            where T : struct
        {
            BindInternalStruct(new BindingKey(key), onUpdate, context, customComparer);
        }

        // 绑定指定玩家数据
        public static void BindPlayerClass<T>(UIPropertyDefine key, int playerId, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer = null)
            where T : class
        {
            BindInternalClass(new BindingKey(key, DataScope.SpecificPlayer, playerId), onUpdate, context, customComparer);
        }
        
        public static void BindPlayerStruct<T>(UIPropertyDefine key, int playerId, Action<T> onUpdate, Component context, IEqualityComparer<T> customComparer = null)
            where T : struct
        {
            BindInternalStruct(new BindingKey(key, DataScope.SpecificPlayer, playerId), onUpdate, context, customComparer);
        }
        #endregion

        #region 核心实现
        private static void UpdateValue<T>(BindingKey key, T value)
        {
            LockSlim.EnterWriteLock();
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

                var reactiveProp = property as ReactiveProperty<T>;
                reactiveProp?.SetValue(LockSlim, key.PropertyKey, value);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void BindInternalClass<T>(
            BindingKey key,
            Action<T> onUpdate,
            Component context,
            IEqualityComparer<T> customComparer = null)
            where T : class
        {
            GetEnhancedObservableClass(key, context, customComparer)
                .Subscribe(onUpdate)
                .AddTo(context);
        }
        
        private static void BindInternalStruct<T>(
            BindingKey key,
            Action<T> onUpdate, 
            Component context,
            IEqualityComparer<T> customComparer = null)
            where T : struct
        {
            GetEnhancedObservableStruct(key, context, customComparer)
                .Subscribe(onUpdate)
                .AddTo(context);
        }

        private static IObservable<T> GetEnhancedObservableStruct<T>(
            BindingKey key,
            Component context,
            IEqualityComparer<T> customComparer)
            where T : struct
        {
            return Observable.Create<T>(observer =>
            {
                var disposable = new CompositeDisposable();

                // 智能选择比较策略
                var finalComparer = customComparer ?? new ValueTypeComparer<T>();

                // 获取基础数据流
                GetObservable<T>(key, context)
                    .DistinctUntilChanged(finalComparer)
                    .Subscribe(observer.OnNext)
                    .AddTo(disposable);

                return disposable;
            });
        }

        private static IObservable<T> GetEnhancedObservableClass<T>(
            BindingKey key,
            Component context,
            IEqualityComparer<T> customComparer)
            where T : class
        {
            return Observable.Create<T>(observer =>
            {
                var disposable = new CompositeDisposable();

                // 智能选择比较策略
                var finalComparer = customComparer ?? EqualityComparer<T>.Default;

                // 获取基础数据流
                GetObservable<T>(key, context)
                    .DistinctUntilChanged(finalComparer)
                    .Subscribe(observer.OnNext)
                    .AddTo(disposable);

                return disposable;
            });
        }
        public static IObservable<T> GetObservable<T>(BindingKey key, Component context)
        {
            ValidateType(key, typeof(T));
        
            var property = GetOrCreateProperty<T>(key);
            return property.AsObservable()
                .TakeUntilDestroy(context.gameObject);
        }

        private class ValueTypeComparer<T> : IEqualityComparer<T> where T : struct
        {
            public bool Equals(T x, T y) => x.Equals(y);
        
            public int GetHashCode(T obj) => obj.GetHashCode();
        }

        private static ReactiveProperty<T> GetOrCreateProperty<T>(BindingKey key)
        {
            LockSlim.EnterUpgradeableReadLock();
            try
            {
                if (!KeyPropertyMap.TryGetValue(key, out var property))
                {
                    LockSlim.EnterWriteLock();
                    try
                    {
                        property = new ReactiveProperty<T>();
                        KeyPropertyMap[key] = property;
                    }
                    finally
                    {
                        LockSlim.ExitWriteLock();
                    }
                }
                return (ReactiveProperty<T>)property;
            }
            finally
            {
                LockSlim.ExitUpgradeableReadLock();
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
            return GetOrCreateProperty<T>(key)
                .AsObservable()
                .Select(x => (Value: x, Version: ++version))
                .TakeUntilDestroy(context.gameObject)
                .Subscribe(t => onUpdate(t.Value, t.Version))
                .AddTo(context);
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

        private class ReactiveProperty<T>
        {
            private readonly BehaviorSubject<T> _subject;
            private IEqualityComparer<T> _comparer;

            public ReactiveProperty() => _subject = new BehaviorSubject<T>(default(T));
            public ReactiveProperty(T value) => _subject = new BehaviorSubject<T>(value);
            public ReactiveProperty(T initialValue = default, 
                IEqualityComparer<T> comparer = null)
            {
                _comparer = comparer ?? EqualityComparer<T>.Default;
                _subject = new BehaviorSubject<T>(initialValue);
            }

            // 扩展SetValue方法
            public void SetValue(ReaderWriterLockSlim locker,
                UIPropertyDefine key, 
                T value, 
                IEqualityComparer<T> comparer = null,
                DataScope scope = DataScope.LocalPlayer,
                int playerId = 0)
            {
                var bindingKey = new BindingKey(key, scope, playerId);
    
                locker.EnterWriteLock();
                try
                {
                    if(KeyPropertyMap.TryGetValue(bindingKey, out var existing))
                    {
                        var prop = existing as ReactiveProperty<T>;
                        prop?.SetValue(LockSlim, key, value, comparer, scope, playerId);
                    }
                    else
                    {
                        KeyPropertyMap[bindingKey] = new ReactiveProperty<T>(value, comparer);
                    }
                }
                finally
                {
                    locker.ExitWriteLock();
                }
            }
            public IObservable<T> AsObservable() => _subject.AsObservable();
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