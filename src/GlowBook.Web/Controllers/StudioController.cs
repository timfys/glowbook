using GlowBook.Web.Data;
using GlowBook.Web.Filters;
using GlowBook.Web.Helpers;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Blog;
using GlowBook.Web.Models.Studio;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[RequireMasterAccount]
[Route("studio")]
public class StudioController : Controller
{
    private const int MaxPortfolio = 20;
    private const int MaxPromos = 12;
    private const int MaxArticles = 100;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly MasterProfileService _profiles;
    private readonly BlogService _blog;

    public StudioController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        MasterProfileService profiles,
        BlogService blog)
    {
        _db = db;
        _users = users;
        _profiles = profiles;
        _blog = blog;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return View("PremiumRequired");
        return View(await BuildPageAsync(profile));
    }

    [HttpPost("appearance")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAppearance(string? accentColor, string? color, bool showOnMap)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        profile.PageAccentColor = CalendarColors.Normalize(accentColor ?? color);
        profile.ShowOnMap = showOnMap;
        await _db.SaveChangesAsync();
        TempData["StudioSaved"] = "Оформление сохранено";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("portfolio")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> AddPortfolio(IFormFile? photo, string? caption)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        if (photo is not { Length: > 0 })
        {
            TempData["StudioError"] = "Выберите фото";
            return RedirectToAction(nameof(Index));
        }

        var count = await _db.MasterPortfolioPhotos.CountAsync(p => p.MasterProfileId == profile.Id);
        if (count >= MaxPortfolio)
        {
            TempData["StudioError"] = $"Можно загрузить не больше {MaxPortfolio} фото";
            return RedirectToAction(nameof(Index));
        }

        var (data, contentType, error) = await ImageContentHelper.ReadAsync(photo);
        if (error != null || data == null || contentType == null)
        {
            TempData["StudioError"] = error ?? "Не удалось загрузить фото";
            return RedirectToAction(nameof(Index));
        }

        _db.MasterPortfolioPhotos.Add(new MasterPortfolioPhoto
        {
            MasterProfileId = profile.Id,
            Data = data,
            ContentType = contentType,
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            SortOrder = count,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["StudioSaved"] = "Фото добавлено в портфолио";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("portfolio/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePortfolio(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        var photo = await _db.MasterPortfolioPhotos
            .FirstOrDefaultAsync(p => p.Id == id && p.MasterProfileId == profile.Id);
        if (photo != null)
        {
            _db.MasterPortfolioPhotos.Remove(photo);
            await _db.SaveChangesAsync();
        }

        TempData["StudioSaved"] = "Фото удалено";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("photo/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Photo(int id)
    {
        var photo = await _db.MasterPortfolioPhotos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (photo == null || photo.Data.Length == 0)
            return NotFound();

        Response.Headers.CacheControl = "public,max-age=86400";
        return File(photo.Data, photo.ContentType);
    }

    [HttpPost("promo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPromo([Bind(Prefix = "Promo")] PromoForm model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        if (!ModelState.IsValid)
        {
            TempData["StudioError"] = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage).FirstOrDefault() ?? "Проверьте акцию";
            return RedirectToAction(nameof(Index));
        }

        var count = await _db.MasterPromos.CountAsync(p => p.MasterProfileId == profile.Id);
        if (count >= MaxPromos)
        {
            TempData["StudioError"] = $"Можно сохранить не больше {MaxPromos} акций";
            return RedirectToAction(nameof(Index));
        }

        _db.MasterPromos.Add(new MasterPromo
        {
            MasterProfileId = profile.Id,
            Title = model.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Badge = string.IsNullOrWhiteSpace(model.Badge) ? null : model.Badge.Trim(),
            ValidUntil = model.ValidUntil?.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["StudioSaved"] = "Акция добавлена";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("promo/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePromo(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        var promo = await _db.MasterPromos
            .FirstOrDefaultAsync(p => p.Id == id && p.MasterProfileId == profile.Id);
        if (promo != null)
        {
            promo.IsActive = !promo.IsActive;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("promo/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePromo(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        var promo = await _db.MasterPromos
            .FirstOrDefaultAsync(p => p.Id == id && p.MasterProfileId == profile.Id);
        if (promo != null)
        {
            _db.MasterPromos.Remove(promo);
            await _db.SaveChangesAsync();
        }

        TempData["StudioSaved"] = "Акция удалена";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("review/{id:int}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishReview(int id, bool publish = true)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        var review = await _db.MasterReviews
            .FirstOrDefaultAsync(r => r.Id == id && r.MasterProfileId == profile.Id);
        if (review != null)
        {
            review.IsPublished = publish;
            await _db.SaveChangesAsync();
        }

        TempData["StudioSaved"] = publish ? "Отзыв опубликован" : "Отзыв скрыт";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("review/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        if (!IsPremium(profile))
            return RedirectToSubscription();

        var review = await _db.MasterReviews
            .FirstOrDefaultAsync(r => r.Id == id && r.MasterProfileId == profile.Id);
        if (review != null)
        {
            _db.MasterReviews.Remove(review);
            await _db.SaveChangesAsync();
        }

        TempData["StudioSaved"] = "Отзыв удалён";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("blog")]
    public async Task<IActionResult> Blog()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var articles = await _blog.ListForStudioAsync(profile.Id);
        return View(new StudioBlogPageViewModel
        {
            BookingSlug = profile.BookingSlug,
            PublicBlogUrl = Url.Action("Index", "Blog", new { username = profile.BookingSlug }, Request.Scheme) ?? "",
            Articles = articles
        });
    }

    [HttpGet("blog/new")]
    public async Task<IActionResult> ArticleCreate()
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();
        return View("ArticleEdit", new ArticleEditForm());
    }

    [HttpPost("blog/new")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> ArticleCreate(ArticleEditForm model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        if (!ModelState.IsValid)
            return View("ArticleEdit", model);

        var count = await _db.MasterArticles.CountAsync(a => a.MasterProfileId == profile.Id);
        if (count >= MaxArticles)
        {
            ModelState.AddModelError(string.Empty, $"Не больше {MaxArticles} статей");
            return View("ArticleEdit", model);
        }

        var (ok, error, _) = await _blog.CreateAsync(profile, model);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Не удалось сохранить");
            return View("ArticleEdit", model);
        }

        TempData["StudioSaved"] = "Статья создана";
        return RedirectToAction(nameof(Blog));
    }

    [HttpGet("blog/{id:int}/edit")]
    public async Task<IActionResult> ArticleEdit(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var article = await _blog.GetOwnedAsync(profile.Id, id);
        if (article == null) return NotFound();

        return View(new ArticleEditForm
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Excerpt = article.Excerpt,
            Body = article.Body,
            IsPremiumOnly = article.IsPremiumOnly,
            IsPublished = article.IsPublished,
            HasCover = article.HasCover
        });
    }

    [HttpPost("blog/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> ArticleEdit(int id, ArticleEditForm model)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var article = await _blog.GetOwnedAsync(profile.Id, id);
        if (article == null) return NotFound();

        model.Id = id;
        model.HasCover = article.HasCover;
        if (!ModelState.IsValid)
            return View(model);

        var (ok, error) = await _blog.UpdateAsync(article, model);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Не удалось сохранить");
            return View(model);
        }

        TempData["StudioSaved"] = "Статья сохранена";
        return RedirectToAction(nameof(Blog));
    }

    [HttpPost("blog/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArticleToggle(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var article = await _blog.GetOwnedAsync(profile.Id, id);
        if (article == null) return NotFound();

        await _blog.TogglePublishAsync(article);
        TempData["StudioSaved"] = article.IsPublished ? "Статья опубликована" : "Статья скрыта";
        return RedirectToAction(nameof(Blog));
    }

    [HttpPost("blog/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArticleDelete(int id)
    {
        var profile = await GetProfileAsync();
        if (profile == null) return Challenge();

        var article = await _blog.GetOwnedAsync(profile.Id, id);
        if (article != null)
            await _blog.DeleteAsync(article);

        TempData["StudioSaved"] = "Статья удалена";
        return RedirectToAction(nameof(Blog));
    }

    private async Task<StudioPageViewModel> BuildPageAsync(MasterProfile profile)
    {
        var location = PublicPageService.FormatLocation(profile.City, profile.Address);
        return new StudioPageViewModel
        {
            BookingSlug = profile.BookingSlug,
            BookingUrl = Url.Action("Profile", "Blog", new { username = profile.BookingSlug }, Request.Scheme),
            IsPremium = profile.Subscription?.IsPremiumActive == true,
            AccentColor = CalendarColors.Normalize(profile.PageAccentColor),
            ShowOnMap = profile.ShowOnMap,
            City = profile.City,
            Address = profile.Address,
            Location = location,
            Portfolio = await _db.MasterPortfolioPhotos
                .AsNoTracking()
                .Where(p => p.MasterProfileId == profile.Id)
                .OrderBy(p => p.SortOrder)
                .ThenByDescending(p => p.Id)
                .Select(p => new MasterPortfolioPhoto
                {
                    Id = p.Id,
                    Caption = p.Caption,
                    CreatedAt = p.CreatedAt,
                    MasterProfileId = p.MasterProfileId
                })
                .ToListAsync(),
            Promos = await _db.MasterPromos
                .AsNoTracking()
                .Where(p => p.MasterProfileId == profile.Id)
                .OrderByDescending(p => p.Id)
                .ToListAsync(),
            Reviews = await _db.MasterReviews
                .AsNoTracking()
                .Where(r => r.MasterProfileId == profile.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync()
        };
    }

    private async Task<MasterProfile?> GetProfileAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user == null ? null : await _profiles.EnsureForUserAsync(user);
    }

    private static bool IsPremium(MasterProfile profile) =>
        profile.Subscription?.IsPremiumActive == true;

    private IActionResult RedirectToSubscription() =>
        RedirectToAction("Index", "Subscription");
}
