namespace HotUpdate.Scripts.UI.UIs.Common
{
    public class UIGameData
    {
        
    }

    public enum ItemType
    {
        None,
        Equipment,
        Consume,
        Item,
    }

    public enum UISyncType
    {
        ReadOnly, //静态/只读UI,由服务器同步
        Interact, //交互式UI,由客户端预测+服务器验证
    }
}