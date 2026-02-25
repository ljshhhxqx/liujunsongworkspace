using System;
using UnityEngine;

public class PooledObject: MonoBehaviour
{
    public int PrefabId { get; set; }
    public uint Uid { get; set; }// 在实例化后设置
    
    public Action OnSelfSpawn;
    public Action OnSelfDespawn;
    public virtual void OnReset()
    {
        
    }
}

// 可池化接口
public interface IPoolable
{
    void OnSelfSpawn();
    void OnSelfDespawn();
}