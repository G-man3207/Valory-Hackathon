using System.Text.Json;

internal static class KubernetesLabChecks
{
    internal static void Run()
    {
        const string bad = "http://missing-service/inventory";
        const string good = "http://inventory/inventory";
        using var deployment = JsonDocument.Parse(
            """
            {"metadata":{"uid":"unique-deployment","generation":7},"spec":{"template":{"spec":{"containers":[
                {"name":"sidecar","env":[]},
                {"name":"app","env":[{"name":"OTHER","value":"keep"},{"name":"INVENTORY_URL","value":"http://missing-service/inventory"}]}]}}}}
            """
        );
        using var patch = JsonDocument.Parse(
            KubernetesLab.BuildPatch(deployment.RootElement, bad, good)
        );
        var operations = patch.RootElement;
        if (
            operations.GetArrayLength() != 4
            || operations[0].GetProperty("op").GetString() != "test"
            || operations[0].GetProperty("value").GetString() != "unique-deployment"
            || operations[1].GetProperty("op").GetString() != "test"
            || operations[1].GetProperty("value").GetInt64() != 7
            || operations[2].GetProperty("op").GetString() != "test"
            || operations[2].GetProperty("value").GetString() != bad
            || operations[3].GetProperty("path").GetString()
                != "/spec/template/spec/containers/1/env/1/value"
            || operations[3].GetProperty("value").GetString() != good
        )
            throw new InvalidOperationException(
                "Kubernetes mutation must test identity, version and old value."
            );
        try
        {
            KubernetesLab.BuildPatch(deployment.RootElement, good, bad);
            throw new ArgumentException("Unexpected configuration was accepted.");
        }
        catch (InvalidOperationException) { }
        Console.WriteLine("Kubernetes patch guard checks passed.");
    }
}
