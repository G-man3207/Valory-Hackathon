if (args.Length != 0 && !(args.Length == 1 && args[0] == "--live-kubernetes"))
    throw new ArgumentException("Only --live-kubernetes is supported; omit it for offline checks.");

var clock = new CheckClock();
var state = new IncidentState(clock);
var healthy = new LabMetrics(0, 27, 2, 2);
var fault = new LabMetrics(0.8, 173, 2, 2);
Check(
    state.Metrics is null && state.Phase != IncidentPhase.Healthy,
    "Unknown metrics cannot imply healthy infrastructure."
);
Reject(state.BeginRecovery);
Reject(() => state.Reset(healthy with { ErrorRate = double.NaN }, "uid:1"));
Reject(() => state.Reset(healthy, ""));
state.Reset(fault, "uid:1");
Check(
    state.Phase == IncidentPhase.Escalated && state.Metrics == fault,
    "Failed preflight must retain actual failures."
);
state.Reset(healthy, "uid:1");
state.Inject(fault, "uid:2");
var firstId = state.Id;
Reject(() => state.Inject(fault, "uid:3"));
Reject(state.BeginRecovery);
Reject(state.VerifyRecovery);
state.ProposeRollback();
var code = state.PendingApproval!.Code;
Check(
    state.PendingApproval.DeploymentId == "uid:2",
    "Approval must bind the observed deployment UID and generation."
);
Reject(() => state.Decide("wrong-code", true));
Check(state.Phase == IncidentPhase.WaitingForApproval, "Bad codes cannot approve actions.");
state.Decide(code, false);
Reject(state.BeginRecovery);
Reject(() => state.Decide(code, true));
Check(
    state.BeforeRecovery is null && state.Metrics == fault,
    "Denial must preserve real failures without a recovery comparison."
);

state.Reset(healthy, "uid:3");
state.Inject(fault, "uid:4");
Check(state.Id != firstId, "Each incident needs a new identity.");
state.ProposeRollback();
code = state.PendingApproval!.Code;
state.Reset(healthy, "uid:5");
Reject(() => state.Decide(code, true));
state.Inject(fault, "uid:6");
state.ProposeRollback();
Reject(() => state.Decide(code, true));
clock.Now = state.PendingApproval!.ExpiresAt;
Reject(() => state.Decide(state.PendingApproval!.Code, true));
Check(
    state.Phase == IncidentPhase.Escalated && state.PendingApproval is null,
    "Expiry must invalidate approval."
);
Reject(state.BeginRecovery);

foreach (
    var recovered in new[]
    {
        healthy,
        fault,
        healthy with
        {
            ReadyReplicas = 1,
        },
        healthy with
        {
            DesiredReplicas = 0,
            ReadyReplicas = 0,
        },
    }
)
{
    state.Reset(healthy, "uid:7");
    state.Inject(fault, "uid:8");
    state.ProposeRollback();
    code = state.PendingApproval!.Code;
    state.Decide(code, true);
    Reject(() => state.Decide(code, true));
    Reject(() => state.RollbackCompleted(recovered, "uid:9"));
    state.BeginRecovery();
    Check(
        state.BeforeRecovery == fault && state.Metrics == fault,
        "Beginning recovery must capture, never modify, real measurements."
    );
    Reject(state.BeginRecovery);
    state.RollbackCompleted(recovered, "uid:9");
    Reject(() => state.RollbackCompleted(recovered, "uid:10"));
    state.VerifyRecovery();
    Check(
        state.Metrics == recovered && state.DeploymentId == "uid:9",
        "Completion must retain the observed deployment and measurements."
    );
    Check(
        state.Phase == (recovered == healthy ? IncidentPhase.Resolved : IncidentPhase.Escalated),
        "Resolution requires all probes and requested replicas to succeed."
    );
}

Check(
    state.Events.Zip(state.Events.Skip(1)).All(pair => pair.First.At <= pair.Second.At),
    "Timeline must be ordered."
);
var snapshot = state.Events;
var snapshotCount = snapshot.Count;
Parallel.For(
    0,
    100,
    _ =>
    {
        state.Record("Check", "Concurrent event.");
        foreach (var entry in state.Events)
            Check(entry is not null, "Snapshots must contain complete events.");
    }
);
Check(state.Events.Count == snapshotCount + 100, "Concurrent writes must not lose events.");
state.Reset(healthy, "uid:11");
Check(
    state.BeforeRecovery is null && state.PendingApproval is null && state.Id == "",
    "Reset must clear prior incident state."
);
Check(
    snapshot.Count == snapshotCount
        && state.Events.Count == 1
        && state.Events[0].Actor == "Preflight",
    "Reset retains only new live preflight; previous snapshots remain stable."
);
Console.WriteLine("Real incident state checks passed.");
SmsChecks.Run();
KubernetesLabChecks.Run();
if (args.Length == 1)
    await KubernetesLabChecks.RunLiveAsync();

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
