using System;
using AOTScripts.Tool.Coroutine;
using DG.Tweening;
using HotUpdate.Scripts.Tool.ReactiveProperty;
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
        private HReactiveProperty<float> FillAmount { get; } = new HReactiveProperty<float>();

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
                RepeatedTask.Instance.StartRepeatingTask(OnFillAmountChanged, 0.1f);
                FillAmount.Subscribe(f =>
                {
                    _tween?.Kill(true);
                    _tween = _image.DOFillAmount(f, duration);
                })
                .AddTo(this);
                FillAmount.Value = targetImage.fillAmount;
            }
        }

        private void OnFillAmountChanged()
        {
            if (showImage)
            {
                if (!Mathf.Approximately(FillAmount.Value, targetImage.fillAmount))
                {
                    FillAmount.Value = targetImage.fillAmount;
                }
            }
        }

        private void OnDestroy()
        {
            RepeatedTask.Instance.StopRepeatingTask(OnFillAmountChanged);
        }
    }
}