using System;
using UnityEngine;

namespace UI.UIBase
{
    public abstract class ScreenUIBase : MonoBehaviour
    {
        public abstract UIType Type { get; }
        public abstract UICanvasType CanvasType { get; }
    }

    [Serializable]
    public enum UIType
    {
        None,
        Login,
        Register,
        TipsPopup,
        Room,
        Main,
        InGame,
        Setting,
        Ranking,
        Develop,
        LocalConnect,
        Help,
        RoomScreen,
        CreateRoom,
        Password,
        Loading,
        PlayerInfo,
        ModifyName
    }
 
    [Serializable]
    public enum UICanvasType
    {
        Overlay,
        Panel,
        SecondPanel,
        ThirdPanel,
        Popup,
        Exception,
    }
}