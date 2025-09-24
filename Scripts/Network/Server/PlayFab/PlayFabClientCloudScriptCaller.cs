using System;
using HotUpdate.Scripts.UI.UIBase;
using PlayFab;
using PlayFab.CloudScriptModels;
using VContainer;
using ExecuteCloudScriptResult = PlayFab.CloudScriptModels.ExecuteCloudScriptResult;

namespace HotUpdate.Scripts.Network.Server.PlayFab
{
    public class PlayFabClientCloudScriptCaller: IPlayFabClientCloudScriptCaller
    {
        [Inject] private UIManager _uiManager;

        public void ExecuteCloudScript(ExecuteEntityCloudScriptRequest request,
            Action<ExecuteCloudScriptResult> successCallback,
            Action<PlayFabError> errorCallback,
            bool showUI = true)
        {
            if (showUI)
                _uiManager.SwitchLoadingPanel(true);
            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, success =>
            {
                if (showUI)
                    _uiManager.SwitchLoadingPanel(false);
                if (success.Error != null)
                {
                    throw new Exception($"{success.Error.Error}-${success.Error.Message}-${success.Error.StackTrace}");
                }
                successCallback?.Invoke(success);
            }, error =>
            {
                if (showUI)
                    _uiManager.SwitchLoadingPanel(false);
                errorCallback?.Invoke(error);
            });
        }
    }

    public interface IPlayFabClientCloudScriptCaller
    {
        void ExecuteCloudScript(ExecuteEntityCloudScriptRequest request,
            Action<ExecuteCloudScriptResult> successCallback,
            Action<PlayFabError> errorCallback, bool showUI = true);
    }
}