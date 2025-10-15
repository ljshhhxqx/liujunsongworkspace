using UnityEngine;

namespace HotUpdate.Scripts.Audio
{
    public interface IAudioManager
    {
        AudioManagerType AudioManagerType { get; }
        void PlayMusic(AudioMusicType musicType);
        public void PlaySFX(AudioEffectType clipType, Vector3 position, Transform parent);
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
        Rainy,
        Sunny,
        Cloudy,
        Snowy,
    }

    public enum AudioEffectType
    {
        Gem,
        Gold,
        Drug,
        Chest,
        
        Attack,
        Skill1,
        Skill2,
        Roll,
        FootStep,
        Jump,
        Die,
        Hurt,
        
        Explode,
        Lighten,
        Thunder,
        
        Win,
        Lose,
    }

    public enum UIAudioEffectType
    {
        Click,
        Confirm,
        Cancel,
        Error,
        Notification,
    }
}
