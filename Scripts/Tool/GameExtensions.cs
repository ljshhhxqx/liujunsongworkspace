using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab.CloudScriptModels;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public static class GameExtensions
{
     /// <summary>
     /// 对字典进行增加或者更新操作
     /// 如果没有键，则直接添加键值对T1,T2所指代的元素
     /// 如果有键，则直接更新键所指示的值
     /// </summary>
     /// <param name="dictionary">字典</param>
     /// <param name="key">键</param>
     /// <param name="value">值</param>
     /// <typeparam name="T1">键类型</typeparam>
     /// <typeparam name="T2">值类型</typeparam>
     public static void AddOrUpdate<T1, T2>(this Dictionary<T1, T2> dictionary, T1 key, T2 value)
     {
          if (dictionary.ContainsKey(key))
          {
               dictionary[key] = value; // 如果键存在，更新对应的值
          }
          else
          {
               dictionary.Add(key, value); // 如果键不存在，添加键值对
          }
     }
     
     public static void InjectWithChildren(IObjectResolver resolver, GameObject root)
     {
          // 注入根对象
          resolver.Inject(root);

          // 遍历所有子物体并注入
          foreach (Transform child in root.transform)
          {
               InjectWithChildren(resolver, child.gameObject);
          }
     }

     public static void BindClick(this Button button, Func<UniTaskVoid> onClick)
     {
          button.onClick.AddListener(() => UniTask.Void(onClick));
     }
     
     // 有网络请求的扩展方法，使用回调函数处理结果
     public static void BindClickWithNetworkRequest<T>(this Button button, Func<UniTask<T>> onClick, Action<T> onResult = null, Action<Exception> onError = null)
     {
          button.onClick.AddListener(Call);
          return;

          async void Call()
          {
               try
               {
                    var result = await onClick();
                    onResult?.Invoke(result);
               }
               catch (Exception e)
               {
                    onError?.Invoke(e);
               }
          }
     }
     
     public static void BindDebouncedListener(this Button button, Action onClick, float debounceTime = 1f)
     {
          bool isProcessing = false;

          if (button == null || onClick == null)
          {
               Debug.LogWarning($"Button {button} or onClick event is null");
               return;
          }

          button.onClick.AddListener(Call);
          return;

          async void Call()
          {
               if (isProcessing) return;

               isProcessing = true;

               try
               {
                    // 执行点击事件
                    onClick.Invoke();

                    // 防抖动时间后允许再次点击
                    await UniTask.Delay((int)(debounceTime * 1000));
                    isProcessing = false;
               }
               finally
               {
                    isProcessing = false;
               }
          }
     }

     public static T ParseCloudScriptResultToData<T>(this object functionResult) 
     {
          try
          {
               // 将 functionResult 转换为 JSON 字符串
               var jsonString = JsonConvert.SerializeObject(functionResult);

               // 解析 JSON 字符串为 JObject
               var jsonResult = JObject.Parse(jsonString);
               Debug.Log($"CloudScript function executed successfully: {jsonResult["message"]}");
               return jsonResult.ToObject<T>();
          }
          catch (Exception e)
          {
               Console.WriteLine(e);
               throw;
          }
     }

     public static Dictionary<string, object> ParseCloudScriptResultToDic(this object functionResult)
     {
          try
          {
               // 将 functionResult 转换为 JSON 字符串
               var jsonString =  JsonConvert.SerializeObject(functionResult);
               
               // 解析 JSON 字符串为 JObject
               var jsonResult = JObject.Parse(jsonString);
               Debug.Log($"CloudScript function executed successfully: {jsonResult["message"]}");
               return jsonResult.ToObject<Dictionary<string, object>>();
          }
          catch (Exception e)
          {
               Console.WriteLine(e);
               throw;
          }
     }

     public static void DebugEntityCloudScriptResult(this ExecuteCloudScriptResult result)
     {
          // 打印日志信息
          Debug.Log("Logs:");
          foreach (var log in result.Logs)
          {
               Debug.Log($"[{log.Level}] {log.Message}");
          }
          // 打印错误信息（如果有）
          if (result.Error != null)
          {
               Debug.LogError($"Cloud Script returned an error: {result.Error.Error}");
               Debug.LogError($"Error Details: {result.Error.Message}");
               Debug.LogError($"Error StackTrace: {result.Error.StackTrace}");
          }

          // 打印返回值（如果有）
          if (result.FunctionResult != null)
          {
               Debug.Log($"Function Result: {result.FunctionResult}");
          }

          // 打印执行统计信息
          Debug.Log($"Execution Time: {result.ExecutionTimeSeconds} seconds");
          Debug.Log($"HTTP Request Used: {result.HttpRequestsIssued}");
          Debug.Log($"Revision: {result.Revision}");
     }

     public static void DebugCloudScriptLogs(this PlayFab.ClientModels.ExecuteCloudScriptResult result)
     {
          // 打印日志信息
          Debug.Log("Logs:");
          foreach (var log in result.Logs)
          {
               Debug.Log($"[{log.Level}] {log.Message}");
          }
          // 打印错误信息（如果有）
          if (result.Error != null)
          {
               Debug.LogError($"Cloud Script returned an error: {result.Error.Error}");
               Debug.LogError($"Error Details: {result.Error.Message}");
               Debug.LogError($"Error Details: {result.Error.StackTrace}");
          }

          // 打印返回值（如果有）
          if (result.FunctionResult != null)
          {
               Debug.Log($"Function Result: {result.FunctionResult}");
          }

          // 打印执行统计信息
          Debug.Log($"Execution Time: {result.ExecutionTimeSeconds} seconds");
          Debug.Log($"HTTP Request Used: {result.HttpRequestsIssued}");
          Debug.Log($"Revision: {result.Revision}");
     }

     public static void DebugCloudScriptResult(this object functionResult)
     {
          // 将 functionResult 转换为 JSON 字符串
          var jsonString = functionResult.ToString();

          // 解析 JSON 字符串为 JObject
          var jsonResult = JObject.Parse(jsonString);

          // 遍历键值对
          foreach (var kvp in jsonResult)
          {
               Debug.Log($"{kvp.Key}: {kvp.Value}");

               // 如果值是嵌套的 JObject
               if (kvp.Value is JObject nestedJson)
               {
                    foreach (var nestedKvp in nestedJson)
                    {
                         Debug.Log($"{nestedKvp.Key}: {nestedKvp.Value}");
                    }
               }

               // 如果值是数组
               if (kvp.Value is JArray jsonArray)
               {
                    foreach (var item in jsonArray)
                    {
                         Debug.Log($"  Array item: {item}");
                    }
               }
          }
     }
}