using GlowBook.Web.Models;
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
    private readonly BookingService _booking;

    public BlogController(BlogService blog, UserManager<ApplicationUser> users, BookingService booking)
    {
        _blog = blog;
        _users = users;
        _booking = booking;
    }

    [HttpGet("{username}")]
    public async Task<IActionResult> Profile(string username, CancellationToken ct)
    {
        var list = await _blog.BuildBlogListAsync(username, ct);
        if (list == null) return NotFound();

        var profile = await _blog.GetProfileByUsernameAsync(username, ct);
        ViewBag.Description = profile?.Description;
        ViewBag.Location = PublicPageService.FormatLocation(profile?.City, profile?.Address);
        ViewBag.CanBook = profile != null && _booking.IsOnlineBookingEnabled(profile);
        return View("Profile", list);
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
}
