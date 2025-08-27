namespace PASS.Web.Models
{
    public class UserAccount
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
    }
}
