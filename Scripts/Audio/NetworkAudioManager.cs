using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Audio
{
    public class NetworkAudioManager : ServerNetworkComponent, IAudioManager
    {
        private AudioSource _musicAudioSource;
        private AudioSource _effectAudioSource;
        private GameObject _audioSourcePrefab;
        
        private readonly Dictionary<AudioMusicType, AudioClip> _audioClips = new Dictionary<AudioMusicType, AudioClip>();
        private readonly Dictionary<AudioEffectType, AudioClip> _effectAudioClips = new Dictionary<AudioEffectType, AudioClip>();
        private readonly List<AudioSource> _activeAudioSources = new List<AudioSource>();
        public AudioManagerType AudioManagerType => AudioManagerType.Game;

        [Inject]
        private void Init()
        {
            GetAudioClipAsync().Forget();
        }

        private async UniTask GetAudioClipAsync() 
        {
            var operation = await ResourceManager.Instance.GetAudioGameClip(AudioManagerType.ToString());
            foreach (var clip in operation)
            {
                if (Enum.TryParse(clip.name, out AudioMusicType audioMusicType))
                {
                    _audioClips.Add(audioMusicType, clip);
                }
                else if (Enum.TryParse(clip.name, out AudioEffectType audioEffectType))
                {
                    _effectAudioClips.Add(audioEffectType, clip);
                }
                else
                {
                    throw new Exception("AudioManager: AudioClip name is not valid.");
                }
            }

            var audioRes = DataJsonManager.Instance.GetResourceData("AudioGameEffectPrefab");
            _audioSourcePrefab = await ResourceManager.Instance.LoadResourceAsync<GameObject>(audioRes);
        }

        private void OnDestroy()
        {
            _audioClips.Clear();
            _effectAudioClips.Clear();
            Object.Destroy(_musicAudioSource?.gameObject);
            Object.Destroy(_effectAudioSource?.gameObject);
        }
        
        public void PlayMusic(AudioMusicType musicType)
        {
            if (_audioClips.TryGetValue(musicType, out var clip))
            {
                _musicAudioSource.clip = clip;
                _musicAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("Music clip not found: " + musicType);
            }
        }

        [Command]
        public void CmdPlayMusic(AudioMusicType musicType)
        {
            PlayMusic(musicType);
        }

        [Command]
        public void CmdPlaySFX(AudioEffectType clipType, Vector3 position, Transform parent)
        {
            PlaySFXRpc(clipType, position, parent);
        }
        
        [ClientRpc]
        public void PlaySFXRpc(AudioEffectType clipType, Vector3 position, Transform parent)
        {
            if (_effectAudioClips.TryGetValue(clipType, out var clip))
            {
                var audioSourceObj = GameObjectPoolManger.Instance.GetObject(_audioSourcePrefab, position, Quaternion.identity, parent);
                var audioSource = audioSourceObj.GetComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.Play();
                _activeAudioSources.Add(audioSource);
                ReturnAudioSourceToPool(audioSourceObj, clip.length).Forget();
            }
            else
            {
                Debug.LogWarning("SFX clip not found: " + clipType);
            }
        }

        private async UniTask ReturnAudioSourceToPool(GameObject audioSourceObj, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));//new WaitForSeconds(delay);
            if (audioSourceObj != null && audioSourceObj.activeInHierarchy)
            {
                GameObjectPoolManger.Instance.ReturnObject(audioSourceObj);
                _activeAudioSources.Remove(audioSourceObj.GetComponent<AudioSource>());
            }
        }

        public void StopMusic()
        {
            _musicAudioSource.Stop();
        }

        public void SetMusicVolume(float volume)
        {
            _musicAudioSource.volume = volume;
        }

        public void SetSFXVolume(float volume)
        {
            foreach (var source in _activeAudioSources)
            {
                source.volume = volume;
            }
        }
    }
}
