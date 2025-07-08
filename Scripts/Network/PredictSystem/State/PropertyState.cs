using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
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
        public PlayerSyncStateType GetStateType();
    }
    
    public enum PlayerSyncStateType
    {
        PlayerInput = 2,
        PlayerProperty = 3,
        PlayerItem = 1,
        PlayerEquipment = 0,
        PlayerShop = 4,
        PlayerSkill = 5,
    }
}