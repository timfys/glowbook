using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GlowBook.Web.Configuration;
using GlowBook.Web.Data;
using GlowBook.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Services;

public class YooKassaService
{
    private readonly HttpClient _http;
    private readonly YooKassaSettings _settings;
    private readonly GlowBookSettings _glowBook;
    private readonly ApplicationDbContext _db;
    private readonly SubscriptionService _subscriptions;

    public YooKassaService(
        HttpClient http,
        IOptions<YooKassaSettings> settings,
        IOptions<GlowBookSettings> glowBook,
        ApplicationDbContext db,
        SubscriptionService subscriptions)
    {
        _http = http;
        _settings = settings.Value;
        _glowBook = glowBook.Value;
        _db = db;
        _subscriptions = subscriptions;
    }

    public async Task<(bool Ok, string? RedirectUrl, string? Error)> CreatePremiumPaymentAsync(
        int masterProfileId,
        string returnUrl,
        CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
            return (false, null, "YooKassa is not configured");

        var idempotenceKey = Guid.NewGuid().ToString();
        var amount = _glowBook.PremiumPriceRub.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        var payload = new
        {
            amount = new { value = amount, currency = "RUB" },
            capture = true,
            confirmation = new { type = "redirect", return_url = returnUrl },
            description = "GlowBook Premium — 1 month",
            metadata = new { masterProfileId = masterProfileId.ToString() }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.yookassa.ru/v3/payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ShopId}:{_settings.SecretKey}")));
        request.Headers.Add("Idempotence-Key", idempotenceKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (false, null, $"YooKassa error: {response.StatusCode}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var paymentId = root.GetProperty("id").GetString()!;
        var status = root.GetProperty("status").GetString() ?? "pending";
        var redirect = root.GetProperty("confirmation").GetProperty("confirmation_url").GetString();

        _db.PaymentOrders.Add(new PaymentOrder
        {
            MasterProfileId = masterProfileId,
            YooKassaPaymentId = paymentId,
            AmountRub = _glowBook.PremiumPriceRub,
            Status = status
        });
        await _db.SaveChangesAsync(ct);

        return (true, redirect, null);
    }

    public async Task HandleWebhookAsync(JsonElement notification, CancellationToken ct = default)
    {
        if (!notification.TryGetProperty("event", out var eventProp))
            return;

        var eventName = eventProp.GetString();
        if (eventName != "payment.succeeded" && eventName != "payment.canceled")
            return;

        if (!notification.TryGetProperty("object", out var obj))
            return;

        var paymentId = obj.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(paymentId))
            return;

        var status = obj.GetProperty("status").GetString() ?? "unknown";
        var order = await _db.PaymentOrders.FirstOrDefaultAsync(x => x.YooKassaPaymentId == paymentId, ct);
        if (order == null)
            return;

        order.Status = status;
        if (status == "succeeded")
        {
            order.PaidAt = DateTime.UtcNow;
            await _subscriptions.ActivatePremiumAsync(order.MasterProfileId, paymentId, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryConfirmPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
            return false;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.yookassa.ru/v3/payments/{paymentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ShopId}:{_settings.SecretKey}")));

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return false;

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();

        var order = await _db.PaymentOrders.FirstOrDefaultAsync(x => x.YooKassaPaymentId == paymentId, ct);
        if (order == null)
            return false;

        order.Status = status ?? order.Status;
        if (status == "succeeded" && order.PaidAt == null)
        {
            order.PaidAt = DateTime.UtcNow;
            await _subscriptions.ActivatePremiumAsync(order.MasterProfileId, paymentId, ct);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        await _db.SaveChangesAsync(ct);
        return status == "succeeded";
    }

    public async Task<bool> TryConfirmLatestPendingAsync(int masterProfileId, CancellationToken ct = default)
    {
        var order = await _db.PaymentOrders
            .Where(o => o.MasterProfileId == masterProfileId && o.Status != "succeeded")
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (order == null)
            return false;
        return await TryConfirmPaymentAsync(order.YooKassaPaymentId, ct);
    }
}
