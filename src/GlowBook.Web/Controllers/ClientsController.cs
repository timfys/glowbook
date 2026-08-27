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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireMasterAccount]
public class ClientsController : Controller
{
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private const long MaxPhotoBytes = 2 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;
    private readonly ClientAccountService _clientAccounts;
    private readonly ClientChatService _chat;

    public ClientsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        MasterProfileService profiles,
        ClientAccountService clientAccounts,
        ClientChatService chat)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
        _clientAccounts = clientAccounts;
        _chat = chat;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var query = _db.Clients.Where(c => c.MasterProfileId == profile.Id && !c.IsArchived);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q) || c.Phone.Contains(q)
                || (c.Allergies != null && c.Allergies.Contains(q))
                || (c.SkinConcerns != null && c.SkinConcerns.Contains(q)));

        var clients = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        ViewBag.Query = q;
        return View(clients);
    }

    public IActionResult Create() => View(new Client { Name = "", Phone = "" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Client model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!ModelState.IsValid) return View(model);

        model.MasterProfileId = profile.Id;
        model.CreatedAt = DateTime.UtcNow;
        _db.Clients.Add(model);
        await _db.SaveChangesAsync();
        await _clientAccounts.TryLinkClientRecordAsync(model);
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        var vm = new ClientDetailsViewModel
        {
            Client = client,
            Appointments = await _db.Appointments
                .Include(a => a.Service)
                .Where(a => a.ClientId == id)
                .OrderByDescending(a => a.StartsAt)
                .Take(20)
                .ToListAsync(),
            Treatments = await _db.TreatmentRecords
                .Include(t => t.Service)
                .Where(t => t.ClientId == id)
                .OrderByDescending(t => t.PerformedAt)
                .ToListAsync(),
            Photos = await _db.ClientPhotos
                .Where(p => p.ClientId == id)
                .OrderByDescending(p => p.TakenAt)
                .Select(p => new ClientPhoto
                {
                    Id = p.Id,
                    ClientId = p.ClientId,
                    MasterProfileId = p.MasterProfileId,
                    Kind = p.Kind,
                    ContentType = p.ContentType,
                    Caption = p.Caption,
                    TakenAt = p.TakenAt,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(),
            HomeCare = await _db.HomeCarePrescriptions
                .Where(h => h.ClientId == id)
                .OrderByDescending(h => h.PrescribedAt)
                .ToListAsync()
        };

        ViewBag.Services = new SelectList(
            await _db.Services.Where(s => s.MasterProfileId == profile.Id && s.IsActive).OrderBy(s => s.Name).ToListAsync(),
            "Id", "Name");

        ViewBag.LinkedAccount = await _clientAccounts.GetLinkedAccountForClientAsync(client);

        return View(vm);
    }

    public async Task<IActionResult> Account(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        var account = await _clientAccounts.GetLinkedAccountViewAsync(client);
        if (account == null)
            return RedirectToAction(nameof(Details), new { id });

        ViewBag.ClientName = client.Name;
        return View(account);
    }

    [HttpGet]
    public async Task<IActionResult> LinkedAvatar(int clientId)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        var linked = await _clientAccounts.GetLinkedAccountForClientAsync(client);
        if (linked == null) return NotFound();

        var avatar = await _db.ClientAvatars.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == linked.Id);
        if (avatar == null || avatar.Data.Length == 0)
            return NotFound();

        Response.Headers.CacheControl = "public,max-age=86400";
        return File(avatar.Data, avatar.ContentType);
    }


    [HttpGet]
    public async Task<IActionResult> Chats()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var conversations = await _chat.GetConversationsForMasterAsync(profile.Id);
        return View(new ChatInboxViewModel
        {
            IsMasterView = true,
            Conversations = conversations
        });
    }

    [HttpGet]
    public async Task<IActionResult> Chat(int id)
    {
        var (profile, user) = await GetProfileAndUserAsync();
        if (profile == null || user == null) return Challenge();

        var client = await _db.Clients
            .Include(c => c.MasterProfile)
            .FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        if (!await _chat.CanAccessChatAsync(id, user.Id))
            return Forbid();

        var linked = await _clientAccounts.GetLinkedAccountForClientAsync(client);
        if (linked == null)
            return RedirectToAction(nameof(Details), new { id });

        var messages = await _chat.GetMessagesAsync(id);

        return View(new ClientChatViewModel
        {
            ClientRecordId = id,
            Title = linked.DisplayName ?? linked.Email ?? client.Name,
            BackUrl = Url.Action(nameof(Chats)),
            IsMasterView = true,
            CurrentUserId = user.Id,
            Messages = messages
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat(int id, string message)
    {
        var (profile, user) = await GetProfileAndUserAsync();
        if (profile == null || user == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        if (string.IsNullOrWhiteSpace(message))
            return RedirectToAction(nameof(Chat), new { id });

        await _chat.SendAsync(id, user.Id, message);
        return RedirectToAction(nameof(Chat), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ChatMessages(int id, int after = 0)
    {
        var (profile, user) = await GetProfileAndUserAsync();
        if (profile == null || user == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        if (!await _chat.CanAccessChatAsync(id, user.Id))
            return Forbid();

        var messages = await _chat.GetMessagesAfterAsync(id, after);
        return Json(messages.Select(m => ClientChatService.ToDto(m, user.Id)));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();
        return View(client);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Client model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        client.Name = model.Name.Trim();
        client.Phone = model.Phone.Trim();
        client.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        client.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        client.Allergies = string.IsNullOrWhiteSpace(model.Allergies) ? null : model.Allergies.Trim();
        client.SkinConcerns = string.IsNullOrWhiteSpace(model.SkinConcerns) ? null : model.SkinConcerns.Trim();
        await _db.SaveChangesAsync();
        await _clientAccounts.TryLinkClientRecordAsync(client);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTreatment(TreatmentFormModel model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == model.ClientId && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Проверьте поля процедуры";
            return RedirectToAction(nameof(Details), new { id = model.ClientId });
        }

        string? procedureName = model.ProcedureName;
        decimal? price = model.Price;
        if (model.ServiceId is int serviceId)
        {
            var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == serviceId && s.MasterProfileId == profile.Id);
            if (service == null)
            {
                TempData["Error"] = "Услуга не найдена";
                return RedirectToAction(nameof(Details), new { id = model.ClientId });
            }

            procedureName ??= service.Name;
            price ??= service.Price;
        }

        _db.TreatmentRecords.Add(new TreatmentRecord
        {
            ClientId = client.Id,
            MasterProfileId = profile.Id,
            ServiceId = model.ServiceId,
            PerformedAt = model.PerformedAt,
            ProcedureName = procedureName,
            ProductsUsed = NullIfEmpty(model.ProductsUsed),
            EquipmentUsed = NullIfEmpty(model.EquipmentUsed),
            Notes = NullIfEmpty(model.Notes),
            Price = price,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.ClientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> AddPhoto(PhotoUploadModel model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == model.ClientId && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        if (model.File == null || model.File.Length == 0)
        {
            TempData["Error"] = "Выберите файл фото";
            return RedirectToAction(nameof(Details), new { id = model.ClientId });
        }

        if (!AllowedImageTypes.Contains(model.File.ContentType))
        {
            TempData["Error"] = "Допустимы JPEG, PNG, WebP, GIF";
            return RedirectToAction(nameof(Details), new { id = model.ClientId });
        }

        if (model.File.Length > MaxPhotoBytes)
        {
            TempData["Error"] = "Фото не больше 2 МБ";
            return RedirectToAction(nameof(Details), new { id = model.ClientId });
        }

        await using var ms = new MemoryStream();
        await model.File.CopyToAsync(ms);

        _db.ClientPhotos.Add(new ClientPhoto
        {
            ClientId = client.Id,
            MasterProfileId = profile.Id,
            Kind = model.Kind,
            Data = ms.ToArray(),
            ContentType = model.File.ContentType,
            Caption = NullIfEmpty(model.Caption),
            TakenAt = model.TakenAt,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.ClientId });
    }

    [HttpGet]
    public async Task<IActionResult> Photo(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var photo = await _db.ClientPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.MasterProfileId == profile.Id);
        if (photo == null) return NotFound();

        return File(photo.Data, photo.ContentType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(int id, int clientId)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var photo = await _db.ClientPhotos.FirstOrDefaultAsync(p => p.Id == id && p.MasterProfileId == profile.Id);
        if (photo != null)
        {
            _db.ClientPhotos.Remove(photo);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id = clientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddHomeCare(HomeCareFormModel model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == model.ClientId && c.MasterProfileId == profile.Id);
        if (client == null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Проверьте поля домашнего ухода";
            return RedirectToAction(nameof(Details), new { id = model.ClientId });
        }

        _db.HomeCarePrescriptions.Add(new HomeCarePrescription
        {
            ClientId = client.Id,
            MasterProfileId = profile.Id,
            Title = model.Title.Trim(),
            Instructions = NullIfEmpty(model.Instructions),
            Products = NullIfEmpty(model.Products),
            PrescribedAt = model.PrescribedAt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.ClientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeCare(int id, int clientId)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var item = await _db.HomeCarePrescriptions
            .FirstOrDefaultAsync(h => h.Id == id && h.MasterProfileId == profile.Id);
        if (item != null)
        {
            item.IsActive = !item.IsActive;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new { id = clientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.MasterProfileId == profile.Id && !c.IsArchived);
        if (client == null) return NotFound();

        client.IsArchived = true;
        client.ArchivedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Клиент «{client.Name}» убран из списка";
        return RedirectToAction(nameof(Index));
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }

    private async Task<(MasterProfile? Profile, ApplicationUser? User)> GetProfileAndUserAsync()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return (null, null);

        var profile = await _profiles.EnsureForUserAsync(user);
        return (profile, user);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
