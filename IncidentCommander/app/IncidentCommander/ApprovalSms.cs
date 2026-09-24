using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class ApprovalSms : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _from;
    private readonly string _oncall;

    private ApprovalSms(string username, string password, string from, string oncall)
    {
        _from = from;
        _oncall = oncall;
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri("https://api.46elks.com/a1/"),
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 1_048_576,
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))
        );
    }

    public static ApprovalSms? FromEnvironment()
    {
        var username = Environment.GetEnvironmentVariable("ELKS_API_USERNAME");
        var password = Environment.GetEnvironmentVariable("ELKS_API_PASSWORD");
        var from = Environment.GetEnvironmentVariable("ELKS_FROM");
        var oncall = Environment.GetEnvironmentVariable("ONCALL_PHONE");
        if (
            string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(from)
            || string.IsNullOrWhiteSpace(oncall)
        )
            return null;
        if (!IsPhone(from) || !IsPhone(oncall) || username.Contains(':', StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SMS configuration is invalid; phone numbers must use E.164."
            );
        return new ApprovalSms(username, password, from, oncall);
    }

    private static bool IsPhone(string value) =>
        value.Length is >= 3 and <= 16
        && value[0] == '+'
        && value[1] is >= '1' and <= '9'
        && value.AsSpan(2).IndexOfAnyExceptInRange('0', '9') < 0;

    public async Task SendAsync(ApprovalRequest approval, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (DateTimeOffset.UtcNow >= approval.ExpiresAt)
            throw new InvalidOperationException("Approval expired.");
        using var body = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["from"] = _from,
                ["to"] = _oncall,
                ["message"] =
                    "K8s DEMO: restore checkout inventory route in namespace incident-lab. "
                    + $"Reply APPROVE {approval.Code} or DENY {approval.Code} within 5 minutes.",
            }
        );
        using var response = await _http.PostAsync("sms", body, token).ConfigureAwait(false);
        EnsureSuccess(response);
    }

    public async Task<bool?> GetDecisionAsync(ApprovalRequest approval, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (DateTimeOffset.UtcNow >= approval.ExpiresAt)
            return null;
        var since = approval
            .ExpiresAt.AddMinutes(-5)
            .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ff", CultureInfo.InvariantCulture);
        using var response = await _http
            .GetAsync(
                $"sms?limit=100&to={Uri.EscapeDataString(_from)}&end={Uri.EscapeDataString(since)}",
                token
            )
            .ConfigureAwait(false);
        EnsureSuccess(response);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(token).ConfigureAwait(false)
        );
        return ParseDecision(document.RootElement, approval, _oncall, _from, DateTimeOffset.UtcNow);
    }

    internal static bool? ParseDecision(
        JsonElement root,
        ApprovalRequest approval,
        string sender,
        string recipient,
        DateTimeOffset now
    )
    {
        if (
            now >= approval.ExpiresAt
            || root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
        )
            return null;
        // ponytail: one bounded history page; use authenticated webhook delivery if traffic reaches 100 replies per five minutes.
        if (data.GetArrayLength() >= 100)
            return null;
        bool? decision = null;
        DateTimeOffset? first = null;
        foreach (var sms in data.EnumerateArray())
        {
            if (
                Text(sms, "direction") != "incoming"
                || Text(sms, "from") != sender
                || Text(sms, "to") != recipient
                || !DateTimeOffset.TryParse(
                    Text(sms, "created"),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var created
                )
                || created < approval.ExpiresAt.AddMinutes(-5)
                || created >= approval.ExpiresAt
                || created > now
            )
                continue;
            var message = Text(sms, "message");
            bool? candidate =
                message == $"APPROVE {approval.Code}" ? true
                : message == $"DENY {approval.Code}" ? false
                : null;
            if (candidate is null || (first is not null && created > first))
                continue;
            // A denial wins if conflicting replies have identical timestamps.
            decision = first == created && decision == false ? false : candidate;
            first = created;
        }
        return decision;
    }

    private static string? Text(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                "SMS provider request failed.",
                null,
                response.StatusCode
            );
    }

    public void Dispose() => _http.Dispose();
}
