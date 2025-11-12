using DG.Tweening;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class HiddenItem : CollectBehaviour, IPoolable
    {
        private bool _isHidden;
        private float _translucence;
        private HideType _hideType;
        private float _mysteryTime;
        private float _translucenceTime;
        
        private Sequence _sequence;
    
        public void HideItem()
        {
            _isHidden = true;
            SetEnabled(false);
            // Collider保持启用，仍然可以交互
        }
    
        public void RevealItem()
        {
            _isHidden = false;
            SetEnabled(true);
        }
        

        protected override void OnInitialize()
        {
            
        }

        public void Init(HideType hideType, float translucence, float mysteryTime, float translucenceTime)
        {
            _hideType = hideType;
            _translucence = translucence;
            _mysteryTime = mysteryTime;
            _translucenceTime = translucenceTime;
            switch (hideType)
            {
                case HideType.Inactive:
                    HideItem();
                    break;
                case HideType.Mystery:
                    MysteryItem();
                    break;
                case HideType.Translucence:
                    TranslucenceItem();
                    break;
            }
        }

        private void TranslucenceItem()
        {
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.red);
            });
            _sequence.AppendInterval(_translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.yellow);
            });
            _sequence.AppendInterval(_translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.green);
            });
            _sequence.AppendInterval(_translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.cyan);
            });
            _sequence.AppendInterval(_translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.blue);
            });
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.white);
            });
            _sequence.SetLoops(int.MaxValue, LoopType.Yoyo);
        }

        private void MysteryItem()
        {
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.AppendCallback(() =>
            {
                SetEnabled(false);
            });
            _sequence.AppendInterval(_mysteryTime);
            _sequence.AppendCallback(() =>
            {
                SetEnabled(true);
            });
            _sequence.SetLoops(int.MaxValue, LoopType.Restart);
        }

        public void OnSelfSpawn()
        {
            HideItem();
        }

        public void OnSelfDespawn()
        {
            _sequence?.Kill();
            
            SetEnabled(true);
        }
    }

    public enum HideType
    {
        //完全消失
        Inactive,
        //不可预知，一会消失，一会出现
        Mystery,
        //透明度低于50%
        Translucence,
    }
}