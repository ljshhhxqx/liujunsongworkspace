using System;
using HotUpdate.Scripts.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Common
{
    [RequireComponent(typeof(Button))]
    public class SoundButton : MonoBehaviour
    {
        [SerializeField]
        private Button button;
        [SerializeField]
        private UIAudioEffectType soundName;

        private void Start()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            UIAudioManager.Instance.PlayUIEffect(soundName);
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(OnClick);
        }
    }
}
