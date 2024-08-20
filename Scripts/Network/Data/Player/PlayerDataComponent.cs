using Mirror;
using UniRx;

namespace Network.Data
{
    public class PlayerDataComponent : NetworkBehaviour
    {
        public ReactiveProperty<int> UID { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<int> ConnectionId { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<string> UserName { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<int> Score { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<float> Speed { get; } = new ReactiveProperty<float>();
        public ReactiveProperty<float> Strength { get; } = new ReactiveProperty<float>();
    }
}