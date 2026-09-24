using System.Text.Json;

var clock = new CheckClock();
var simulation = new IncidentSimulation(clock);
Reject(() => simulation.Rollback());
simulation.Inject();
var firstId = simulation.Id;
Reject(() => simulation.Rollback());
Reject(() => simulation.VerifyRecovery());
Reject(() => simulation.ReadTool("rollback_deployment"));
foreach (var name in new[] { "get_metrics", "get_logs", "get_recent_deployments", "inspect_db_connections", "get_dependency_health" })
{
    using var json = JsonDocument.Parse(simulation.ReadTool(name));
    Check(json.RootElement.GetProperty("evidence_id").GetString() is not null, "Every tool must cite evidence.");
}
Check(simulation.Events.Count == 1, "Investigation tools must be read-only.");
simulation.ProposeRollback();
var code = simulation.PendingApproval!.Code;
Reject(() => simulation.Decide("wrong-code", true));
Check(simulation.Phase == IncidentPhase.WaitingForApproval, "Bad code must not approve an action.");
simulation.Decide(code, false);
Check(simulation.BeforeRecovery is null, "Denied actions must not create recovery comparisons.");
Reject(() => simulation.Rollback());
Reject(() => simulation.Decide(code, true));
Check(simulation.ErrorRate > 0.01, "Denial must not fix the incident.");

simulation.Inject();
Check(simulation.Id != firstId, "Each incident needs a new identity.");
simulation.ProposeRollback();
code = simulation.PendingApproval!.Code;
simulation.Reset();
Reject(() => simulation.Decide(code, true));
simulation.Inject();
simulation.ProposeRollback();
Reject(() => simulation.Decide(code, true));
clock.Now = simulation.PendingApproval!.ExpiresAt;
Reject(() => simulation.Decide(simulation.PendingApproval!.Code, true));
Check(simulation.Phase == IncidentPhase.Escalated && simulation.PendingApproval is null, "Expiry must invalidate approval.");
Reject(() => simulation.Rollback());

simulation.Inject();
simulation.ProposeRollback();
code = simulation.PendingApproval!.Code;
simulation.Decide(code, true);
Reject(() => simulation.Decide(code, true));
var beforeRollback = (simulation.ErrorRate, simulation.LatencyMs, simulation.ConnectionUsage);
simulation.Rollback();
Check(simulation.BeforeRecovery == beforeRollback, "Recovery must retain the actual pre-rollback measurements.");
Reject(() => simulation.Rollback());
simulation.VerifyRecovery();
Check(simulation.Phase == IncidentPhase.Resolved && simulation.ErrorRate < 0.01, "Approved rollback must recover.");
Check(simulation.Events.Zip(simulation.Events.Skip(1)).All(pair => pair.First.At <= pair.Second.At), "Timeline must be ordered.");
var snapshot = simulation.Events;
var snapshotCount = snapshot.Count;
Parallel.For(0, 100, _ =>
{
    simulation.Record("Check", "Concurrent event.");
    foreach (var entry in simulation.Events)
        Check(entry is not null, "Snapshots must contain complete events.");
});
Check(simulation.Events.Count == snapshotCount + 100, "Concurrent writes must not lose events.");
simulation.Reset();
Check(simulation.BeforeRecovery is null, "Reset must clear the previous incident's recovery comparison.");
Check(snapshot.Count == snapshotCount && simulation.Events.Count == 0, "Rendering snapshots must survive writes and reset.");
Console.WriteLine("Incident simulation checks passed.");
SmsChecks.Run();

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void Reject(Action action)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }
    catch (ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException("Expected invalid action to be rejected.");
}

internal sealed class CheckClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
