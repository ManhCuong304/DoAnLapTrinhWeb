using Microsoft.AspNetCore.Identity;

namespace DoAnLapTrinhWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string? Image {  get; set; }
        public string? Description { get; set; }
        public List<Follow> Followings { get; set; }
        public List<Follow> Followers { get; set; }
    }
}
