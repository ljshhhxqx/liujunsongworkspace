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
        None,
        Main,
        Game,
        Rainy,
        Sunny,
        Cloudy,
        Snowy,
    }

    public enum AudioEffectType
    {
        None,
        Gem,
        Gold,
        Drug,
        Chest,
        
        Attack,
        Heal,
        Damage,
        Buff,
        Debuff,
        Control,
        Roll,
        FootStep,
        Sprint,
        Jump,
        Die,
        Hurt,
        
        Explode,
        Lighten,
        Thunder,
        Rain,
        
        Start,
    }

    public enum UIAudioEffectType
    {
        Click,
        Confirm,
        Cancel,
        Error,
        Notification,
        Bag,
        Warning,
        
        Win,
        Lose,
    }
}
