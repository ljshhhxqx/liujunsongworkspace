using System;
using HotUpdate.Scripts.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Common
{
    [RequireComponent(typeof(Button))]
    public class SoundButton : MonoBehaviour
    {
        private Button _button;
        [SerializeField]
        private UIAudioEffectType soundName;

        private void Start()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            UIAudioManager.Instance.PlayUIEffect(soundName);
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveListener(OnClick);
        }
    }
}
