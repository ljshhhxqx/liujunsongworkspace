using System;
using System.Runtime.CompilerServices;

namespace HotUpdate.Scripts.Common
{
    public static class StateOperations
    {
        // 添加状态（无装箱）
        public static T AddState<T>(this T currentState, T statesToAdd) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            byte result = (byte)(currentState.ToByte() | statesToAdd.ToByte());
            return Unsafe.As<byte, T>(ref result);
        }

        // 移除状态（无装箱）
        public static T RemoveState<T>(this T currentState, T statesToRemove) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            byte result = (byte)(currentState.ToByte() & ~statesToRemove.ToByte());
            
            return Unsafe.As<byte, T>(ref result);
        }

        // 切换状态（无装箱）
        public static T ToggleState<T>(this T currentState, T statesToToggle) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            byte result = (byte)(currentState.ToByte() ^ statesToToggle.ToByte());
            return Unsafe.As<byte, T>(ref result);
        }

        // 检查是否包含所有状态
        public static bool HasAllStates<T>(this T currentState, T requiredStates) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            return (currentState.ToByte() & requiredStates.ToByte()) == requiredStates.ToByte();
        }

        // 检查是否包含任意状态
        public static bool HasAnyState<T>(this T currentState, T anyStates) where T : unmanaged, Enum
        {
            ValidateEnumType<T>();
            return (currentState.ToByte() & anyStates.ToByte()) != 0;
        }

        // 验证枚举类型（优化版）
        private static void ValidateEnumType<T>() where T : unmanaged, Enum
        {
            Type enumType = typeof(T);
            if (!enumType.IsDefined(typeof(FlagsAttribute), false))
                throw new ArgumentException($"{enumType.Name} 必须标记为 [Flags]");

            if (Unsafe.SizeOf<T>() != sizeof(byte))
                throw new ArgumentException($"{enumType.Name} 的底层类型必须为 byte");
        }

        // 高性能的 ToByte 转换（避免 IConvertible 接口调用）
        private static byte ToByte<T>(this T value) where T : unmanaged, Enum
        {
            return Unsafe.As<T, byte>(ref value);
        }
    }
}
