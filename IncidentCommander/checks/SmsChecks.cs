using System.Text.Json;

internal static class SmsChecks
{
    internal static void Run()
    {
        var now = DateTimeOffset.UtcNow;
        var approval = new ApprovalRequest("ABCD12", "incident", "deployment", now.AddMinutes(4));
        var valid = $$"""{"data":[{"direction":"incoming","from":"+46700000001","to":"+46700000002","created":"{{now:O}}","message":"APPROVE ABCD12"}]}""";
        bool? Parse(string json, ApprovalRequest? request = null)
        {
            using var document = JsonDocument.Parse(json);
            return ApprovalSms.ParseDecision(document.RootElement, request ?? approval, "+46700000001", "+46700000002", now);
        }
        if (Parse(valid) != true || Parse(valid.Replace("APPROVE", "DENY", StringComparison.Ordinal)) != false ||
            Parse(valid.Replace("incoming", "outgoing", StringComparison.Ordinal)) != null ||
            Parse(valid.Replace("+46700000001", "+46700000003", StringComparison.Ordinal)) != null ||
            Parse(valid.Replace("+46700000002", "+46700000003", StringComparison.Ordinal)) != null ||
            Parse(valid.Replace("ABCD12", "STALE", StringComparison.Ordinal)) != null ||
            Parse(valid, approval with { ExpiresAt = now }) != null ||
            Parse(valid, approval with { ExpiresAt = now.AddMinutes(6) }) != null ||
            Parse(valid.Replace($"{now:O}", $"{now.AddSeconds(1):O}", StringComparison.Ordinal)) != null ||
            Parse(valid.Replace("APPROVE ABCD12", "APPROVE ABCD12 extra", StringComparison.Ordinal)) != null || Parse("{\"data\":[null,{}]}") != null)
            throw new InvalidOperationException("SMS approval trust boundary check failed.");
        Console.WriteLine("SMS parser checks passed.");
    }
}
