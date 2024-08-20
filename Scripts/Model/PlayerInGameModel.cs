using UniRx;
using UnityEngine;

namespace Model
{
    public class PlayerInGameModel : GameModel
    {
        public ReactiveProperty<Vector3> Position { get; } = new ReactiveProperty<Vector3>();
        public ReactiveProperty<Quaternion> Rotation { get; } = new ReactiveProperty<Quaternion>();
        public ReactiveProperty<string> CurrentAnimation { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<bool> IsMoving { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<bool> IsJumping { get; } = new ReactiveProperty<bool>();

        //在此处注销所有Model
        protected override void OnDispose()
        {
            Position.Dispose();
            Rotation.Dispose();
            CurrentAnimation.Dispose();
            IsMoving.Dispose();
            IsJumping.Dispose();    
        }
    }
}