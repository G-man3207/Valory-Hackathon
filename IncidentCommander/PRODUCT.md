# Incident Commander

<!-- impeccable:product-schema 1 -->

## Platform
web

## Users
Hackathon presenters and observers investigating a real incident in an isolated local Kubernetes lab.

## Product Purpose
Demonstrate four AI roles investigating a checkout dependency failure, challenging a diagnosis, requesting human approval, restoring the inventory URL, and verifying recovery with live HTTP probes.

## Capabilities and Constraints
IkonAI is required. Native C# reactive UI. One isolated checkout-to-inventory lab runs in the local kind cluster's incident-lab namespace. Each injection randomly selects one of three inventory URL faults: a missing DNS name, a refused connection on port 81, or an HTTP 404 path. Checkout returns HTTP 503 while its pod can remain Ready. The selected fault is discovered through evidence, not disclosed in advance as the diagnosis. The agents read actual Kubernetes configuration, logs and probe results. Approved remediation restores the known inventory URL on the guarded checkout deployment. No production systems are connected. Random fault selection uses the existing application runtime; no chaos framework is added.

The dashboard provides fault injection, reset, live metrics, a timeline, human approval and an incident report. Optional SMS approval uses 46elks when configured. Local lab approval remains available and is labeled explicitly. Reset restores the lab and invalidates pending approval. Unchecked metrics display Not checked.

## Evidence on Hand
The lab workloads and adapter provide the runtime evidence. The root hackathon plan is historical design context; its earlier simulated database scenario is no longer an active scenario. Confidence is an AI estimate, never a measured probability.

## Brand Commitments
One dark operations dashboard, focused on the scenario, status, agents, hypotheses, timeline and approval.
