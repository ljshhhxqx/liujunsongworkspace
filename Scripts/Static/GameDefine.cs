public static class GameDefine
{
    public static string GetCollectPrefabName(CollectType type)
    {
        return type switch
        {
            CollectType.TreasureChest => "Chest",
            _ => ""
        };
    }
}