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

        private Slider _slider;
        private Tween _tween;

        private void Start()
        {
            _slider = GetComponent<Slider>();
            targetSlider.OnValueChangedAsObservable().Subscribe(value =>
            {
                _tween?.Kill(true);
                _slider.DOValue(value, 0.2f);
            }).AddTo(this);
        }
        
    }
}