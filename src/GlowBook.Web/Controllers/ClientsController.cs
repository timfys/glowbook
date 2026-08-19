using GlowBook.Web.Data;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlowBook.Web.Models;

namespace GlowBook.Web.Controllers;

[Authorize]
public class ClientsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;

    public ClientsController(ApplicationDbContext db, UserManager<ApplicationUser> users, MasterProfileService profiles)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var query = _db.Clients.Where(c => c.MasterProfileId == profile.Id);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q) || c.Phone.Contains(q));

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
        return RedirectToAction(nameof(Index));
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }
}
