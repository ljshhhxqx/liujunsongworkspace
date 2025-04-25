using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Tool.ObjectPool;
using Sirenix.Utilities;
using UniRx;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public static class UIPropertyBinder
    {
        private static readonly Dictionary<BindingKey, ReactiveProperty<IUIDatabase>> KeyPropertyMap =
            new Dictionary<BindingKey, ReactiveProperty<IUIDatabase>>();

        //必须使用int作为响应式字典的key
        private static readonly Dictionary<BindingKey, ReactiveDictionary<int, IUIDatabase>> KeyDictionaryMap =
            new Dictionary<BindingKey, ReactiveDictionary<int, IUIDatabase>>();

        private static readonly Dictionary<BindingKey, ReactiveCollection<IUIDatabase>> KeyListMap =
            new Dictionary<BindingKey, ReactiveCollection<IUIDatabase>>();

        #region Property Operations

        public static bool HasProperty(BindingKey key)
        {
            return KeyPropertyMap.ContainsKey(key);
        }

        public static IObservable<T> ObserveProperty<T>(BindingKey key) where T : IUIDatabase
        {
            return GetOrCreateProperty<T>(key).AsObservable();
        }

        public static void SetProperty<T>(BindingKey key, T value) where T : IUIDatabase
        {
            GetOrCreateProperty<T>(key).Value = value;
        }

        private static ReactiveProperty<T> GetOrCreateProperty<T>(BindingKey key)  where T : IUIDatabase
        {
            if (!KeyPropertyMap.TryGetValue(key, out var property))
            {
                property = ObjectPool<ReactiveProperty<IUIDatabase>>.Get();
                property.Value = null;
                KeyPropertyMap[key] = property;
            }

            return property as ReactiveProperty<T>;
        }

        #endregion

        #region Dictionary Operations

        public static bool HasDictionary(BindingKey key)
        {
            return KeyDictionaryMap.ContainsKey(key);
        }
        public static void OptimizedBatchAdd(BindingKey key, Dictionary<int, IUIDatabase> items)
        {
            var tempDict = new Dictionary<int, IUIDatabase>();
            foreach (var item in items)
            {
                tempDict.Add(item.Key, item.Value);
            }
    
            var dict = GetOrCreateDictionary(key);
            dict.Clear();
            foreach (var item in tempDict)
            {
                dict.Add(item.Key, item.Value);
            }
        }

        public static void AddToDictionary(BindingKey dictKey, int itemKey, IUIDatabase value)
        {
            GetOrCreateDictionary(dictKey).Add(itemKey, value);
        }

        public static void RemoveFromDictionary(BindingKey dictKey, int itemKey)
        {
            GetOrCreateDictionary(dictKey).Remove(itemKey);
        }

        public static void BatchAddToDictionary(BindingKey dictKey, IEnumerable<KeyValuePair<int, IUIDatabase>> items)
        {
            var dict = GetOrCreateDictionary(dictKey);
            foreach (var item in items)
            {
                dict.Add(item.Key, item.Value);
            }
        }

        public static ReactiveDictionary<int, T> GetReactiveDictionary<T>(BindingKey key) where T : IUIDatabase
        {
            return GetOrCreateDictionary(key) as ReactiveDictionary<int, T>;
        }

        private static ReactiveDictionary<int, IUIDatabase> GetOrCreateDictionary(BindingKey key)
        {
            if (!KeyDictionaryMap.TryGetValue(key, out var dict))
            {
                dict = ObjectPool<ReactiveDictionary<int, IUIDatabase>>.Get();
                dict.Clear();
                KeyDictionaryMap[key] = dict;
            }

            return dict;
        }

        #endregion

        #region List Operations

        public static bool HasList(BindingKey key)
        {
            return KeyListMap.ContainsKey(key);
        }

        public static void AddToList(BindingKey listKey, IUIDatabase item)
        {
            GetOrCreateList(listKey).Add(item);
        }

        public static void RemoveFromList(BindingKey listKey, IUIDatabase item)
        {
            GetOrCreateList(listKey).Remove(item);
        }

        public static void BatchAddToList(BindingKey listKey, IEnumerable<IUIDatabase> items)
        {
            var list = GetOrCreateList(listKey);
            list.AddRange(items);
        }

        public static ReactiveCollection<T> GetReactiveCollection<T>(BindingKey listKey) where T : IUIDatabase
        {
            return GetOrCreateList(listKey) as ReactiveCollection<T>;
        }

        private static ReactiveCollection<IUIDatabase> GetOrCreateList(BindingKey key)
        {
            if (!KeyListMap.TryGetValue(key, out var list))
            {
                list = ObjectPool<ReactiveCollection<IUIDatabase>>.Get();
                list.Clear();
                KeyListMap[key] = list;
            }

            return list;
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
        
    }
}