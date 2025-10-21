using AOTScripts.Data;
using Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config.ArrayConfig;

namespace HotUpdate.Scripts.Data
{
    public static class GameDataJitInit
    {
        public static void Init()
        {
            AudioManagerType audioManager = AudioManagerType.Game;
            AudioEffectType audioEffect = AudioEffectType.None;
            AudioMusicType audioMusic = AudioMusicType.None;
            GameData gameData = new GameData();
            PlayerBaseData playerBaseData = new PlayerBaseData();
            PlayerAccountData playerAccountData = new PlayerAccountData();
            PlayerInGameData playerInGameData = new PlayerInGameData();
            GameLoopData gameLoopData = new GameLoopData();
            MapType mapType = MapType.Town;
            GameResultData gameResultData = new GameResultData();
            TriggerType triggerType = TriggerType.None;
            ConditionTargetType conditionTargetType = ConditionTargetType.None;
            ConditionHeader conditionHeader = new ConditionHeader();
            AttackRangeType attackRangeType = AttackRangeType.None;
            SkillBaseType skillBaseType = SkillBaseType.None;
            DamageType damageType = DamageType.None;
            DamageCastType damageCastType = DamageCastType.None;
            EffectType   effectType   = EffectType.None;
            PlayerInternalData playerInternalData = new PlayerInternalData();
            PlayerGameResultData playerGameResultData = new PlayerGameResultData();
            PlayerReadOnlyData playerReadOnlyData = new PlayerReadOnlyData();
            FriendStatus friendStatus = FriendStatus.None;
            NonFriendOnlinePlayersResult nonFriendOnlinePlayersResult = new NonFriendOnlinePlayersResult();
            FriendData friendData = new FriendData();
            FriendList friendList = new FriendList();
            PlayerStatus playerStatus = PlayerStatus.Offline;
            RoomGlobalInfo roomGlobalInfo = new RoomGlobalInfo();
            RoomsData roomsData = new RoomsData();
            RoomData roomData = new RoomData();
            RoomCustomInfo    roomCustomInfo    = new RoomCustomInfo();
            PlayerGameStatus playerGameStatus = PlayerGameStatus.None;
            PlayerGameDuty playerGameDuty = PlayerGameDuty.None;
            MainGameInfo mainGameInfo = new MainGameInfo();
            GamePlayerInfo gamePlayerInfo = new GamePlayerInfo();
            RoomData roomDataRoom = new RoomData();
            GameMode gameMode = GameMode.Score;
        }
    }
}