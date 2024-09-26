using UnityEngine;

namespace HotUpdate.Scripts.Audio
{
    public interface IAudioManager
    {
        AudioManagerType AudioManagerType { get; }
        void PlayMusic(AudioMusicType musicType);
        public void PlaySFXRpc(AudioEffectType clipType, Vector3 position, Transform parent);
        void StopMusic();
        void SetMusicVolume(float volume);
        void SetSFXVolume(float volume);
    }

    public enum AudioManagerType
    {
        UI,
        Game,
    }

    public enum AudioMusicType
    {
        Main,
        Menu,
    }

    public enum AudioEffectType
    {
        Explode,
        FootStep,
        Gem,
        Gold,
        Lighten,
        Rain,
        Thunder2,
        Thunder3,
        Thunder4,
    }
}
