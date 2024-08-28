using System;
using Config;
using UnityEngine;

/// <summary>
/// 所有可被拾取的物品都应该继承该接口
/// </summary>
public interface ICollect
{
    int CollectId { get; }
    CollectObjectData CollectData { get; }
    Collider Collider { get; }
}

public abstract class CollectObject : MonoBehaviour, ICollect
{
    public int CollectId { get; set; }
    public abstract CollectObjectData CollectData { get; }
    public abstract Collider Collider { get; }
    protected abstract void Collect(int pickerId, PickerType pickerType);
}

/// <summary>
/// 拾取者枚举
/// </summary>
[Serializable]
public enum PickerType
{
    Player,
    Computer,
}

/// <summary>
/// 可拾取物枚举
/// </summary>
[Serializable]
public enum CollectType
{
    TreasureChest,
    SliverCoin,
    GoldCoin,
    Gem,
    StrengthGem,
    SpeedGem,
}

[Serializable]
public enum CollectObjectClass
{
    TreasureChest,
    Score,
    Buff,
}