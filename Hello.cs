using UnityEngine;

public class Hello 
{
    public static void SayHello()
    {
        Debug.Log("SayHello");
        Debug.Log("SayHello!");
        Debug.Log("SayHello!!#");
        Debug.Log("SayHello!?#");
    }

    public static void TestType<T>(T obj)
    {
        if (obj != null) Debug.Log(obj);   
    }

    public static void TestAddComponent()
    {
        var go = new GameObject("aaaaaaa");
        go.AddComponent<TestComponent>();
    }
}
