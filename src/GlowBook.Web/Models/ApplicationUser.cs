using Microsoft.AspNetCore.Identity;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public UserAccountType AccountType { get; set; } = UserAccountType.Master;

    public MasterProfile? MasterProfile { get; set; }
}
