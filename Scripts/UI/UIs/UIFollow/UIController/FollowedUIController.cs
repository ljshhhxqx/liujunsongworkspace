using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using HotUpdate.Scripts.UI.UIs.WorldUI;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.UIController
{
    public abstract class FollowedUIController : MonoBehaviour, IUIController
    {
        public WorldUIType worldUIType;
        protected UIFollower UIFollower;
        public abstract void BindToModel(IUIDataModel uiDataModel);
        public abstract void UnBindFromModel(IUIDataModel uiDataModel);
        public uint SceneId { get; private set; }

        private void Awake()
        {
            ObjectInjectProvider.Instance.Inject(this);
            if (!UIFollower)
            {
                if (!TryGetComponent(out UIFollower))
                {
                    UIFollower = gameObject.AddComponent<UIFollower>();
                }
            }
        }

        public virtual void InitFollowedInstance(GameObject go, uint sceneId, Transform playerTransform, Camera uiCamera)
        {
            UIFollower?.Initialize(go, uiCamera, playerTransform);
            SceneId = sceneId;
        }
    }
}