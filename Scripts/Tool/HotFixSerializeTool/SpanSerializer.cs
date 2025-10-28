using System;
using System.Runtime.CompilerServices;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.HotFixSerializeTool
{
    // public ref struct SpanSerializer<T> where T : struct
    // {
    //     private Span<byte> _buffer;
    //
    //     public SpanSerializer(Span<byte> buffer)
    //     {
    //         _buffer = buffer;
    //     }
    //
    //     // 为结构体提供基于Span的序列化，完全避免装箱
    //     public void Serialize(in T value)
    //     {
    //         // 手写结构体序列化逻辑，避免使用反射
    //         if (typeof(T) == typeof(Vector3))
    //         {
    //             SerializeVector3(Unsafe.As<T, Vector3>(ref Unsafe.AsRef(value)));
    //         }
    //         else if (typeof(T) == typeof(Quaternion))
    //         {
    //             SerializeQuaternion(Unsafe.As<T, Quaternion>(ref Unsafe.AsRef(value)));
    //         }
    //         // ... 其他结构体类型
    //         else
    //         {
    //             // 回退到 MemoryPack 非泛型方法
    //             MemoryPackSerializer.Serialize(typeof(T), value, _buffer);
    //         }
    //     }
    //
    //     public T Deserialize()
    //     {
    //         // 类似手写逻辑
    //         if (typeof(T) == typeof(Vector3))
    //         {
    //             return Unsafe.As<Vector3, T>(ref DeserializeVector3());
    //         }
    //         // ... 其他类型
    //     
    //         return (T)MemoryPackSerializer.Deserialize(typeof(T), _buffer);
    //     }
    //
    //     private void SerializeVector3(in Vector3 value)
    //     {
    //         BitConverter.TryWriteBytes(_buffer, value.x);
    //         BitConverter.TryWriteBytes(_buffer.Slice(4), value.y);
    //         BitConverter.TryWriteBytes(_buffer.Slice(8), value.z);
    //     }
    //
    //     private Vector3 DeserializeVector3()
    //     {
    //         return new Vector3(
    //             BitConverter.ToSingle(_buffer),
    //             BitConverter.ToSingle(_buffer.Slice(4)),
    //             BitConverter.ToSingle(_buffer.Slice(8))
    //         );
    //     }
    //     
    //     private void SerializeQuaternion(in Quaternion value)
    //     {   
    //         BitConverter.TryWriteBytes(_buffer, value.x);
    //         BitConverter.TryWriteBytes(_buffer.Slice(4), value.y);
    //         BitConverter.TryWriteBytes(_buffer.Slice(8), value.z);
    //         BitConverter.TryWriteBytes(_buffer.Slice(12), value.w);
    //     }
    //
    //     private Quaternion DeserializeQuaternion()
    //     {
    //         return new Quaternion(
    //             BitConverter.ToSingle(_buffer),
    //             BitConverter.ToSingle(_buffer.Slice(4)),
    //             BitConverter.ToSingle(_buffer.Slice(8)),
    //             BitConverter.ToSingle(_buffer.Slice(12))
    //         );
    //     }
    // }
}