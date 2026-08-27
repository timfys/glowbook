using GlowBook.Web.Data;
using GlowBook.Web.Filters;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Clients;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireClientAccount]
[Route("my")]
public class MyController : Controller
{
    private static readonly HashSet<string> AllowedAvatarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly ClientAccountService _clients;
    private readonly ClientChatService _chat;

    public MyController(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        ClientAccountService clients,
        ClientChatService chat)
    {
        _users = users;
        _db = db;
        _clients = clients;
        _chat = chat;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? record)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var appointments = await _clients.GetAppointmentsAsync(user);
        var masters = await _clients.GetMastersAsync(user);

        ViewBag.DisplayName = user.DisplayName ?? user.Email;
        ViewBag.Masters = masters;
        ViewBag.FilterRecordId = record;

        if (record is int clientRecordId)
            appointments = appointments.Where(a => a.ClientRecordId == clientRecordId).ToList();

        return View(appointments);
    }

    [HttpGet("masters")]
    public async Task<IActionResult> Masters()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var masters = await _clients.GetMastersAsync(user);
        return View(masters);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var avatar = await _db.ClientAvatars.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        return View(new ClientProfileEditViewModel
        {
            DisplayName = user.DisplayName ?? "",
            Email = user.Email ?? "",
            Phone = user.PhoneNumber,
            HasAvatar = avatar != null,
            AvatarVersion = avatar?.UpdatedAt.Ticks
        });
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ClientProfileEditViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
        {
            var avatar = await _db.ClientAvatars.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == user.Id);
            model.HasAvatar = avatar != null;
            model.AvatarVersion = avatar?.UpdatedAt.Ticks;
            return View(model);
        }

        user.DisplayName = model.DisplayName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

        if (!string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await _users.SetEmailAsync(user, model.Email.Trim());
            if (!setEmail.Succeeded)
            {
                foreach (var err in setEmail.Errors)
                    ModelState.AddModelError(nameof(model.Email), err.Description);
                var avatar = await _db.ClientAvatars.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.UserId == user.Id);
                model.HasAvatar = avatar != null;
                model.AvatarVersion = avatar?.UpdatedAt.Ticks;
                return View(model);
            }

            user.UserName = model.Email.Trim();
            await _users.SetUserNameAsync(user, model.Email.Trim());
        }

        await _users.UpdateAsync(user);
        await _clients.LinkClientsToUserAsync(user);

        TempData["Success"] = "РџСЂРѕС„РёР»СЊ СЃРѕС…СЂР°РЅС‘РЅ";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost("avatar")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatar)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (avatar is not { Length: > 0 })
        {
            TempData["AvatarError"] = "Р’С‹Р±РµСЂРёС‚Рµ С„РѕС‚Рѕ";
            return RedirectToAction(nameof(Profile));
        }

        var error = await SaveAvatarAsync(user.Id, avatar);
        if (error != null)
        {
            TempData["AvatarError"] = error;
            return RedirectToAction(nameof(Profile));
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Р¤РѕС‚Рѕ РѕР±РЅРѕРІР»РµРЅРѕ";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("avatar")]
    public async Task<IActionResult> Avatar()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var avatar = await _db.ClientAvatars.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == user.Id);
        if (avatar == null || avatar.Data.Length == 0)
            return NotFound();

        Response.Headers.CacheControl = "public,max-age=86400";
        return File(avatar.Data, avatar.ContentType);
    }


    [HttpGet("chats")]
    public async Task<IActionResult> Chats()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        var conversations = await _chat.GetConversationsForClientAsync(user);
        return View(new ChatInboxViewModel
        {
            IsMasterView = false,
            Conversations = conversations
        });
    }

    [HttpGet("chat/{clientRecordId:int}")]
    public async Task<IActionResult> Chat(int clientRecordId)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!await _chat.CanAccessChatAsync(clientRecordId, user.Id))
            return Forbid();

        var client = await _db.Clients
            .Include(c => c.MasterProfile)
            .FirstOrDefaultAsync(c => c.Id == clientRecordId && !c.IsArchived);
        if (client?.MasterProfile == null)
            return NotFound();

        var messages = await _chat.GetMessagesAsync(clientRecordId);
        var master = client.MasterProfile;

        return View(new ClientChatViewModel
        {
            ClientRecordId = clientRecordId,
            Title = string.IsNullOrWhiteSpace(master.BusinessName) ? "РњР°СЃС‚РµСЂ" : master.BusinessName,
            BackUrl = Url.Action(nameof(Chats)),
            IsMasterView = false,
            CurrentUserId = user.Id,
            Messages = messages
        });
    }

    [HttpPost("chat/{clientRecordId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat(int clientRecordId, string message)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (string.IsNullOrWhiteSpace(message))
            return RedirectToAction(nameof(Chat), new { clientRecordId });

        await _chat.SendAsync(clientRecordId, user.Id, message);
        return RedirectToAction(nameof(Chat), new { clientRecordId });
    }

    [HttpGet("chat/{clientRecordId:int}/messages")]
    public async Task<IActionResult> ChatMessages(int clientRecordId, int after = 0)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!await _chat.CanAccessChatAsync(clientRecordId, user.Id))
            return Forbid();

        var messages = await _chat.GetMessagesAfterAsync(clientRecordId, after);
        return Json(messages.Select(m => ClientChatService.ToDto(m, user.Id)));
    }

    private async Task<string?> SaveAvatarAsync(string userId, IFormFile file)
    {
        if (file.Length <= 0)
            return "Р¤Р°Р№Р» РїСѓСЃС‚РѕР№";

        if (file.Length > MaxAvatarBytes)
            return "Р¤РѕС‚Рѕ РЅРµ РґРѕР»Р¶РЅРѕ Р±С‹С‚СЊ Р±РѕР»СЊС€Рµ 5 РњР‘";

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var data = ms.ToArray();

        var contentType = DetectImageContentType(data, file.ContentType);
        if (contentType == null)
            return "РџРѕРґРґРµСЂР¶РёРІР°СЋС‚СЃСЏ JPEG, PNG, WebP Рё GIF.";

        var existing = await _db.ClientAvatars.FirstOrDefaultAsync(a => a.UserId == userId);
        if (existing == null)
        {
            _db.ClientAvatars.Add(new ClientAvatar
            {
                UserId = userId,
                Data = data,
                ContentType = contentType,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Data = data;
            existing.ContentType = contentType;
            existing.UpdatedAt = DateTime.UtcNow;
        }

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
            return reported.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : reported;

        return null;
    }
}
