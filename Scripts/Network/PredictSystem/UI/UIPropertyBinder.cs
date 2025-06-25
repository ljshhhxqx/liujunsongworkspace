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

        public static IObservable<T> ObserveProperty<T>(BindingKey key) where T : IUIDatabase
        {
            var property = GetOrCreateProperty<T>(key);
            return property;
        }

        public static void SetProperty<T>(BindingKey key, T value) where T : IUIDatabase
        {
            GetOrCreateProperty<T>(key).Value = value;
        }

        private static ReactiveProperty<T> GetOrCreateProperty<T>(BindingKey key)  where T : IUIDatabase
        {
            if (!KeyPropertyMap.TryGetValue(key, out var property))
            {
                var newRp = new ReactiveProperty<T>(default(T));
                property = new ReactivePropertyWrapper<T>(newRp);
                KeyPropertyMap[key] = property;
                return newRp;
            }

            return  ((ReactivePropertyWrapper<T>)property).Property;
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
                var newRp = new ReactiveDictionary<int, IUIDatabase>();
                dict = new ReactiveDictionaryWrapper<IUIDatabase>(newRp);
                KeyDictionaryMap[key] = dict;
            }

            return ((ReactiveDictionaryWrapper<IUIDatabase>)dict).Dictionary;
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
                var newRp = new ReactiveCollection<IUIDatabase>();
                list = new ReactiveCollectionWrapper<IUIDatabase>(newRp);
                KeyListMap[key] = list;
            }

            return ((ReactiveCollectionWrapper<IUIDatabase>)list).Collection;
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
            public ReactiveProperty<T> Property { get; }

            public ReactivePropertyWrapper(ReactiveProperty<T> property)
            {
                Property = property;
            }
        }

        private class ReactiveDictionaryWrapper<T> : IReactivePropertyWrapper where T : IUIDatabase
        {
            public ReactiveDictionary<int, T> Dictionary { get; }

            public ReactiveDictionaryWrapper(ReactiveDictionary<int, T> dictionary)
            {
                Dictionary = dictionary;
            }
        }

        private class ReactiveCollectionWrapper<T> : IReactivePropertyWrapper where T : IUIDatabase
        {
            public ReactiveCollection<T> Collection { get; }

            public ReactiveCollectionWrapper(ReactiveCollection<T> collection)
            {
                Collection = collection;
            }
        }
    }
}