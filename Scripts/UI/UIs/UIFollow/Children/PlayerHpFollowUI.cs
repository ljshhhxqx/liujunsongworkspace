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
            if (_infoDataModel == null)
            {
                _controller = UIFollowManager.Instance.GetController<PlayerHpFollowController>();
                _infoDataModel = new InfoDataModel();
            }
            _controller.BindToModel(_infoDataModel);
            _gameEventManager.Subscribe<PlayerInfoChangedEvent>(OnPlayerInfoChanged);
        }

        private void OnPlayerInfoChanged(PlayerInfoChangedEvent playerInfoChangedEvent)
        {
            if (SceneId.Value == playerInfoChangedEvent.PlayerId)
            {
                _infoDataModel.Health.Value = playerInfoChangedEvent.Health;
                _infoDataModel.MaxHealth.Value = playerInfoChangedEvent.MaxHealth;
                _infoDataModel.Mana.Value = playerInfoChangedEvent.Mana;
                _infoDataModel.MaxMana.Value = playerInfoChangedEvent.MaxMana;
                _infoDataModel.Name.Value = playerInfoChangedEvent.PlayerName;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _infoDataModel?.Dispose();
        }
    }
}