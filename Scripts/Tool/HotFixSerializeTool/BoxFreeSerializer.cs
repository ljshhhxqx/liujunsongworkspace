using System;
using System.Collections.Generic;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.HotFixSerializeTool
{
    public static class BoxingFreeSerializer
    {
        private static Dictionary<Type, Delegate> _jsonSerializers = new Dictionary<Type, Delegate>();
        private static Dictionary<Type, Delegate> _jsonDeserializers = new Dictionary<Type, Delegate>();
        private static Dictionary<Type, Delegate> _memorySerializers = new Dictionary<Type, Delegate>();
        private static Dictionary<Type, Delegate> _memoryDeserializers = new Dictionary<Type, Delegate>();
    
        // 注册结构体的序列化方法，避免运行时反射
        public static void RegisterStruct<T>() where T : struct
        {
            _memorySerializers[typeof(T)] = new Func<T, byte[]>(SerializeInternal<T>);
            _memoryDeserializers[typeof(T)] = new Func<byte[], T>(DeserializeInternal<T>);
            _jsonSerializers[typeof(T)] = new Func<T, string>(JsonSerializeInternal<T>);
            _jsonDeserializers[typeof(T)] = new Func<string, T>(JsonDeserializeInternal<T>);
        }
    
        // 注册引用类型的序列化方法
        public static void RegisterClass<T>() where T : class
        {
            _memorySerializers[typeof(T)] = new Func<T, byte[]>(SerializeInternal<T>);
            _memoryDeserializers[typeof(T)] = new Func<byte[], T>(DeserializeInternal<T>);
            _jsonSerializers[typeof(T)] = new Func<T, string>(JsonSerializeInternal<T>);
            _jsonDeserializers[typeof(T)] = new Func<string, T>(JsonDeserializeInternal<T>);
        }

        public static string JsonSerialize<T>(T value)
        {
            var type = typeof(T);
            string json;
            if (_jsonSerializers.TryGetValue(type, out var serializer))
            {
                json = ((Func<T, string>)serializer)(value);
                return json;
            }

            json = JsonUtility.ToJson(value);
            return json;
        }

        public static T JsonDeserialize<T>(string json)
        {
            var type = typeof(T);
            if (_jsonDeserializers.TryGetValue(type, out var deserializer))
            {
                return ((Func<string, T>)deserializer)(json);
            }

            return (T)JsonUtility.FromJson(json, type);
        }

        // 序列化（避免装箱）
        public static byte[] MemorySerialize<T>(T value)
        {
            var type = typeof(T);
            if (_memorySerializers.TryGetValue(type, out var serializer))
            {
                return ((Func<T, byte[]>)serializer)(value);
            }
        
            return MemoryPackSerializer.Serialize(value);
        }
    
        // 反序列化（避免拆箱）
        public static T MemoryDeserialize<T>(byte[] data)
        {
            var type = typeof(T);
            if (_memoryDeserializers.TryGetValue(type, out var deserializer))
            {
                return ((Func<byte[], T>)deserializer)(data);
            }
        
            return (T)MemoryPackSerializer.Deserialize(type, data);
        }
    
        private static byte[] SerializeInternal<T>(T value)
        {
            return MemoryPackSerializer.Serialize(typeof(T), value);
        }
    
        private static T DeserializeInternal<T>(byte[] data)
        {
            return (T)MemoryPackSerializer.Deserialize(typeof(T), data);
        }

        private static T JsonDeserializeInternal<T>(string arg)
        {
            return (T)JsonUtility.FromJson(arg, typeof(T));
        }

        private static string JsonSerializeInternal<T>(T arg)
        {
            return JsonUtility.ToJson(arg);
        }
    }
}