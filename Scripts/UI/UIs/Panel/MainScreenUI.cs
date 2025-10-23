using System;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.UniRxTool;
using Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using TMPro;
using UI.UIBase;
using UI.UIs;
using UI.UIs.SecondPanel;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class MainScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabRoomManager _playFabRoomManager;
        private PlayFabAccountManager _playFabAccountManager;
        [SerializeField]
        private Button matchButton;
        [SerializeField]
        private Button createRoomButton;
        [SerializeField]
        private Button joinRoomButton;
        [SerializeField]
        private Button helpButton;
        [SerializeField]
        private Button logoutButton;
        [SerializeField]
        private Button quitButton;
        [SerializeField]
        private Button infoButton;
        [SerializeField]
        private Button friendButton;
        [SerializeField] 
        private TextMeshProUGUI timerText;
        [SerializeField] 
        private TextMeshProUGUI infoText;
        [SerializeField] 
        private TextMeshProUGUI idText;
        [SerializeField] 
        private TextMeshProUGUI nameText;
        private float _timer;
        private RepeatedTask _repeatedTask;
        private TimeSpan _timeSpan;
        private string _idTitle;
        private string _nameTitle;
        public override UIType Type => UIType.Main;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager, PlayFabAccountManager playFabAccountManager)
        {
            _idTitle = idText.text;
            _nameTitle = nameText.text;
            _uiManager = uiManager;
            _playFabRoomManager = playFabRoomManager;
            _repeatedTask = RepeatedTask.Instance;
            _playFabAccountManager = playFabAccountManager;
            _playFabRoomManager.OnMatchmakingChanged += OnMatchmakingChanged;
            matchButton.BindDebouncedListener(OnMatchButtonClick);
            createRoomButton.BindDebouncedListener(OnCreateRoomClick);
            helpButton.BindDebouncedListener(OnHelpButtonClick);
            joinRoomButton.BindDebouncedListener(OnJoinRoomClick);
            infoButton.BindDebouncedListener(OnInfoButtonClick);
            logoutButton.BindDebouncedListener(OnLogoutButtonClick);
            quitButton.BindDebouncedListener(OnQuitButtonClick);
            friendButton.BindDebouncedListener(OnFriendButtonClick);
            Debug.Log("MainScreenUI Init");
            // ReactivePropertySpecificTests.RunReactivePropertyTests();
            //ReactivePropertyDiagnosticTests.RunAllTests();
            HReactiveProperty<int> test = new HReactiveProperty<int>();
            test.Subscribe(value =>
            {
                Debug.Log($"Test: {value}");
            });
            test.Value = 10;
            Debug.Log("testData Init");
            HReactiveProperty<TestData> internalData = new HReactiveProperty<TestData>();
            internalData.Subscribe(value =>
            {
                Debug.Log($"PlayerId: {value.id}");
            });
            internalData.Value = new TestData() { id = 13213, value = "test" };
            Debug.Log("PlayerInternalData Init");
            PlayFabData.PlayerReadOnlyData.Subscribe(value =>
            {
                Debug.Log($"PlayerId: {value.PlayerId}, Nickname: {value.Nickname}");
                idText.text = _idTitle + value.PlayerId;
                nameText.text = _nameTitle + value.Nickname;
            })
            .AddTo(this);
        }

        private void OnPlayerDataTest<T>(T data)
        {
            if (data is PlayerInternalData internalData)
            {
                Debug.Log($"PlayerDataTest: {internalData}");
            }
        }

        private void ActionTest<T>(Action<T> action, T param)
        {
            action?.Invoke(param);
        }

        private void OnFriendButtonClick()
        {
            _uiManager.SwitchUI<FriendScreenUI>();
        }

        private void OnInfoButtonClick()
        {
            _uiManager.SwitchUI<PlayerInfoScreenUI>();
        }

        private void OnQuitButtonClick()
        {
            _playFabAccountManager.Logout(Application.Quit);
        }

        private void OnLogoutButtonClick()
        {
            _playFabAccountManager.Logout(() =>
            {
                _uiManager.SwitchUI<LoginScreenUI>();
            });
        }

        private void OnMatchmakingChanged(bool obj)
        {
            if (!_playFabRoomManager.IsMatchmaking)
            {
                infoText.text = "取消匹配";
                timerText.gameObject.SetActive(true);
                _repeatedTask.StartRepeatingTask(UpdateMatchmakingInfo, 1f);
            }
            else
            {
                _repeatedTask.StopRepeatingTask(UpdateMatchmakingInfo);             
                infoText.text = "匹配对战";
                timerText.text = _timeSpan.ToString("00:00");
                timerText.gameObject.SetActive(false);
            }
            _timer = 0;
        }

        private void OnJoinRoomClick()
        {
            _uiManager.SwitchUI<RoomListScreenUI>();
        }

        private void OnHelpButtonClick()
        {
            _uiManager.ShowHelp("<b>匹配对战：</b>将默认使用远程服务器，系统匹配与您实力相匹配的玩家作为对手进行游戏。\n\n<b>创建房间：</b>允许自定义房间名、玩家人数、密码，并可以使用本地服务器(也可以使用远程服务器邀请不在同一局域网下的玩家)进行游戏。\n\n<b>加入房间：</b>允许加入别的自定义房间进行游戏。");
        }
        
        private void OnCreateRoomClick()
        {
            _uiManager.SwitchUI<CreateRoomScreenUI>();
        }

        private void OnMatchButtonClick()
        {
            _uiManager.ShowTips("敬请期待！");
            return;
            if (!_playFabRoomManager.IsMatchmaking)
            {
                _playFabRoomManager.CreateOrJoinMatchingRoom();
            }
            else
            {
                _playFabRoomManager.CancelMatchmaking();   
            }
        }
        
        private void UpdateMatchmakingInfo()
        {
            _timeSpan = TimeSpan.FromSeconds(_timer);
            timerText.text = _timeSpan.ToString(@"mm\\:ss");
            _timer += 1f;
            Debug.Log($"Matchmaking: {_timer}");
        }
    }
    // public static class ReactivePropertyDiagnosticTests
    // {
    //     private static TestData _testData = new TestData { Value = "test" };
    //
    //     public static void RunAllTests()
    //     {
    //         Debug.Log("=== 开始 HybridCLR ReactiveProperty 诊断测试 ===");
    //         
    //         TestCase1_DirectGenericDelegate();
    //         TestCase2_InterfaceWithoutConversion();
    //         TestCase3_TypeCheckOnly();
    //         TestCase4_ConversionOnly();
    //         TestCase5_FullInterfaceWithConversion();
    //         TestCase6_GenericInterfaceCall();
    //         
    //         Debug.Log("=== 诊断测试完成 ===");
    //     }
    //
    //     // 测试用例1：直接泛型委托调用（基准测试）
    //     private static void TestCase1_DirectGenericDelegate()
    //     {
    //         try
    //         {
    //             Debug.Log("测试1: 直接泛型委托调用");
    //             Action<TestData> action = data => Debug.Log($"直接委托: {data.Value}");
    //             action(_testData);
    //             Debug.Log("✅ 测试1通过 - 直接泛型委托正常");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 测试1失败: {e}");
    //         }
    //     }
    //
    //     // 测试用例2：接口调用但不涉及类型转换
    //     private static void TestCase2_InterfaceWithoutConversion()
    //     {
    //         try
    //         {
    //             Debug.Log("测试2: 接口调用(无类型转换)");
    //             var listener = new SimpleListener<TestData>(data => Debug.Log($"简单接口: {data.Value}"));
    //             listener.OnValueChangedDirect(_testData);
    //             Debug.Log("✅ 测试2通过 - 接口调用(无转换)正常");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 测试2失败: {e}");
    //         }
    //     }
    //
    //     // 测试用例3：只测试类型检查(is操作)
    //     private static void TestCase3_TypeCheckOnly()
    //     {
    //         try
    //         {
    //             Debug.Log("测试3: 类型检查(is操作)");
    //             object obj = _testData;
    //             bool isTestData = obj is TestData;
    //             Debug.Log($"类型检查结果: {isTestData}");
    //             Debug.Log("✅ 测试3通过 - 类型检查正常");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 测试3失败: {e}");
    //         }
    //     }
    //
    //     // 测试用例4：只测试类型转换(as操作)
    //     private static void TestCase4_ConversionOnly()
    //     {
    //         try
    //         {
    //             Debug.Log("测试4: 类型转换(as操作)");
    //             object obj = _testData;
    //             var converted = obj as TestData;
    //             Debug.Log($"转换结果: {converted?.Value}");
    //             Debug.Log("✅ 测试4通过 - 类型转换正常");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 测试4失败: {e}");
    //         }
    //     }
    //
    //     // 测试用例5：完整接口调用+类型转换（模拟我们问题代码）
    //     private static void TestCase5_FullInterfaceWithConversion()
    //     {
    //         try
    //         {
    //             Debug.Log("测试5: 完整接口+类型转换");
    //             var listener = new ConvertingListener<TestData>(data => Debug.Log($"转换接口: {data.Value}"));
    //             IValueListener interfaceRef = listener;
    //             interfaceRef.OnValueChanged(_testData);
    //             Debug.Log("✅ 测试5通过 - 完整接口+转换正常");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 测试5失败: {e}");
    //         }
    //     }
    //
    //     // 测试用例6：泛型接口方法调用
    //     private static void TestCase6_GenericInterfaceCall()
    //     {
    //         try
    //         {
    //             Debug.Log("测试6: 泛型接口方法调用");
    //             var handler = new GenericHandler<TestData>();
    //             IGenericHandler<TestData> interfaceRef = handler;
    //             interfaceRef.Handle(_testData);
    //             Debug.Log("✅ 测试6通过 - 泛型接口调用正常");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 测试6失败: {e}");
    //         }
    //     }
    // }
    //
    // // 测试数据类
    // public class TestData
    // {
    //     public string Value { get; set; }
    // }
    //
    // // 简单接口（无转换）
    // public interface ISimpleListener<T>
    // {
    //     void OnValueChangedDirect(T value);
    // }
    //
    // public class SimpleListener<T> : ISimpleListener<T>
    // {
    //     private readonly Action<T> _action;
    //
    //     public SimpleListener(Action<T> action)
    //     {
    //         _action = action;
    //     }
    //
    //     public void OnValueChangedDirect(T value)
    //     {
    //         _action(value);
    //     }
    // }
    //
    // // 带转换的接口（模拟问题代码）
    // public interface IValueListener
    // {
    //     void OnValueChanged(object value);
    //     Type ValueType { get; }
    // }
    //
    // public class ConvertingListener<T> : IValueListener
    // {
    //     private readonly Action<T> _action;
    //
    //     public ConvertingListener(Action<T> action)
    //     {
    //         _action = action;
    //     }
    //
    //     public Type ValueType => typeof(T);
    //
    //     public void OnValueChanged(object value)
    //     {
    //         // 这里模拟我们问题代码的转换逻辑
    //         if (value is T typedValue)
    //         {
    //             _action(typedValue);
    //         }
    //     }
    // }
    //
    // // 泛型接口测试
    // public interface IGenericHandler<T>
    // {
    //     void Handle(T value);
    // }
    //
    // public class GenericHandler<T> : IGenericHandler<T>
    // {
    //     public void Handle(T value)
    //     {
    //         Debug.Log($"泛型接口处理: {value}");
    //     }
    // }
    //
    // public static class ReactivePropertySpecificTests
    // {
    //     public static void RunReactivePropertyTests()
    //     {
    //         Debug.Log("=== 开始 ReactiveProperty 特定场景测试 ===");
    //         
    //         TestSubscriptionWithConversion();
    //         TestNotificationWithConversion();
    //         TestMultipleSubscribers();
    //         TestValueTypes();
    //         TestNullValues();
    //         
    //         Debug.Log("=== ReactiveProperty 场景测试完成 ===");
    //     }
    //
    //     // 测试订阅时的立即通知（包含转换）
    //     private static void TestSubscriptionWithConversion()
    //     {
    //         try
    //         {
    //             Debug.Log("测试: 订阅时的立即通知(含转换)");
    //             var testData = new TestData { Value = "initial" };
    //             bool notified = false;
    //             
    //             var reactiveProp = new ConvertingReactiveProperty<TestData>(testData);
    //             reactiveProp.Subscribe(data => 
    //             {
    //                 notified = true;
    //                 Debug.Log($"立即通知: {data.Value}");
    //             }, notifyImmediately: true);
    //             
    //             Debug.Log($"✅ 立即通知测试 - 通知状态: {notified}");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 立即通知测试失败: {e}");
    //         }
    //     }
    //
    //     // 测试值改变时的通知（包含转换）
    //     private static void TestNotificationWithConversion()
    //     {
    //         try
    //         {
    //             Debug.Log("测试: 值改变通知(含转换)");
    //             var reactiveProp = new ConvertingReactiveProperty<TestData>();
    //             bool notified = false;
    //             
    //             reactiveProp.Subscribe(data => 
    //             {
    //                 notified = true;
    //                 Debug.Log($"值改变通知: {data.Value}");
    //             });
    //             
    //             reactiveProp.Value = new TestData { Value = "changed" };
    //             Debug.Log($"✅ 值改变通知测试 - 通知状态: {notified}");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 值改变通知测试失败: {e}");
    //         }
    //     }
    //
    //     // 测试多个订阅者
    //     private static void TestMultipleSubscribers()
    //     {
    //         try
    //         {
    //             Debug.Log("测试: 多个订阅者");
    //             var reactiveProp = new ConvertingReactiveProperty<int>(0);
    //             int notificationCount = 0;
    //             
    //             reactiveProp.Subscribe(val => { notificationCount++; Debug.Log($"订阅者1: {val}"); });
    //             reactiveProp.Subscribe(val => { notificationCount++; Debug.Log($"订阅者2: {val}"); });
    //             
    //             reactiveProp.Value = 1;
    //             Debug.Log($"✅ 多订阅者测试 - 总通知次数: {notificationCount}");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 多订阅者测试失败: {e}");
    //         }
    //     }
    //
    //     // 测试值类型
    //     private static void TestValueTypes()
    //     {
    //         try
    //         {
    //             Debug.Log("测试: 值类型处理");
    //             var reactiveProp = new ConvertingReactiveProperty<int>(0);
    //             reactiveProp.Subscribe(val => Debug.Log($"值类型通知: {val}"));
    //             reactiveProp.Value = 42;
    //             Debug.Log("✅ 值类型测试通过");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ 值类型测试失败: {e}");
    //         }
    //     }
    //
    //     // 测试 null 值处理
    //     private static void TestNullValues()
    //     {
    //         try
    //         {
    //             Debug.Log("测试: null值处理");
    //             var reactiveProp = new ConvertingReactiveProperty<TestData>(new TestData { Value = "initial" });
    //             reactiveProp.Subscribe(data => Debug.Log($"null值测试: {(data == null ? "null" : data.Value)}"));
    //             reactiveProp.Value = null;
    //             Debug.Log("✅ null值测试通过");
    //         }
    //         catch (Exception e)
    //         {
    //             Debug.LogError($"❌ null值测试失败: {e}");
    //         }
    //     }
    // }
    //
    // // 模拟有问题的 ReactiveProperty 实现（用于测试）
    // public class ConvertingReactiveProperty<T>
    // {
    //     private T _value;
    //     private readonly List<IValueListener> _listeners = new List<IValueListener>();
    //
    //     public ConvertingReactiveProperty() : this(default(T)) { }
    //
    //     public ConvertingReactiveProperty(T initialValue)
    //     {
    //         _value = initialValue;
    //     }
    //
    //     public T Value
    //     {
    //         get => _value;
    //         set
    //         {
    //             if (EqualityComparer<T>.Default.Equals(_value, value))
    //                 return;
    //
    //             _value = value;
    //             NotifyValueChanged();
    //         }
    //     }
    //
    //     public IDisposable Subscribe(Action<T> listener, bool notifyImmediately = true)
    //     {
    //         var valueListener = new ConvertingListener<T>(listener);
    //         _listeners.Add(valueListener);
    //
    //         if (notifyImmediately)
    //         {
    //             try
    //             {
    //                 valueListener.OnValueChanged(_value); // 这里可能出问题
    //             }
    //             catch (Exception e)
    //             {
    //                 Debug.LogError($"立即通知错误: {e}");
    //                 throw;
    //             }
    //         }
    //
    //         return new Subscription(this, valueListener);
    //     }
    //
    //     private void NotifyValueChanged()
    //     {
    //         foreach (var listener in _listeners)
    //         {
    //             try
    //             {
    //                 listener.OnValueChanged(_value); // 这里可能出问题
    //             }
    //             catch (Exception e)
    //             {
    //                 Debug.LogError($"值改变通知错误: {e}");
    //                 throw;
    //             }
    //         }
    //     }
    //
    //     private class Subscription : IDisposable
    //     {
    //         private ConvertingReactiveProperty<T> _property;
    //         private IValueListener _listener;
    //
    //         public Subscription(ConvertingReactiveProperty<T> property, IValueListener listener)
    //         {
    //             _property = property;
    //             _listener = listener;
    //         }
    //
    //         public void Dispose()
    //         {
    //             _property?._listeners.Remove(_listener);
    //         }
    //     }
    // }
    
    public struct TestData
    {
        public int id;
        public string value;
    }
}