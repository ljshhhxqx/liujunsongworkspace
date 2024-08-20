using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate
{
    public class ConsoleToScreen : MonoBehaviour
    {
        const int maxLines = 50;
        const int maxLineLength = 120;
        private string _logStr = "";

        private readonly List<string> _lines = new List<string>();
        private readonly List<LogType> _logTypes = new List<LogType>();

        public int fontSize = 15;

        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }

        public void Log(string logString, string stackTrace, LogType type)
        {
            foreach (var line in logString.Split('\n'))
            {
                if (line.Length <= maxLineLength)
                {
                    _lines.Add(line);
                    _logTypes.Add(type);
                    continue;
                }
                var lineCount = line.Length / maxLineLength + 1;
                for (int i = 0; i < lineCount; i++)
                {
                    if ((i + 1) * maxLineLength <= line.Length)
                    {
                        _lines.Add(line.Substring(i * maxLineLength, maxLineLength));
                    }
                    else
                    {
                        _lines.Add(line.Substring(i * maxLineLength, line.Length - i * maxLineLength));
                    }
                    _logTypes.Add(type);
                }
            }
            if (_lines.Count > maxLines)
            {
                int removeCount = _lines.Count - maxLines;
                _lines.RemoveRange(0, removeCount);
                _logTypes.RemoveRange(0, removeCount);
            }
            _logStr = string.Join("\n", _lines);
        }

        void OnGUI()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1200.0f, Screen.height / 800.0f, 1.0f));

            for (int i = 0; i < _lines.Count; i++)
            {
                switch (_logTypes[i])
                {
                    case LogType.Error:
                    case LogType.Exception:
                        GUI.contentColor = Color.red;
                        break;
                    case LogType.Warning:
                        GUI.contentColor = Color.yellow;
                        break;
                    default:
                        GUI.contentColor = Color.black;
                        break;
                }
                GUI.Label(new Rect(10, 10 + i * 20, 800, 20), _lines[i], new GUIStyle() { fontSize = Math.Max(10, fontSize) });
            }

            GUI.contentColor = Color.white; // 恢复默认颜色
        }
    }
}
