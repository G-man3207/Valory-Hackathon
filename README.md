# AI Incident Commander

A hackathon demo built with Ikon: four AI roles investigate simulated checkout telemetry, challenge a diagnosis, request human approval, roll back a simulated deployment, and verify recovery. No real infrastructure is modified.

## Development

Install Ikon using its official installer and run `ikon login`. The project uses .NET 10 and Node 24. Ikon's login configures access to its SDK packages.

```sh
npm ci --prefix IncidentCommander/frontend-node
dotnet tool restore
./verify.sh
cd IncidentCommander
ikon app run --no-auto-frontend-login
```

`verify.sh` runs locally and requires zero ESLint warnings, strict TypeScript, a frontend build, a C# build with warnings as errors, CSharpier formatting, and executable domain/SMS checks. The generated SDK frontend currently emits a bundle-size advisory. The backend build needs the authenticated Ikon package feed.

Format C# with the repository's pinned formatter:

```sh
dotnet csharpier format IncidentCommander/app/IncidentCommander/*.cs IncidentCommander/checks/*.cs
```

## Demo

1. Inject an incident. The Observability role chooses read-only tools; Hypothesis ranks explanations; Critic challenges them; Commander chooses more evidence or proposes rollback.
2. Review the evidence. The Commander cannot execute rollback: the domain requires a current, unexpired, single-use approval tied to the incident and deployment.
3. Reply to the SMS exactly as instructed, or enter the pending code in the explicitly labelled local demo fallback.
4. Inspect verified recovery and copy or download the report. Reset invalidates outstanding approvals.

Model runs have timeouts and a three-round investigation limit. Failure escalates to an operator; it never invents a successful diagnosis. Denial or expired approval leaves the service degraded. The simulation and incident state are in memory and reset when the app restarts.

## SMS configuration

The process reads `ELKS_API_USERNAME`, `ELKS_API_PASSWORD`, `ELKS_FROM`, and `ONCALL_PHONE`. Phone numbers use E.164 format. Keep credentials in the ignored `.env` or the process environment, never in source. The existing local `46USER` and `46PASS` entries can be mapped to the two API variables when launching.

`node dev.mjs` loads the root `.env` using Node's native loader, maps those legacy credential names, and starts Ikon with developer auto-login disabled. Set `TAILSCALE_HOST` in `.env` for tailnet access. On this devbox, start with:

```sh
node dev.mjs --config-file /home/dev/.config/incident-commander/server.json
```

The outside-repository server config points to the Tailscale-issued certificate and key. Tailscale Serve proxies HTTPS 443 to local frontend 9443; the SDK connects to backend 8444 with that same valid certificate. This uses no public Ikon relay or Tailscale Funnel. Renew the certificate and restart before its expiry.

With SMS configured, the **Inject incident & request SMS** action can send one real approval request. Replies are polled through 46elks' authenticated API, so the private Tailscale demo does not need a public webhook. The parser checks direction, sender, recipient, timestamp, and exact command/code. Missing SMS configuration leaves the local demo approval available.

## Current scope

One database-connection leak fixture, five investigative tools, one guarded rollback, four real AI roles, a responsive native Ikon dashboard, and an incident report. One incident per session; no production integrations or durable incident history. Tailscale controls access to the development preview; the local approval fallback is for the simulation, not production authorization.

## Real Kubernetes lab

The separate [seed lab](lab/README.md) runs checkout and inventory on a real local Kubernetes cluster. `./lab/lab.sh check` verifies healthy HTTP 200, an injected deployment causing a real dependency DNS failure and HTTP 503, then recovery to HTTP 200. `break`, `evidence`, and `recover` support manual investigation. The Ikon dashboard still uses its original in-memory simulation; it does not yet read or change this cluster.

Local verification additionally requires ShellCheck and Python 3 for shell checks and Python syntax validation. The cluster integration check runs explicitly on the devbox.
