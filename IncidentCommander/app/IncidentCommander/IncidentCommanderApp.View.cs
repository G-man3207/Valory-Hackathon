using System.Globalization;
using System.Text;

public partial class IncidentCommanderApp
{
    private void RenderDashboard()
    {
        UI.Root(["h-screen bg-zinc-950 text-zinc-100"], content: view =>
        {
            _ = _revision.Value;
            var phase = _simulation.Phase;
            var busy = _busy.Value;
            var healthy = phase is IncidentPhase.Healthy or IncidentPhase.Resolved;
            view.ScrollArea(rootStyle: ["h-screen w-full"], content: view =>
            {
                view.Column(["w-full max-w-[1440px] mx-auto px-4 py-6 md:px-8 md:py-8 gap-6"], content: view =>
                {
                    view.Row(["items-center justify-between gap-4 flex-wrap"], content: view =>
                    {
                        view.Column(["gap-1"], content: view =>
                        {
                            view.Heading(["text-2xl md:text-3xl font-semibold tracking-tight"], text: "Incident Commander");
                            view.Text(["text-sm text-zinc-400"], text: "Simulated infrastructure. Real investigation. Human control.");
                        });
                        view.Row(["gap-2 items-center flex-wrap"], content: view =>
                        {
                            view.Button([Button.OutlineMd, "min-h-10"], text: "Reset demo", disabled: busy || phase == IncidentPhase.Healthy, onClick: ResetAsync);
                            view.Button([Button.PrimaryMd, "min-h-10"], text: busy ? "Investigation running…" : _sms is not null ? "Inject incident & request SMS" : "Inject incident", disabled: busy || phase != IncidentPhase.Healthy, onClick: InjectAsync);
                        });
                    });

                    view.Box(["border border-zinc-800 rounded-lg overflow-hidden"], content: view =>
                    {
                        view.Row(["px-5 py-4 bg-zinc-900 items-center justify-between gap-3 flex-wrap"], content: view =>
                        {
                            view.Row(["gap-3 items-center"], content: view =>
                            {
                                view.Icon(["w-5 h-5", healthy ? "text-emerald-400" : "text-amber-400"], name: healthy ? "shield-check" : "activity");
                                view.Text(["font-semibold text-base"], text: phase == IncidentPhase.WaitingForApproval ? "Awaiting human approval" : phase.ToString());
                            });
                            view.Text(["text-sm text-zinc-400 font-mono break-all"], text: string.IsNullOrEmpty(_simulation.Id) ? "No active incident" : _simulation.Id);
                        });
                        view.Row(["px-5 py-5 gap-6 md:gap-12 flex-wrap"], content: view =>
                        {
                            RenderMetric(view, "Error rate", _simulation.ErrorRate.ToString("P1", CultureInfo.InvariantCulture), "Failed requests", _simulation.ErrorRate > 0.01);
                            RenderMetric(view, "P95 latency", $"{_simulation.LatencyMs.ToString("N0", CultureInfo.InvariantCulture)} ms", "Request duration", _simulation.LatencyMs > 500);
                            RenderMetric(view, "DB connections", _simulation.ConnectionUsage.ToString("P0", CultureInfo.InvariantCulture), "Pool utilization", _simulation.ConnectionUsage > 0.8);
                            view.Column(["gap-1 min-w-32"], content: view =>
                            {
                                view.Text(["text-sm text-zinc-400"], text: "Deployment");
                                view.Text(["text-lg font-mono text-zinc-200 break-all"], text: _simulation.DeploymentId);
                                view.Text(["text-xs text-zinc-400"], text: "Simulated service");
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(_error.Value))
                    {
                        view.Box(["border border-red-800 rounded-lg p-4 bg-red-950 text-red-200"], props: new Dictionary<string, object> { ["role"] = "alert" }, content: view => view.Text(text: _error.Value));
                    }

                    view.Row(["gap-6 items-start flex-col lg:flex-row"], content: view =>
                    {
                        view.Column(["w-full lg:w-3/5 min-w-0 gap-0"], content: view =>
                        {
                            view.Row(["items-center justify-between gap-3 pb-4 border-b border-zinc-800"], content: view =>
                            {
                                view.Heading(["text-lg font-semibold"], text: "Investigation");
                                if (busy)
                                {
                                    view.Row(["gap-2 items-center text-amber-300"], content: view =>
                                    {
                                        view.Spinner(size: SpinnerSize.Sm);
                                        view.Text(["text-sm"], text: _agent.Value);
                                    });
                                }
                                else
                                {
                                    view.Text(["text-sm text-zinc-400"], text: "Four AI roles");
                                }
                            });
                            RenderRole(view, "Observability", "Read the signals", _observations.Value, "Metrics and logs will appear when you inject an incident.");
                            RenderRole(view, "Hypothesis", "Rank the explanations", _hypotheses.Value, "Waiting for observations. Confidence is an AI estimate.");
                            RenderRole(view, "Critic", "Challenge the diagnosis", _critique.Value, "Waiting for a hypothesis to challenge against the evidence.");
                            RenderRole(view, "Commander", "Decide the next action", _decision.Value, phase == IncidentPhase.WaitingForApproval ? "A rollback is proposed. Human approval is required before any action." : "Waiting for the investigation and safety review.");
                        });

                        view.Column(["w-full lg:w-2/5 min-w-0 gap-6"], content: view =>
                        {
                            view.Box(["bg-zinc-900 border border-zinc-800 rounded-lg p-5"], content: view =>
                            {
                                view.Column(["gap-3"], content: view =>
                                {
                                    view.Heading(["text-lg font-semibold"], text: "Action control");
                                    if (!string.IsNullOrWhiteSpace(_smsStatus.Value))
                                    {
                                        view.Text(["text-sm text-amber-200 leading-relaxed"], text: _smsStatus.Value);
                                    }
                                    if (_sms is not null && phase == IncidentPhase.Healthy)
                                    {
                                        view.Text(["text-sm text-zinc-300 leading-relaxed"], text: "Inject incident & request SMS starts the investigation and sends a real approval SMS if a rollback is proposed.");
                                    }
                                    if (_simulation.PendingApproval is { } approval)
                                    {
                                        view.Text(["text-sm text-amber-300 font-medium"], text: _sms is null ? "Local demo approval · SMS not connected" : "Local demo approval · SMS fallback");
                                        view.Text(["text-sm text-zinc-300 leading-relaxed"], text: $"Roll back deployment {approval.DeploymentId}. This changes only the simulation.");
                                        view.Text(["text-sm text-zinc-400"], text: $"Approval expires at {approval.ExpiresAt.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}.");
                                        view.Text(["font-mono text-xl text-amber-300 tracking-widest"], text: approval.Code);
                                        view.TextField(["default", "w-full"], bind: _approvalCode, label: "Approval code", placeholder: "Enter the code above", disabled: busy, debounceMs: 0);
                                        view.Row(["gap-2 flex-wrap"], content: view =>
                                        {
                                            view.Button([Button.PrimaryMd, "min-h-10"], text: "Approve rollback", disabled: busy, onClick: ApproveAsync);
                                            view.Button([Button.OutlineMd, "min-h-10"], text: "Deny rollback", disabled: busy, onClick: DenyAsync);
                                        });
                                    }
                                    else
                                    {
                                        view.Text(["text-sm text-zinc-300 leading-relaxed"], text: phase switch
                                        {
                                            IncidentPhase.Healthy => "Inject a connection leak to start. The agents investigate before proposing an action.",
                                            IncidentPhase.Resolved => "Recovery verified. The incident report is ready below.",
                                            IncidentPhase.Escalated => "Human review required. No further remediation will run. Review the timeline, then reset to try again.",
                                            _ => "The agents can propose a rollback. Only a human can authorize it.",
                                        });
                                    }
                                    view.Text(["text-xs text-zinc-400"], text: "Reset demo clears this run and invalidates its approval.");
                                });
                            });

                            view.Column(["gap-3"], content: view =>
                            {
                                view.Row(["items-center justify-between"], content: view =>
                                {
                                    view.Heading(["text-lg font-semibold"], text: "Incident timeline");
                                    view.Text(["text-xs text-zinc-400"], text: "UTC");
                                });
                                if (_simulation.Events.Count == 0)
                                {
                                    view.Text(["text-sm text-zinc-400 py-3"], text: "No events yet. Each investigation step and decision will be recorded here.");
                                }
                                foreach (var entry in _simulation.Events.Reverse())
                                {
                                    view.Row(["gap-3 py-3 border-b border-zinc-800 items-start"], content: view =>
                                    {
                                        view.Text(["text-xs font-mono text-zinc-400 shrink-0 pt-1"], text: entry.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                                        view.Column(["gap-1 min-w-0"], content: view =>
                                        {
                                            view.Text(["text-sm font-medium text-zinc-200"], text: entry.Actor);
                                            view.Text(["text-sm text-zinc-400 leading-relaxed break-words"], text: entry.Message);
                                        });
                                    });
                                }
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(_report.Value))
                    {
                        view.Column(["border-t border-zinc-800 pt-5 gap-4"], content: view =>
                        {
                            view.Heading(["text-lg font-semibold"], text: "Incident report");
                            view.Text(["text-sm text-zinc-200 leading-relaxed whitespace-pre-wrap break-words max-w-3xl"], text: _report.Value);
                        });
                        view.Row(["gap-3 justify-between items-center flex-wrap"], content: view =>
                        {
                            view.Text(["text-sm text-zinc-400"], text: "Take the incident report with you.");
                            view.Row(["gap-2 flex-wrap"], content: view =>
                            {
                                view.ActionButton([Button.OutlineMd], action: ActionKind.CopyToClipboard, text: "Copy report", options: new CopyToClipboardActionOptions { Text = _report.Value });
                                view.ActionButton([Button.OutlineMd], action: ActionKind.DownloadFile, text: "Download report", options: new DownloadFileActionOptions { Filename = "incident-report.md", Data = Encoding.UTF8.GetBytes(_report.Value) });
                            });
                        });
                    }
                });
            });
        });
    }

    private static void RenderMetric(IView view, string label, string value, string detail, bool degraded)
    {
        view.Column(["gap-1 min-w-32"], content: view =>
        {
            view.Text(["text-sm text-zinc-400"], text: label);
            view.Text(["text-2xl font-semibold tabular-nums", degraded ? "text-amber-300" : "text-zinc-100"], text: value);
            view.Text(["text-xs text-zinc-400"], text: detail);
        });
    }

    private void RenderRole(IView view, string role, string purpose, string output, string empty)
    {
        view.Column(["py-5 gap-3 border-b border-zinc-800"], content: view =>
        {
            view.Row(["gap-3 items-center justify-between flex-wrap"], content: view =>
            {
                view.Row(["gap-2 items-center"], content: view =>
                {
                    view.Icon(["w-4 h-4", _agent.Value == role ? "text-amber-300" : "text-zinc-400"], name: string.IsNullOrWhiteSpace(output) ? "circle" : "circle-check");
                    view.Heading(["text-base font-semibold"], text: role);
                });
                view.Text(["text-xs text-zinc-400"], text: purpose);
            });
            view.Text(["text-sm leading-relaxed whitespace-pre-wrap break-words", string.IsNullOrWhiteSpace(output) ? "text-zinc-400" : "text-zinc-200"], text: string.IsNullOrWhiteSpace(output) ? empty : output);
        });
    }
}
