using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    /// <summary>
    /// 客户端预测+服务器同步+客户端回滚
    /// </summary>
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PlayerInputState))]
    [MemoryPackUnion(1, typeof(PlayerPredictablePropertyState))]
    //[MemoryPackUnion(2, typeof(PlayerItemState))]
    public partial interface IPredictablePropertyState : ISyncPropertyState
    {
        bool IsEqual(IPredictablePropertyState other, float tolerance = 0.01f);
    }
    
    /// <summary>
    /// 服务器强制同步内容
    /// </summary>
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PlayerEquipmentState))]
    [MemoryPackUnion(1, typeof(PlayerItemState))]
    [MemoryPackUnion(2, typeof(PlayerInputState))]
    [MemoryPackUnion(3, typeof(PlayerPredictablePropertyState))]
    [MemoryPackUnion(4, typeof(PlayerShopState))]
    [MemoryPackUnion(5, typeof(PlayerSkillState))]
    public partial interface ISyncPropertyState
    {
        
    }
}