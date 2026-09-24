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
                view.Column(["w-full max-w-[1440px] mx-auto px-4 py-4 md:px-8 md:py-6 gap-5"], content: view =>
                {
                    view.Row(["items-center justify-between gap-3 flex-wrap"], content: view =>
                    {
                        view.Column(["gap-1"], content: view =>
                        {
                            view.Heading(["text-xl font-semibold tracking-tight"], text: "Incident Commander");
                            view.Text(
                                ["text-xs text-zinc-400"],
                                text: "Simulated infrastructure · Human-approved remediation");
                        });
                        view.Row(["gap-3 items-center flex-wrap"], content: view =>
                        {
                            if (busy)
                            {
                                view.Spinner(size: SpinnerSize.Sm);
                                view.Text(["text-sm text-amber-300"], text: $"{_agent.Value} working");
                            }
                            if (phase == IncidentPhase.Healthy)
                            {
                                view.Button(
                                    [Button.PrimaryMd, "min-h-11"],
                                    text: _sms is not null ? "Inject incident & request SMS" : "Inject incident",
                                    disabled: busy,
                                    onClick: InjectAsync);
                            }
                            else
                            {
                                view.Button(
                                    [Button.OutlineMd, "min-h-11"],
                                    text: "Reset demo",
                                    disabled: busy,
                                    onClick: ResetAsync,
                                    tooltip: "Clears this run and invalidates its approval.");
                            }
                        });
                    });

                    view.Box(["border-y border-zinc-800 py-4"], content: view =>
                    {
                        view.Row(["items-center justify-between gap-2 flex-wrap mb-4"], content: view =>
                        {
                            view.Box(
                                ["flex gap-2 items-center"],
                                props: new Dictionary<string, object>
                                {
                                    ["role"] = "status",
                                    ["aria-live"] = "polite",
                                },
                                content: view =>
                            {
                                view.Icon(
                                    ["w-4 h-4", healthy ? "text-emerald-400" : "text-amber-400"],
                                    name: healthy ? "shield-check" : "activity");
                                view.Text(
                                    ["font-semibold text-sm"],
                                    text: phase == IncidentPhase.WaitingForApproval ? "Awaiting human approval" : phase.ToString());
                            });
                            view.Text(
                                ["text-xs text-zinc-400 font-mono break-all"],
                                text: string.IsNullOrEmpty(_simulation.Id) ? "No active incident" : _simulation.Id);
                        });
                        view.Box(["grid grid-cols-2 md:grid-cols-4 gap-x-6 gap-y-4"], content: view =>
                        {
                            RenderMetric(
                                view,
                                "Error rate",
                                _simulation.ErrorRate.ToString("P1", CultureInfo.InvariantCulture),
                                _simulation.ErrorRate > 0.01);
                            RenderMetric(
                                view,
                                "P95 latency",
                                $"{_simulation.LatencyMs.ToString("N0", CultureInfo.InvariantCulture)} ms",
                                _simulation.LatencyMs > 500);
                            RenderMetric(
                                view,
                                "DB pool utilization",
                                _simulation.ConnectionUsage.ToString("P0", CultureInfo.InvariantCulture),
                                _simulation.ConnectionUsage > 0.8);
                            view.Column(["gap-1 min-w-0"], content: view =>
                            {
                                view.Text(["text-xs text-zinc-400"], text: "Deployment");
                                view.Text(
                                    ["text-lg font-mono text-zinc-200 break-all"],
                                    text: _simulation.DeploymentId);
                            });
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(_error.Value))
                    {
                        view.Box(
                            ["border border-red-800 rounded-sm p-4 bg-red-950 text-red-200"],
                            props: new Dictionary<string, object> { ["role"] = "alert" },
                            content: view => view.Text(text: _error.Value));
                    }

                    view.Row(["gap-6 items-start flex-col lg:flex-row border-b border-zinc-800 pb-5"], content: view =>
                    {
                        view.Column(["w-full lg:w-2/5 min-w-0 gap-3"], content: view =>
                        {
                            view.Heading(["text-base font-semibold"], text: "Action control");
                            if (!string.IsNullOrWhiteSpace(_smsStatus.Value))
                            {
                                view.Box(
                                    props: new Dictionary<string, object>
                                    {
                                        ["role"] = "status",
                                        ["aria-live"] = "polite",
                                    },
                                    content: view => view.Text(["text-sm text-amber-200 leading-relaxed"], text: _smsStatus.Value));
                            }
                            if (_simulation.PendingApproval is { } approval)
                            {
                                view.Text(
                                    ["text-sm text-zinc-200 leading-relaxed"],
                                    text: $"Roll back deployment {approval.DeploymentId}. This changes only the simulation.");
                                view.Text(
                                    ["text-xs text-zinc-400"],
                                    text: _sms is null
                                        ? "Local demo approval · SMS not connected"
                                        : "Local demo approval · SMS fallback");
                                view.Row(["justify-between gap-2 items-center flex-wrap"], content: view =>
                                {
                                    view.Text(["font-mono text-lg text-amber-300 tracking-wide"], text: approval.Code);
                                    view.Text(
                                        ["text-xs text-zinc-400"],
                                        text: $"Expires {approval.ExpiresAt.ToString("HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}");
                                });
                                view.TextField(
                                    ["default", "w-full min-h-11"],
                                    bind: _approvalCode,
                                    label: "Approval code",
                                    placeholder: "Enter the code above",
                                    disabled: busy,
                                    debounceMs: 0);
                                view.Row(["gap-2 flex-wrap"], content: view =>
                                {
                                    view.Button(
                                        [Button.PrimaryMd, "min-h-11"],
                                        text: "Approve rollback",
                                        disabled: busy,
                                        onClick: ApproveAsync);
                                    view.Button(
                                        [Button.OutlineMd, "min-h-11"],
                                        text: "Deny rollback",
                                        disabled: busy,
                                        onClick: DenyAsync);
                                });
                            }
                            else
                            {
                                view.Text(["text-sm text-zinc-300 leading-relaxed"], text: phase switch
                                {
                                    IncidentPhase.Healthy when _sms is not null =>
                                        "Inject incident & request SMS starts a connection leak investigation " +
                                        "and sends a real approval SMS if a rollback is proposed.",
                                    IncidentPhase.Healthy =>
                                        "Inject a connection leak to start. The agents investigate before proposing an action.",
                                    IncidentPhase.Resolved => "Recovery verified. The incident report is ready below.",
                                    IncidentPhase.Escalated =>
                                        "Human review required. No further remediation will run. " +
                                        "Review the evidence, then reset to try again.",
                                    _ => "The agents can propose a rollback. Only a human can authorize it.",
                                });
                            }
                            view.Text(
                                ["text-xs text-zinc-400"],
                                text: "Reset demo clears this run and invalidates its approval.");
                        });
                        view.Column(["w-full lg:w-3/5 min-w-0 gap-3"], content: view =>
                        {
                            view.Row(["gap-2 items-center"], content: view =>
                            {
                                view.Icon(["w-4 h-4 text-amber-300"], name: "shield-check");
                                view.Heading(["text-base font-semibold"], text: "Commander decision");
                            });
                            if (string.IsNullOrWhiteSpace(_decision.Value))
                            {
                                view.Text(
                                    ["text-sm text-zinc-400 leading-relaxed"],
                                    text: "Waiting for observations, ranked hypotheses and a critical review. " +
                                        "No action has been authorized.");
                            }
                            else
                            {
                                RenderMarkdown(view, _decision.Value);
                            }
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(_report.Value))
                    {
                        view.Column(["gap-4 border-b border-zinc-800 pb-5"], content: view =>
                        {
                            view.Row(["gap-3 justify-between items-center flex-wrap"], content: view =>
                            {
                                view.Heading(["text-lg font-semibold"], text: "Incident report");
                                view.Row(["gap-2 flex-wrap"], content: view =>
                                {
                                    view.ActionButton(
                                        [Button.OutlineMd, "min-h-11"],
                                        action: ActionKind.CopyToClipboard,
                                        text: "Copy report",
                                        options: new CopyToClipboardActionOptions { Text = _report.Value });
                                    view.ActionButton(
                                        [Button.OutlineMd, "min-h-11"],
                                        action: ActionKind.DownloadFile,
                                        text: "Download report",
                                        options: new DownloadFileActionOptions
                                        {
                                            Filename = "incident-report.md",
                                            Data = Encoding.UTF8.GetBytes(_report.Value),
                                        });
                                });
                            });
                            RenderMarkdown(view, _report.Value);
                        });
                    }

                    view.Row(["gap-6 items-start flex-col lg:flex-row"], content: view =>
                    {
                        view.Column(["w-full lg:w-3/5 min-w-0 gap-0"], content: view =>
                        {
                            view.Row(["items-center justify-between gap-3 pb-3"], content: view =>
                            {
                                view.Heading(["text-lg font-semibold"], text: "Investigation evidence");
                                view.Text(["text-xs text-zinc-400"], text: "Full assessments");
                            });
                            RenderRole(
                                view,
                                "Observability",
                                "Read the signals",
                                _observations.Value,
                                "Metrics and logs will appear when you inject an incident.");
                            RenderRole(
                                view,
                                "Hypothesis",
                                "Rank the explanations",
                                _hypotheses.Value,
                                "Waiting for observations. Confidence is an AI estimate.");
                            RenderRole(
                                view,
                                "Critic",
                                "Challenge the diagnosis",
                                _critique.Value,
                                "Waiting for a hypothesis to challenge against the evidence.");
                        });
                        view.Column(["w-full lg:w-2/5 min-w-0 gap-3"], content: view =>
                        {
                            view.Row(["items-center justify-between"], content: view =>
                            {
                                view.Heading(["text-lg font-semibold"], text: "Incident timeline");
                                view.Text(["text-xs text-zinc-400"], text: "UTC · Latest first");
                            });
                            if (_simulation.Events.Count == 0)
                            {
                                view.Text(
                                    ["text-sm text-zinc-400 py-3"],
                                    text: "Investigation steps and decisions will be recorded here.");
                            }
                            foreach (var entry in _simulation.Events.Reverse())
                            {
                                view.Row(["gap-3 py-2 border-b border-zinc-800 items-start"], content: view =>
                                {
                                    view.Text(
                                        ["text-xs font-mono text-zinc-400 shrink-0 pt-1"],
                                        text: entry.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                                    view.Column(["gap-1 min-w-0 flex-1"], content: view =>
                                    {
                                        view.Text(["text-xs font-medium text-zinc-200"], text: entry.Actor);
                                        if (entry.Message.Length > 240)
                                        {
                                            view.Collapsible(defaultOpen: false, content: view =>
                                            {
                                                view.CollapsibleTrigger(
                                                    ["text-sm text-zinc-300 min-h-11 flex items-center gap-2 underline underline-offset-4"],
                                                    content: view => view.Text(text: "Read full assessment"));
                                                view.CollapsibleContent(content: view => RenderMarkdown(view, entry.Message));
                                            });
                                        }
                                        else
                                        {
                                            RenderMarkdown(view, entry.Message);
                                        }
                                    });
                                });
                            }
                        });
                    });
                });
            });
        });
    }

    private static void RenderMetric(IView view, string label, string value, bool degraded)
    {
        view.Column(["gap-1 min-w-0"], content: view =>
        {
            view.Text(["text-xs text-zinc-400"], text: label);
            view.Text(
                ["text-xl font-semibold tabular-nums", degraded ? "text-amber-300" : "text-zinc-100"],
                text: value);
        });
    }

    private static void RenderMarkdown(IView view, string content) =>
        view.Markdown(["default", "text-sm text-zinc-200 leading-relaxed break-words max-w-prose"], content: content);

    private void RenderRole(IView view, string role, string purpose, string output, string empty)
    {
        view.Collapsible(["border-t border-zinc-800"], defaultOpen: false, content: view =>
        {
            view.CollapsibleTrigger(
                [
                    "w-full py-3 min-h-11 flex items-center justify-between gap-3 text-left",
                    "hover:text-amber-200 focus-visible:ring-2 focus-visible:ring-amber-400",
                ],
                content: view =>
            {
                view.Row(["gap-2 items-center"], content: view =>
                {
                    view.Icon(
                        ["w-4 h-4", _agent.Value == role ? "text-amber-300" : "text-zinc-400"],
                        name: string.IsNullOrWhiteSpace(output) ? "circle" : "circle-check");
                    view.Text(["text-sm font-semibold"], text: role);
                });
                view.Row(["gap-2 items-center"], content: view =>
                {
                    view.Text(["text-xs text-zinc-400"], text: purpose);
                    view.Icon(["w-4 h-4 text-zinc-400"], name: "chevron-down");
                });
            });
            view.CollapsibleContent(["pb-4"], content: view =>
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    view.Text(["text-sm text-zinc-400 leading-relaxed"], text: empty);
                }
                else
                {
                    RenderMarkdown(view, output);
                }
            });
        });
    }
}
