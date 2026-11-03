---
name: agent-companion-context
description: Used together with the Agent Companion MCP - the XrmToolBox plugin that gives a coding agent person-approved access to Dynamics 365 CE / Dataverse environments - and needs a plugin tab bound to the environment. Establishes and keeps the delivery context that every agent-companion skill shares - the connection and matching PAC profile, the publisher, the segmentation map and transport order, which transport solutions exist and where each is extracted from, the close-out strategy for each WIP solution (promote to a build environment, or consolidate in development), the pipelines and who runs them - from what the person has said, the project and its environments, asking with a recommendation only for what cannot be derived, and records what is settled in the project's delivery-facts.md so later tasks start from it. Also the guide to reading a live environment through the plugin's read tools. States the concrete steps of a task for the person to confirm, and holds the WIP ledger. Use when a task will create, shape, edit, import or end a WIP solution through the Agent Companion MCP; when a change that will go through WIP solutions is being planned, because the close-out strategy decides the plan's tasks; when a plan, an open point or a question needs a fact from the environment - column types and required levels, option values, existing plug-in steps and flows, which solution holds a component; or when another agent-companion skill needs a delivery fact it does not have.
---

# Delivery context for the Agent Companion MCP

Every agent-companion skill needs the same facts about how the project delivers. This skill owns them. It establishes them once per task, from the project first and the person last, and the other skills read the answers instead of deciding for themselves.

It applies whatever stage the project is said to be in. A cold start for one missing transport solution, a story, and a fix to a component already in production can all happen in one task; the facts below are the same for all of them. Planning a change is part of the task, not a stage before it.

---

## What this skill owns

| Decision | Here | Elsewhere |
|---|---|---|
| Which connection, and that the PAC profile targets the same organization | ✓ | |
| The publisher of each transport solution | ✓ | Creating one, if ever: `agent-companion-transport-bootstrap` |
| Segmentation map, transport order, where each transport solution is extracted from | ✓ | |
| Close-out strategy of each WIP | Derived here | Carried out by `agent-companion-wip-close-out` |
| The steps of the task, and their confirmation | ✓ | Each skill supplies its part |
| The WIP ledger's shape | ✓ | Every skill reads and updates it |
| The project's delivery-facts record | ✓ | Every skill adds what it settles |
| A component's group, route and WIP | | `agent-companion-wip-shaping` |
| Import order and flags | | `agent-companion-wip-edit-loop` |
| When a WIP ends | | `agent-companion-wip-close-out` |

Rules are cited by the IDs of the plugin specification. [rules.md](./references/rules.md) indexes them; this skill enforces R7, R15, R16 and T1–T6.

### The two close-out strategies

The specification letters them A and B; people name them by what happens. Map what the person says to the strategy, and use both the name and the letter when you write.

| Strategy | What happens to a WIP | What people say |
|---|---|---|
| **A — Promote to build** | A person runs a promotion pipeline that imports the WIP into a build environment and copies it into its transport solution there; transport is extracted from build; the development WIP is deleted after the promotion is verified | "promote", "promotion", "build environment", "close-out with build", "transport solutions are only consolidated through promotion", WIPs in development named `… - PROMOTED` |
| **B — Consolidate in development** | The agent adds placement components to the transport solution in development; transport is extracted from development | "add to the Base solution", "extract from dev", no build environment |

**Promotion is not a downstream deployment.** R11 forbids deploying a WIP to test, UAT or production. Promotion to a build environment is how a WIP reaches its transport solution under A — never argue against it on R11's grounds.

---

## Reading the environment for research

The plugin's read tools answer questions about the development environment directly — what is there now, not what the last extract showed, and what was never put in a solution at all. Use them whenever a plan, an open point or a question depends on the environment, **before** writing "confirm in the environment" anywhere. When the person says to use the MCP to check something, check it now and report the result; do not turn it into a step for later.

Reads need no WIP and no confirmed steps. Each call is still approved by the person in the plugin (R2), so batch what you need and give each call a clear `reason`.

| Question | Read |
|---|---|
| Is the plugin there, which environment | `list_connections`, `get_connection_info` (section 1) |
| A table's type code, ownership, primary columns | `list_tables`, `get_table_metadata` |
| A column's type, required level, max length, lookup targets, local option values | `get_table_metadata` with `include: ["columns"]` |
| Global choice values — for enums in code, or status mappings | `get_option_sets` with `includeOptions: true` |
| Relationships | `get_relationships` |
| Plug-in steps, images and custom APIs on a table; workflows and cloud flows on a table | `query` — the research queries in `agent-companion-wip-shaping`'s [queries.md](../agent-companion-wip-shaping/references/queries.md#research--what-already-runs-on-a-table) |
| Which solutions hold a component — its transport home, overlapping WIPs | `query` over `solutioncomponent` ([queries.md](../agent-companion-wip-shaping/references/queries.md#transport-home-and-overlap)) |
| What a transport solution or a WIP holds | `list_solution_components` |
| Earlier WIPs, transport solutions and their publishers | `list_solutions`, `list_publishers` |

The plugin's metadata does not say whether a column is calculated or rollup; read `SourceType` from the extract, or say it was not checked.

**When the tools seem missing.** Some clients list MCP tools as deferred — only their names are visible until they are searched for or loaded. Search for the server's tools (for example `d365-agent`, `list_connections`) before concluding the plugin is not there; failing that, use [Invoke-CompanionTool.ps1](./scripts/Invoke-CompanionTool.ps1) (section 1). If the plugin is really not connected, research falls to the tooling that is — another MCP server with access to the environment, PAC CLI, the extract — under the planning skill's rules; WIP work still stops here.

## When the task is a plan

A plan that will change the environment through WIPs needs the delivery context before its task list is written, because the close-out strategy decides the tasks:

| | A — Promote to build | B — Consolidate in development |
|---|---|---|
| Agentic tasks end at | The WIP imported and validated, left for promotion | Placement components added to the transport solution and verified; WIP deleted |
| Then, a person | Runs the promotion; the extract is taken from build | Runs or waits for the extract from development |
| Then, the agent | Verifies the build environment's transport solution; deletes the development WIP | — |

1. Establish the facts below with read calls only, during planning — never as a step of the plan.
2. Settle the close-out strategy for each destination transport solution. It is never assumed: if neither the record, the repository, the environments nor the person's words settle it, ask (section 3). Ask before writing any close-out or extract task.
3. Write the delivery context into the plan, and the steps (section 4) as its task list. Record the settled facts (section 6).

When the plan is carried out, state the steps by reference to the plan and to anything that changed since; the person confirms (T3).

---

## 1. The plugin is there

1. **Call `list_connections`.** If the agent's client has not loaded the plugin's tools — for example the server was registered after the session began, or the client lists them as deferred — search for them first; failing that, call the same endpoint with [Invoke-CompanionTool.ps1](./scripts/Invoke-CompanionTool.ps1). It sends the same MCP request, so the plugin still asks the person to approve and logs every call; it needs the bearer token from the plugin panel or the client configuration, passed with `-Token` or set in `AGENT_COMPANION_TOKEN` (never written to a file). If the endpoint does not answer either, stop WIP work and say: *this needs the Agent Companion MCP plugin running in XrmToolBox and registered with the agent client.* Do not fall back to any route that bypasses the plugin for WIP work. If the list is empty, ask the person to open the plugin and connect a tab.

   ```powershell
   .\scripts\Invoke-CompanionTool.ps1 -Tool list_connections
   .\scripts\Invoke-CompanionTool.ps1 -Tool list_solution_components -All -OutFile <work>\wip.json `
       -Arguments @{ connection = '<connectionId>'; solutionUniqueName = '<wip>' }
   ```

   `-All` follows the paging cursor and merges the pages, joining ribbon XML chunks in order. The script returns the tool's structured result; exit code 1 is a tool error (`denied_by_user`, `not_found`, …), 2 an HTTP or protocol error.
2. **Choose the connection.** With one bound connection, use it and name it to the person. With several, use the one the project's repositories or delivery facts point to and name it; ask when nothing does. Never switch connection within a task without saying so.
3. **`get_connection_info`.** Note `devEnvironmentConfirmed`. Write tools refuse on an unconfirmed connection; only the person can confirm it, in the plugin tab. A build environment is bound and deliberately left unconfirmed (A2).
4. **Match PAC.** Run `pac org who` and compare its organization URL with `organization.url`. If they differ, stop; the person selects the right profile (`pac auth list`, `pac auth select`). Repeat this check whenever another skill is about to run PAC after a connection change.

## 2. Establish the facts (T1)

Read in this order, and stop looking for a fact once it is settled:

1. **What the person has said** — in the task, in a plan they wrote or edited, or earlier in the conversation. A statement such as "we promote to build" settles the fact unless the next sources contradict it. A contradiction is reported to the person; their words are never rewritten into another answer.
2. **Recorded facts** — `delivery-facts.md` in the project's `<client>-Context/` repository, or next to the segmentation map in `<client>-d365/` (T5). A record is checked against the environments before use; it is never taken on trust.
3. **The project's repositories** — the segmentation map, the solutions folder, pipeline definitions.
4. **The bound environments** — `list_solutions`, `list_publishers`, a second tab for a build environment.
5. **The person** — only for what the first four do not show.

| Fact | Used for | Where to look first |
|---|---|---|
| Development connection and PAC profile | Every call and every PAC command | Section 1 |
| Build environment | Strategy A; where transport homes are looked up | A second bound tab; pipeline definitions naming it |
| Publisher of each transport solution | WIP publisher (W3) | `list_solutions` → each transport solution's publisher; `list_publishers` |
| Segmentation map and transport order | Grouping (W1), order (W5, W6) | The map next to the solutions in `<client>-d365/` |
| Transport solutions present | Bootstrap for missing ones; transport homes (W2) | `list_solutions` on development and on build; `Other/Solution.xml` files in the repository |
| Transport source of each transport solution | Close-out strategy; where homes are looked up (T4); when an in-place WIP ends | Extraction pipeline definitions; whether a build environment exists |
| Close-out strategy of each WIP | `agent-companion-wip-close-out`; the plan's tasks | What the person said; the transport source of its destination; how earlier WIPs ended |
| How earlier WIPs ended, and how they were named | Strategy evidence; WIP naming; leftover search (X9) | Unmanaged non-transport solutions in development — a name such as `… - PROMOTED` is evidence of A |
| Pipelines — promotion, extraction and commit, build, deployment | Close-out steps | Pipeline definitions: triggers and parameters |
| Protected prefixes | WIP names (W4) | The promotion pipeline's configuration |
| Work item ID | WIP names | The task |
| Automated runs between close-outs | Partial close-out (X8) | Pipeline triggers |
| Others working in the development environment | Shared documents (X1); publish-all on import (X11) | The person |

[delivery-facts.md](./references/delivery-facts.md) says what to search for in each source, how to read a pipeline definition for its source environment and trigger, and how to record the answers.

**Check for a managed project solution in development (R16).** While listing solutions, look for a managed solution whose publisher is one of the project's transport publishers. If there is one, stop and tell the person — it locks the project's own components under a managed layer, and removing it deletes them. Never propose importing a managed project solution into development.

## 3. Ask with a recommendation (T2)

A question carries the recommended answer, why, and what the alternatives change. Never fill a gap with a default without saying so.

**The close-out strategy is never skipped as an unnecessary question.** Some agent clients push to act without asking; this one question still has to be settled, because it decides who does what, which environment the release is extracted from, and when each WIP is deleted. A plan or a close-out built on the wrong strategy puts work where it never ships. If no source settles it, ask — once, early, with the evidence.

```
I could not settle how the Assemblies WIP ends. No delivery facts are recorded. The
repository has no pipeline definitions, and no build environment is bound. In development,
earlier WIPs are named "… Assemblies - PROMOTED".

Recommendation: A — promote to build. The WIP is imported and validated in development and
left for you to promote; transport is extracted from build. Please bind the build
environment in a second plugin tab, left unconfirmed, so I can verify after promotion.

If Assemblies is instead extracted from development (B — consolidate in development), I add
the new components to the Assemblies transport solution in development, verify it and
delete the WIP; the next extract ships them. Which is it?
```

## 4. State the steps (T3)

Before the first Write call of a task, and again before close-out, list the concrete steps and have the person confirm them. In a plan, the steps are its task list. A change of plan during the task is stated again before acting on it. The steps name:

- each WIP with its group, route, connection and destination;
- the import order (transport order, W5);
- how and when each WIP ends — at import and validation, after promotion, or at close-out;
- each pipeline run, commit or push, **who runs it** (a person, unless they delegate that specific run, T6) and what it triggers;
- the checks after each step;
- when each WIP is deleted.

The template is in [wip-ledger.md](./references/wip-ledger.md#steps).

## 5. Keep the ledger (K5, K6)

The delivery context, the confirmed steps and the WIP ledger are kept in the shape defined in [wip-ledger.md](./references/wip-ledger.md), in the conversation and — where the person wants it kept — in the task's implementation plan. Every agent-companion skill reads and updates the same record.

**The environment wins over the ledger.** When a skill enters a task already under way — after another skill's hand-off, or in a new session — it re-reads the task's WIPs with `list_solutions` and `list_solution_components` and reconciles the ledger before acting. The procedure is in [wip-ledger.md](./references/wip-ledger.md#reconcile).

## 6. Record what is settled (T5)

The project's `delivery-facts.md` is its memory of how it delivers: each task starts from it and leaves it more complete. Whenever a task settles a fact that is not recorded — derived, stated by the person, or answered — add it; when a recorded fact turns out to have changed, update it. Do it as part of the task, not on request, and tell the person in one line what was added or changed.

- In `<client>-Context/` when the project has one;
- otherwise in `<client>-d365/`, next to the segmentation map.

Never in a repository that is not the client project's (R7). The agent writes the file; **a person commits and pushes it** (T6). The shape is in [delivery-facts.md](./references/delivery-facts.md#recording).

## 7. Pipelines, commits and pushes are a person's (T6)

Prepare them — the pipeline and its parameters, the files and the commit message — and hand them to the person. Run one only when the person explicitly delegates that specific run, commit or push. Never start a deployment to a downstream environment, and never one that deploys a WIP (R11). Promotion to a build environment is not a downstream deployment; it is still a pipeline run, so a person runs it.

---

## Working with the other agent-companion skills

| When | Hand to |
|---|---|
| A transport solution or publisher the work needs is missing, or a first-party app was installed | `agent-companion-transport-bootstrap` |
| A change touches a component | `agent-companion-wip-shaping` |
| A WIP is ready for edits | `agent-companion-wip-edit-loop` |
| A WIP is imported and validated, or the person asks to end one | `agent-companion-wip-close-out` |

Skills in the repository that do not depend on the plugin — for example a flow-editing or planning skill — keep their own procedures. Where one covers a step, the agent-companion skills defer to it for that step (K7). A planning skill applies this skill for the delivery context before it writes tasks for WIP work. If two active skills give different instructions for the same step, follow neither silently: tell the person, and follow the stricter one only if they agree (K8).

---

## Guardrails — what a person confirms

| Moment | The person confirms |
|---|---|
| Choosing the connection | The tab is the intended development environment; the PAC profile targets the same organization |
| Each fact the agent could not derive | The answer, after reading the recommendation |
| The close-out strategy of each destination | A or B, before any close-out or extract task is written or carried out |
| The steps, before the first Write call and before close-out | Every WIP, route, ending and pipeline run; who runs each run, commit and push |
| A managed project solution found in development | How to proceed — the agent does not continue (R16) |
| Recording facts | What was added to or changed in `delivery-facts.md`; the person commits it |
