namespace Tool.Message
{
    /// <summary>
    /// MessageType更适合处理网络消息
    /// </summary>
    public enum MessageType
    {
        PlayerMoved,
        PlayerRotated,
        PlayerInput,
        PlayerGravityEffect,
        PlayerTouchedCollectable,
        PlayerTouchedChest,
    }
}