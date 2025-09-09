using System;
using Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using UniRx;

namespace HotUpdate.Scripts.Data
{
    public static class GameLoopDataModel
    {
        public static readonly ReactiveProperty<float> WarmupRemainingTime = new ReactiveProperty<float>();
        public static readonly ReactiveProperty<float> GameRemainingTime = new ReactiveProperty<float>();
        public static readonly ReactiveProperty<GameLoopData> GameLoopData = new ReactiveProperty<GameLoopData>();
        public static readonly ReactiveProperty<MapType> GameSceneName = new ReactiveProperty<MapType>();
    }

    [Serializable]
    public struct GameLoopData
    {
        public GameMode GameMode;
        public int TargetScore;
        public float TimeLimit;
        public bool IsStartGame;
    }
}