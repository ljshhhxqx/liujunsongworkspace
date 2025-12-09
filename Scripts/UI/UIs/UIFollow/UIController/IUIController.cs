using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;

namespace HotUpdate.Scripts.UI.UIs.UIFollow.UIController
{
    public interface IUIController
    {
        void BindToModel(IUIDataModel uiDataModel);
        void UnBindFromModel(IUIDataModel uiDataModel);
    }
}