using UnityEngine;

public class Hello 
{
    public static void SayHello()
    {
        Debug.Log("Hello");
        Debug.Log("Hello!");
        Debug.Log("Hello!!#");
        Debug.Log("Hello?#");
    }

    public static void TestType<T>(T obj)
    {
        if (obj != null) Debug.Log(obj + 11111.ToString());   
    }

    public static void TestAddComponent()
    {
        var go = new GameObject("aaaaaaa");
        go.AddComponent<TestComponent>();
    }
}
