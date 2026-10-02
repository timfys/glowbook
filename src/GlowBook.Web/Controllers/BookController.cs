using GlowBook.Web.Models.Booking;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Controllers;

[AllowAnonymous]
[Route("book")]
public class BookController : Controller
{
    private readonly BookingService _booking;
    private readonly PublicPageService _pages;

    public BookController(BookingService booking, PublicPageService pages)
    {
        _booking = booking;
        _pages = pages;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Index(string slug, int? serviceId, DateTime? date)
    {
        var profile = await _booking.GetBookableProfileAsync(slug);
        if (profile == null)
            return NotFound();

        if (!_booking.IsOnlineBookingEnabled(profile))
            return View("Unavailable", profile);

        var form = await BuildBookingFormAsync(profile, serviceId, date);
        var page = await _pages.BuildAsync(profile, form, slug);
        page.ReviewFlash = TempData["ReviewFlash"] as string;
        return View(page);
    }

    [HttpPost("{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string slug, PublicBookingForm model)
    {
        var profile = await _booking.GetBookableProfileAsync(slug);
        if (profile == null)
            return NotFound();

        if (!_booking.IsOnlineBookingEnabled(profile))
            return View("Unavailable", profile);

        if (!ModelState.IsValid)
        {
            await FillBookingFormAsync(profile, model);
            return View(await _pages.BuildAsync(profile, model, slug));
        }

        var (ok, error) = await _booking.CreatePublicBookingAsync(profile, model);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Не удалось записаться");
            await FillBookingFormAsync(profile, model);
            return View(await _pages.BuildAsync(profile, model, slug));
        }

        return View("Success", profile);
    }

    [HttpPost("{slug}/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string slug, [Bind(Prefix = "Review")] PublicReviewForm model)
    {
        var profile = await _booking.GetBookableProfileAsync(slug);
        if (profile == null)
            return NotFound();

        if (!_booking.IsOnlineBookingEnabled(profile))
            return View("Unavailable", profile);

        if (!ModelState.IsValid)
        {
            TempData["ReviewFlash"] = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage).FirstOrDefault() ?? "Проверьте отзыв";
            return RedirectToAction(nameof(Index), new { slug });
        }

        var (_, message) = await _pages.SubmitReviewAsync(profile, model);
        TempData["ReviewFlash"] = message;
        return RedirectToAction(nameof(Index), new { slug });
    }

    private async Task<PublicBookingForm> BuildBookingFormAsync(
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

        return new PublicBookingForm
        {
            Profile = profile,
            Services = profile.Services.OrderBy(s => s.SortOrder).ToList(),
            ServiceId = selectedServiceId,
            Date = formDate,
            AvailableTimes = times,
            Time = times.FirstOrDefault() ?? string.Empty
        };
    }

    private async Task FillBookingFormAsync(Models.Entities.MasterProfile profile, PublicBookingForm model)
    {
        model.Profile = profile;
        model.Services = profile.Services.OrderBy(s => s.SortOrder).ToList();
        model.AvailableTimes = await _booking.GetAvailableTimesAsync(profile.Id, model.ServiceId, model.Date);
    }
}
