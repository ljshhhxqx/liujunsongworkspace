using HotUpdate.Scripts.Tool.ReactiveProperty;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.DataModel
{
    public class InfoDataModel : IUIDataModel
    {
        public HReactiveProperty<int> Health { get; } = new HReactiveProperty<int>();
        public HReactiveProperty<int> MaxHealth { get; } = new HReactiveProperty<int>();
        public HReactiveProperty<int> Mana { get; } = new HReactiveProperty<int>();
        public HReactiveProperty<int> MaxMana { get; } = new HReactiveProperty<int>();
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