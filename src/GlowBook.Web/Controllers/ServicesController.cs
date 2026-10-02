using GlowBook.Web.Data;
using GlowBook.Web.Filters;
using GlowBook.Web.Helpers;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireMasterAccount]
public class ServicesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public ServicesController(ApplicationDbContext db, UserManager<ApplicationUser> users, MasterProfileService profiles)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
    }

    public async Task<IActionResult> Index()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var services = await _db.Services
            .Where(s => s.MasterProfileId == profile.Id)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        return View(services);
    }

    public async Task<IActionResult> Create()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var used = await _db.Services
            .Where(s => s.MasterProfileId == profile.Id)
            .Select(s => s.Color)
            .ToListAsync();

        return View(new Service
        {
            Name = "",
            DurationMinutes = 60,
            Price = 0,
            IsActive = true,
            Color = CalendarColors.NextUnused(used)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Service model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!ModelState.IsValid) return View(model);

        model.MasterProfileId = profile.Id;
        model.Color = CalendarColors.Normalize(model.Color);
        _db.Services.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.MasterProfileId == profile.Id);
        if (service == null) return NotFound();

        return View(service);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Service model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.MasterProfileId == profile.Id);
        if (service == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        service.Name = model.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        service.DurationMinutes = model.DurationMinutes;
        service.Price = model.Price;
        service.IsActive = model.IsActive;
        service.Color = CalendarColors.Normalize(model.Color);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var service = await _db.Services
            .FirstOrDefaultAsync(s => s.Id == id && s.MasterProfileId == profile.Id);
        if (service == null) return NotFound();

        var hasAppointments = await _db.Appointments.AnyAsync(a => a.ServiceId == id);
        if (hasAppointments)
        {
            TempData["ServicesError"] = "Нельзя удалить услугу — есть записи с ней. Скройте услугу вместо удаления.";
            return RedirectToAction(nameof(Index));
        }

        _db.Services.Remove(service);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }
}
