using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using Sirenix.Utilities;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Network.UI
{
    public static class UIPropertyBinder
    {
        private static readonly Dictionary<BindingKey, IReactivePropertyWrapper> KeyPropertyMap =
            new Dictionary<BindingKey, IReactivePropertyWrapper>();

        //必须使用int作为响应式字典的key
        private static readonly Dictionary<BindingKey, IReactivePropertyWrapper> KeyDictionaryMap =
            new Dictionary<BindingKey, IReactivePropertyWrapper>();

        private static readonly Dictionary<BindingKey, IReactivePropertyWrapper> KeyListMap =
            new Dictionary<BindingKey, IReactivePropertyWrapper>();

        #region Property Operations

        public static bool HasProperty(BindingKey key)
        {
            return KeyPropertyMap.ContainsKey(key);
        }

        public static HReactiveProperty<T> ObserveProperty<T>(BindingKey key) where T : IUIDatabase
        {
            var property = GetOrCreateProperty<T>(key);
            return property;
        }

        public static void SetProperty<T>(BindingKey key, T value) where T : IUIDatabase
        {
            GetOrCreateProperty<T>(key).Value = value;
        }

        private static HReactiveProperty<T> GetOrCreateProperty<T>(BindingKey key)  where T : IUIDatabase
        {
            if (!KeyPropertyMap.TryGetValue(key, out var property))
            {
                var newRp = new HReactiveProperty<T>(default(T));
                property = new ReactivePropertyWrapper<T>(newRp);
                KeyPropertyMap[key] = property;
                return newRp;
            }

            if (property is ReactivePropertyWrapper<T> reactivePropertyWrapper)
            {
                return reactivePropertyWrapper.Property;
            }
            Debug.LogError($"GetOrCreateProperty {property.GetType().Name} is not a reactive property wrapper");

            return null;
        }

        #endregion

        #region Dictionary Operations

        public static bool HasDictionary(BindingKey key)
        {
            return KeyDictionaryMap.ContainsKey(key);
        }
        public static void OptimizedBatchAdd<T>(BindingKey key, Dictionary<int, T> items) where T : IUIDatabase
        {
            var tempDict = new Dictionary<int, T>();
            foreach (var item in items)
            {
                tempDict.Add(item.Key, item.Value);
            }
    
            var dict = GetOrCreateDictionary<T>(key).Dictionary;
            dict.Clear();
            foreach (var item in tempDict)
            {
                dict.Add(item.Key, item.Value);
            }
        }

        public static void AddToDictionary<T>(BindingKey dictKey, int itemKey, T value) where T : IUIDatabase
        {
            GetOrCreateDictionary<T>(dictKey).Dictionary.Add(itemKey, value);
        }

        public static void RemoveFromDictionary<T>(BindingKey dictKey, int itemKey) where T : IUIDatabase
        {
            GetOrCreateDictionary<T>(dictKey).Dictionary.Remove(itemKey);
        }

        public static void BatchAddToDictionary<T>(BindingKey dictKey, IEnumerable<KeyValuePair<int, T>> items) where T : IUIDatabase
        {
            var dict = GetOrCreateDictionary<T>(dictKey);
            foreach (var item in items)
            {
                dict.Dictionary.Add(item.Key, item.Value);
            }
        }

        public static HReactiveDictionary<int, T> GetReactiveDictionary<T>(BindingKey key) where T : IUIDatabase
        {
            return GetOrCreateDictionary<T>(key).Dictionary;
        }
        
        public static void UpdateDictionary<T>(BindingKey dictKey, int itemKey, T value) where T : IUIDatabase
        {
            var dic = GetOrCreateDictionary<T>(dictKey).Dictionary;
            if (dic.ContainsKey(itemKey))
            {
                dic[itemKey] = value;
            }
            else
            {
                dic.Add(itemKey, value);
            }
        }

        private static ReactiveDictionaryWrapper<T> GetOrCreateDictionary<T>(BindingKey key) where T : IUIDatabase
        {
            if (!KeyDictionaryMap.TryGetValue(key, out var dict))
            {
                var newRp = new HReactiveDictionary<int, T>();
                dict = new ReactiveDictionaryWrapper<T>(newRp);
                KeyDictionaryMap[key] = dict;
            }
            if (dict is ReactiveDictionaryWrapper<T> reactiveDictionaryWrapper)
            {
                return reactiveDictionaryWrapper;
            }
            if (dict is { } existingDict)
            {
                var type = existingDict.GetType();
                var storedType = type.GenericTypeArguments[0];
                throw new InvalidCastException(
                    $"Type mismatch for key '{key}'. " +
                    $"Requested: {typeof(T).Name}, " +
                    $"Stored: {storedType.Name}");
            }
            else
            {
                var actualType = dict.GetType().Name;
                throw new InvalidCastException(
                    $"Container type mismatch for key '{key}'. " +
                    $"Requested dictionary, but found: {actualType}");
            }
        }

        #endregion

        #region List Operations

        public static bool HasList(BindingKey key)
        {
            return KeyListMap.ContainsKey(key);
        }

        public static void AddToList<T>(BindingKey listKey, T item) where T : IUIDatabase
        {
            GetOrCreateList<T>(listKey).Add(item);
        }

        public static void RemoveFromList<T>(BindingKey listKey, T item) where T : IUIDatabase
        {
            GetOrCreateList<T>(listKey).Remove((T)item);
        }

        public static void BatchAddToList<T>(BindingKey listKey, IEnumerable<T> items) where T : IUIDatabase
        {
            var list = GetOrCreateList<T>(listKey);
            list.AddRange(items);
        }

        public static HReactiveCollection<T> GetReactiveCollection<T>(BindingKey listKey) where T : IUIDatabase
        {
            return GetOrCreateList<T>(listKey) as HReactiveCollection<T>;
        }

        private static HReactiveCollection<T> GetOrCreateList<T>(BindingKey key) where T : IUIDatabase
        {
            if (!KeyListMap.TryGetValue(key, out var list))
            {
                var newRp = new HReactiveCollection<T>();
                list = new ReactiveCollectionWrapper<T>(newRp);
                KeyListMap[key] = list;
            }

            if (list is ReactiveCollectionWrapper<T> reactiveCollectionWrapper)
            {
                return reactiveCollectionWrapper.Collection;
            }

            Debug.LogError($"GetOrCreateList {list.GetType().Name} is not a reactive collection wrapper");
            return null;
        }

        #endregion

        #region Helper Methods

        public static void ClearAllData()
        {
            KeyPropertyMap.Clear();
            KeyDictionaryMap.Clear();
            KeyListMap.Clear();
        }

        #endregion

        private static int _localPlayerId;
        public static int LocalPlayerId 
        { 
            get=> _localPlayerId;
            set
            {
                if (_localPlayerId != value && value != -1)
                {
                    foreach (var key in KeyDictionaryMap.Keys)
                    {
                        if (key.Scope == DataScope.LocalPlayer)
                        {
                            KeyDictionaryMap.Remove(key);
                        }   
                    }
                    foreach (var key in KeyListMap.Keys)
                    {
                        if (key.Scope == DataScope.LocalPlayer)
                        {
                            KeyListMap.Remove(key);
                        }   
                    }

                    foreach (var key in KeyPropertyMap.Keys)
                    {
                        if (key.Scope == DataScope.LocalPlayer)
                        {
                            KeyPropertyMap.Remove(key);
                        } 
                    }
                }
                _localPlayerId = value;
            }
        }
        // 非泛型接口用于统一存储
        private interface IReactivePropertyWrapper { }
        private class ReactivePropertyWrapper<T> : IReactivePropertyWrapper where T : IUIDatabase
        {
            public HReactiveProperty<T> Property { get; }

            public ReactivePropertyWrapper(HReactiveProperty<T> property)
            {
                Property = property;
            }
        }

        private class ReactiveDictionaryWrapper<T> : IReactivePropertyWrapper where T : IUIDatabase
        {
            public HReactiveDictionary<int, T> Dictionary { get; }

            public ReactiveDictionaryWrapper(HReactiveDictionary<int, T> dictionary)
            {
                Dictionary = dictionary;
            }
        }

        private class ReactiveCollectionWrapper<T> : IReactivePropertyWrapper where T : IUIDatabase
        {
            public HReactiveCollection<T> Collection { get; }

            public ReactiveCollectionWrapper(HReactiveCollection<T> collection)
            {
                Collection = collection;
            }
        }
    }
}