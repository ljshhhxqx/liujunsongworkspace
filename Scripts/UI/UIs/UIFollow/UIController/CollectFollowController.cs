using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.UIController
{
    public class CollectFollowController : FollowedUIController, IUIController
    {
        [SerializeField] private Slider hp;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        public override void BindToModel(IUIDataModel model)
        {
            if (model is InfoDataModel infoDataModel)
            {
                infoDataModel.Name.Subscribe(n => nameText.text = n).AddTo(this);
                infoDataModel.Health.Subscribe(h =>
                {
                    hpText.text = $"{h}/{infoDataModel.MaxHealth.Value}";
                    hp.value = h / infoDataModel.MaxHealth.Value;
                }).AddTo(this);
                infoDataModel.MaxHealth.Subscribe(m =>
                {
                    hpText.text = $"{infoDataModel.Health.Value}/{m}";
                    hp.maxValue = m;
                }).AddTo(this);
                return;
            }
            Debug.LogError("BindToModel error" + model.GetType());
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