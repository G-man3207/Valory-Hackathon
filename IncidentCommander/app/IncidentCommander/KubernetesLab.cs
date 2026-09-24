using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed record LabMetrics(
    double ErrorRate,
    int LatencyMs,
    int ReadyReplicas,
    int DesiredReplicas
);

public sealed class KubernetesLab : IDisposable
{
    private const string GoodUrl = "http://inventory/inventory";

    // ponytail: bounded configuration chaos; add other fault types only with equally guarded recovery.
    internal static IReadOnlyList<string> FaultUrls { get; } =
        Array.AsReadOnly<string>([
            "http://missing-service/inventory",
            "http://inventory:81/inventory",
            "http://inventory/missing",
        ]);
    private static readonly string[] KubectlScope =
    [
        "--kubeconfig",
        "/home/dev/.config/incident-commander/lab.kubeconfig",
        "--context",
        "kind-incident-lab",
        "--namespace",
        "incident-lab",
        "--request-timeout=15s",
    ];
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public async Task<LabMetrics> ReadMetricsAsync(CancellationToken token)
    {
        var failures = 0;
        var durations = new List<int>();
        // ponytail: ten live samples are demo evidence, not production percentile telemetry.
        for (var sample = 0; sample < 10; sample++)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                using var response = await _http
                    .GetAsync("http://127.0.0.1:19080/checkout", token)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    failures++;
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException
                    && !token.IsCancellationRequested
                )
            {
                failures++;
            }
            durations.Add((int)timer.ElapsedMilliseconds);
        }
        using var deployment = await DeploymentAsync(token).ConfigureAwait(false);
        var root = deployment.RootElement;
        durations.Sort();
        return new LabMetrics(
            failures / 10.0,
            durations[^1],
            root.GetProperty("status").TryGetProperty("readyReplicas", out var ready)
                ? ready.GetInt32()
                : 0,
            root.GetProperty("spec").GetProperty("replicas").GetInt32()
        );
    }

    public static async Task<string> GetDeploymentIdAsync(CancellationToken token)
    {
        using var deployment = await DeploymentAsync(token).ConfigureAwait(false);
        return DeploymentId(deployment.RootElement);
    }

    public static async Task InjectAsync(CancellationToken token)
    {
        using var deployment = await DeploymentAsync(token).ConfigureAwait(false);
        var fault = FaultUrls[RandomNumberGenerator.GetInt32(FaultUrls.Count)];
        await PatchAsync(deployment.RootElement, GoodUrl, fault, token).ConfigureAwait(false);
    }

    public static async Task RecoverAsync(string expectedDeploymentId, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedDeploymentId);
        using var deployment = await DeploymentAsync(token).ConfigureAwait(false);
        if (DeploymentId(deployment.RootElement) != expectedDeploymentId)
            throw new InvalidOperationException(
                "Deployment changed after approval was requested; recovery rejected."
            );
        await PatchAsync(
                deployment.RootElement,
                DependencyValue(deployment.RootElement),
                GoodUrl,
                token
            )
            .ConfigureAwait(false);
    }

    public static async Task ResetAsync(CancellationToken token)
    {
        using var deployment = await DeploymentAsync(token).ConfigureAwait(false);
        var root = deployment.RootElement;
        if (DependencyValue(root) == GoodUrl)
            return;
        await PatchAsync(root, DependencyValue(root), GoodUrl, token).ConfigureAwait(false);
    }

    public async Task<string> ReadToolAsync(string name, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        object evidence = name switch
        {
            "get_metrics" => await ReadMetricsAsync(token).ConfigureAwait(false),
            "get_logs" => await KubectlAsync(
                    token,
                    "logs",
                    "deployment/checkout",
                    "--all-pods=true",
                    "--tail=40",
                    "--limit-bytes=16000",
                    "--timestamps=true"
                )
                .ConfigureAwait(false),
            "get_recent_deployments" => JsonSerializer.Deserialize<JsonElement>(
                await KubectlAsync(
                        token,
                        "get",
                        "deployments",
                        "checkout",
                        "inventory",
                        "-o",
                        "json"
                    )
                    .ConfigureAwait(false)
            ),
            "inspect_service_routes" => new
            {
                deployments = JsonSerializer.Deserialize<JsonElement>(
                    await KubectlAsync(
                            token,
                            "get",
                            "deployments",
                            "checkout",
                            "inventory",
                            "-o",
                            "json"
                        )
                        .ConfigureAwait(false)
                ),
                services = JsonSerializer.Deserialize<JsonElement>(
                    await KubectlAsync(
                            token,
                            "get",
                            "services",
                            "checkout",
                            "inventory",
                            "-o",
                            "json"
                        )
                        .ConfigureAwait(false)
                ),
                endpoints = JsonSerializer.Deserialize<JsonElement>(
                    await KubectlAsync(token, "get", "endpointslices", "-o", "json")
                        .ConfigureAwait(false)
                ),
            },
            "get_dependency_health" => new
            {
                deployment = JsonSerializer.Deserialize<JsonElement>(
                    await KubectlAsync(token, "get", "deployment", "inventory", "-o", "json")
                        .ConfigureAwait(false)
                ),
                logs = await KubectlAsync(
                        token,
                        "logs",
                        "deployment/inventory",
                        "--all-pods=true",
                        "--tail=20",
                        "--limit-bytes=8000",
                        "--timestamps=true"
                    )
                    .ConfigureAwait(false),
            },
            _ => throw new ArgumentException("Unknown read-only lab tool.", nameof(name)),
        };
        return JsonSerializer.Serialize(
            new
            {
                evidence_id = Guid.NewGuid().ToString("N"),
                observed_at = DateTimeOffset.UtcNow,
                source = "kind-incident-lab/incident-lab",
                evidence,
            }
        );
    }

    private static string DeploymentId(JsonElement deployment)
    {
        var metadata = deployment.GetProperty("metadata");
        return metadata.GetProperty("uid").GetString()
            + ":"
            + metadata.GetProperty("generation").GetInt64().ToString(CultureInfo.InvariantCulture);
    }

    private static string DependencyValue(JsonElement deployment) =>
        deployment
            .GetProperty("spec")
            .GetProperty("template")
            .GetProperty("spec")
            .GetProperty("containers")
            .EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == "app")
            .GetProperty("env")
            .EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "INVENTORY_URL")
            .GetProperty("value")
            .GetString()
        ?? throw new InvalidOperationException("Missing inventory URL.");

    internal static string BuildPatch(JsonElement deployment, string oldUrl, string newUrl)
    {
        if (
            DependencyValue(deployment) != oldUrl
            || !(
                oldUrl == GoodUrl && FaultUrls.Contains(newUrl)
                || newUrl == GoodUrl && FaultUrls.Contains(oldUrl)
            )
        )
            throw new InvalidOperationException(
                "Lab configuration differs from the known scenario; mutation rejected."
            );
        var containers = deployment
            .GetProperty("spec")
            .GetProperty("template")
            .GetProperty("spec")
            .GetProperty("containers");
        var container = containers
            .EnumerateArray()
            .Select((value, index) => (value, index))
            .Single(c => c.value.GetProperty("name").GetString() == "app");
        var env = container
            .value.GetProperty("env")
            .EnumerateArray()
            .Select((value, index) => (value, index))
            .Single(e => e.value.GetProperty("name").GetString() == "INVENTORY_URL");
        var path = $"/spec/template/spec/containers/{container.index}/env/{env.index}/value";
        return JsonSerializer.Serialize(
            new object[]
            {
                new
                {
                    op = "test",
                    path = "/metadata/uid",
                    value = deployment.GetProperty("metadata").GetProperty("uid").GetString(),
                },
                new
                {
                    op = "test",
                    path = "/metadata/generation",
                    value = deployment.GetProperty("metadata").GetProperty("generation").GetInt64(),
                },
                new
                {
                    op = "test",
                    path,
                    value = oldUrl,
                },
                new
                {
                    op = "replace",
                    path,
                    value = newUrl,
                },
            }
        );
    }

    private static async Task PatchAsync(
        JsonElement deployment,
        string oldUrl,
        string newUrl,
        CancellationToken token
    )
    {
        var patch = BuildPatch(deployment, oldUrl, newUrl);
        await KubectlAsync(
                token,
                "patch",
                "deployment",
                "checkout",
                "--type=json",
                "--patch",
                patch
            )
            .ConfigureAwait(false);
        await KubectlAsync(token, "rollout", "status", "deployment/checkout", "--timeout=90s")
            .ConfigureAwait(false);
        // Ready can precede service routing convergence; observe traffic before taking the incident sample.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        var expected =
            newUrl == GoodUrl
                ? System.Net.HttpStatusCode.OK
                : System.Net.HttpStatusCode.ServiceUnavailable;
        for (var consecutive = 0; consecutive < 3; )
        {
            try
            {
                using var response = await http.GetAsync(
                        "http://127.0.0.1:19080/checkout",
                        timeout.Token
                    )
                    .ConfigureAwait(false);
                consecutive = response.StatusCode == expected ? consecutive + 1 : 0;
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException
                    && !timeout.IsCancellationRequested
                )
            {
                consecutive = 0;
            }
            if (consecutive < 3)
                await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token).ConfigureAwait(false);
        }
    }

    private static async Task<JsonDocument> DeploymentAsync(CancellationToken token) =>
        JsonDocument.Parse(
            await KubectlAsync(token, "get", "deployment", "checkout", "-o", "json")
                .ConfigureAwait(false)
        );

    private static async Task<string> KubectlAsync(
        CancellationToken token,
        params string[] arguments
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(95));
        var start = new ProcessStartInfo("kubectl")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in KubectlScope.Concat(arguments))
            start.ArgumentList.Add(argument);
        using var process =
            Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the lab command.");
        try
        {
            var output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var error = ReadBoundedAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(timeout.Token))
                .ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("The local Kubernetes lab command failed.");
            return await output.ConfigureAwait(false);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var output = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + count > 262144)
                throw new InvalidOperationException("Lab command output exceeded its limit.");
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }

    public void Dispose() => _http.Dispose();
}
