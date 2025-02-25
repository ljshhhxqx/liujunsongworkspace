using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.InteractSystem
{
    public enum InteractCategory : byte
    {
        PlayerToScene,    // 玩家与场景交互
        PlayerToPlayer,   // 玩家间交互
        SceneToPlayer     // 场景主动影响玩家
    }
    
    public enum InteractResult : byte
    {
        Pending,         // 等待处理
        Success,         // 交互成功
        Failed,          // 交互失败
        PartiallySuccess // 部分成功
    }

    public enum InteractionType : byte
    {
        PickupItem = 0,
        PickupChest,
    }

    // 基础交互头
    [MemoryPackable]
    public partial struct InteractHeader
    {
        [MemoryPackOrder(0)] 
        public uint CommandId;              // 命令唯一ID（时间戳+序列号）
        [MemoryPackOrder(1)] 
        public int RequestConnectionId;               // 发起者connectionId
        [MemoryPackOrder(2)]
        public int Tick;
        [MemoryPackOrder(3)] 
        public InteractCategory Category;
        [MemoryPackOrder(4)] 
        public CompressedVector3 Position; // 交互发生位置
        [MemoryPackOrder(5)] 
        public long Timestamp;     // UTC时间戳（毫秒）
        // 执行上下文
        [MemoryPackOrder(6)] 
        public CommandAuthority Authority;
    }
    
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(SceneInteractRequest))]
    [MemoryPackUnion(1, typeof(PlayerInteractRequest))]
    [MemoryPackUnion(2, typeof(EnvironmentInteractRequest))]
    public partial interface IInteractRequest
    {
        InteractHeader GetHeader();
        bool IsValid();
    }

    // 玩家与场景交互
    [MemoryPackable]
    public partial struct SceneInteractRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint SceneItemId; // 场景物体ID（由服务器生成的id）
        [MemoryPackOrder(2)] public InteractionType InteractionType; // 交互类型（开门/拾取等）
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            throw new System.NotImplementedException();
        }
    }

    // 玩家间交互
    [MemoryPackable]
    public partial struct PlayerInteractRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint TargetPlayerId;
        [MemoryPackOrder(2)] public ushort InteractionId; // 预定义的交互ID
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            throw new System.NotImplementedException();
        }
    }

    // 环境主动交互
    [MemoryPackable]
    public partial struct EnvironmentInteractRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint HazardId;    // 环境危险区域ID
        [MemoryPackOrder(2)] public float Intensity;  // 影响强度
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            throw new System.NotImplementedException();
        }
    }
}