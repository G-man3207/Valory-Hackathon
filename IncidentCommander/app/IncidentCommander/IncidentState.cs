using System.Security.Cryptography;

public enum IncidentPhase
{
    Healthy,
    Investigating,
    WaitingForApproval,
    Executing,
    Verifying,
    Resolved,
    Escalated,
}

public sealed record IncidentEvent(DateTimeOffset At, string Actor, string Message);

public sealed record ApprovalRequest(
    string Code,
    string IncidentId,
    string DeploymentId,
    DateTimeOffset ExpiresAt
);

public sealed class IncidentState(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly List<IncidentEvent> _events = [];
    private readonly Lock _eventGate = new();
    public IncidentPhase Phase { get; private set; } = IncidentPhase.Escalated;
    public string Id { get; private set; } = "";
    public string DeploymentId { get; private set; } = "";
    public LabMetrics? Metrics { get; private set; }
    public LabMetrics? BeforeRecovery { get; private set; }
    public ApprovalRequest? PendingApproval { get; private set; }
    public IReadOnlyList<IncidentEvent> Events
    {
        get
        {
            lock (_eventGate)
            {
                return _events.ToArray();
            }
        }
    }

    public void Reset(LabMetrics metrics, string deploymentId)
    {
        ValidateObservation(metrics, deploymentId);
        Id = "";
        DeploymentId = deploymentId;
        Metrics = metrics;
        BeforeRecovery = null;
        PendingApproval = null;
        Phase = IsHealthy(metrics) ? IncidentPhase.Healthy : IncidentPhase.Escalated;
        lock (_eventGate)
        {
            _events.Clear();
        }
        Record(
            "Preflight",
            IsHealthy(metrics)
                ? "Live checkout probes succeeded and all requested replicas are ready."
                : "Live preflight found request failures or unavailable replicas; operator review required."
        );
    }

    public void Inject(LabMetrics metrics, string deploymentId)
    {
        RequirePhase(IncidentPhase.Healthy);
        ValidateObservation(metrics, deploymentId);
        Id = Guid.NewGuid().ToString("N");
        DeploymentId = deploymentId;
        Metrics = metrics;
        BeforeRecovery = null;
        PendingApproval = null;
        Phase = IncidentPhase.Investigating;
        Record(
            "Monitor",
            FormattableString.Invariant(
                $"Live checkout probes after fault deployment: error rate {metrics.ErrorRate:P0}; {metrics.ReadyReplicas}/{metrics.DesiredReplicas} replicas ready."
            )
        );
    }

    public void ProposeRollback()
    {
        RequirePhase(IncidentPhase.Investigating);
        PendingApproval = new(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(6)),
            Id,
            DeploymentId,
            _time.GetUtcNow().AddMinutes(5)
        );
        Phase = IncidentPhase.WaitingForApproval;
        Record(
            "Commander",
            $"Recovery of deployment {DeploymentId} proposed; waiting for human approval."
        );
    }

    public void Decide(string code, bool approved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        RequirePhase(IncidentPhase.WaitingForApproval);
        var pending =
            PendingApproval
            ?? throw new InvalidOperationException("No action is awaiting approval.");
        if (pending.ExpiresAt <= _time.GetUtcNow())
        {
            Escalate("Approval expired; no action executed.");
            throw new InvalidOperationException("Approval expired.");
        }
        if (
            pending.IncidentId != Id
            || pending.DeploymentId != DeploymentId
            || !string.Equals(pending.Code, code, StringComparison.Ordinal)
        )
            throw new InvalidOperationException("Approval does not match the pending action.");
        PendingApproval = null;
        Phase = approved ? IncidentPhase.Executing : IncidentPhase.Escalated;
        Record(
            "Human",
            approved ? "Recovery approved." : "Recovery denied; manual investigation required."
        );
    }

    public void BeginRecovery()
    {
        RequirePhase(IncidentPhase.Executing);
        if (BeforeRecovery is not null)
            throw new InvalidOperationException("Recovery has already started.");
        BeforeRecovery =
            Metrics ?? throw new InvalidOperationException("No live measurements are available.");
        Record("Executor", "Beginning approved recovery; captured live incident measurements.");
    }

    public void RollbackCompleted(LabMetrics metrics, string newDeploymentId)
    {
        RequirePhase(IncidentPhase.Executing);
        if (BeforeRecovery is null)
            throw new InvalidOperationException("Recovery has not started.");
        ValidateObservation(metrics, newDeploymentId);
        Metrics = metrics;
        DeploymentId = newDeploymentId;
        Phase = IncidentPhase.Verifying;
        Record(
            "Executor",
            "Recovery deployment completed; checking its live request and replica measurements."
        );
    }

    public void VerifyRecovery()
    {
        RequirePhase(IncidentPhase.Verifying);
        if (Metrics is not { } metrics || !IsHealthy(metrics))
        {
            Escalate("Live recovery verification failed; manual investigation required.");
            return;
        }
        Phase = IncidentPhase.Resolved;
        Record(
            "Verifier",
            "Live checkout probes all succeeded and all requested replicas are ready."
        );
    }

    public void Record(string actor, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        lock (_eventGate)
        {
            _events.Add(new(_time.GetUtcNow(), actor, message));
        }
    }

    public void Escalate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        PendingApproval = null;
        Phase = IncidentPhase.Escalated;
        Record("Commander", reason);
    }

    private static bool IsHealthy(LabMetrics metrics) =>
        metrics.ErrorRate == 0
        && metrics.DesiredReplicas > 0
        && metrics.ReadyReplicas == metrics.DesiredReplicas;

    private static void ValidateObservation(LabMetrics metrics, string deploymentId)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (
            !double.IsFinite(metrics.ErrorRate)
            || metrics.ErrorRate is < 0 or > 1
            || metrics.LatencyMs < 0
            || metrics.ReadyReplicas < 0
            || metrics.DesiredReplicas < 0
        )
            throw new ArgumentException(
                "Live measurements contain invalid values.",
                nameof(metrics)
            );
    }

    private void RequirePhase(IncidentPhase expected)
    {
        if (Phase != expected)
            throw new InvalidOperationException(
                $"Action requires {expected}; current phase is {Phase}."
            );
    }
}
