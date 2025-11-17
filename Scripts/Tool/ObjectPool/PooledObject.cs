using System;
using UnityEngine;

public class PooledObject: MonoBehaviour
{
    public int PrefabId { get; set; }
    
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