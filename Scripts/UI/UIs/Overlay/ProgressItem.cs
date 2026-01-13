using System;
using DG.Tweening;
using Sirenix.OdinInspector;
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

        [Button]
        private void TestProgressTween()
        {
            SetProgress("Test11111", 1f, null, null);
        }

        public void SetProgress(string text, float countdownTime, Action callback, Func<bool> condition)
        {
            transform.localScale = Vector3.one;
            Debug.Log("ProgressItem set progress to " + text + $"transform.localScale = {transform.localScale.ToString()}");
            progressText.text = text;
            progressText.alpha = 1f;
            progressImage.fillAmount = 01f;
            _progressTween?.Kill();
            _progressTween = DOTween.Sequence();
            _progressTween.Append(progressImage.DOFillAmount(0, countdownTime).SetEase(Ease.Linear));
            _progressTween.Join(progressText.DOFade(0f, 0.5f).SetLoops((int) (countdownTime / 0.5f), LoopType.Yoyo).SetEase(Ease.Linear));
            _progressTween.OnUpdate(() =>
            {
                if (condition != null && !condition.Invoke())
                {
                    Debug.Log("ProgressItem condition not met, stopping tween");
                    _progressTween?.Kill();
                    transform.localScale = Vector3.zero;
                }
            });
            _progressTween.OnComplete(() =>
            {
                Debug.Log(" ProgressItem complete, calling callback");
                callback?.Invoke();
                transform.localScale = Vector3.zero;
            });
        }
    }
}