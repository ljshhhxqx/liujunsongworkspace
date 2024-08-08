using UnityEngine;

namespace HotUpdate
{
    public class Hello 
    {
        public static void SayHello()
        {
            Debug.Log("Hello from HotUpdate!");
        }

        public static void TestType<T>()
        {
            var obj = default(T);
            if (obj != null) Debug.Log(obj.GetType());
        }

        public static void TestAddComponent()
        {
            var go = new GameObject("Test");
            go.AddComponent<TestComponent>();
        }
    }
}
