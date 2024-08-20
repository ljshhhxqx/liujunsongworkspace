using Cysharp.Threading.Tasks;
using Game.Inject;
using Tool.GameEvent;
using UI.UIBase;
using UI.UIs;
using UI.UIs.Exception;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game
{
    public class GameSceneManager
    {
        private IGameMapLifeScope _currentMapLifeScope;
        private readonly ResourceManager _resourceManager;
        private readonly UIManager _uiManager;
        private readonly GameEventManager _gameEventManager;

        [Inject]
        private GameSceneManager(ResourceManager resourceManager, UIManager uiManager, GameEventManager gameEventManager)
        {
            _resourceManager = resourceManager;
            _uiManager = uiManager;
            _gameEventManager = gameEventManager;
        }

        public async UniTask LoadScene(string mapName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive)
        {
            _uiManager.SwitchUI<LoadingScreenUI>();
            var mapScene = SceneManager.LoadSceneAsync(mapName, loadSceneMode);

            while (!mapScene.isDone)
            {
                var progress = Mathf.Clamp01(mapScene.progress / 0.9f);
                var progressStr = progress.ToString("P1");
                _gameEventManager.Publish(new GameSceneLoadingEvent(mapName, progress.ToString("P1")));
                Debug.Log($"Map {mapName} loading... {progressStr}");
                if (progress >= 0.9f)
                {
                    _uiManager.CloseUI(UIType.Loading);
                    Debug.Log($"Map {mapName} loaded");
                    _gameEventManager.Publish(new GameSceneLoadedEvent(mapName));
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