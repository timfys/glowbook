using Microsoft.AspNetCore.Identity;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;

namespace GlowBook.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public UserAccountType AccountType { get; set; } = UserAccountType.Master;

    /// <summary>Reader / client Premium (доступ к premium-статьям). UTC.</summary>
    public DateTime? PremiumExpiresAt { get; set; }

    public MasterProfile? MasterProfile { get; set; }

    public bool HasReaderPremium =>
        PremiumExpiresAt != null && PremiumExpiresAt > DateTime.UtcNow;
}
