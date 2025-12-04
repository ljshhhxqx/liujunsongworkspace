using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Common
{
    public class ItemFillChanged : MonoBehaviour
    {
        [SerializeField]
        private Slider targetSlider;
        [SerializeField]
        private Image targetImage;

        [SerializeField] private bool showSlider;
        [SerializeField] private bool showImage;
        [SerializeField] private float duration = 0.2f;

        private Slider _slider;
        private Image _image;
        private Tween _tween;

        private void Start()
        {
            _slider = GetComponent<Slider>();
            if (showSlider)
            {
                targetSlider.OnValueChangedAsObservable().Subscribe(value =>
                {
                    _tween?.Kill(true);
                    _tween = _slider.DOValue(value, duration);
                }).AddTo(this);
            }
            else
            {
                _image = GetComponent<Image>();
                _image.fillAmount = targetImage.fillAmount;
            }
        }

        public void AnimationChangeValue()
        {
            if (showImage)
            {
                _tween?.Kill(true);
                _tween = _image.DOFillAmount(targetImage.fillAmount, duration);
            }
        }

        public void ChaneValue()
        {
            if (showImage)
            {
                _tween?.Kill(true);
                _image.fillAmount = targetImage.fillAmount;
            }
        }
    }
}