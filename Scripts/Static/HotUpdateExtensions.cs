using System;
using System.Collections.Generic;

namespace HotUpdate.Scripts.Static
{
    public static class HotUpdateExtensions
    {
        public static bool AddOrUpdate<T1, T2>(this IDictionary<T1, T2> dictionary, T1 key, T2 value)
        {
            try
            {
                if (dictionary.ContainsKey(key))
                {
                    dictionary[key] = value; // 如果键存在，更新对应的值
                }
                else
                {
                    dictionary.Add(key, value); // 如果键不存在，添加键值对
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return false;
        }
    }
}