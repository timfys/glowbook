using Microsoft.AspNetCore.Identity;
using GlowBook.Web.Models.Entities;

namespace GlowBook.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public MasterProfile? MasterProfile { get; set; }
}
