using Microsoft.AspNetCore.Identity;

namespace Core.Data
{
    public class AppUser : IdentityUser
    {
        public string DID { get; set; }
        public string PrivateKey { get; set; }
    }
}