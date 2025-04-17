using System;
using HotUpdate.Scripts.UI.UIBase;
using PlayFab;
using PlayFab.CloudScriptModels;
using UI.UIBase;
using VContainer;

namespace Network.Server.PlayFab
{
    public class PlayFabClientCloudScriptCaller: IPlayFabClientCloudScriptCaller
    {
        [Inject] private UIManager _uiManager;

        public void ExecuteCloudScript(ExecuteEntityCloudScriptRequest request,
            Action<ExecuteCloudScriptResult> successCallback,
            Action<PlayFabError> errorCallback)
        {
            _uiManager.SwitchLoadingPanel(true);
            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, success =>
            {
                _uiManager.SwitchLoadingPanel(false);
                if (success.Error != null)
                {
                    throw new Exception($"{success.Error.Error}-${success.Error.Message}-${success.Error.StackTrace}");
                }
                successCallback?.Invoke(success);
            }, error =>
            {
                _uiManager.SwitchLoadingPanel(false);
                errorCallback?.Invoke(error);
            });
        }
    }

    public interface IPlayFabClientCloudScriptCaller
    {
        void ExecuteCloudScript(ExecuteEntityCloudScriptRequest request,
            Action<ExecuteCloudScriptResult> successCallback,
            Action<PlayFabError> errorCallback);
    }
}