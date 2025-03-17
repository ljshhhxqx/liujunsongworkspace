using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HotUpdate.Scripts.Tool.Coroutine
{
    public static class DelayInvoker
    {
        // 改用线程安全的并发字典（应对多线程场景）
        private static readonly ConcurrentDictionary<int, DelayTaskData> ConcurrentDictionary = new ConcurrentDictionary<int, DelayTaskData>();

        /// <summary>
        /// 支持条件验证的延时调用
        /// </summary>
        /// <param name="delaySeconds">基础延迟时间</param>
        /// <param name="action">要执行的动作</param>
        /// <param name="condition">持续验证条件（返回true时继续等待，返回false时取消）</param>
        /// <param name="checkInterval">条件检查间隔（秒）</param>
        /// <param name="token">外部取消令牌</param>
        public static void DelayInvoke(
            float delaySeconds,
            Action action,
            Func<bool> condition = null,
            float checkInterval = 0.1f,
            CancellationToken token = default)
        {
            var taskId = action.GetHashCode();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            // 存储取消令牌源以便后续取消
            var taskData = new DelayTaskData(action, cts);
            if (!ConcurrentDictionary.TryAdd(taskId, taskData))
            {
                throw new InvalidOperationException("Duplicate action detected!");
            }

            // 启动异步任务
            RunDelayTask(delaySeconds, action, condition, checkInterval, cts).Forget();
        }

        public static void CancelInvoke(Action action)
        {
            var taskId = action.GetHashCode();
            if (ConcurrentDictionary.TryRemove(taskId, out var data))
            {
                data.Cts.Cancel();
                data.Cts.Dispose();
            }
        }

        private static async UniTaskVoid RunDelayTask(
            float delaySeconds,
            Action action,
            Func<bool> condition,
            float checkInterval,
            CancellationTokenSource cts)
        {
            try
            {
                // 混合等待：基础延时 + 条件轮询
                await UniTask.WhenAny(UniTask.Delay(TimeSpan.FromSeconds(delaySeconds)),  PollConditionAsync(condition, checkInterval, cts.Token));

                // 执行前最终验证
                if (ShouldExecute(condition))
                {
                    action.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // 取消属于正常流程，无需处理
            }
            finally
            {
                ConcurrentDictionary.TryRemove(action.GetHashCode(), out _);
                cts.Dispose();
            }
        }

        private static async UniTask PollConditionAsync(
            Func<bool> condition, 
            float interval, 
            CancellationToken token)
        {
            if (condition == null) return;

            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                if (!condition.Invoke())
                {
                    throw new OperationCanceledException("Condition validation failed");
                }
            }
        }

        private static bool ShouldExecute(Func<bool> condition)
        {
            try
            {
                return condition?.Invoke() ?? true;
            }
            catch
            {
                return false; // 条件验证异常视为失败
            }
        }

        // 存储任务元数据
        private class DelayTaskData
        {
            public Action Action { get; }
            public CancellationTokenSource Cts { get; }

            public DelayTaskData(Action action, CancellationTokenSource cts)
            {
                Action = action;
                Cts = cts;
            }
        }
    }
}