using Mirror;
using UnityEngine;

public class Singleton<T> where T : new()
{
    private static T instance;
    public static T Instance
    {
        get
        {
            instance ??= new T();
            return instance;
        }
    }
}


public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    public static T Instance => instance;

    protected virtual void Awake()
    {
        instance = this as T;
    }
}

public class SingletonAutoMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                // 先尝试找到现有的实例
                instance = FindObjectOfType<T>();
                if (instance == null)
                {
                    // 如果没有找到，创建一个新的GameObject来附加这个组件
                    var obj = new GameObject(typeof(T).Name);
                    instance = obj.AddComponent<T>();
                    // 确保这个GameObject不会在加载新场景时被销毁
                    DontDestroyOnLoad(instance.gameObject);
                }
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(this);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
}


public class SingletonNetMono<T> : NetworkBehaviour where T : NetworkBehaviour
{
    private static T instance;
    public static T Instance => instance;

    protected virtual void Awake()
    {
        instance = this as T;
    }
}

public class SingletonAutoNetMono<T> : NetworkBehaviour where T : NetworkBehaviour
{
    private static T instance;
    public static T Instance => instance;

    protected virtual void Awake()
    {
        GameObject obj = new GameObject();
        obj.name = typeof(T).ToString();
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<T>();
    }
}