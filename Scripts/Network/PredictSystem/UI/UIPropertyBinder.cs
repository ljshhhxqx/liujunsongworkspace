using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public static class UIPropertyBinder
    {
        private static readonly Dictionary<UIPropertyDefine, Type> KeyTypeMap = new Dictionary<UIPropertyDefine, Type>();
        private static readonly Dictionary<UIPropertyDefine, object> KeyPropertyMap = new Dictionary<UIPropertyDefine, object>();

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

        // 数据层接口
        public static void SetValue<T>(UIPropertyDefine key, T value)
        {
            ValidateType(key, typeof(T));
        
            if (!KeyPropertyMap.TryGetValue(key, out var property))
            {
                property = new ReactiveProperty<T>(value);
                KeyPropertyMap[key] = property;
                return;
            }

            ((ReactiveProperty<T>)property).SetValue(value);
        }

        // UI绑定接口
        public static void Bind<T>(UIPropertyDefine key, Action<T> onValueChanged, Component context)
        {
            GetObservable<T>(key, context)
                .Subscribe(onValueChanged)
                .AddTo(context);
        }

        public static IObservable<T> GetObservable<T>(UIPropertyDefine key, Component context)
        {
            ValidateType(key, typeof(T));
        
            var property = GetOrCreateProperty<T>(key);
            return property.AsObservable()
                .TakeUntilDestroy(context.gameObject);
        }

        private static ReactiveProperty<T> GetOrCreateProperty<T>(UIPropertyDefine key)
        {
            if (!KeyPropertyMap.TryGetValue(key, out var property))
            {
                property = new ReactiveProperty<T>();
                KeyPropertyMap[key] = property;
            }
            return (ReactiveProperty<T>)property;
        }

        private static void ValidateType(UIPropertyDefine key, Type requestedType)
        {
            if (!KeyTypeMap.TryGetValue(key, out var definedType))
                throw new KeyNotRegisteredException(key);

            if (definedType != requestedType)
                throw new TypeMismatchException(key, definedType, requestedType);
        }

        // 响应式属性封装
        private class ReactiveProperty<T>
        {
            private readonly BehaviorSubject<T> _subject;

            public ReactiveProperty() => _subject = new BehaviorSubject<T>(default);
            public ReactiveProperty(T initialValue) => _subject = new BehaviorSubject<T>(initialValue);

            public void SetValue(T value) => _subject.OnNext(value);
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