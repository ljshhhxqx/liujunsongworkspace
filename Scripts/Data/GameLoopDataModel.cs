using System;
using AOTScripts.Data;
using Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Data
{
    public static class GameLoopDataModel
    {
        public static ReactiveProperty<float> WarmupRemainingTime { get; } = new ReactiveProperty<float>();
        public static ReactiveProperty<float> GameRemainingTime{ get; }  = new ReactiveProperty<float>();
        public static ReactiveProperty<GameLoopData> GameLoopData { get; } = new ReactiveProperty<GameLoopData>();
        public static ReactiveProperty<MapType> GameSceneName { get; } = new ReactiveProperty<MapType>();
        public static ReactiveProperty<MapConfigData> MapConfig { get; } = new ReactiveProperty<MapConfigData>();
        public static ReactiveProperty<GameResultData> GameResult { get; } = new ReactiveProperty<GameResultData>();
        public static ReactiveProperty<Vector3> LocalPlayerPosition { get; } = new ReactiveProperty<Vector3>();
    }
}