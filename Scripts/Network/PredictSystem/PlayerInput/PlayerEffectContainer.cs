using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class PlayerEffectContainer : MonoBehaviour
    {
        [SerializeField]
        private PlayerEffectType playerEffectType;
        [SerializeField]
        private ParticleSystem ps;
        
        public PlayerEffectType PlayerEffectType => playerEffectType;
        
        public void PlayEffect()
        {
            ps?.Play();
        }
        
        public void StopEffect()
        {
            ps?.Stop();
        }
    }
}