using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LeadDelivery.Application.Abstractions;
using LeadDelivery.Domain.Enums;
using LeadDelivery.Infrastructure.Persistence;
using LeadDelivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace LeadDelivery.Infrastructure.Channels;

public sealed class HttpDeliveryChannelExecutor : IDeliveryChannelExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LeadDeliveryDbContext _db;

    public HttpDeliveryChannelExecutor(IHttpClientFactory httpClientFactory, LeadDeliveryDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
    }

    public ChannelType ChannelType => ChannelType.Http;

    public async Task<DeliveryAttemptResult> ExecuteAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var cfg = JsonSerializer.Deserialize<HttpChannelConfig>(context.Endpoint.ChannelConfigJson)
            ?? throw new InvalidOperationException("Invalid HTTP channel config");

        var body = BuildBody(context.Endpoint.BodyTemplate, cfg, context.LeadDocumentJson);
        using var request = new HttpRequestMessage(new HttpMethod(cfg.Method ?? "POST"), cfg.Url);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", context.CorrelationId);

        var contentType = cfg.ContentType ?? "application/json";
        request.Content = new StringContent(body, Encoding.UTF8, contentType);

        await ApplyAuthAsync(request, cfg, cancellationToken);

        var client = _httpClientFactory.CreateClient("delivery");
        client.Timeout = TimeSpan.FromMilliseconds(cfg.TimeoutMs ?? 30000);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            sw.Stop();
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var successCodes = cfg.SuccessStatusCodes ?? [200, 201, 202, 204];
            var ok = successCodes.Contains((int)response.StatusCode);

            return new DeliveryAttemptResult
            {
                Status = ok ? DeliveryAttemptStatus.Succeeded : DeliveryAttemptStatus.Failed,
                HttpStatusCode = (int)response.StatusCode,
                RequestSnapshotRedacted = Redact(body),
                ResponseSnapshotRedacted = Redact(responseBody),
                ErrorMessage = ok ? null : $"HTTP {(int)response.StatusCode}",
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new DeliveryAttemptResult
            {
                Status = DeliveryAttemptStatus.Failed,
                RequestSnapshotRedacted = Redact(body),
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request, HttpChannelConfig cfg, CancellationToken ct)
    {
        if (cfg.CredentialProfileId is null) return;

        var profile = await _db.CredentialProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == cfg.CredentialProfileId, ct);
        if (profile is null) return;

        var auth = JsonSerializer.Deserialize<AuthConfig>(profile.ConfigJson) ?? new AuthConfig();
        switch (profile.AuthType.ToLowerInvariant())
        {
            case "basic":
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
                break;
            case "bearer":
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
                break;
        }
    }

    private static string BuildBody(string? endpointTemplate, HttpChannelConfig cfg, string leadJson)
    {
        var template = endpointTemplate ?? cfg.BodyTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            return leadJson;
        }

        return CottleTemplateRenderer.Render(template, leadJson);
    }

    private static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= 4000 ? value : value[..4000] + "…(truncated)";
    }

    private sealed class HttpChannelConfig
    {
        public string? Url { get; set; }
        public string? Method { get; set; }
        public string? ContentType { get; set; }
        public string? BodyTemplate { get; set; }
        public List<int>? SuccessStatusCodes { get; set; }
        public Guid? CredentialProfileId { get; set; }
        public int? TimeoutMs { get; set; }
    }

    private sealed class AuthConfig
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Token { get; set; }
    }
}