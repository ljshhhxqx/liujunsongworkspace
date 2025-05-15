using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Mat;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class PlayerEffectPlayer : MonoBehaviour
    {
        [SerializeField]
        private Renderer[] renderers;

        private static readonly int AlphaShaderProperty = Shader.PropertyToID("_Metallic");

        private Dictionary<PlayerEffectType, PlayerEffectContainer> _playerEffectContainers = new Dictionary<PlayerEffectType, PlayerEffectContainer>();
        
        public void PlayEffect(PlayerEffectContainer ps)
        {
            if (_playerEffectContainers.ContainsKey(ps.PlayerEffectType))
            {
                _playerEffectContainers[ps.PlayerEffectType].StopEffect();
            }
            _playerEffectContainers[ps.PlayerEffectType] = ps;
            ps.PlayEffect();
        }
        
        public void StopEffect(PlayerEffectContainer ps)
        {
            if (_playerEffectContainers.ContainsKey(ps.PlayerEffectType))
            {
                _playerEffectContainers[ps.PlayerEffectType].StopEffect();
            }
            
        }
        
        public void StopAllEffect(Action<PlayerEffectContainer> onFinished)
        {
            foreach (var effect in _playerEffectContainers.Values)
            {
                effect.StopEffect();
                onFinished?.Invoke(effect);
            }
            _playerEffectContainers.Clear();
        }

        public void SetAlpha(float alpha)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var material = MatStaticExtension.GetPropertyBlock(r);
                var mode = MatStaticExtension.GetStandardShaderType(material);
                if (mode == StandardShaderType.Transparent)
                {
                    material.SetFloat(AlphaShaderProperty, alpha);
                }
                else
                {
                    r.enabled = alpha > 0.5f;
                }
            }
        }
    }
}
