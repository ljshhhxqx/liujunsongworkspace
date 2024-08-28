using UnityEngine;

public class Hello 
{
    public static void SayHello()
    {
        Debug.Log("SayHello214241231");
        Debug.Log("SayHello5325r2?#");
        Debug.Log("SayHello23124e?#!");
    }

    public static void TestType<T>(T obj)
    {
        if (obj != null) Debug.Log(obj + "-------------".ToString());   
    }

    public static void TestAddComponent()
    {
        var go = new GameObject("aaaaaaa");
        //go.AddComponent<TestComponent>();
    }
}
