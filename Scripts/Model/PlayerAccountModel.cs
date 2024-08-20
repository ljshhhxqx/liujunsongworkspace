using UniRx;

namespace Model
{
    public class PlayerAccountModel : GameModel
    {
        public ReactiveProperty<int> Level { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<int> Experience { get; } = new ReactiveProperty<int>();
        
        protected override void OnDispose()
        {
            
        }
    }
}