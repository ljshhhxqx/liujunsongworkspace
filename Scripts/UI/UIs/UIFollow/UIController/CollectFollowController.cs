using DG.Tweening;
using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.UIController
{
    public class CollectFollowController : FollowedUIController, IUIController
    {
        [SerializeField] private Image hp;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private CanvasGroup canvasGroup;
        private Sequence _tween;
        
        public override void BindToModel(IUIDataModel model)
        {
            if (model is InfoDataModel infoDataModel)
            {
                infoDataModel.Name.Subscribe(n =>
                {
                    nameText.text = n;
                    DoAnimationTween();
                }).AddTo(this);
                infoDataModel.Health.Subscribe(h =>
                {
                    hpText.text = $"{h}/{infoDataModel.MaxHealth.Value}";
                    hp.fillAmount = h / (float)infoDataModel.MaxHealth.Value;
                    if (h <= 0)
                    {
                        DoFadeOutAnimation();
                        return;
                    }
                    DoAnimationTween();
                }).AddTo(this);
                infoDataModel.MaxHealth.Subscribe(m =>
                {
                    hpText.text = $"{infoDataModel.Health.Value}/{m}";
                    hp.fillAmount = infoDataModel.Health.Value / (float)m;
                    DoAnimationTween();
                }).AddTo(this);
                return;
            }
            Debug.LogError("BindToModel error" + model.GetType());
        }

        private void DoFadeOutAnimation()
        {
            canvasGroup.alpha = 1;
            _tween?.Kill();
            _tween = DOTween.Sequence();
            _tween.AppendInterval(0.1f);
            _tween.Append(canvasGroup.DOFade(0, 0.15f));
            _tween.AppendInterval(0.1f);
            _tween.Append(canvasGroup.DOFade(1, 0.15f));
            _tween.SetLoops(3);
            _tween.OnComplete(() =>
            {
                canvasGroup.alpha = 0;
            });
        }

        private void DoAnimationTween()
        {
            canvasGroup.alpha = 1;
            _tween?.Kill();
            _tween = DOTween.Sequence();
            _tween.AppendInterval(2F);
            _tween.Append(canvasGroup.DOFade(0, 1));
        }

        public override void UnBindFromModel(IUIDataModel model)
        {
            if (model is InfoDataModel infoDataModel)
            {
                infoDataModel.Name.Dispose();
                infoDataModel.Health.Dispose();
                infoDataModel.MaxHealth.Dispose();
                infoDataModel.Mana.Dispose();
                infoDataModel.MaxMana.Dispose();
                return;
            }
            Debug.LogError("UnBindFromModel error" + model.GetType());
        }
    }
}