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

    public BookController(BookingService booking) => _booking = booking;

    [HttpGet("{slug}")]
    public async Task<IActionResult> Index(string slug, int? serviceId, DateTime? date)
    {
        var profile = await _booking.GetBookableProfileAsync(slug);
        if (profile == null)
            return NotFound();

        if (!_booking.IsOnlineBookingEnabled(profile))
            return View("Unavailable", profile);

        var formDate = date?.Date ?? DateTime.Today;
        if (formDate < DateTime.Today)
            formDate = DateTime.Today;

        var selectedServiceId = serviceId ?? profile.Services.OrderBy(s => s.SortOrder).FirstOrDefault()?.Id ?? 0;
        var times = selectedServiceId > 0
            ? await _booking.GetAvailableTimesAsync(profile.Id, selectedServiceId, formDate)
            : new List<string>();

        var model = new PublicBookingForm
        {
            Profile = profile,
            Services = profile.Services.OrderBy(s => s.SortOrder).ToList(),
            ServiceId = selectedServiceId,
            Date = formDate,
            AvailableTimes = times,
            Time = times.FirstOrDefault() ?? string.Empty
        };

        ViewBag.Slug = slug;
        return View(model);
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
            model.Profile = profile;
            model.Services = profile.Services.OrderBy(s => s.SortOrder).ToList();
            model.AvailableTimes = await _booking.GetAvailableTimesAsync(profile.Id, model.ServiceId, model.Date);
            ViewBag.Slug = slug;
            return View(model);
        }

        var (ok, error) = await _booking.CreatePublicBookingAsync(profile, model);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Booking failed");
            model.Profile = profile;
            model.Services = profile.Services.OrderBy(s => s.SortOrder).ToList();
            model.AvailableTimes = await _booking.GetAvailableTimesAsync(profile.Id, model.ServiceId, model.Date);
            ViewBag.Slug = slug;
            return View(model);
        }

        return View("Success", profile);
    }
}
