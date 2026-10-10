using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GlowBook.Web.Data;
using GlowBook.Web.Helpers;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Blog;
using GlowBook.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class BlogService
{
    private static readonly Regex MultiNewline = new(@"\n{3,}", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly BookingService _booking;
    private readonly PremiumAccessService _premium;

    public BlogService(ApplicationDbContext db, BookingService booking, PremiumAccessService premium)
    {
        _db = db;
        _booking = booking;
        _premium = premium;
    }

    public async Task<MasterProfile?> GetProfileByUsernameAsync(string username, CancellationToken ct = default)
    {
        var slug = NormalizeUsername(username);
        if (slug == null) return null;
        return await _db.MasterProfiles
            .Include(p => p.Subscription)
            .FirstOrDefaultAsync(p => p.BookingSlug == slug, ct);
    }

    public async Task<List<MasterArticle>> ListForStudioAsync(int masterProfileId, CancellationToken ct = default) =>
        await _db.MasterArticles
            .AsNoTracking()
            .Where(a => a.MasterProfileId == masterProfileId)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(ct);

    public async Task<MasterArticle?> GetOwnedAsync(int masterProfileId, int id, CancellationToken ct = default) =>
        await _db.MasterArticles.FirstOrDefaultAsync(a => a.Id == id && a.MasterProfileId == masterProfileId, ct);

    public async Task<(bool Ok, string? Error, MasterArticle? Article)> CreateAsync(
        MasterProfile profile,
        ArticleEditForm form,
        CancellationToken ct = default)
    {
        var title = form.Title.Trim();
        if (title.Length == 0)
            return (false, "Укажите заголовок", null);

        var slug = await EnsureUniqueArticleSlugAsync(profile.Id, form.Slug, title, null, ct);
        var article = new MasterArticle
        {
            MasterProfileId = profile.Id,
            Title = title,
            Slug = slug,
            Excerpt = NullIfEmpty(form.Excerpt),
            Body = form.Body.Trim(),
            IsPremiumOnly = form.IsPremiumOnly,
            IsPublished = form.IsPublished,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = form.IsPublished ? DateTime.UtcNow : null
        };

        if (form.Cover is { Length: > 0 })
        {
            var (data, contentType, error) = await ImageContentHelper.ReadAsync(form.Cover);
            if (error != null)
                return (false, error, null);
            article.CoverData = data;
            article.CoverContentType = contentType;
        }

        _db.MasterArticles.Add(article);
        await _db.SaveChangesAsync(ct);
        return (true, null, article);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(
        MasterArticle article,
        ArticleEditForm form,
        CancellationToken ct = default)
    {
        var title = form.Title.Trim();
        if (title.Length == 0)
            return (false, "Укажите заголовок");

        article.Title = title;
        article.Slug = await EnsureUniqueArticleSlugAsync(article.MasterProfileId, form.Slug, title, article.Id, ct);
        article.Excerpt = NullIfEmpty(form.Excerpt);
        article.Body = form.Body.Trim();
        article.IsPremiumOnly = form.IsPremiumOnly;
        article.UpdatedAt = DateTime.UtcNow;

        if (form.IsPublished && !article.IsPublished)
            article.PublishedAt = DateTime.UtcNow;
        if (!form.IsPublished)
            article.PublishedAt = article.PublishedAt;
        article.IsPublished = form.IsPublished;
        if (article.IsPublished && article.PublishedAt == null)
            article.PublishedAt = DateTime.UtcNow;

        if (form.RemoveCover && form.Cover == null)
        {
            article.CoverData = null;
            article.CoverContentType = null;
        }

        if (form.Cover is { Length: > 0 })
        {
            var (data, contentType, error) = await ImageContentHelper.ReadAsync(form.Cover);
            if (error != null)
                return (false, error);
            article.CoverData = data;
            article.CoverContentType = contentType;
        }

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task DeleteAsync(MasterArticle article, CancellationToken ct = default)
    {
        _db.MasterArticles.Remove(article);
        await _db.SaveChangesAsync(ct);
    }

    public async Task TogglePublishAsync(MasterArticle article, CancellationToken ct = default)
    {
        article.IsPublished = !article.IsPublished;
        article.UpdatedAt = DateTime.UtcNow;
        if (article.IsPublished && article.PublishedAt == null)
            article.PublishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PublicBlogListViewModel?> BuildBlogListAsync(string username, CancellationToken ct = default)
    {
        var profile = await GetProfileByUsernameAsync(username, ct);
        if (profile == null) return null;

        var articles = await _db.MasterArticles
            .AsNoTracking()
            .Where(a => a.MasterProfileId == profile.Id && a.IsPublished)
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Select(a => new PublicArticleCard
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                Excerpt = a.Excerpt,
                IsPremiumOnly = a.IsPremiumOnly,
                HasCover = a.CoverContentType != null,
                PublishedAt = a.PublishedAt ?? a.CreatedAt
            })
            .ToListAsync(ct);

        return new PublicBlogListViewModel
        {
            Username = profile.BookingSlug,
            BusinessName = profile.BusinessName,
            Specialization = profile.Specialization,
            AccentColor = CalendarColors.Normalize(profile.PageAccentColor),
            HasAvatar = profile.HasAvatar,
            ProfileId = profile.Id,
            AvatarVersion = profile.AvatarUpdatedAt?.Ticks,
            BookingUrl = _booking.IsOnlineBookingEnabled(profile)
                ? $"/book/{profile.BookingSlug}"
                : null,
            Articles = articles
        };
    }

    public async Task<PublicArticleViewModel?> BuildArticleAsync(
        string username,
        string articleSlug,
        ApplicationUser? viewer,
        CancellationToken ct = default)
    {
        var profile = await GetProfileByUsernameAsync(username, ct);
        if (profile == null) return null;

        var slug = MasterProfileService.Slugify(articleSlug);
        var article = await _db.MasterArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.MasterProfileId == profile.Id && a.Slug == slug && a.IsPublished, ct);
        if (article == null) return null;

        var viewerPremium = await _premium.HasPremiumAsync(viewer, ct);
        var isOwner = viewer != null && profile.UserId == viewer.Id;
        var canRead = !article.IsPremiumOnly || viewerPremium || isOwner;

        return new PublicArticleViewModel
        {
            Username = profile.BookingSlug,
            BusinessName = profile.BusinessName,
            Specialization = profile.Specialization,
            AccentColor = CalendarColors.Normalize(profile.PageAccentColor),
            HasAvatar = profile.HasAvatar,
            ProfileId = profile.Id,
            AvatarVersion = profile.AvatarUpdatedAt?.Ticks,
            BookingUrl = _booking.IsOnlineBookingEnabled(profile)
                ? $"/book/{profile.BookingSlug}"
                : null,
            ArticleId = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Excerpt = article.Excerpt,
            BodyHtml = canRead ? FormatBodyHtml(article.Body) : null,
            IsPremiumOnly = article.IsPremiumOnly,
            HasCover = article.HasCover,
            CanReadFull = canRead,
            IsAuthenticated = viewer != null,
            ViewerHasPremium = viewerPremium,
            PublishedAt = article.PublishedAt ?? article.CreatedAt
        };
    }

    public async Task<(byte[] Data, string ContentType)?> GetCoverAsync(int articleId, CancellationToken ct = default)
    {
        var row = await _db.MasterArticles
            .AsNoTracking()
            .Where(a => a.Id == articleId && a.CoverData != null)
            .Select(a => new { a.CoverData, a.CoverContentType })
            .FirstOrDefaultAsync(ct);
        if (row?.CoverData == null || string.IsNullOrWhiteSpace(row.CoverContentType))
            return null;
        return (row.CoverData, row.CoverContentType);
    }

    public async Task<List<PublicArticleCard>> RecentForMasterAsync(int masterProfileId, int take = 6, CancellationToken ct = default) =>
        await _db.MasterArticles
            .AsNoTracking()
            .Where(a => a.MasterProfileId == masterProfileId && a.IsPublished)
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Take(take)
            .Select(a => new PublicArticleCard
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                Excerpt = a.Excerpt,
                IsPremiumOnly = a.IsPremiumOnly,
                HasCover = a.CoverContentType != null,
                PublishedAt = a.PublishedAt ?? a.CreatedAt
            })
            .ToListAsync(ct);

    public static string? NormalizeUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim().TrimStart('@').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(s) ? null : MasterProfileService.Slugify(s);
    }

    public static bool IsValidUsername(string username)
    {
        if (username.Length is < 3 or > 32) return false;
        return Regex.IsMatch(username, @"^[a-z0-9](?:[a-z0-9_-]*[a-z0-9])?$");
    }

    public async Task<(string? Normalized, string? Error)> ValidateUsernameAvailableAsync(
        string? username,
        CancellationToken ct = default)
    {
        var normalized = NormalizeUsername(username);
        if (normalized == null)
            return (null, "Укажите юзернейм");
        if (!IsValidUsername(normalized))
            return (null, "Юзернейм: 3–32 символа, латиница, цифры, _ и -");

        var taken = await _db.MasterProfiles.AnyAsync(p => p.BookingSlug == normalized, ct);
        if (taken)
            return (null, "Этот юзернейм уже занят");

        return (normalized, null);
    }

    public async Task<(bool Ok, string? Error)> TrySetUsernameAsync(
        MasterProfile profile,
        string? username,
        CancellationToken ct = default)
    {
        var normalized = NormalizeUsername(username);
        if (normalized == null)
            return (false, "Укажите юзернейм");

        // Текущий slug можно оставить как есть (в т.ч. старые кириллические).
        if (string.Equals(normalized, profile.BookingSlug, StringComparison.OrdinalIgnoreCase)
            || string.Equals((username ?? "").Trim().TrimStart('@'), profile.BookingSlug, StringComparison.OrdinalIgnoreCase))
            return (true, null);

        if (!IsValidUsername(normalized))
            return (false, "Юзернейм: 3–32 символа, латиница, цифры, _ и -");

        var taken = await _db.MasterProfiles.AnyAsync(
            p => p.BookingSlug == normalized && p.Id != profile.Id, ct);
        if (taken)
            return (false, "Этот юзернейм уже занят");

        profile.BookingSlug = normalized;
        return (true, null);
    }

    private async Task<string> EnsureUniqueArticleSlugAsync(
        int masterProfileId,
        string? requested,
        string title,
        int? excludeId,
        CancellationToken ct)
    {
        var baseSlug = !string.IsNullOrWhiteSpace(requested)
            ? MasterProfileService.Slugify(requested)
            : MasterProfileService.Slugify(title);
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "post";

        var slug = baseSlug;
        var i = 1;
        while (await _db.MasterArticles.AnyAsync(
                   a => a.MasterProfileId == masterProfileId
                        && a.Slug == slug
                        && (excludeId == null || a.Id != excludeId), ct))
        {
            slug = $"{baseSlug}-{++i}";
        }

        return slug;
    }

    public static string FormatBodyHtml(string body)
    {
        var text = body.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        text = MultiNewline.Replace(text, "\n\n");
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sb = new StringBuilder();
        foreach (var p in paragraphs)
        {
            var escaped = WebUtility.HtmlEncode(p).Replace("\n", "<br />");
            sb.Append("<p>").Append(escaped).Append("</p>");
        }

        return sb.ToString();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
