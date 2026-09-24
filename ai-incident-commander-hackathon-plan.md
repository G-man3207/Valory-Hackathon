# AI Incident Commander — Detailed Hackathon Build Plan

## 1. Concept

Build an **AI-native incident response system** that behaves like a small virtual SRE/operations team.

Instead of sending all telemetry to one chatbot and asking it for a summary, the system uses **four specialized AI agents** that cooperate, challenge each other, gather additional evidence, decide what to do next, and escalate risky actions to a human.

The intended stack is:

- **IkonAI** for the agent/runtime layer and reactive AI UI
- **46elks** for human-in-the-loop communication and approval by SMS
- A **simulated production environment** for metrics, logs, deployments, dependency health, and actions such as rollback/restart

The core demo should show the system going from:

> Production is healthy

to:

> Something breaks

to:

> Four agents investigate

to:

> The system forms and challenges hypotheses

to:

> A human receives an approval request by SMS

to:

> The action is approved

to:

> Production recovers

to:

> AI writes the incident report

The project should feel like an **AI control plane for incident response**, not like another support chatbot.

---

# 2. Elevator pitch

> **AI Incident Commander is a multi-agent SRE system that detects, investigates, challenges, and resolves production incidents. Four specialized AI agents work together on logs, metrics, deployments, and dependencies. When a risky action is needed, the system escalates to a human through 46elks SMS, waits for approval, executes the action, and automatically documents the incident.**

A shorter stage version:

> **We built an AI incident response team, not an AI chatbot.**

---

# 3. Why this is a good hackathon project

This project has several useful properties for a short hackathon:

1. **It looks more advanced than it is difficult to implement.**
2. It makes meaningful use of AI rather than sprinkling an LLM onto a normal CRUD application.
3. The multi-agent architecture has a real reason to exist.
4. 46elks is used as a meaningful system component rather than as a gimmick.
5. The environment can be fully simulated, so no time is wasted integrating Kubernetes, Datadog, Grafana, PagerDuty, etc.
6. The demo can be extremely visual and easy to understand.
7. There is a natural human-in-the-loop moment where someone's real phone receives an SMS.
8. The architecture is close enough to real SRE tooling to be technically credible.

---

# 4. Product vision

The system should behave roughly like this:

```text
Production event
     |
     v
Incident created
     |
     +----------------------+
     |                      |
     v                      v
Observability Agent     System context
     |
     v
Structured evidence
     |
     v
Hypothesis Agent
     |
     v
Possible root causes
     |
     v
Critic Agent
     |
     v
Counter-evidence / challenges
     |
     v
Incident Commander
     |
     +------ more evidence needed? ------+
     |                                    |
    yes                                  no
     |                                    |
     v                                    v
call tools                           propose action
     |                                    |
     +------------ loop ------------------+
                                          |
                                          v
                                safe or risky action?
                                   /          \
                                safe          risky
                                 |              |
                                 v              v
                               act         46elks SMS
                                                |
                                                v
                                        human approval
                                                |
                                                v
                                              act
                                                |
                                                v
                                       verify recovery
                                                |
                                                v
                                      incident summary
```

---

# 5. The four agents

The key is that each agent has a **narrow and distinct role**.

Do not make four agents that all receive the same prompt and produce slightly different opinions.

## Agent 1 — Observability Agent

### Purpose

Collect and normalize evidence.

This agent should behave like a very disciplined telemetry analyst.

Its job is **not** to decide the root cause.

It receives the incident context and can call tools such as:

- `get_service_health()`
- `get_metrics(service)`
- `get_logs(service)`
- `get_recent_deployments()`
- `get_dependency_health(service)`
- `get_error_samples(service)`
- `get_db_connections()`

### Responsibilities

- Find anomalous metrics
- Identify time correlations
- Extract useful log patterns
- Compare current behavior to baseline
- Produce structured observations
- Explicitly separate observation from interpretation

### Example output

```json
{
  "incident_id": "inc_0042",
  "observations": [
    {
      "fact": "checkout-api error rate increased from 0.4% to 18.2%",
      "timestamp": "17:42:13",
      "source": "metrics"
    },
    {
      "fact": "postgres active connections increased from 42% to 97%",
      "timestamp": "17:41:56",
      "source": "metrics"
    },
    {
      "fact": "checkout-api deployment 8f41a2 completed 11 minutes before the incident",
      "timestamp": "17:31:02",
      "source": "deployment_history"
    }
  ],
  "anomalies": [
    "checkout-api error rate",
    "postgres connection saturation"
  ],
  "missing_evidence": [
    "connection ownership by service",
    "comparison with pre-deploy connection pattern"
  ]
}
```

### Important prompt rule

The Observability Agent should be told:

> Do not claim causality. Only report observations supported by available evidence.

That makes the following agents more meaningful.

---

# 6. Agent 2 — Hypothesis Agent

### Purpose

Generate possible explanations for the incident.

This agent receives:

- incident details
- structured observations from Agent 1
- optionally previous hypotheses from earlier iterations

### Responsibilities

- Generate 2–4 plausible root-cause hypotheses
- Rank them by confidence
- State supporting evidence
- State contradictory evidence
- Identify what evidence would most efficiently distinguish between hypotheses

### Example output

```json
{
  "hypotheses": [
    {
      "id": "h1",
      "title": "DB connection leak introduced by checkout deployment",
      "confidence": 0.68,
      "evidence_for": [
        "DB saturation follows recent checkout deployment",
        "checkout error spike correlates with DB saturation"
      ],
      "evidence_against": [
        "no direct connection ownership data yet"
      ],
      "next_best_test": "inspect database connections grouped by client service"
    },
    {
      "id": "h2",
      "title": "Independent database capacity exhaustion",
      "confidence": 0.21,
      "evidence_for": [
        "database connection utilization reached 97%"
      ],
      "evidence_against": [
        "capacity was stable before the recent deployment"
      ],
      "next_best_test": "compare connection creation rate before and after deployment"
    },
    {
      "id": "h3",
      "title": "Payment provider instability causing retry storms",
      "confidence": 0.11,
      "evidence_for": [
        "checkout is the affected service"
      ],
      "evidence_against": [
        "payment provider health currently appears normal"
      ],
      "next_best_test": "inspect outbound payment error logs"
    }
  ]
}
```

### Important prompt rule

This agent should be encouraged to maintain uncertainty.

Avoid:

> “The deployment caused the outage.”

Prefer:

> “The deployment is currently the strongest hypothesis because X and Y, but Z remains unverified.”

---

# 7. Agent 3 — Critic Agent

### Purpose

Actively attack the current leading diagnosis.

This is what makes the swarm interesting.

The Critic Agent should behave like the skeptical senior engineer in an incident room who asks:

> “What evidence would prove us wrong?”

### Inputs

- Observations
- Current hypotheses
- Confidence values
- Timeline
- Current proposed action

### Responsibilities

- Search for logical inconsistencies
- Detect correlation-vs-causation mistakes
- Look for missing evidence
- Challenge overconfidence
- Suggest competing explanations
- Flag actions that are premature
- Reduce confidence when evidence is weak

### Example

Hypothesis Agent:

```text
Likely cause: bad checkout deployment.
Confidence: 82%.
```

Critic Agent:

```json
{
  "assessment": "leading hypothesis is plausible but overconfident",
  "issues": [
    "database saturation began approximately three minutes before the deployment completed",
    "no connection ownership data has been inspected",
    "checkout errors may be downstream symptoms rather than the source"
  ],
  "recommended_tests": [
    "inspect connection owners",
    "check database workload immediately before deployment",
    "inspect retry rates from dependent services"
  ],
  "confidence_adjustment": -0.18
}
```

The Hypothesis Agent or Commander can then revise:

```text
Bad deployment: 64%
Database capacity issue: 28%
Other: 8%
```

This gives the system a visible "debate" rather than a single opaque LLM answer.

---

# 8. Agent 4 — Incident Commander

## Purpose

The Incident Commander is the orchestrator and decision-maker.

It does not need to be the smartest agent at diagnosis. Its main job is deciding:

- what to investigate next
- when confidence is sufficient
- which tool should be called
- whether an action is safe
- whether human approval is required
- whether the incident is resolved

### Inputs

The Commander receives:

- incident metadata
- observations
- hypotheses
- critic feedback
- action history
- human approvals
- system health after actions

### Responsibilities

1. Determine whether more evidence is needed
2. Select the next tool call
3. Re-run selected agents if new evidence arrives
4. Decide whether a remediation action is justified
5. Classify action risk
6. Escalate risky actions to a human
7. Execute approved actions
8. Verify that the incident actually recovered
9. Close the incident
10. Generate a post-incident report

---

# 9. Action risk model

The Commander should classify actions into something like:

## Low-risk autonomous actions

Can execute automatically:

- fetch metrics
- fetch logs
- inspect health
- inspect deployment history
- inspect DB connections
- check service dependencies
- collect error samples

Optional:

- refresh cache
- restart a simulated non-critical worker

## High-risk actions requiring approval

Require human confirmation:

- rollback deployment
- restart production service
- fail over traffic
- scale database
- disable feature flag
- drain a node
- revoke traffic to a dependency

For the hackathon, the main risky action should probably be:

> `rollback_deployment()`

That is enough to demonstrate human-in-the-loop safely.

---

# 10. 46elks integration

46elks should be used for one particularly strong moment:

## Human approval by SMS

Example incident state:

```text
Severity: SEV-1
Service: checkout-api
Root cause confidence: 92%
Recommended remediation: rollback deployment 8f41a2
Action risk: HIGH
Human approval required
```

The system calls:

```text
send_approval_sms(
  phone_number,
  incident_id,
  action
)
```

The SMS:

```text
SEV-1: Checkout outage

AI Incident Commander recommends:
ROLLBACK checkout-api deployment 8f41a2

Evidence:
• DB connections: 97%
• 89% owned by checkout-api
• issue began after deployment

Reply:
APPROVE 42
or
DENY 42
```

The user replies:

```text
APPROVE 42
```

46elks calls the application's inbound webhook.

The webhook should:

1. Parse sender
2. Parse command
3. Resolve incident ID
4. Validate pending approval
5. Store approval event
6. Wake/resume the Incident Commander

The UI should immediately update:

```text
17:48:21 Human approval received
17:48:22 Rollback initiated
```

This is an excellent live-demo moment because the AI system interacts with the physical world through a real phone.

---

# 11. Simulated production environment

Do not integrate a real Kubernetes cluster unless there is a lot of spare time.

Create a deterministic simulation layer.

## Services

Use a small service graph:

```text
frontend
   |
   v
checkout-api
   | \
   |  \
   v   v
postgres redis
   |
   v
payment-api
```

You can optionally add:

```text
inventory-api
```

but avoid making the topology too large.

---

# 12. Environment model

A simple application state can look like:

```json
{
  "clock": "2026-09-24T17:42:00+02:00",
  "services": {
    "frontend": {
      "status": "healthy",
      "latency_ms": 120,
      "error_rate": 0.003
    },
    "checkout-api": {
      "status": "degraded",
      "latency_ms": 4800,
      "error_rate": 0.182
    },
    "postgres": {
      "status": "degraded",
      "connection_usage": 0.97,
      "cpu": 0.61
    },
    "redis": {
      "status": "healthy"
    },
    "payment-api": {
      "status": "healthy"
    }
  }
}
```

The simulation should expose functions rather than giving all state directly to the agents.

This matters because the agents should have to investigate.

---

# 13. Tool layer

The most important implementation decision is to expose the simulated environment through **tools**.

Suggested tools:

```text
get_system_overview()
get_service_health(service)
get_metrics(service, metric?, window?)
get_logs(service, query?, window?)
get_recent_deployments(service?)
get_dependency_health(service)
inspect_db_connections()
get_error_samples(service)
rollback_deployment(deployment_id)
restart_service(service)
get_incident_timeline()
send_oncall_sms(message)
request_human_approval(action)
```

The tools should return structured JSON.

---

# 14. Example tool definitions

## get_metrics

Input:

```json
{
  "service": "checkout-api",
  "window_minutes": 30
}
```

Output:

```json
{
  "service": "checkout-api",
  "window_minutes": 30,
  "metrics": {
    "error_rate": [
      ["17:20", 0.004],
      ["17:30", 0.005],
      ["17:35", 0.031],
      ["17:40", 0.142],
      ["17:42", 0.182]
    ],
    "latency_p95_ms": [
      ["17:20", 180],
      ["17:30", 210],
      ["17:35", 620],
      ["17:40", 3300],
      ["17:42", 4800]
    ]
  }
}
```

---

# 15. Incident scenarios

Create several scenario fixtures so the system can investigate without knowing which scenario is active.

## Scenario A — DB connection leak

### Hidden ground truth

A new checkout deployment fails to release PostgreSQL connections.

### Symptoms

- checkout latency rises
- checkout error rate rises
- DB connections hit 97%
- Redis remains healthy
- payment provider remains healthy
- most DB connections belong to checkout-api

### Correct remediation

Rollback checkout deployment.

---

## Scenario B — Bad deployment without DB issue

### Hidden ground truth

A code deployment introduces exceptions.

### Symptoms

- checkout errors spike
- DB healthy
- CPU moderate
- logs show new stack trace
- deployment happened 5 minutes before incident

### Correct remediation

Rollback deployment.

---

## Scenario C — Payment provider outage

### Hidden ground truth

External payment provider is failing.

### Symptoms

- checkout error rate rises
- DB healthy
- payment dependency latency rises
- outbound requests timeout
- no recent deployment

### Correct remediation

Do not rollback internal service.

Possible action:

- mark external dependency outage
- degrade gracefully
- alert operator

---

## Scenario D — Database capacity issue

### Hidden ground truth

Database connection pool is undersized due to load.

### Symptoms

- DB saturation
- several services show increased usage
- no recent deploy
- traffic volume doubled
- connections distributed across clients

### Correct remediation

Scale or expand connection pool in simulation.

---

# 16. Random incident mode

Have a button:

```text
INJECT RANDOM INCIDENT
```

The app randomly chooses one of the fixtures.

Do not tell the agents which fixture was selected.

This makes the demo much more credible.

The team can truthfully say:

> “The agents do not know which incident we inject. They have to investigate it.”

---

# 17. State machine

A simple incident state machine:

```text
HEALTHY
   |
   v
DETECTED
   |
   v
INVESTIGATING
   |
   v
HYPOTHESIS_FORMED
   |
   +---- insufficient confidence ----+
   |                                  |
   v                                  |
MORE_EVIDENCE                         |
   |                                  |
   +------------- loop ---------------+
   |
   v
ACTION_PROPOSED
   |
   +---- low risk ----> EXECUTING
   |
   +---- high risk ---> WAITING_FOR_HUMAN
                           |
                     approve / deny
                       /       \
                      v         v
                 EXECUTING   INVESTIGATING
                      |
                      v
                   VERIFYING
                    /      \
             recovered    not recovered
                |             |
                v             v
             RESOLVED    INVESTIGATING
```

---

# 18. Shared incident memory

All agents should read/write a shared incident object.

Example:

```json
{
  "incident_id": "inc_0042",
  "status": "investigating",
  "severity": "SEV-1",
  "affected_services": [
    "checkout-api"
  ],
  "observations": [],
  "hypotheses": [],
  "critic_findings": [],
  "tool_calls": [],
  "proposed_actions": [],
  "human_approvals": [],
  "timeline": []
}
```

Avoid relying on natural-language chat history as the only shared state.

Structured shared state makes the system much easier to debug.

---

# 19. Agent orchestration loop

Pseudo-flow:

```text
incident created

while incident not resolved:

    observations = ObservabilityAgent.run(state)

    hypotheses = HypothesisAgent.run(
        state,
        observations
    )

    critique = CriticAgent.run(
        observations,
        hypotheses
    )

    decision = Commander.run(
        observations,
        hypotheses,
        critique,
        state
    )

    if decision.type == "collect_more_evidence":
        call requested tool
        update state
        continue

    if decision.type == "safe_action":
        execute action
        update state
        verify

    if decision.type == "high_risk_action":
        request SMS approval
        pause execution

    if decision.type == "resolved":
        generate incident report
        stop
```

---

# 20. Avoid infinite agent loops

Set explicit limits.

For example:

```text
max investigation rounds: 5
max tool calls: 20
max hypotheses per round: 4
max critic challenges: 5
```

If confidence is still insufficient:

```text
Escalate to human:
"Unable to establish root cause with sufficient confidence."
```

This is both realistic and protects the demo.

---

# 21. Confidence model

Confidence should be visible in the UI.

Example:

```text
Round 1

Deployment regression      41%
Database capacity          35%
Payment provider           24%
```

After more evidence:

```text
Round 2

Deployment regression      73%
Database capacity          19%
Payment provider            8%
```

After Critic:

```text
Round 3

Deployment regression      61%
Database capacity          31%
Other                       8%
```

After inspecting connection ownership:

```text
Round 4

Connection leak from deployment    92%
Database capacity                    6%
Other                                2%
```

The exact percentages do not need to be mathematically rigorous.

Their purpose is to show belief updates.

---

# 22. Better than fake chain-of-thought

Do not display raw hidden reasoning.

Instead display:

- evidence found
- hypothesis
- confidence
- critique
- tool selected
- next action
- final justification

Example UI:

```text
Hypothesis Agent

Leading hypothesis
DB connection leak after checkout deployment

Confidence
68%

Supporting evidence
• deployment 11 min before outage
• DB saturation correlates with checkout failures

Missing evidence
• connection ownership
```

This is cleaner and safer than pretending to expose internal reasoning.

---

# 23. Suggested dashboard

Main dashboard sections:

```text
┌──────────────────────────────────────────────┐
│ AI INCIDENT COMMANDER                       │
│ Production              ● INCIDENT ACTIVE   │
└──────────────────────────────────────────────┘

┌────────────── SYSTEM HEALTH ────────────────┐
│ frontend       ✓ healthy                    │
│ checkout-api   ✕ degraded                   │
│ postgres       ! degraded                   │
│ redis          ✓ healthy                    │
│ payment-api    ✓ healthy                    │
└─────────────────────────────────────────────┘

┌──────────── AGENT SWARM ────────────────────┐
│ Observability Agent     ✓ evidence found    │
│ Hypothesis Agent        ● investigating     │
│ Critic Agent            ○ waiting           │
│ Incident Commander      ○ waiting           │
└─────────────────────────────────────────────┘

┌──────────── LEADING HYPOTHESES ─────────────┐
│ 68% DB connection leak                     │
│ 21% DB capacity issue                      │
│ 11% payment provider                       │
└─────────────────────────────────────────────┘

┌──────────── INCIDENT TIMELINE ──────────────┐
│ 17:42 Incident detected                    │
│ 17:43 Metrics inspected                    │
│ 17:43 DB saturation found                  │
│ 17:44 Deployment correlation identified    │
└─────────────────────────────────────────────┘
```

---

# 24. Reactive UI ideas

If IkonAI makes reactive/dynamic UI easy, lean into it.

For example:

When a hypothesis appears, dynamically render a hypothesis card.

When the Critic finds a contradiction, show:

```text
⚠ HYPOTHESIS CHALLENGED

"DB saturation began before the deployment completed."

Confidence:
82% -> 64%
```

When human approval is needed:

```text
WAITING FOR HUMAN APPROVAL

Action:
Rollback checkout-api 8f41a2

SMS sent to:
+46 XX XXX XX XX
```

When the SMS comes back:

```text
✓ HUMAN APPROVAL RECEIVED
```

---

# 25. Demo scenario

Use the DB connection leak as the primary demo.

## Starting state

Everything green.

```text
frontend       healthy
checkout       healthy
postgres       healthy
redis          healthy
payments       healthy
```

Button:

```text
INJECT INCIDENT
```

Click it.

---

# 26. Demo timeline

## T+0 sec

Dashboard changes:

```text
checkout-api degraded
```

Error rate:

```text
0.4% -> 18%
```

---

## T+5 sec

Observability Agent activates.

UI:

```text
OBSERVABILITY AGENT
Analyzing service health...
```

Then:

```text
✓ Checkout error spike detected
✓ PostgreSQL connection saturation detected
✓ Deployment found 11 minutes before outage
```

---

## T+15 sec

Hypothesis Agent:

```text
Likely causes

68% DB connection leak after deployment
21% database capacity issue
11% external dependency
```

---

## T+25 sec

Critic Agent:

```text
CHALLENGE

Current evidence does not prove
the deployment owns the connections.

Recommendation:
inspect active DB connection owners.
```

---

## T+35 sec

Commander calls:

```text
inspect_db_connections()
```

Result:

```text
checkout-api        89%
inventory-api        5%
admin-tools          3%
other                3%
```

---

## T+45 sec

Hypothesis updates:

```text
92% checkout deployment connection leak
```

Commander proposes:

```text
Rollback deployment 8f41a2
```

Risk:

```text
HIGH
```

---

## T+50 sec

46elks SMS arrives on a real phone.

```text
SEV-1 Checkout outage.

AI recommends rollback of deployment 8f41a2.

Reply APPROVE 42 or DENY 42.
```

---

## T+60 sec

Reply:

```text
APPROVE 42
```

Dashboard instantly shows:

```text
✓ HUMAN APPROVAL RECEIVED
```

---

## T+65 sec

Rollback runs.

Simulation updates:

```text
DB connections
97% -> 73% -> 51%

Checkout errors
18% -> 4% -> 0.6%
```

---

## T+75 sec

Commander verifies recovery.

```text
✓ checkout healthy
✓ database recovered
✓ error rate within baseline
```

---

## T+80 sec

Incident closes.

```text
INCIDENT RESOLVED
Duration: 6m 14s
```

---

# 27. Automatic incident report

At the end, generate:

```markdown
# Incident INC-0042

## Summary
Checkout API experienced elevated latency and an 18.2% error rate.

## Root cause
Deployment 8f41a2 introduced a PostgreSQL connection leak.

## Evidence
- DB connection utilization reached 97%.
- 89% of active connections belonged to checkout-api.
- Connection utilization returned to baseline after rollback.

## Impact
Checkout requests were partially unavailable for 6 minutes.

## Resolution
Deployment 8f41a2 was rolled back after human approval.

## Suggested follow-ups
1. Add DB connection leak detection.
2. Add pre-deploy connection pool regression test.
3. Add deployment-aware DB saturation alerting.
```

---

# 28. Optional fifth "virtual role" without adding another agent

If you want the system to feel richer without actually adding another LLM agent, implement a deterministic policy engine:

```text
Policy Engine
```

It decides:

```text
read-only inspection -> automatic
restart worker -> automatic
rollback production -> human approval
database failover -> human approval
```

This is useful because not everything should be an LLM decision.

---

# 29. Data model

Possible TypeScript types:

```ts
type IncidentStatus =
  | "detected"
  | "investigating"
  | "waiting_for_human"
  | "executing"
  | "verifying"
  | "resolved";

type Observation = {
  id: string;
  fact: string;
  source: string;
  timestamp: string;
};

type Hypothesis = {
  id: string;
  title: string;
  confidence: number;
  evidenceFor: string[];
  evidenceAgainst: string[];
  nextBestTest?: string;
};

type Critique = {
  hypothesisId?: string;
  issues: string[];
  recommendedTests: string[];
  confidenceAdjustment?: number;
};

type ProposedAction = {
  type: string;
  target?: string;
  risk: "low" | "high";
  rationale: string;
};

type IncidentState = {
  id: string;
  status: IncidentStatus;
  severity: "SEV-1" | "SEV-2" | "SEV-3";
  observations: Observation[];
  hypotheses: Hypothesis[];
  critiques: Critique[];
  actions: ProposedAction[];
  timeline: TimelineEvent[];
};
```

---

# 30. Backend structure

A simple folder structure:

```text
src/
  agents/
    observability.ts
    hypothesis.ts
    critic.ts
    commander.ts

  simulation/
    environment.ts
    scenarios/
      db-connection-leak.ts
      bad-deployment.ts
      payment-outage.ts
      db-capacity.ts

  tools/
    metrics.ts
    logs.ts
    deployments.ts
    dependencies.ts
    db.ts
    remediation.ts

  telecom/
    sms.ts
    webhook.ts

  incidents/
    state.ts
    orchestrator.ts
    policy.ts

  ui/
    dashboard
    agent-status
    timeline
    hypotheses
    system-health
```

Adapt this to whatever IkonAI's project structure expects.

---

# 31. API endpoints

Potential endpoints:

```text
POST /api/incidents/inject
POST /api/incidents/:id/run
GET  /api/incidents/:id
POST /api/incidents/:id/approve
POST /api/webhooks/46elks/sms
GET  /api/environment
```

If the framework provides realtime primitives, use them instead of polling.

---

# 32. Event types

Useful internal events:

```text
incident.created
agent.started
agent.completed
tool.called
tool.completed
hypothesis.created
hypothesis.updated
hypothesis.challenged
action.proposed
approval.requested
approval.received
action.started
action.completed
recovery.detected
incident.resolved
```

This makes the UI easy to drive from an event stream.

---

# 33. System prompts

## Observability Agent prompt skeleton

```text
You are the Observability Agent in an incident-response system.

Your job is to collect facts from telemetry.

Rules:
- Do not claim causality.
- Distinguish facts from interpretation.
- Use available tools when evidence is missing.
- Prefer concise structured output.
- Identify anomalies and missing evidence.
- Do not recommend remediation actions.

Return structured JSON.
```

---

# 34. Hypothesis Agent prompt skeleton

```text
You are the Hypothesis Agent.

Given structured observations from production telemetry,
generate the most plausible root-cause hypotheses.

For each hypothesis:
- title
- confidence from 0 to 1
- evidence supporting it
- evidence contradicting it
- the single best next test

Maintain uncertainty.
Do not treat correlation as causation.
Return a maximum of four hypotheses.
```

---

# 35. Critic Agent prompt skeleton

```text
You are the Critic Agent.

Your job is to challenge the current incident hypotheses.

Look for:
- missing evidence
- timeline inconsistencies
- correlation mistaken for causation
- alternative explanations
- unjustified confidence
- unsafe or premature remediation proposals

Be skeptical but constructive.

Return:
- issues
- recommended tests
- optional confidence adjustments
```

---

# 36. Commander prompt skeleton

```text
You are the Incident Commander.

You coordinate an AI incident-response team.

You receive:
- observations
- hypotheses
- critic feedback
- incident history
- available tools
- policy constraints

Your job is to decide the next best action.

Possible decisions:
1. collect_more_evidence
2. execute_safe_action
3. request_human_approval
4. verify_recovery
5. resolve_incident
6. escalate_to_human

Rules:
- Never execute high-risk actions without approval.
- Prefer evidence-gathering when confidence is insufficient.
- Do not declare resolution until system health has been verified.
- Keep decisions concise and structured.
```

---

# 37. SMS webhook logic

Pseudo-code:

```ts
export async function onSmsWebhook(req) {
  const { from, message } = parse46ElksWebhook(req);

  const match = message
    .trim()
    .toUpperCase()
    .match(/^(APPROVE|DENY)\s+(\d+)$/);

  if (!match) {
    return replySms(
      "Reply APPROVE <incident> or DENY <incident>."
    );
  }

  const [, decision, incidentNumber] = match;

  const incident = await findIncident(incidentNumber);

  if (!incident) {
    return replySms("Incident not found.");
  }

  await addApprovalEvent({
    incidentId: incident.id,
    from,
    decision
  });

  await resumeIncidentCommander(incident.id);

  return replySms(
    decision === "APPROVE"
      ? "Approval received."
      : "Action denied. Incident investigation resumed."
  );
}
```

---

# 38. Simulation behavior after rollback

For a DB connection leak scenario:

Before rollback:

```text
t0
DB connections 97%
checkout errors 18%
latency 4800 ms
```

After rollback:

```text
t+5s
DB connections 82%
checkout errors 11%
latency 2700 ms

t+10s
DB connections 64%
checkout errors 3.2%
latency 820 ms

t+15s
DB connections 51%
checkout errors 0.6%
latency 230 ms
```

Animate this in the UI if practical.

---

# 39. Important realism detail

The system should not magically solve every incident immediately.

Sometimes it should say:

```text
Confidence insufficient.
Collecting more evidence.
```

And occasionally:

```text
Unable to establish root cause with confidence >70%.
Escalating to human operator.
```

This makes the system feel much more credible.

---

# 40. Scope for a four-hour hackathon

## Must-have

- one simulated incident scenario
- four agents
- shared structured incident state
- at least three investigative tools
- one remediation action
- 46elks SMS approval
- dashboard showing agent activity
- automated incident summary

## Strongly desirable

- three or four random incident scenarios
- confidence updates
- Critic changing the leading hypothesis
- realtime UI updates
- animated recovery

## Nice-to-have

- voice escalation
- real Kubernetes
- Grafana integration
- actual cloud logs
- persistence
- authentication
- multi-tenant support
- proper incident history

Do not sacrifice the demo for nice-to-have features.

---

# 41. Build order

## Phase 1 — 30–45 minutes

Build the simulation.

Create:

```text
get_metrics()
get_logs()
get_recent_deployments()
inspect_db_connections()
rollback_deployment()
```

Hard-code one complete DB connection leak scenario.

Test manually.

---

## Phase 2 — 30–45 minutes

Build the Incident Commander alone.

Give it tools.

Make sure a single agent can:

1. inspect metrics
2. inspect deployment
3. inspect DB connections
4. recommend rollback

At this stage, do not worry about the other agents.

This gives you a functional fallback.

---

## Phase 3 — 45 minutes

Split responsibilities into four agents.

Implement:

```text
Observability -> Hypothesis -> Critic -> Commander
```

Use structured JSON between each.

---

## Phase 4 — 30 minutes

Add shared state and timeline events.

Make the UI show:

```text
agent started
tool called
hypothesis created
critique generated
action proposed
```

---

## Phase 5 — 30–45 minutes

Integrate 46elks.

Implement:

```text
request approval -> SMS
SMS reply -> webhook
webhook -> resume incident
```

Test on a real phone.

---

## Phase 6 — remaining time

Add visual polish:

- health cards
- hypothesis confidence
- agent status
- event timeline
- animated rollback recovery
- postmortem report

Then add additional scenarios if time remains.

---

# 42. Fallback plan

If IkonAI proves harder than expected, keep the architecture but reduce scope.

The minimum viable demo can be:

```text
React dashboard
      |
Node backend
      |
4 LLM calls
      |
fixture tools
      |
46elks
```

Do not let framework experimentation kill the project.

---

# 43. Debug mode

Add a hidden or visible developer panel.

Show:

```text
Active scenario
Current incident state
Agent outputs
Tool calls
Raw webhook events
Pending approval
```

This will save a lot of time during the hackathon.

---

# 44. Demo safety switch

Have a button:

```text
RESET DEMO
```

It should:

- set environment healthy
- clear incident state
- clear pending approval
- reset scenario
- reset UI

Also have:

```text
INJECT DB LEAK
```

in addition to random mode.

Use the deterministic scenario for the actual presentation.

Random mode is impressive during casual testing, but deterministic mode is safer on stage.

---

# 45. Suggested visual design

A dark operations-dashboard aesthetic works naturally.

Keep the screen focused on:

- system status
- agents
- hypotheses
- timeline
- action/approval

Avoid building lots of navigation.

One single dashboard is enough.

---

# 46. Example end-to-end transcript

```text
17:42:03
Incident detected:
checkout-api error rate exceeded 10%.

17:42:05
Observability Agent started.

17:42:08
Observation:
checkout-api error rate = 18.2%.

17:42:09
Observation:
PostgreSQL connection usage = 97%.

17:42:12
Observation:
deployment 8f41a2 completed 11 minutes earlier.

17:42:15
Hypothesis Agent:
68% connection leak caused by new deployment.

17:42:19
Critic Agent:
Connection ownership has not been verified.
Confidence should be lower.

17:42:21
Incident Commander:
Calling inspect_db_connections().

17:42:23
Tool result:
89% of active DB connections belong to checkout-api.

17:42:26
Hypothesis Agent:
92% connection leak introduced by deployment 8f41a2.

17:42:28
Commander:
Rollback recommended.
High-risk action.
Human approval required.

17:42:29
SMS sent.

17:42:44
Human:
APPROVE 42

17:42:45
Approval received.

17:42:46
Rollback started.

17:42:53
Checkout error rate = 4.1%.

17:43:02
Checkout error rate = 0.6%.

17:43:03
Database connections = 51%.

17:43:05
Incident Commander:
Recovery verified.

17:43:06
Incident resolved.
```

---

# 47. What makes the AI use meaningful

The AI should be responsible for:

- choosing what evidence to gather
- turning telemetry into structured observations
- generating competing hypotheses
- challenging those hypotheses
- choosing the next diagnostic step
- updating confidence as evidence arrives
- deciding when enough evidence exists
- selecting a proposed remediation
- summarizing the incident

The AI should **not** be responsible for:

- blindly executing dangerous commands
- deciding security policy
- authenticating SMS senders
- storing state reliably
- deterministic parsing of approval syntax

Use normal software for deterministic problems and AI for ambiguous reasoning.

That distinction itself makes the project technically stronger.

---

# 48. Potential judging questions

## “Why four agents instead of one?”

Answer:

> Different roles create useful checks and balances. One agent collects evidence without diagnosing, another generates hypotheses, a third actively challenges them, and the Commander decides what to investigate or do next. That reduces premature conclusions and makes the process observable.

## “Isn’t this just four LLM calls?”

Answer:

> The interesting part is the control loop. Agents operate on shared state, call tools, gather new evidence, update hypotheses, challenge prior conclusions, pause for human approval, execute an action, and verify whether the system recovered.

## “Why SMS?”

Answer:

> Incident responders are not always inside the monitoring application. SMS provides an independent human-in-the-loop channel that works even when the operator is away from their computer.

## “Why not let AI rollback automatically?”

Answer:

> Read-only investigation is autonomous, but production-changing actions are gated by policy and require explicit human approval.

---

# 49. Stretch ideas

Only implement these if the main flow is already solid.

## Voice escalation

Instead of only SMS, Incident Commander can call the on-call engineer.

## Multi-person approval

Require two approvals for extreme actions.

```text
1/2 approvals received
```

## Agent cost dashboard

Show:

```text
Tokens used
Inference cost
Time saved
```

## Incident memory

Store past incidents and let the Hypothesis Agent retrieve similar cases.

Example:

```text
Similar incident INC-0017
same checkout service
same DB connection pattern
resolved by rollback
```

## Adaptive routing

Use a small/fast model for Observability and a stronger model for Commander/Critic.

This fits nicely with an AI-infrastructure angle.

---

# 50. Final recommended scope

If time is limited, build exactly this:

### Agents

1. Observability
2. Hypothesis
3. Critic
4. Commander

### Tools

1. `get_metrics`
2. `get_logs`
3. `get_recent_deployments`
4. `inspect_db_connections`
5. `rollback_deployment`
6. `request_human_approval`

### Scenarios

1. DB connection leak
2. Payment provider outage
3. Bad deployment

### External integration

46elks SMS approval.

### UI

One realtime incident dashboard.

### Demo ending

AI diagnoses outage -> Critic challenges it -> additional evidence raises confidence -> SMS approval -> rollback -> metrics recover -> automatic incident report.

That is enough to look like a coherent product rather than a hacky prototype.

---

# 51. One-line product identity

Possible names:

- Incident Swarm
- OpsMind
- Aegis
- AutoSRE
- SwarmOps
- Sentinel
- IncidentOS
- Resolve
- Commander
- OpsPilot

A simple working title is probably best:

# **AI Incident Commander**

Subtitle:

> **A multi-agent SRE team that investigates first and asks humans before touching production.**
