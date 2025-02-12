using MemoryPack;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.State
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(Network.PredictSystem.State.PlayerInputState))]
    [MemoryPackUnion(1, typeof(PlayerPropertyState))]
    public partial interface IPropertyState
    {
        bool IsEqual(IPropertyState other, float tolerance = 0.01f);
    }
}