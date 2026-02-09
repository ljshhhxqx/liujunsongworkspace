using AOTScripts.Tool;
using AOTScripts.Tool.Resource;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class EntityObjectTest : ScreenUIBase
{
    // 绑定UI（可选，用于显示结果）
    public TMP_InputField objectKeyInput; // 输入实体对象的键（如 "PlayerSettings"）
    public TMP_InputField playerNameInput; // 输入要存储的玩家名称
    public TMP_InputField playerLevelInput; // 输入要存储的玩家等级
    public TextMeshProUGUI resultText; // 显示存/取结果
    public Button objectKeySaveConfirm;
    public Button objectKeyGetConfirm;

    // 【按钮点击】存储实体对象
    public void OnSetEntityObjectClick()
    {
        // 1. 获取UI输入（或直接硬编码测试）
        string objectKey = objectKeyInput.text.Trim();
        if (string.IsNullOrEmpty(objectKey))
        {
            ShowResult("错误：请输入 Object Key（如 PlayerSettings）");
            return;
        }

        // 2. 构造要存储的JSON数据（自定义结构，支持任意可序列化类型）
        var playerData = new
        {
            PlayerName = playerNameInput.text.Trim(),
            PlayerLevel = int.TryParse(playerLevelInput.text.Trim(), out int level) ? level : 1,
            LastSaveTime = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") // UTC时间
        };

        // 3. 调用云脚本的 SetEntityObjectData 函数
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "SetEntityObjectData", // 云脚本中定义的handler名称
            FunctionParameter = new // 传入云脚本的参数（与云脚本 args 对应）
            {
                objectKey = objectKey,
                objectData = playerData
            },
            GeneratePlayStreamEvent = true // 可选，生成PlayStream事件用于调试
        }, OnSetSuccess, OnError);
    }

    // 【按钮点击】读取实体对象
    public void OnGetEntityObjectClick()
    {
        // 1. 获取UI输入的 Object Key
        string objectKey = objectKeyInput.text.Trim();
        if (string.IsNullOrEmpty(objectKey))
        {
            ShowResult("错误：请输入 Object Key（如 PlayerSettings）");
            return;
        }

        // 2. 调用云脚本的 GetEntityObjectData 函数
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName = "GetEntityObjectData", // 云脚本中定义的handler名称
            FunctionParameter = new // 传入云脚本的参数
            {
                objectKey = objectKey
            }
        }, OnGetSuccess, OnError);
    }

    // 存储成功的回调
    private void OnSetSuccess(ExecuteCloudScriptResult result)
    {
        // 解析云脚本返回的结果（JSON → 动态对象）
        var resultData = result.ParseCloudScriptResultToDic();
        if (resultData.TryGetValue("success", out var success) && success.ToString().ToLower() == "true")
        {
            string message = resultData["message"].ToString();

            string storedData = JsonUtility.ToJson(resultData["storedData"]);
            ShowResult($"存储成功！\n消息：{message}\n存储的数据：{storedData}");
        }
    }

    // 读取成功的回调
    private void OnGetSuccess(ExecuteCloudScriptResult result)
    {
        var resultData = result.ParseCloudScriptResultToDic();
        if (resultData.TryGetValue("success", out var success) && success.ToString().ToLower() == "true")
        {
            string message = resultData["message"].ToString();
            // 解析存储的数据（动态对象 → 自定义类，或直接读取字段）
            var storedData = (PlayFab.Json.JsonObject)resultData["storedData"];
            string playerName = storedData["PlayerName"].ToString();
            int playerLevel = int.Parse(storedData["PlayerLevel"].ToString());
            string lastSaveTime = storedData["LastSaveTime"].ToString();

            Debug.Log($"读取成功！\n消息：{message}\n玩家名称：{playerName}\n玩家等级：{playerLevel}\n最后保存时间：{lastSaveTime}");

        }
    }

    // API调用错误的回调
    private void OnError(PlayFabError error)
    {
        ShowResult($"API调用错误！\n错误码：{error.Error}\n错误消息：{error.ErrorMessage}\n详细信息：{error.ErrorDetails}");
    }

    // 显示结果到UI
    private void ShowResult(string content)
    {
        resultText.text = content;
        Debug.Log(content); // 同时打印到控制台
    }

    // 初始化PlayFab（需在启动时调用，如Awake或Start）
    private void Start()
    {
        // 设置TitleId（替换为你的PlayFab TitleId）
        Debug.Log("PlayFab TitleId：" + PlayFabSettings.TitleId);
        objectKeySaveConfirm.onClick.AddListener(OnSetEntityObjectClick);
        objectKeyGetConfirm.onClick.AddListener(OnGetEntityObjectClick);
    }

    public override UIType Type => UIType.EntityObjectTest;
    public override UICanvasType CanvasType => UICanvasType.SecondPanel;
}