using GlowBook.Web.Data;
using GlowBook.Web.Helpers;
using GlowBook.Web.Models.Booking;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Services;

public class PublicPageService
{
    private readonly ApplicationDbContext _db;

    public PublicPageService(ApplicationDbContext db) => _db = db;

    public async Task<PublicMasterPageViewModel> BuildAsync(
        MasterProfile profile,
        PublicBookingForm booking,
        string slug,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow.Date;
        var portfolio = await _db.MasterPortfolioPhotos
            .AsNoTracking()
            .Where(p => p.MasterProfileId == profile.Id)
            .OrderBy(p => p.SortOrder)
            .ThenByDescending(p => p.Id)
            .Select(p => new PublicMasterPageViewModel.PortfolioCard { Id = p.Id, Caption = p.Caption })
            .ToListAsync(ct);

        var promos = await _db.MasterPromos
            .AsNoTracking()
            .Where(p => p.MasterProfileId == profile.Id && p.IsActive
                && (p.ValidUntil == null || p.ValidUntil >= now))
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

        var reviews = await _db.MasterReviews
            .AsNoTracking()
            .Where(r => r.MasterProfileId == profile.Id && r.IsPublished)
            .OrderByDescending(r => r.CreatedAt)
            .Take(30)
            .ToListAsync(ct);

        var location = FormatLocation(profile.City, profile.Address);
        var showMap = profile.ShowOnMap && !string.IsNullOrWhiteSpace(location);

        return new PublicMasterPageViewModel
        {
            Booking = booking,
            Slug = slug,
            Portfolio = portfolio,
            Promos = promos,
            Reviews = reviews,
            ReviewCount = reviews.Count,
            AverageRating = reviews.Count == 0 ? null : Math.Round(reviews.Average(r => r.Rating), 1),
            Location = location,
            MapEmbedUrl = showMap ? MapUrls.Embed(location!) : null,
            MapOpenUrl = showMap ? MapUrls.Open(location!) : null,
            AccentColor = CalendarColors.Normalize(profile.PageAccentColor)
        };
    }

    public async Task<(bool Ok, string Message)> SubmitReviewAsync(
        MasterProfile profile,
        PublicReviewForm form,
        CancellationToken ct = default)
    {
        var phone = PhoneHelper.Normalize(form.AuthorPhone);
        if (string.IsNullOrEmpty(phone))
            return (false, "Укажите телефон");

        var recent = await _db.MasterReviews.AnyAsync(
            r => r.MasterProfileId == profile.Id && r.AuthorPhone == phone, ct);
        if (recent)
            return (false, "Вы уже оставляли отзыв этому мастеру");

        var clients = await _db.Clients
            .Where(c => c.MasterProfileId == profile.Id && !c.IsArchived)
            .ToListAsync(ct);
        var client = clients.FirstOrDefault(c => PhoneHelper.Match(c.Phone, form.AuthorPhone));

        var visited = false;
        if (client != null)
        {
            visited = await _db.Appointments.AnyAsync(
                a => a.ClientId == client.Id
                    && a.MasterProfileId == profile.Id
                    && a.Status == AppointmentStatus.Completed, ct);
        }

        _db.MasterReviews.Add(new MasterReview
        {
            MasterProfileId = profile.Id,
            ClientId = client?.Id,
            AuthorName = form.AuthorName.Trim(),
            AuthorPhone = phone,
            Rating = Math.Clamp(form.Rating, 1, 5),
            Text = form.Text.Trim(),
            IsPublished = visited,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return visited
            ? (true, "Спасибо! Отзыв опубликован.")
            : (true, "Спасибо! Отзыв появится после проверки мастером.");
    }

    public static string? FormatLocation(string? city, string? address)
    {
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(address))
            return null;
        if (string.IsNullOrWhiteSpace(city))
            return address;
        if (string.IsNullOrWhiteSpace(address))
            return city;
        return city + ", " + address;
    }
}

public static class MapUrls
{
    public static string Embed(string location) =>
        "https://yandex.ru/map-widget/v1/?z=16&text=" + Uri.EscapeDataString(location);

    public static string Open(string location) =>
        "https://yandex.ru/maps/?text=" + Uri.EscapeDataString(location);

    public const string AddOrganization = "https://business.yandex.ru/";
}
