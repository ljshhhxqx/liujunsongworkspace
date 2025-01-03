using DG.Tweening;
using Sirenix.OdinInspector;
using Tool.Coroutine;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Network.Server.Collect
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

        public void SetOutlineColor(Color color)
        {
            _outline.sharedMaterials[0].SetColor(OutlineColor, color);
        }

        [Button("播放所有动画")]
        public void Play()
        {
            KillAll();
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
            _animationSequence.Append(transform.DORotate(new Vector3(0, 360, 0), 2f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental));
            _scaleSequence.Append(transform.DOScale(new Vector3(0.65f, 0.65f, 0.65f), 1f)
                .SetEase(Ease.Linear));
            _scaleSequence.Append(transform.DOScale(Vector3.one, 1f)
                .SetEase(Ease.Linear));
            _animationSequence.SetEase(Ease.Linear);
            _animationSequence.SetLoops(-1);  
            _scaleSequence.SetEase(Ease.Linear);
            _scaleSequence.SetLoops(-1);
        }

        [Button("停止所有动画")]
        private void KillAll()
        {
            _scaleSequence?.Kill();
            _colorSequence?.Kill();
            _animationSequence?.Kill();
            var mat = _outline.sharedMaterials[0];
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            mat.SetColor(OutlineColor, _originalColor);
        }

        private void OnDestroy()
        {
            KillAll();
        }
    }
}
