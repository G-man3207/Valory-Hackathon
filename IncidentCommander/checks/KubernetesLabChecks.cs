using System.Text.Json;

internal static class KubernetesLabChecks
{
    internal static void Run()
    {
        const string good = "http://inventory/inventory";
        if (KubernetesLab.FaultUrls.Count != 3 || KubernetesLab.FaultUrls.Distinct().Count() != 3)
            throw new InvalidOperationException(
                "Chaos must expose three distinct fixed lab faults."
            );
        foreach (var fault in KubernetesLab.FaultUrls)
        {
            CheckPatch(good, fault);
            CheckPatch(fault, good);
            foreach (var otherFault in KubernetesLab.FaultUrls)
                RejectPatch(fault, fault, otherFault);
            RejectPatch(fault, good, fault);
            RejectPatch(fault, fault, "http://unknown/inventory");
        }
        RejectPatch(good, good, good);
        RejectPatch(good, good, "http://unknown/inventory");
        RejectPatch("http://unknown/inventory", "http://unknown/inventory", good);
        Console.WriteLine("All three Kubernetes fault roundtrips and mutation guards passed.");
    }

    private static JsonDocument Deployment(string url) =>
        JsonDocument.Parse(
            """
            {"metadata":{"uid":"unique-deployment","generation":7},"spec":{"template":{"spec":{"containers":[
                {"name":"sidecar","env":[]},
                {"name":"app","env":[{"name":"OTHER","value":"keep"},{"name":"INVENTORY_URL","value":$URL}]}]}}}}
            """.Replace("$URL", JsonSerializer.Serialize(url), StringComparison.Ordinal)
        );

    private static void CheckPatch(string oldUrl, string newUrl)
    {
        using var deployment = Deployment(oldUrl);
        using var patch = JsonDocument.Parse(
            KubernetesLab.BuildPatch(deployment.RootElement, oldUrl, newUrl)
        );
        var operations = patch.RootElement;
        const string envPath = "/spec/template/spec/containers/1/env/1/value";
        if (
            operations.GetArrayLength() != 4
            || operations[0].GetProperty("op").GetString() != "test"
            || operations[0].GetProperty("path").GetString() != "/metadata/uid"
            || operations[0].GetProperty("value").GetString() != "unique-deployment"
            || operations[1].GetProperty("op").GetString() != "test"
            || operations[1].GetProperty("path").GetString() != "/metadata/generation"
            || operations[1].GetProperty("value").GetInt64() != 7
            || operations[2].GetProperty("op").GetString() != "test"
            || operations[2].GetProperty("path").GetString() != envPath
            || operations[2].GetProperty("value").GetString() != oldUrl
            || operations[3].GetProperty("op").GetString() != "replace"
            || operations[3].GetProperty("path").GetString() != envPath
            || operations[3].GetProperty("value").GetString() != newUrl
        )
            throw new InvalidOperationException(
                "Kubernetes mutation must test identity, generation and old value."
            );
    }

    private static void RejectPatch(string actualUrl, string oldUrl, string newUrl)
    {
        using var deployment = Deployment(actualUrl);
        try
        {
            KubernetesLab.BuildPatch(deployment.RootElement, oldUrl, newUrl);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Unexpected configuration or transition was accepted.");
    }

    internal static async Task RunLiveAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        using var lab = new KubernetesLab();
        try
        {
            await KubernetesLab.ResetAsync(timeout.Token);
            var before = await lab.ReadMetricsAsync(timeout.Token);
            RequireHealthy(before);
            var previousId = await KubernetesLab.GetDeploymentIdAsync(timeout.Token);
            await KubernetesLab.InjectAsync(timeout.Token);
            var deploymentId = await KubernetesLab.GetDeploymentIdAsync(timeout.Token);
            var broken = await lab.ReadMetricsAsync(timeout.Token);
            using var deployments = JsonDocument.Parse(
                await lab.ReadToolAsync("get_recent_deployments", timeout.Token)
            );
            var checkout = deployments
                .RootElement.GetProperty("evidence")
                .GetProperty("items")
                .EnumerateArray()
                .Single(item =>
                    item.GetProperty("metadata").GetProperty("name").GetString() == "checkout"
                );
            var faultUrl = checkout
                .GetProperty("spec")
                .GetProperty("template")
                .GetProperty("spec")
                .GetProperty("containers")
                .EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == "app")
                .GetProperty("env")
                .EnumerateArray()
                .Single(item => item.GetProperty("name").GetString() == "INVENTORY_URL")
                .GetProperty("value")
                .GetString();
            if (faultUrl is null || !KubernetesLab.FaultUrls.Contains(faultUrl))
                throw new InvalidOperationException("Random injection selected an unknown fault.");
            if (deploymentId == previousId || broken.ErrorRate <= 0)
                throw new InvalidOperationException(
                    "Live chaos did not produce a new failing deployment."
                );
            using var logs = JsonDocument.Parse(await lab.ReadToolAsync("get_logs", timeout.Token));
            var logText = logs.RootElement.GetProperty("evidence").GetString();
            if (
                logText is null
                || !logText.Contains(faultUrl, StringComparison.Ordinal)
                || !logText.Contains("\"status\": 503", StringComparison.Ordinal)
            )
                throw new InvalidOperationException(
                    "Live logs must show the configured dependency fault and HTTP 503."
                );
            await KubernetesLab.RecoverAsync(deploymentId, timeout.Token);
            RequireHealthy(await lab.ReadMetricsAsync(timeout.Token));
            Console.WriteLine(
                $"Live Kubernetes fault {faultUrl} recovered with ten successful probes."
            );
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(110));
            await KubernetesLab.ResetAsync(cleanup.Token);
        }
    }

    private static void RequireHealthy(LabMetrics metrics)
    {
        if (
            metrics.ErrorRate != 0
            || metrics.DesiredReplicas <= 0
            || metrics.ReadyReplicas != metrics.DesiredReplicas
        )
            throw new InvalidOperationException(
                "Live lab failed healthy response and replica checks."
            );
    }
}
