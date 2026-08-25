using GlowBook.Web.Data;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[Route("profile")]
public class ProfileController : Controller
{
    private static readonly HashSet<string> AllowedAvatarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public ProfileController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        MasterProfileService profiles)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var (user, profile) = await GetCurrentAsync();
        if (user == null || profile == null)
            return Challenge();

        return View(new ProfileCardViewModel
        {
            ProfileId = profile.Id,
            DisplayName = user.DisplayName ?? profile.BusinessName,
            BusinessName = profile.BusinessName,
            Specialization = profile.Specialization,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            City = profile.City,
            Address = profile.Address,
            Description = profile.Description,
            BookingSlug = profile.BookingSlug,
            HasAvatar = profile.HasAvatar,
            AvatarVersion = profile.AvatarUpdatedAt?.Ticks,
            IsPremium = profile.Subscription?.IsPremiumActive == true,
            PremiumUntil = profile.Subscription?.ExpiresAt
        });
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit()
    {
        var (user, profile) = await GetCurrentAsync();
        if (user == null || profile == null)
            return Challenge();

        return View(ToEditModel(user, profile));
    }

    [HttpPost("edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> Edit(ProfileEditViewModel model)
    {
        var (user, profile) = await GetCurrentAsync();
        if (user == null || profile == null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            model.HasAvatar = profile.HasAvatar;
            model.ProfileId = profile.Id;
            model.AvatarVersion = profile.AvatarUpdatedAt?.Ticks;
            return View(model);
        }

        user.DisplayName = model.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        var userResult = await _users.UpdateAsync(user);
        if (!userResult.Succeeded)
        {
            foreach (var err in userResult.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            model.HasAvatar = profile.HasAvatar;
            model.ProfileId = profile.Id;
            model.AvatarVersion = profile.AvatarUpdatedAt?.Ticks;
            return View(model);
        }

        profile.BusinessName = model.BusinessName.Trim();
        profile.Specialization = NullIfEmpty(model.Specialization);
        profile.City = NullIfEmpty(model.City);
        profile.Address = NullIfEmpty(model.Address);
        profile.Description = NullIfEmpty(model.Description);

        if (model.RemoveAvatar && model.Avatar == null)
            await RemoveAvatarAsync(profile);

        if (model.Avatar is { Length: > 0 })
        {
            var error = await SaveAvatarAsync(profile, model.Avatar);
            if (error != null)
            {
                ModelState.AddModelError(nameof(model.Avatar), error);
                model.HasAvatar = profile.HasAvatar;
                model.ProfileId = profile.Id;
                model.AvatarVersion = profile.AvatarUpdatedAt?.Ticks;
                return View(model);
            }
        }

        await _db.SaveChangesAsync();
        TempData["ProfileSaved"] = true;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("avatar/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Avatar(int id)
    {
        var avatar = await _db.MasterAvatars.AsNoTracking()
            .FirstOrDefaultAsync(a => a.MasterProfileId == id);

        if (avatar == null || avatar.Data.Length == 0)
            return NotFound();

        Response.Headers.CacheControl = "public,max-age=86400";
        return File(avatar.Data, avatar.ContentType);
    }

    private async Task<(ApplicationUser? User, MasterProfile? Profile)> GetCurrentAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return (null, null);

        var profile = await _profiles.EnsureForUserAsync(user);
        return (user, profile);
    }

    private static ProfileEditViewModel ToEditModel(ApplicationUser user, MasterProfile profile) => new()
    {
        DisplayName = user.DisplayName ?? profile.BusinessName,
        PhoneNumber = user.PhoneNumber,
        BusinessName = profile.BusinessName,
        Specialization = profile.Specialization,
        City = profile.City,
        Address = profile.Address,
        Description = profile.Description,
        HasAvatar = profile.HasAvatar,
        ProfileId = profile.Id,
        AvatarVersion = profile.AvatarUpdatedAt?.Ticks
    };

    private async Task<string?> SaveAvatarAsync(MasterProfile profile, IFormFile file)
    {
        if (file.Length <= 0)
            return "Файл пустой";

        if (file.Length > MaxAvatarBytes)
            return "Фото не должно быть больше 5 МБ";

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var data = ms.ToArray();

        var contentType = DetectImageContentType(data, file.ContentType);
        if (contentType == null)
            return "Поддерживаются JPEG, PNG, WebP и GIF. На iPhone выберите «Самое совместимое» или JPEG.";

        var existing = await _db.MasterAvatars.FirstOrDefaultAsync(a => a.MasterProfileId == profile.Id);
        if (existing == null)
        {
            _db.MasterAvatars.Add(new MasterAvatar
            {
                MasterProfileId = profile.Id,
                Data = data,
                ContentType = contentType
            });
        }
        else
        {
            existing.Data = data;
            existing.ContentType = contentType;
        }

        profile.HasAvatar = true;
        profile.AvatarUpdatedAt = DateTime.UtcNow;
        return null;
    }

    private static string? DetectImageContentType(byte[] data, string? reported)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return "image/gif";
        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";

        if (!string.IsNullOrWhiteSpace(reported) && AllowedAvatarTypes.Contains(reported))
            return NormalizeContentType(reported);

        return null;
    }

    private async Task RemoveAvatarAsync(MasterProfile profile)
    {
        var existing = await _db.MasterAvatars.FirstOrDefaultAsync(a => a.MasterProfileId == profile.Id);
        if (existing != null)
            _db.MasterAvatars.Remove(existing);

        profile.HasAvatar = false;
        profile.AvatarUpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeContentType(string contentType) =>
        contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : contentType;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
