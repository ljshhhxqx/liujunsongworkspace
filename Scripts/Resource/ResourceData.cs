using System;
using System.Collections.Generic;

[Serializable]
public class ResourcesContainer
{
    public List<ResourceData> Resources;
}

[Serializable]
public class ResourceData
{
    //主键
    public uint UID;
    //资源对应的强类型
    public string ResourceType;
    //资源名称
    public string Name;
    //资源是否是永久存在
    public bool IsPermanent;
    //是否在游戏最开始就加载
    public bool IsPreload;
    //资源路径
    public string Address;
    
    public override bool Equals(object obj)
    {
        return obj is ResourceData data &&
               (UID == data.UID);
    }
    
    public override int GetHashCode()
    {
        return UID.GetHashCode();
    }
}