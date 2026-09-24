using System.Security.Cryptography;
using System.Text.Json;

public enum IncidentPhase
{
    Healthy, Investigating, WaitingForApproval, Executing, Verifying, Resolved, Escalated
}
public sealed record IncidentEvent(DateTimeOffset At, string Actor, string Message);
public sealed record ApprovalRequest(string Code, string IncidentId, string DeploymentId, DateTimeOffset ExpiresAt);

public sealed class IncidentSimulation(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly List<IncidentEvent> _events = [];
    private readonly Lock _eventGate = new();
    private DateTimeOffset _startedAt;
    public IncidentPhase Phase
    {
        get; private set;
    }
    public string Id { get; private set; } = "";
    public string DeploymentId { get; private set; } = "7e30b1";
    public double ErrorRate { get; private set; } = 0.002;
    public int LatencyMs { get; private set; } = 180;
    public double ConnectionUsage { get; private set; } = 0.42;
    public (double ErrorRate, int LatencyMs, double ConnectionUsage)? BeforeRecovery
    {
        get; private set;
    }
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
    public ApprovalRequest? PendingApproval
    {
        get; private set;
    }

    public void Inject()
    {
        if (Phase is not (IncidentPhase.Healthy or IncidentPhase.Resolved or IncidentPhase.Escalated))
            throw new InvalidOperationException("Reset or finish the current incident before starting another.");
        Reset();
        Id = Guid.NewGuid().ToString("N");
        _startedAt = _time.GetUtcNow();
        DeploymentId = "8f41a2";
        ErrorRate = 0.23;
        LatencyMs = 3200;
        ConnectionUsage = 0.97;
        Phase = IncidentPhase.Investigating;
        Record("Monitor", "Checkout error rate exceeded 5%; investigation opened.");
    }

    public void Reset()
    {
        Phase = IncidentPhase.Healthy;
        Id = "";
        DeploymentId = "7e30b1";
        ErrorRate = 0.002;
        LatencyMs = 180;
        ConnectionUsage = 0.42;
        BeforeRecovery = null;
        PendingApproval = null;
        lock (_eventGate)
        {
            _events.Clear();
        }
        _startedAt = _time.GetUtcNow();
    }

    public string ReadTool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var observedAt = _time.GetUtcNow();
        var degraded = ErrorRate > 0.01;
        object evidence = name switch
        {
            "get_metrics" => new
            {
                evidence_id = "metrics-1",
                observed_at = observedAt,
                service = "checkout-api",
                error_rate = ErrorRate,
                p95_latency_ms = LatencyMs,
                db_connection_usage = ConnectionUsage,
                baseline = new
                {
                    error_rate = 0.002,
                    p95_latency_ms = 180,
                    db_connection_usage = 0.42
                },
                request_rate_change = 0.03
            },
            "get_logs" => new
            {
                evidence_id = "logs-1",
                observed_at = observedAt,
                service = "checkout-api",
                entries = degraded
                    ? new[]
                    {
                        "Timeout acquiring PostgreSQL connection after 3000ms",
                        "POST /checkout returned 503: connection pool exhausted"
                    }
                    : new[] { "POST /checkout returned 200", "PostgreSQL connection acquired in 4ms" }
            },
            "get_recent_deployments" => new
            {
                evidence_id = "deployments-1",
                observed_at = observedAt,
                service = "checkout-api",
                deployment_id = DeploymentId,
                previous_deployment_id = "7e30b1",
                completed_at = Id.Length == 0 ? observedAt.AddHours(-2) : _startedAt.AddMinutes(-11),
                alert_started_at = Id.Length == 0 ? (DateTimeOffset?)null : _startedAt,
                changes = new[] { "Checkout database session handling", "Request tracing" }
            },
            "inspect_db_connections" => new
            {
                evidence_id = "connections-1",
                observed_at = observedAt,
                database = "postgres",
                utilization = ConnectionUsage,
                checkout_owner_fraction = degraded ? 0.89 : 0.34,
                checkout_idle_in_transaction = degraded ? 142 : 2,
                other_services_stable = true,
                cpu_usage = 0.31,
                observation = degraded ? "Checkout sessions continue accumulating at steady request volume." : "Session counts are stable."
            },
            "get_dependency_health" => new
            {
                evidence_id = "dependencies-1",
                observed_at = observedAt,
                payments = "healthy",
                inventory = "healthy",
                database_reachable = true
            },
            _ => throw new ArgumentException("Unknown investigation tool.", nameof(name))
        };
        return JsonSerializer.Serialize(evidence);
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

    public void ProposeRollback()
    {
        RequirePhase(IncidentPhase.Investigating);
        PendingApproval = new(Convert.ToHexString(RandomNumberGenerator.GetBytes(6)), Id, DeploymentId,
            _time.GetUtcNow().AddMinutes(5));
        Phase = IncidentPhase.WaitingForApproval;
        Record("Commander", $"Rollback of {DeploymentId} proposed; waiting for human approval.");
    }

    public void Decide(string code, bool approved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        RequirePhase(IncidentPhase.WaitingForApproval);
        var pending = PendingApproval ?? throw new InvalidOperationException("No action is awaiting approval.");
        if (pending.ExpiresAt <= _time.GetUtcNow())
        {
            Escalate("Approval expired; no action executed.");
            throw new InvalidOperationException("Approval expired.");
        }
        if (pending.IncidentId != Id || pending.DeploymentId != DeploymentId ||
            !string.Equals(pending.Code, code, StringComparison.Ordinal))
            throw new InvalidOperationException("Approval does not match the pending action.");
        PendingApproval = null;
        Phase = approved ? IncidentPhase.Executing : IncidentPhase.Escalated;
        Record("Human", approved ? "Rollback approved." : "Rollback denied; manual investigation required.");
    }

    public void Rollback()
    {
        RequirePhase(IncidentPhase.Executing);
        BeforeRecovery = (ErrorRate, LatencyMs, ConnectionUsage);
        DeploymentId = "7e30b1";
        ErrorRate = 0.003;
        LatencyMs = 210;
        ConnectionUsage = 0.51;
        Phase = IncidentPhase.Verifying;
        Record("Executor", "Simulated rollback completed; checking recovery metrics.");
    }

    public void VerifyRecovery()
    {
        RequirePhase(IncidentPhase.Verifying);
        if (ErrorRate >= 0.01 || LatencyMs >= 500 || ConnectionUsage >= 0.8)
        {
            Escalate("Recovery thresholds not met; manual investigation required.");
            return;
        }
        Phase = IncidentPhase.Resolved;
        Record("Verifier", "Error rate, latency and database utilization meet recovery thresholds.");
    }

    public void Escalate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        PendingApproval = null;
        Phase = IncidentPhase.Escalated;
        Record("Commander", reason);
    }

    private void RequirePhase(IncidentPhase expected)
    {
        if (Phase != expected)
            throw new InvalidOperationException($"Action requires {expected}; current phase is {Phase}.");
    }
}
