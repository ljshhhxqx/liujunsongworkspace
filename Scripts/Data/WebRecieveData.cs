namespace Data
{
    public abstract class WebReceiveData
    {
        
    }
    
    public class AccountReceiveData : WebReceiveData
    {
        public string Token { get; set; }
    }

    public class RegisterReceiveData : WebReceiveData
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}