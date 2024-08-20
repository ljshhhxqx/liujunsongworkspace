using System;

namespace Data
{
    [Serializable]
    public abstract class WebRequestData
    {
        
    }
    
    [Serializable]
    public class AccountData : WebRequestData
    {
        public string AccountName;
        public string Password;
    }
    
    [Serializable]
    public class RegisterData : WebRequestData
    {
        public string AccountName;
        public string Email;
        public string Password;
    }   
}