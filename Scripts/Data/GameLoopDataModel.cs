using System;
using AOTScripts.Data;
using Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Data
{
    public static class GameLoopDataModel
    {
        public static HReactiveProperty<float> WarmupRemainingTime { get; private set;} = new HReactiveProperty<float>();
        public static HReactiveProperty<float> GameRemainingTime{ get; private set; }  = new HReactiveProperty<float>();
        public static HReactiveProperty<GameLoopData> GameLoopData { get;private set; } = new HReactiveProperty<GameLoopData>();
        public static HReactiveProperty<int> GameSceneName { get; private set;} = new HReactiveProperty<int>();
        public static HReactiveProperty<MapConfigData> MapConfig { get; private set;} = new HReactiveProperty<MapConfigData>();
        public static HReactiveProperty<GameResultData> GameResult { get; private set;} = new HReactiveProperty<GameResultData>();
        public static HReactiveProperty<Vector3> LocalPlayerPosition { get; private set;} = new HReactiveProperty<Vector3>();

        public static void Clear()
        {
            WarmupRemainingTime.Value = 0;
            GameRemainingTime.Value = 0;
            GameLoopData.Value = default;
            GameSceneName.Value = (int)MapType.Town;
            MapConfig.Value = default;
            GameResult.Value = default;
            LocalPlayerPosition.Value = Vector3.zero;
        }
    }
}