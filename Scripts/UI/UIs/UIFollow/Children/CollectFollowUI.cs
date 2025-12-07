using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using HotUpdate.Scripts.UI.UIs.UIFollow.UIController;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.Children
{
    public class CollectFollowUI : ModularUIFollower
    {
        [Inject]
        private GameEventManager _gameEventManager;
        private InfoDataModel _infoDataModel;
        private CollectFollowController _controller;
        
        protected override void BindControllersToModels()
        {
            _infoDataModel = new InfoDataModel();
            _controller.BindToModel(_infoDataModel);
            _gameEventManager.Subscribe<SceneItemInfoChangedEvent>(OnSceneItemInfoChanged);
        }

        private void OnSceneItemInfoChanged(SceneItemInfoChangedEvent sceneItemInfoChangedEvent)
        {
            if (SceneId.Value == sceneItemInfoChangedEvent.ItemId)
            {
                _infoDataModel.Health.Value = sceneItemInfoChangedEvent.SceneItemInfo.health;
                _infoDataModel.MaxHealth.Value = sceneItemInfoChangedEvent.SceneItemInfo.maxHealth;
                _infoDataModel.Name.Value = sceneItemInfoChangedEvent.SceneItemInfo.sceneItemId.ToString();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _gameEventManager?.Unsubscribe<SceneItemInfoChangedEvent>(OnSceneItemInfoChanged);
            _infoDataModel?.Dispose();
        }
    }
}