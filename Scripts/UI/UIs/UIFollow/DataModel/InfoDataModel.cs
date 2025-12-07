using HotUpdate.Scripts.Tool.ReactiveProperty;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.DataModel
{
    public class InfoDataModel : IUIDataModel
    {
        public HReactiveProperty<float> Health { get; } = new HReactiveProperty<float>();
        public HReactiveProperty<float> MaxHealth { get; } = new HReactiveProperty<float>();
        public HReactiveProperty<float> Mana { get; } = new HReactiveProperty<float>();
        public HReactiveProperty<float> MaxMana { get; } = new HReactiveProperty<float>();
        public HReactiveProperty<string> Name { get; } = new HReactiveProperty<string>();
        public HReactiveProperty<int> Level { get; } = new HReactiveProperty<int>();
        
        public void Dispose()
        {
            Health.Dispose();
            MaxHealth.Dispose();
            Mana.Dispose();
            MaxMana.Dispose();
            Name.Dispose();
            Level.Dispose();
        }
    }
}