# Development guide

[← Back to the project](../README.md)

## Prerequisites

| Tool | Used for |
| --- | --- |
| Ikon CLI and an authenticated Ikon account | App runtime, AI calls and access to the SDK package feed. |
| .NET 10 SDK | C# app, formatter and executable checks. |
| Node.js 24 and npm | Frontend tooling and the optional development launcher. |
| Docker, kind, kubectl and curl | The real local Kubernetes lab. |
| Bash and GNU coreutils (`timeout`) | Lab and verification scripts. |
| Python 3 and ShellCheck | Python syntax validation and shell checks. |

Install Ikon using its official installer, then run `ikon login`. Login configures access to its SDK packages; a backend build requires the authenticated package feed.

The lab is currently configured for a specific devbox layout. Both [`lab/lab.sh`](../lab/lab.sh) and [`KubernetesLab.cs`](../IncidentCommander/app/IncidentCommander/KubernetesLab.cs) use `/home/dev/.config/incident-commander/lab.kubeconfig`. They explicitly target the `kind-incident-lab` context, the `incident-lab` namespace and the checkout endpoint at `127.0.0.1:19080`. On another machine, use that layout or adjust both paths consistently. These values are not environment-variable overrides.

## Set up and start

Run these commands from the repository root:

```sh
ikon login
npm ci --prefix IncidentCommander/frontend-node
dotnet tool restore
```

Create the local kind cluster using the exact configuration in the [lab guide](../lab/README.md), then seed it before starting the app:

```sh
./lab/lab.sh seed
./verify.sh
(cd IncidentCommander && ikon app run --no-auto-frontend-login)
```

Use the frontend URL reported by Ikon. The labelled local lab approval control works without SMS configuration.

## Local verification

```sh
./verify.sh
```

This runs ShellCheck, Python syntax validation, Node syntax checks, ESLint with zero warnings, strict TypeScript checks, a frontend build, a C# build with warnings as errors, the pinned CSharpier formatter check and executable domain/SMS/Kubernetes guard checks. The generated SDK frontend can emit a bundle-size advisory.

These are local checks. The live-cluster integration check is a separate, explicit operation.

Format C# using the pinned formatter:

```sh
dotnet csharpier format IncidentCommander/app/IncidentCommander/*.cs IncidentCommander/checks/*.cs
```

### Live Kubernetes checks

Both commands below change the isolated lab and restore it afterward. They send no SMS. If interrupted, run `./lab/lab.sh recover`.

```sh
# Verify a healthy request, a real injected failure and recovery: 200 → 503 → 200.
./lab/lab.sh check

# Exercise random injection and guarded recovery through the C# adapter.
dotnet run --project IncidentCommander/checks/IncidentCommander.Checks.csproj --configuration Release -- --live-kubernetes
```

For manual investigation, use `./lab/lab.sh break`, `./lab/lab.sh evidence`, `./lab/lab.sh status` and `./lab/lab.sh recover`. The supported faults only change checkout's inventory URL; no node or pod disruption is involved. See the [lab guide](../lab/README.md) for the workloads and cluster setup.

## Optional SMS approval

SMS approval uses 46elks. Supply these variables through the process environment, or through the ignored root `.env` when using `node dev.mjs`:

| Variable | Value |
| --- | --- |
| `ELKS_API_USERNAME` | 46elks API username. |
| `ELKS_API_PASSWORD` | 46elks API password. |
| `ELKS_FROM` | Reply-capable sender number in E.164 format. |
| `ONCALL_PHONE` | On-call recipient number in E.164 format. |
| `TAILSCALE_HOST` | Optional hostname for the development launcher; defaults to `localhost`. |

Keep credentials out of source control. With SMS configured, **Inject random Kubernetes fault & request SMS** can send one real approval request when restoration is proposed. Reply exactly as instructed in that message. The parser checks incoming direction, sender, recipient, timestamp and the exact command and code. Replies are polled through the authenticated API, so this private demo does not need a public webhook.

Missing SMS configuration leaves local lab approval available. Both approval paths authorize a real change restricted to the lab namespace. Denial or expiration leaves checkout degraded.

### Development launcher

[`dev.mjs`](../dev.mjs) loads the root `.env` using Node's native loader, maps legacy `46USER` and `46PASS` names to the corresponding API variables and starts Ikon with developer auto-login disabled.

The root `.env` must exist when using this launcher, even without SMS credentials:

```sh
touch .env
node dev.mjs
```

This launcher sets the backend HTTPS port to `8444` and the frontend port to `9443`. Running `ikon app run` directly does not load the root `.env` through this helper; export any required variables in the process environment instead.

### Existing private Tailscale preview

The original devbox uses a separate server configuration outside the repository:

```sh
node dev.mjs --config-file /home/dev/.config/incident-commander/server.json
```

That config points to a Tailscale-issued certificate and key. `TAILSCALE_HOST` selects the tailnet hostname. Tailscale Serve proxies HTTPS `443` to local frontend `9443`; the SDK connects to backend `8444` with the same valid certificate. This setup uses no public Ikon relay or Tailscale Funnel. Renew the certificate and restart before it expires.

This is the existing devbox setup, not a public demo URL or a required path for a local run.

## State and interrupted runs

Incident workflow state is held in memory and resets when the app restarts. Kubernetes workload state persists. Use **Reset demo** to restore the inventory URL and invalidate pending approvals, or run `./lab/lab.sh recover` after an interrupted process.

Model calls have timeouts and the investigation allows at most three rounds. Failed diagnosis or recovery escalates to an operator. Only successful live recovery checks move the incident to resolved.
