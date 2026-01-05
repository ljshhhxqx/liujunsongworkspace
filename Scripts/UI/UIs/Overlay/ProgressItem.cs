using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class ProgressItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI progressText;
        [SerializeField]
        private Image progressImage;
        
        private Sequence _progressTween;

        public void SetProgress(string text, float countdownTime, Action callback, Func<bool> condition)
        {
            progressText.text = text;
            progressImage.fillAmount = 01f;
            _progressTween?.Kill();
            _progressTween = DOTween.Sequence();
            _progressTween.Append(progressImage.DOFillAmount(0, countdownTime));
            _progressTween.Join(progressText.DOFade(0f, 0.25f).SetLoops(int.MaxValue, LoopType.Yoyo));
            _progressTween.OnUpdate(() =>
            {
                if (condition != null && !condition.Invoke())
                {
                    _progressTween?.Kill();
                    gameObject.SetActive(false);
                }
            });
            _progressTween.OnComplete(() =>
            {
                callback?.Invoke();
                gameObject.SetActive(false);
            });
            _progressTween.SetLink(gameObject);
        }
    }
}