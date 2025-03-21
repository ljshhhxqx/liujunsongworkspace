using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CustomEditor.Scripts
{
    public static class EnumHeaderParser
    {
        // 缓存枚举类型到 Header 的映射
        private static Dictionary<Type, Dictionary<Enum, string>> _headerCache = new Dictionary<Type, Dictionary<Enum, string>>();

        // 获取枚举的 Header 值
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
        
        // 根据枚举值获取 Header 值
        public static string GetHeader(Enum value)
        {
            var type = value.GetType();
            var headers = GetEnumHeaders(type);
            return headers[value];
        }

        // 根据字符串查找枚举值
        public static bool TryGetEnumFromHeader<T>(string header, out T enumValue) where T : Enum
        {
            var headers = GetEnumHeaders(typeof(T));
            foreach (var kvp in headers)
            {
                if (kvp.Value == header)
                {
                    enumValue = (T)kvp.Key;
                    return true;
                }
            }

            enumValue = default;
            return false;
        }

        // 获取最接近的枚举值（拼写错误时用）
        public static T GetClosestEnumFromHeader<T>(string header, out string closestHeader) where T : Enum
        {
            var headers = GetEnumHeaders(typeof(T));
            var closest = headers.OrderBy(kvp => LevenshteinDistance(kvp.Value, header)).First();
            closestHeader = closest.Value;
            return (T)closest.Key;
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