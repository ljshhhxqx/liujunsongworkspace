using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class CollectAnimationComponent : MonoBehaviour
    {
        private Sequence _colorSequence;
        private Sequence _animationSequence;
        private Sequence _scaleSequence;
        private Color _originalColor;
        public Color OutlineColorValue => _originalColor;

        // public void SetOutlineColor(Color color)
        // {
        //     _outline.sharedMaterials[0].SetColor(OutlineColor, color);
        // }

        [Button("播放所有动画")]
        public void Play()
        {
            KillAll();
            PlayAnimation();
        }

        [Button("播放旋转缩放动画")]
        private void PlayAnimation()
        {
            _animationSequence?.Kill();
            _animationSequence = DOTween.Sequence();
            _scaleSequence?.Kill();
            _scaleSequence = DOTween.Sequence();
            _animationSequence.Append(transform.DORotate(new Vector3(0, 360, 0), 2f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(int.MaxValue, LoopType.Incremental));
            _scaleSequence.Append(transform.DOScale(new Vector3(0.65f, 0.65f, 0.65f), 1f)
                .SetEase(Ease.Linear));
            _scaleSequence.Append(transform.DOScale(Vector3.one, 1f)
                .SetEase(Ease.Linear));
            _animationSequence.SetEase(Ease.Linear);
            _animationSequence.SetLoops(int.MaxValue);  
            _scaleSequence.SetEase(Ease.Linear);
            _scaleSequence.SetLoops(int.MaxValue);
        }

        [Button("停止所有动画")]
        private void KillAll()
        {
            _scaleSequence?.Kill();
            _colorSequence?.Kill();
            _animationSequence?.Kill();
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            KillAll();
        }
    }
}
