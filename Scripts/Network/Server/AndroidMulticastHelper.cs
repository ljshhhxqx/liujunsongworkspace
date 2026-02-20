
using UnityEngine;
namespace HotUpdate.Scripts.Network.Server
{

    public class AndroidMulticastHelper : MonoBehaviour
    {
#if !UNITY_ANDROID && UNITY_EDITOR
    private AndroidJavaObject multicastLock;

    void Awake()
    {
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        var wifiManager = activity.Call<AndroidJavaObject>(
            "getSystemService",
            "wifi"
        );

        multicastLock = wifiManager.Call<AndroidJavaObject>(
            "createMulticastLock",
            "unity-multicast"
        );

        multicastLock.Call("acquire");
        Debug.Log("Android Multicast Lock Acquired");
    }

    void OnDestroy()
    {
        multicastLock?.Call("release");
    }
#endif
    }
}