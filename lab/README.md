# Local Kubernetes incident lab

Requires Docker, kind, kubectl, curl, and an existing `incident-lab` kind cluster. Its loopback port mapping must be `127.0.0.1:19080` → node port `30080`. The kubeconfig stays outside Git at `/home/dev/.config/incident-commander/lab.kubeconfig`; every kubectl command explicitly selects `kind-incident-lab` and namespace `incident-lab`.

This devbox runs Docker inside a container. Its cgroup v2 nesting was repaired using the [Docker-in-Docker process-group setup](https://github.com/moby/moby/blob/master/hack/dind) so Kubernetes can use domain and memory controllers. A recreated devbox may need the same environment preparation; normal Docker hosts do not.

Root setup, once the tools are installed:

```sh
mkdir -p /home/dev/.config/incident-commander
kind create cluster --name incident-lab --config lab/kind.yaml \
  --kubeconfig /home/dev/.config/incident-commander/lab.kubeconfig \
  --image kindest/node:v1.35.8@sha256:07b2536e30b803ed61d1677a79df6115f798ce64c80f9e22f6ed45afd09323c0 \
  --wait 120s
```

```sh
./lab/lab.sh seed       # Build/load the image and deploy both services healthy.
./lab/lab.sh check      # Verify real HTTP 200 → 503 → 200; finishes healthy.
./lab/lab.sh break      # Deploy a bad inventory URL; leave the failure for investigation.
./lab/lab.sh evidence   # Read deployment configuration, service discovery, logs, events.
./lab/lab.sh recover    # Restore the known-good URL, wait for rollout, verify HTTP 200.
./lab/lab.sh status
```

Checkout (`/checkout`) makes a real HTTP request to inventory (`/inventory`). A bad deployment changes `INVENTORY_URL` to `http://missing-service/inventory`, producing an actual Kubernetes DNS failure and HTTP 503. The containers remain Ready: `/healthz` and `/readyz` check process availability, so agents must examine application responses, configuration, and logs instead of assuming Ready means healthy.

Both deployments use the same Python standard-library image, run without root or Kubernetes API credentials, and have CPU/memory limits. JSON stdout logs contain actual request status, duration, and dependency errors. Recovery explicitly restores the known-good value; it never blindly undoes an unrelated deployment. `check` changes this local lab; run `recover` if interrupted. No command creates or deletes a cluster. This is Kubernetes on kind, not OpenShift.

On this devbox the checkout endpoint is also available through Tailscale at `https://dev-0-1.tortoise-saiph.ts.net:8443/checkout`. Only the demo HTTP service is proxied; the Kubernetes API remains on loopback. The Ikon dashboard uses this lab as its only scenario: it reads live evidence and probes, requests approval bound to the current checkout deployment, then restores the known inventory URL and verifies HTTP recovery. Reset restores the lab as well as clearing incident workflow state.
