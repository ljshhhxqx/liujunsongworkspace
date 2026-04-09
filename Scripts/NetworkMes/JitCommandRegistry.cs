using System;
using System.Collections.Generic;
using System.Text;
using AOTScripts.Data;
using UnityEngine;

namespace HotUpdate.Scripts.NetworkMes
{
    public static class JitCommandRegistry
    {
        // ── AOT SerializeCommand 路由时调用 ──
        public static SerializeData SerializeJitCommand(NetworkCommandEnvelope command)
        {
            return JitCommandEnvelope.SerializeJitCommand(command);
        }

        // ── AOT DeserializeCommand 路由时调用 ──
        public static T DeserializeJitCommand<T>(byte[] payload) where T : IJitNetworkCommand
        {
            return (T)JsonUtility.FromJson(Encoding.UTF8.GetString(payload), typeof(T));
        }
    }

    public enum JitNetworkCommandType
    {
        Test = JitCommandEnvelope.AOT_CMD_THRESHOLD,
        
    }

    public interface IJitNetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
        int GetCommandType();
    }

    public struct TestCommand : IJitNetworkCommand
    {
        public NetworkCommandHeader Header;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return true;
        }

        public int GetCommandType()
        {
            return (int)JitNetworkCommandType.Test;
        }
    }
}