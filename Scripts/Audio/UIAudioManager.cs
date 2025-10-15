using System;
using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Audio
{
    public class UIAudioManager : Singleton<UIAudioManager>, IAudioManager
    {
        private AudioSource _musicAudioSource;
        private AudioSource _effectAudioSource;
        private GameObject _audioSourcePrefab;
        
        private readonly Dictionary<AudioMusicType, AudioClip> _audioClips = new Dictionary<AudioMusicType, AudioClip>();
        private readonly Dictionary<AudioEffectType, AudioClip> _effectAudioClips = new Dictionary<AudioEffectType, AudioClip>();
        private readonly Dictionary<UIAudioEffectType, AudioClip> _uiAudioClips = new Dictionary<UIAudioEffectType, AudioClip>();
        private readonly List<AudioSource> _activeAudioSources = new List<AudioSource>();
        public AudioManagerType AudioManagerType => AudioManagerType.UI;
        
        [Inject]
        private async void Init()
        {
            var clips = await ResourceManager.Instance.GetAudioGameClip(AudioManagerType.ToString());
            foreach (var clip in clips)
            {
                if (Enum.TryParse(clip.name, out AudioMusicType audioMusicType))
                {
                    _audioClips.Add(audioMusicType, clip);
                }
                else if (Enum.TryParse(clip.name, out AudioEffectType audioEffectType))
                {
                    _effectAudioClips.Add(audioEffectType, clip);
                }
                else if (Enum.TryParse(clip.name, out UIAudioEffectType uiAudioEffectType))
                {
                    _uiAudioClips.Add(uiAudioEffectType, clip);
                }
                else
                {
                    throw new Exception("AudioManager: AudioClip name is not valid.");
                }
            }
            _audioSourcePrefab = ResourceManager.Instance.GetResource<GameObject>(new ResourceData()
            {
                Name = "AudioSourcePrefab"
            });
        }

        private void OnDestroy()
        {
            _audioClips.Clear();
            _effectAudioClips.Clear();
            Object.Destroy(_musicAudioSource.gameObject);
            Object.Destroy(_effectAudioSource.gameObject);
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

        public void PlayUIEffect(UIAudioEffectType effectType)
        {
            if (_uiAudioClips.TryGetValue(effectType, out var clip))
            {
                var audioSourceObj = GameObjectPoolManger.Instance.GetObject(_audioSourcePrefab);
                var audioSource = audioSourceObj.GetComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.Play();
                _activeAudioSources.Add(audioSource);
                ReturnAudioSourceToPool(audioSourceObj, clip.length).Forget();
            }
            else
            {
                Debug.LogWarning("Effect clip not found: " + effectType);
            }
        }

        public void PlaySFX(AudioEffectType clipType, Vector3 position, Transform parent)
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