using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using HotUpdate.Scripts.UI.UIs.UIFollow.UIController;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.Children
{
    public class PlayerHpFollowUI : ModularUIFollower
    {
        [Inject]
        private GameEventManager _gameEventManager;
        private InfoDataModel _infoDataModel;
        private PlayerHpFollowController _controller;
        
        protected override void BindControllersToModels()
        {
            _controller.BindToModel(_infoDataModel);
            // _gameEventManager.Subscribe<SceneItemInfoChangedEvent>(OnSceneItemInfoChanged);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _infoDataModel?.Dispose();
        }
    }
}