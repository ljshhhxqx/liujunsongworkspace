using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Tool.Coroutine
{
    public static class DelayInvoker
    {
        private static readonly Dictionary<int, Action> DelayInvokeDict = new Dictionary<int, Action>();
        
        public static void DelayInvoke(float delaySeconds, Action action)
        {
            DelayInvokeDict.Add(action.GetHashCode(), action);
            InvokeWithDelay(delaySeconds, action).Forget();
        }
        
        public static void CancelInvoke(Action action)
        {
            DelayInvokeDict.Remove(action.GetHashCode());
        }

        private static async UniTaskVoid InvokeWithDelay(float delaySeconds, Action action)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));
            var key = action.GetHashCode();
            if (DelayInvokeDict.ContainsKey(key))
            {
                action.Invoke();
            }
            DelayInvokeDict.Remove(key);
        }
    }
}