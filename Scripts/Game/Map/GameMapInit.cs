using System;
using Common;
using UnityEngine;

namespace Game.Map
{
    public class GameMapInit : MonoBehaviour
    {
        private void Start()
        {
            var injectObject = GameObject.FindGameObjectWithTag("InjectObjects");
            if (injectObject == null)
            {
                throw new UnityException("InjectObjects GameObject not found.");
            }

            var childObjects = injectObject.GetComponentsInChildren<IInjectableObject>();

            foreach (var injectable in childObjects)
            {
                if (injectable is MonoBehaviour monoBehaviour)
                {
                    var ns = monoBehaviour.GetType().Namespace;
                    if (ns != null && !ns.StartsWith("UnityEngine"))
                    {
                        try
                        {
                            ObjectInjectProvider.Instance.Inject(monoBehaviour);
                            Debug.Log($"成功注入: {monoBehaviour.GetType().Name}");
                        }
                        catch (Exception e)
                        {
                            throw new UnityException($"注入 {monoBehaviour.GetType().Name} 时发生错误: {e.Message}", e);
                        }
                    }
                }
            }
        }
    }
}
