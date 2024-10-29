using Data;
using UniRx;

namespace HotUpdate.Scripts.Data
{
    public static class GameLoopDataModel
    {
        public static readonly ReactiveProperty<float> WarmupRemainingTime = new ReactiveProperty<float>();
        public static readonly ReactiveProperty<float> GameRemainingTime = new ReactiveProperty<float>();
        public static readonly ReactiveProperty<GameLoopData> GameLoopData = new ReactiveProperty<GameLoopData>();
    }

    public struct GameLoopData
    {
        public GameMode GameMode;
        public int TargetScore;
        public float TimeLimit;
    }
}