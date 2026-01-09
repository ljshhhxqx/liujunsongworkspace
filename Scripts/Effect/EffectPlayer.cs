using System;
using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Effect
{
    public class EffectPlayer : SingletonAutoMono<EffectPlayer>
    {
        private readonly Dictionary<ParticlesType, GameObject> _particleSystemsPrefab = new Dictionary<ParticlesType, GameObject>();

        private readonly Dictionary<ParticlesType, ParticleSystem> _activeParticleSystems = new Dictionary<ParticlesType, ParticleSystem>();
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            gameEventManager.Subscribe<GameMapResourceLoadedEvent>(OnGameMapResourceLoaded);  
        }

        private void OnGameMapResourceLoaded(GameMapResourceLoadedEvent loadedEvent)
        {
            LoadEffects(loadedEvent.SceneName);
        }

        private void LoadEffects(string mapName)
        {
            var effectsResource = ResourceManager.Instance.GetEffectsResource(mapName);
            foreach (var effect in effectsResource)
            {
                if (effect.TryGetComponent<ParticleSystem>(out var ps))
                {
                    _particleSystemsPrefab.TryAdd((ParticlesType)Enum.Parse(typeof(ParticlesType), effect.name), effect);
                }
            }
        }

        public void PlayEffect(ParticlesType type, Vector3 position, Transform parent = null)
        {
            if (_activeParticleSystems.ContainsKey(type) && _activeParticleSystems[type])
            {
                _activeParticleSystems[type].gameObject.SetActive(true);
                _activeParticleSystems[type].transform.position = position;
                _activeParticleSystems[type].transform.parent = parent ?? _activeParticleSystems[type].transform.parent;
                _activeParticleSystems[type].transform.localPosition = Vector3.zero;
                _activeParticleSystems[type].Play();
                return;
            }

            if (!_particleSystemsPrefab.TryGetValue(type, out var value))
            {
                Debug.LogError($"Effect {type} not found.");
                return;
            
            }
            var effect = GameObjectPoolManger.Instance.GetObject(value.gameObject, position, parent: parent);
            var ps = effect.GetComponent<ParticleSystem>();
            ps.Play();
            _activeParticleSystems.TryAdd(type, ps);
        }

        public void StopEffect(ParticlesType type)
        {
            if (!_activeParticleSystems.ContainsKey(type))
            {
                return;
            }

            _activeParticleSystems[type].gameObject.SetActive(false);
            _activeParticleSystems[type].Stop();
            GameObjectPoolManger.Instance.ReturnObject(_activeParticleSystems[type].gameObject);
            _activeParticleSystems.Remove(type);
        }
        
        public void StopAllEffects()
        {
            foreach (var effect in _activeParticleSystems)
            {
                effect.Value.gameObject.SetActive(false);
                effect.Value.Stop();
                GameObjectPoolManger.Instance.ReturnObject(effect.Value.gameObject);
            }
            _activeParticleSystems.Clear();
        }

        private void OnDestroy()
        {
            ClearAllEffects();
        }

        public void ClearAllEffects()
        {
            // foreach (var effect in _activeParticleSystems)
            // {
            //     GameObjectPoolManger.Instance.ReturnObject(effect.Value.gameObject);
            // }
            _activeParticleSystems.Clear();
            foreach (var effect in _particleSystemsPrefab)
            {
                ResourceManager.Instance.UnloadResource(effect.Key.ToString());
            }
            _particleSystemsPrefab.Clear();
        }
    }

    public enum ParticlesType
    {
        Explode,
        AttackBuff,
        AttackDebuff,
        DefenseBuff,
        DefenseDebuff,
        Die,
        HitEffect,
        RecoverBuff,
        RecoverDebuff,
        Skill,
        Slash,
        Emit,
        StoneHit
    }

    public interface IEffectPlayer
    {
        void RpcPlayEffect(ParticlesType type);
    }
}