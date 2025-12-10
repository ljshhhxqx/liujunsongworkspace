using DG.Tweening;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.UIController
{
    public class PlayerHpFollowController: FollowedUIController, IUIController
    {
        [SerializeField] private Slider hp;
        [SerializeField] private Slider mp;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI mpText;
        [SerializeField] private CanvasGroup canvasGroup;
        private Sequence _tween;

        private void DoTween()
        {
            canvasGroup.alpha = 1;
            _tween?.Kill();
            _tween = DOTween.Sequence();
            _tween.AppendInterval(1.5F);
            _tween.Append(canvasGroup.DOFade(0, 0.5f));
        }

        public override void BindToModel(IUIDataModel model)
        {
            if (model is InfoDataModel infoDataModel)
            {
                infoDataModel.Name.Subscribe(n =>
                {
                    nameText.text = n;
                    DoTween();
                }).AddTo(this);
                
                infoDataModel.Health.Subscribe(h =>
                {
                    hpText.text = $"{h}/{infoDataModel.MaxHealth.Value}";
                    hp.value = h / infoDataModel.MaxHealth.Value;
                    DoTween();
                }).AddTo(this);
                infoDataModel.MaxHealth.Subscribe(m =>
                {
                    hpText.text = $"{infoDataModel.Health.Value}/{m}";
                    hp.value = infoDataModel.Health.Value / m;
                    DoTween();
                }).AddTo(this);
                
                infoDataModel.Mana.Subscribe(m =>
                {
                    mpText.text = $"{m}/{infoDataModel.MaxMana.Value}";
                    mp.value = m / infoDataModel.MaxMana.Value;
                    DoTween();
                }).AddTo(this);
                infoDataModel.MaxMana.Subscribe(m =>
                {
                    mpText.text = $"{infoDataModel.Mana.Value}/{m}";
                    mp.value = infoDataModel.Mana.Value / m;
                    DoTween();
                }).AddTo(this);
                return;
            }
            Debug.LogError("PlayerHpFollowController BindToModel not implemented" + model.ToString());
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
            Debug.LogError("PlayerHpFollowController UnBindFromModel not implemented" + model.ToString());
        }
    }
}