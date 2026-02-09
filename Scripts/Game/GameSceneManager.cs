using System;
using AOTScripts.Data;
using AOTScripts.Tool.Resource;
using Cysharp.Threading.Tasks;
using Game.Inject;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using UI.UIBase;
using UI.UIs;
using UI.UIs.Exception;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace HotUpdate.Scripts.Game
{
    public class GameSceneManager
    {
        private IGameMapLifeScope _currentMapLifeScope;
        private readonly UIManager _uiManager;
        private readonly GameEventManager _gameEventManager;
        private MapConfig _mapConfig;
        
        public static MapType CurrentMapType { get; private set; }

        [Inject]
        private GameSceneManager(UIManager uiManager, GameEventManager gameEventManager)
        {
            _uiManager = uiManager;
            _gameEventManager = gameEventManager;
        }

        public async UniTask LoadScene(string mapName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive)
        {
            _uiManager.SwitchUI<LoadingScreenUI>();
            var mapScene = ResourceManager.Instance.LoadSceneAsync(mapName, loadSceneMode);

            while (!mapScene.IsDone)
            {
                var progress = Mathf.Clamp01(mapScene.PercentComplete / 0.9f);
                var progressStr = progress.ToString("P1");
                _gameEventManager.Publish(new GameSceneLoadingEvent(mapName, progress.ToString("P1")));
                Debug.Log($"Map {mapName} loading... {progressStr}");
                if (progress >= 0.9f)
                {
                    _uiManager.CloseUI(UIType.Loading);
                    Debug.Log($"Map {mapName} loaded");
                    _gameEventManager.Publish(new GameSceneLoadedEvent(mapName));
                    CurrentMapType = Enum.Parse<MapType>(mapName);
                    break;
                }
                // second += Time.deltaTime;
                // if (second <= updateInterval) continue;
                // second = 0f;

                await UniTask.Yield();
            }
        }

        public async UniTask UnloadScene(string mapName)
        {
            _uiManager.SwitchUI<LoadingScreenUI>();
            var mapScene = SceneManager.UnloadSceneAsync(mapName);

            while (!mapScene.isDone)
            {
                var progress = Mathf.Clamp01(mapScene.progress / 0.9f);
                if (progress >= 0.9f)
                {
                    _uiManager.CloseUI(UIType.Loading);
                    _uiManager.SwitchUI<LoginScreenUI>();
                    Debug.Log($"Map {mapName} unloaded");
                    _gameEventManager.Publish(new GameSceneLoadedEvent(mapName));
                    break;
                }
                await UniTask.Yield();
            }
        }

    }
}