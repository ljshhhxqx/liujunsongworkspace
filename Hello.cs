using UnityEngine;

public class Hello 
{
    public static void SayHello()
    {
        Debug.Log("Hello World!!!!!!!!!");
        Debug.Log("Hello World!!!!!!!!!");
        Debug.Log("Hello World!!!!!!!!!");
        Debug.Log("Hello World!!!!!!!!!");
    }

    public static void TestType<T>(T obj)
    {
        if (obj != null) Debug.Log(obj);   
    }

    public static void TestAddComponent()
    {
        var go = new GameObject("Test");
        go.AddComponent<TestComponent>();
    }
}
