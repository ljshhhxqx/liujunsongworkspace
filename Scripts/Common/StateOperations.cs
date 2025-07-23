using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HotUpdate.Scripts.Common
{
    public static class StateOperations
    {
        // 添加状态（无装箱）
        public static T AddState<T>(this T currentState, T statesToAdd) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            var result = (currentState.ToInt32() | statesToAdd.ToInt32());
            return Unsafe.As<int, T>(ref result);
        }

        // 移除状态（无装箱）
        public static T RemoveState<T>(this T currentState, T statesToRemove) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            var result = (currentState.ToInt32() & ~statesToRemove.ToInt32());
            
            return Unsafe.As<int, T>(ref result);
        }

        // 切换状态（无装箱）
        public static T ToggleState<T>(this T currentState, T statesToToggle) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            var result = (currentState.ToInt32() ^ statesToToggle.ToInt32());
            return Unsafe.As<int, T>(ref result);
        }

        // 检查是否包含所有状态
        public static bool HasAllStates<T>(this T currentState, T requiredStates) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            return (currentState.ToInt32() & requiredStates.ToInt32()) == requiredStates.ToInt32();
        }

        // 检查是否包含任意状态
        public static bool HasAnyState<T>(this T currentState, T anyStates) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            return (currentState.ToInt32() & anyStates.ToInt32()) != 0;
        }
        // 新增扩展方法
        public static IEnumerable<T> GetActiveStates<T>(this T currentState) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            var state = currentState.ToInt32();
            return FlagCache<T>.Flags.Where(flag => (state & flag.ToInt32()) != 0);
        }

        // 新增缓存结构（线程安全且无反射开销）
        private static class FlagCache<T> where T : unmanaged, Enum
        {
            public static readonly IReadOnlyList<T> Flags = InitFlags();

            private static IReadOnlyList<T> InitFlags()
            {
                HashSet<int> seenValues = new();
                List<T> validFlags = new();

                foreach (T value in Enum.GetValues(typeof(T)))
                {
                    byte byteValue = value.ToByte();
            
                    // 过滤规则：非零值 && 单个位标志 && 不重复
                    if (byteValue != 0 && 
                        (byteValue & (byteValue - 1)) == 0 && 
                        seenValues.Add(byteValue))
                    {
                        validFlags.Add(value);
                    }
                }

                // 按位顺序排序（1, 2, 4, 8...）
                return validFlags.OrderBy(v => v.ToByte()).ToList();
            }
        }

        // 验证枚举类型（优化版）
        private static void ValidateEnumType<T>() where T : unmanaged, Enum
        {
            Type enumType = typeof(T);
            if (!enumType.IsDefined(typeof(FlagsAttribute), false))
                throw new ArgumentException($"{enumType.Name} 必须标记为 [Flags]");

            if (Unsafe.SizeOf<T>() != sizeof(byte) && Unsafe.SizeOf<T>() != sizeof(int) && Unsafe.SizeOf<T>() != sizeof(short))
                throw new ArgumentException($"{enumType.Name} 的底层类型必须为 byte | int | short");
        }

        // 高性能的 ToByte 转换（避免 IConvertible 接口调用）
        private static byte ToByte<T>(this T value) where T : unmanaged, Enum
        {
            return Unsafe.As<T, byte>(ref value);
        }
        
        // 高性能的 ToInt32 转换（避免 IConvertible 接口调用）
        private static int ToInt32<T>(this T value) where T : unmanaged, Enum
        {
            return Unsafe.As<T, int>(ref value);
        }
        
    }
}
