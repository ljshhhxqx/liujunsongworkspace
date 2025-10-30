using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public static class PlayerPlatformDefine
    {
        private static readonly HashSet<RuntimePlatform> WindowsPlatforms = new HashSet<RuntimePlatform>()
        {
            RuntimePlatform.WindowsPlayer,
            RuntimePlatform.WindowsEditor,
            RuntimePlatform.OSXEditor,
            RuntimePlatform.LinuxPlayer,
            RuntimePlatform.LinuxEditor,
            RuntimePlatform.WebGLPlayer
        };

        private static readonly HashSet<RuntimePlatform> JoystickPlatforms = new HashSet<RuntimePlatform>()
        {
            RuntimePlatform.Android,
            RuntimePlatform.IPhonePlayer,
            RuntimePlatform.tvOS,
            RuntimePlatform.Switch,
            RuntimePlatform.Stadia
        };

        public static bool IsJoystickPlatform()
        {
            return JoystickPlatforms.Contains(Application.platform);
        }
        
        public static bool IsWindowsPlatform()
        {
            return WindowsPlatforms.Contains(Application.platform);
        }
    }
}