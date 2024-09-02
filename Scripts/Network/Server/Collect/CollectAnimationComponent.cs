using DG.Tweening;
using Sirenix.OdinInspector;
using Tool.Coroutine;
using UnityEngine;

namespace Network.Server.Collect
{
    public class CollectAnimationComponent : MonoBehaviour
    {
        private Renderer _outline;
        private RepeatedTask _repeatedTask;
        private Sequence _colorSequence;
        private Sequence _animationSequence;
        private Sequence _scaleSequence;
        private Color _originalColor;
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        public Color OutlineColorValue => _originalColor;

        private void Awake()
        {
            _outline = transform.Find("Outline").GetComponent<Renderer>();
            _originalColor = _outline.sharedMaterials[0].GetColor(OutlineColor);
        }

        [Button("播放所有动画")]
        public void Play()
        {
            PlayerColorChange(); 
            PlayAnimation();
        }

        [Button("播放颜色变换")]
        private void PlayerColorChange()
        {
            var mat = _outline.sharedMaterials[0];

            _colorSequence?.Kill();
            _colorSequence = DOTween.Sequence();
            _colorSequence.Append(DOTween.To(() => mat.GetColor(OutlineColor),
                x => mat.SetColor(OutlineColor, x), 
                new Color(Random.Range(0.6f, 1f), Random.Range(0.6f, 1f), Random.Range(0.6f, 1f)), 
                0.5f).SetEase(Ease.Linear));
            _colorSequence.Append(DOTween.To(() => mat.GetColor(OutlineColor),
                x => mat.SetColor(OutlineColor, x), 
                _originalColor, 
                0.5f).SetEase(Ease.Linear));
            _colorSequence.SetEase(Ease.Linear);

            _colorSequence.SetLoops(-1); 
        }

        [Button("播放旋转缩放动画")]
        private void PlayAnimation()
        {
            _animationSequence?.Kill();
            _animationSequence = DOTween.Sequence();
            _scaleSequence?.Kill();
            _scaleSequence = DOTween.Sequence();
            _animationSequence.Append(transform.DORotate(new Vector3(0, 360, 90), 1.5f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental));
            _scaleSequence.Append(transform.DOScale(new Vector3(0.85f, 0.85f, 0.85f), 0.75f)
                .SetEase(Ease.Linear));
            _scaleSequence.Append(transform.DOScale(Vector3.one, 0.75f)
                .SetEase(Ease.Linear));
            _animationSequence.SetEase(Ease.Linear);
            _animationSequence.SetLoops(-1);  
            _scaleSequence.SetEase(Ease.Linear);
            _scaleSequence.SetLoops(-1);
        }

        [Button("停止所有动画")]
        private void KillAll()
        {
            var mat = _outline.sharedMaterials[0];
            transform.localRotation = Quaternion.Euler(0,0,90);
            transform.localScale = Vector3.one;
            mat.SetColor(OutlineColor, _originalColor);
            _colorSequence?.Kill();
            _animationSequence?.Kill();
        }
        
        
        private void OnDestroy()
        {
            _colorSequence?.Kill();
            _animationSequence?.Kill();
        }
    }
}
