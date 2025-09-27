using System;
using AOTScripts.Tool;
using HotUpdate.Scripts.UI.UIBase;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;
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
                    // var message = success.ParseCloudScriptResultToDic();
                    // foreach (var key in message)
                    // {
                    //     Debug.LogError(key + " : " + message[key.Key]);
                    // }
                    foreach (var key in success.Logs)
                    {
                        Debug.Log("Log: " + key.Message + " : " + key.Data );
                    }
                    Debug.LogError($"{success.Error.Error}-${success.Error.Message}-${success.Error.StackTrace}");
                    return;
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