using GlowBook.Web.Models;
using GlowBook.Web.Models.Enums;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Controllers;

[AllowAnonymous]
[Route("u")]
public class BlogController : Controller
{
    private readonly BlogService _blog;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly BookingService _booking;
    private readonly PublicPageService _pages;
    private readonly ClientAccountService _clients;

    public BlogController(
        BlogService blog,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        BookingService booking,
        PublicPageService pages,
        ClientAccountService clients)
    {
        _blog = blog;
        _users = users;
        _signIn = signIn;
        _booking = booking;
        _pages = pages;
        _clients = clients;
    }

    [HttpGet("{username}")]
    public async Task<IActionResult> Profile(
        string username,
        int? serviceId,
        DateTime? date,
        CancellationToken ct)
    {
        var profile = await _booking.GetBookableProfileAsync(username, ct);
        if (profile == null) return NotFound();

        var canBook = _booking.IsOnlineBookingEnabled(profile);
        var form = canBook
            ? await BuildBookingFormAsync(profile, serviceId, date)
            : new Models.Booking.PublicBookingForm
            {
                Profile = profile,
                Services = profile.Services.OrderBy(s => s.SortOrder).ToList()
            };

        var page = await _pages.BuildAsync(profile, form, profile.BookingSlug, ct);
        page.CanBook = canBook;
        page.ReviewFlash = TempData["ReviewFlash"] as string;
        page.ChatError = TempData["ChatError"] as string;

        var viewer = await _users.GetUserAsync(User);
        page.IsAuthenticated = viewer != null;
        page.ViewerDisplayName = viewer?.DisplayName;
        page.ShowChat = viewer == null || viewer.Id != profile.UserId;

        return View("~/Views/Book/Index.cshtml", page);
    }

    [HttpPost("{username}/chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartChat(string username, string? guestName, CancellationToken ct)
    {
        var profile = await _blog.GetProfileByUsernameAsync(username, ct);
        if (profile == null) return NotFound();

        var user = await _users.GetUserAsync(User);
        if (user != null && user.Id == profile.UserId)
            return RedirectToAction(nameof(Profile), new { username });

        if (user == null)
        {
            var name = (guestName ?? "").Trim();
            if (name.Length < 2)
            {
                TempData["ChatError"] = "Укажите имя, чтобы написать мастеру";
                return RedirectToAction(nameof(Profile), new { username });
            }

            var guid = Guid.NewGuid().ToString("N");
            user = new ApplicationUser
            {
                UserName = $"guest_{guid}",
                Email = $"guest_{guid}@guest.glowbook.local",
                DisplayName = name,
                AccountType = UserAccountType.Client,
                EmailConfirmed = true
            };

            var created = await _users.CreateAsync(user);
            if (!created.Succeeded)
            {
                TempData["ChatError"] = "Не удалось начать чат. Попробуйте ещё раз.";
                return RedirectToAction(nameof(Profile), new { username });
            }

            await _signIn.SignInAsync(user, isPersistent: true);
        }
        else if (string.IsNullOrWhiteSpace(user.DisplayName) && !string.IsNullOrWhiteSpace(guestName))
        {
            user.DisplayName = guestName.Trim();
            await _users.UpdateAsync(user);
        }

        var client = await _clients.EnsureChatClientAsync(profile, user, guestName, ct);
        return RedirectToAction("Thread", "Chat", new { clientRecordId = client.Id });
    }

    [HttpGet("{username}/blog")]
    public async Task<IActionResult> Index(string username, CancellationToken ct)
    {
        var list = await _blog.BuildBlogListAsync(username, ct);
        if (list == null) return NotFound();
        return View(list);
    }

    [HttpGet("{username}/blog/{articleSlug}")]
    public async Task<IActionResult> Article(string username, string articleSlug, CancellationToken ct)
    {
        var viewer = await _users.GetUserAsync(User);
        var model = await _blog.BuildArticleAsync(username, articleSlug, viewer, ct);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet("~/blog/cover/{id:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Cover(int id, CancellationToken ct)
    {
        var cover = await _blog.GetCoverAsync(id, ct);
        if (cover == null) return NotFound();
        return File(cover.Value.Data, cover.Value.ContentType);
    }

    private async Task<Models.Booking.PublicBookingForm> BuildBookingFormAsync(
        Models.Entities.MasterProfile profile,
        int? serviceId,
        DateTime? date)
    {
        var formDate = date?.Date ?? DateTime.Today;
        if (formDate < DateTime.Today)
            formDate = DateTime.Today;

        var selectedServiceId = serviceId ?? profile.Services.OrderBy(s => s.SortOrder).FirstOrDefault()?.Id ?? 0;
        var times = selectedServiceId > 0
            ? await _booking.GetAvailableTimesAsync(profile.Id, selectedServiceId, formDate)
            : new List<string>();

        return new Models.Booking.PublicBookingForm
        {
            Profile = profile,
            Services = profile.Services.OrderBy(s => s.SortOrder).ToList(),
            ServiceId = selectedServiceId,
            Date = formDate,
            AvailableTimes = times,
            Time = times.FirstOrDefault() ?? string.Empty
        };
    }
}
