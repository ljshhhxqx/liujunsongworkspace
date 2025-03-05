using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    /// <summary>
    /// 客户端预测+服务器同步+客户端回滚
    /// </summary>
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PlayerInputState))]
    [MemoryPackUnion(1, typeof(PlayerPredictablePropertyState))]
    public partial interface IPredictablePropertyState : ISyncPropertyState
    {
        bool IsEqual(IPredictablePropertyState other, float tolerance = 0.01f);
    }
    
    /// <summary>
    /// 服务器强制同步内容
    /// </summary>
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PlayerItemState))]
    [MemoryPackUnion(1, typeof(PlayerInputState))]
    [MemoryPackUnion(2, typeof(PlayerPredictablePropertyState))]
    public partial interface ISyncPropertyState
    {
        
    }
}