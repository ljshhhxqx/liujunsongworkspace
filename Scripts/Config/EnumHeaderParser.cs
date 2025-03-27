using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CustomEditor.Scripts
{
    public static class EnumHeaderParser
    {
        // 缓存枚举类型到 Header 的映射（仅基础值）
        private static Dictionary<Type, Dictionary<Enum, string>> _headerCache = new Dictionary<Type, Dictionary<Enum, string>>();
        public static T GetEnumValue<T>(string header) where T : Enum
        {
            foreach (var field in typeof(T).GetFields())
            {
                if (Attribute.GetCustomAttribute(field, typeof(HeaderAttribute)) is HeaderAttribute attr 
                    && attr.header == header)
                {
                    return (T)field.GetValue(null);
                }
            }
            return (T)Enum.Parse(typeof(T), header);
        }

        // 获取枚举的基础 Header 值
        public static Dictionary<Enum, string> GetEnumHeaders(Type enumType)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException($"{enumType} 不是枚举类型");

            if (_headerCache.TryGetValue(enumType, out var cachedMap))
                return cachedMap;

            var map = new Dictionary<Enum, string>();
            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                var headerAttribute = field.GetCustomAttribute<HeaderAttribute>();
                if (headerAttribute != null)
                {
                    Enum value = (Enum)field.GetValue(null);
                    map[value] = headerAttribute.header;
                }
            }

            _headerCache[enumType] = map;
            return map;
        }

        // 根据枚举值获取 Header 字符串（支持 Flags 组合）
        public static string GetHeader(Enum value)
        {
            var type = value.GetType();
            var headers = GetEnumHeaders(type);
            var underlyingValue = Convert.ToInt32(value); // 获取底层整数值
            if (underlyingValue == 0 && headers.ContainsKey(value))
                return headers[value]; // 处理 None 的情况

            // 分解 Flags 组合
            var flags = headers.Keys
                .Where(k => Convert.ToInt32(k) != 0 && (underlyingValue & Convert.ToInt32(k)) == Convert.ToInt32(k))
                .Select(k => headers[k]);

            return flags.Any() ? string.Join(", ", flags) : "未知";
        }

        // 根据字符串查找枚举值（支持 Flags 组合）
        public static bool TryGetEnumFromHeader<T>(string header, out T enumValue) where T : Enum
        {
            var headers = GetEnumHeaders(typeof(T));
            var headerParts = header.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

            int combinedValue = 0;
            foreach (var part in headerParts)
            {
                var match = headers.FirstOrDefault(kvp => kvp.Value == part);
                if (match.Key == null)
                {
                    enumValue = default;
                    return false; // 有一个部分无法匹配则失败
                }
                combinedValue |= Convert.ToInt32(match.Key);
            }

            enumValue = (T)Enum.ToObject(typeof(T), combinedValue);
            return true;
        }

        // 获取最接近的枚举值（拼写错误时用）
        public static T GetClosestEnumFromHeader<T>(string header, out string closestHeader) where T : Enum
        {
            var headers = GetEnumHeaders(typeof(T));
            var headerParts = header.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

            int combinedValue = 0;
            var closestHeaders = new List<string>();

            foreach (var part in headerParts)
            {
                var closest = headers.OrderBy(kvp => LevenshteinDistance(kvp.Value, part)).First();
                combinedValue |= Convert.ToInt32(closest.Key);
                closestHeaders.Add(closest.Value);
            }

            closestHeader = string.Join(", ", closestHeaders);
            return (T)Enum.ToObject(typeof(T), combinedValue);
        }

        // 计算字符串相似度
        private static int LevenshteinDistance(string s, string t)
        {
            int[,] d = new int[s.Length + 1, t.Length + 1];
            for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Mathf.Min(
                        d[i - 1, j] + 1,
                        d[i, j - 1] + 1,
                        d[i - 1, j - 1] + cost);
                }
            return d[s.Length, t.Length];
        }
    }

}