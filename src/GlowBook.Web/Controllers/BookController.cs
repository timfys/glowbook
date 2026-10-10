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

        return RedirectToAction("Profile", "Blog", new { username = profile.BookingSlug, serviceId, date = date?.ToString("yyyy-MM-dd") });
    }

    [HttpPost("{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string slug, PublicBookingForm model)
    {
        var profile = await _booking.GetBookableProfileAsync(slug);
        if (profile == null)
            return NotFound();

        if (!_booking.IsOnlineBookingEnabled(profile))
            return RedirectToAction("Profile", "Blog", new { username = profile.BookingSlug });

        if (!ModelState.IsValid)
        {
            await FillBookingFormAsync(profile, model);
            var invalidPage = await _pages.BuildAsync(profile, model, slug);
            invalidPage.CanBook = true;
            invalidPage.ShowChat = true;
            return View("Index", invalidPage);
        }

        var (ok, error) = await _booking.CreatePublicBookingAsync(profile, model);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Не удалось записаться");
            await FillBookingFormAsync(profile, model);
            var errorPage = await _pages.BuildAsync(profile, model, slug);
            errorPage.CanBook = true;
            errorPage.ShowChat = true;
            return View("Index", errorPage);
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

        if (!ModelState.IsValid)
        {
            TempData["ReviewFlash"] = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage).FirstOrDefault() ?? "Проверьте отзыв";
            return RedirectToAction("Profile", "Blog", new { username = profile.BookingSlug });
        }

        var (_, message) = await _pages.SubmitReviewAsync(profile, model);
        TempData["ReviewFlash"] = message;
        return RedirectToAction("Profile", "Blog", new { username = profile.BookingSlug });
    }

    private async Task FillBookingFormAsync(Models.Entities.MasterProfile profile, PublicBookingForm model)
    {
        model.Profile = profile;
        model.Services = profile.Services.OrderBy(s => s.SortOrder).ToList();
        model.AvailableTimes = await _booking.GetAvailableTimesAsync(profile.Id, model.ServiceId, model.Date);
    }
}
