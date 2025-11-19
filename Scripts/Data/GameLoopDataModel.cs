using System;
using AOTScripts.Data;
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
        public static readonly ReactiveProperty<MapConfigData> MapConfig = new ReactiveProperty<MapConfigData>();
        public static readonly ReactiveProperty<GameResultData> GameResult = new ReactiveProperty<GameResultData>();
    }
}