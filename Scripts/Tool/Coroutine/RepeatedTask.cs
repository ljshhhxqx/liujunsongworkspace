using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.Coroutine
{
    public class RepeatedTask : Singleton<RepeatedTask>
    {
        private readonly Dictionary<TaskDelegate, float> _taskTimers = new Dictionary<TaskDelegate, float>();
        private readonly Dictionary<TaskDelegate, float> _taskCountdown = new Dictionary<TaskDelegate, float>();
        
        private readonly Dictionary<TaskDelegate, CancellationTokenSource> _taskCancellationTokens = new Dictionary<TaskDelegate, CancellationTokenSource>();
        private readonly Dictionary<UniTaskVoidTaskDelegate, CancellationTokenSource> _uniTaskVoidTaskCancellationTokens = new Dictionary<UniTaskVoidTaskDelegate, CancellationTokenSource>();
        
        // 委托定义，无返回值无参数
        public delegate void TaskDelegate();
        public delegate UniTaskVoid UniTaskVoidTaskDelegate();

        public void StartUniTaskVoidTask(UniTaskVoidTaskDelegate task, float interval)
        {
            // 如果任务已经在运行，先停止
            if (_uniTaskVoidTaskCancellationTokens.ContainsKey(task))
            {
                StopUniTaskVoidTask(task);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _uniTaskVoidTaskCancellationTokens[task] = cancellationTokenSource;

            // 使用 UniTask 异步方法开始执行任务
            PerformUniTaskVoidTask(task, interval, cancellationTokenSource.Token).Forget();
        }

        public void StopUniTaskVoidTask(UniTaskVoidTaskDelegate task)
        {
            if (_uniTaskVoidTaskCancellationTokens.ContainsKey(task))
            {
                _uniTaskVoidTaskCancellationTokens[task].Cancel();
                _uniTaskVoidTaskCancellationTokens.Remove(task);
            }
        }

        private async UniTaskVoid PerformUniTaskVoidTask(UniTaskVoidTaskDelegate task, float interval, CancellationToken token)
        {
            // 使用无限循环来持续执行任务，直到 token 被取消
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        task().Forget();  // 调用委托指向的方法
                        await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Debug.LogError($"RepeatedTask {task.Method.Name} run exception: {e}");
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，这是预期的行为，所以我们可以安静地处理它
                Debug.Log($"RepeatedTask {task.Method.Name} was cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Unexpected error in RepeatedTask {task.Method.Name}: {e}");
            }
        }
        

        // 开始执行周期性任务的方法
        public void StartRepeatingTask(TaskDelegate task, float interval)
        {
            // 如果任务已经在运行，先停止
            if (_taskCancellationTokens.ContainsKey(task))
            {
                StopRepeatingTask(task);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _taskCancellationTokens[task] = cancellationTokenSource;

            // 使用 UniTask 异步方法开始执行任务
            PerformTask(task, interval, cancellationTokenSource.Token).Forget();
        }

        // 停止执行周期性任务的方法
        public void StopRepeatingTask(TaskDelegate task)
        {
            if (_taskCancellationTokens.ContainsKey(task))
            {
                _taskCancellationTokens[task].Cancel();
                _taskCancellationTokens.Remove(task);
            }
        }

        // 使用 UniTask 改写的异步方法
        private async UniTaskVoid PerformTask(TaskDelegate task, float interval, CancellationToken token)
        {
            // 使用无限循环来持续执行任务，直到 token 被取消
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        task();  // 调用委托指向的方法
                        await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Debug.LogError($"RepeatedTask {task.Method.Name} run exception: {e}");
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 任务被取消，这是预期的行为，所以我们可以安静地处理它
                Debug.Log($"RepeatedTask {task.Method.Name} was cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Unexpected error in RepeatedTask {task.Method.Name}: {e}");
            }
        }

        // 停止所有任务的方法
        public void StopAllTasks()
        {
            foreach (var tokenSource in _taskCancellationTokens.Values)
            {
                tokenSource.Cancel();
            }
            foreach (var tokenSource in _uniTaskVoidTaskCancellationTokens.Values)
            {
                tokenSource.Cancel();
            }
            _taskCancellationTokens.Clear();
            _uniTaskVoidTaskCancellationTokens.Clear();
        }

        public void Dispose()
        {
            StopAllTasks();
        }
    }
}
