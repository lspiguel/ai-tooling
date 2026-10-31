---
name: agent-companion-context
description: Used together with the Agent Companion MCP - the XrmToolBox plugin that gives a coding agent person-approved access to Dynamics 365 CE / Dataverse environments - and needs a plugin tab bound to the environment. Establishes and keeps the delivery context that every agent-companion skill shares - the connection and matching PAC profile, the publisher, the segmentation map and transport order, which transport solutions exist and where each is extracted from, the close-out strategy for each WIP solution, the pipelines and who runs them - by deriving it from the project and its environments and asking, with a recommendation, only for what cannot be derived. States the concrete steps of a task for the person to confirm, and holds the WIP ledger. Use when a task will create, shape, edit, import or end a WIP solution through the Agent Companion MCP, or when another agent-companion skill needs a delivery fact it does not have.
---

# Delivery context for the Agent Companion MCP

Every agent-companion skill needs the same facts about how the project delivers. This skill owns them. It establishes them once per task, from the project first and the person last, and the other skills read the answers instead of deciding for themselves.

It applies whatever stage the project is said to be in. A cold start for one missing transport solution, a story, and a fix to a component already in production can all happen in one task; the facts below are the same for all of them.

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
| A component's group, route and WIP | | `agent-companion-wip-shaping` |
| Import order and flags | | `agent-companion-wip-edit-loop` |
| When a WIP ends | | `agent-companion-wip-close-out` |

Rules are cited by the IDs of the plugin specification. [rules.md](./references/rules.md) indexes them; this skill enforces R7, R15, R16 and T1–T6.

---

## 1. The plugin is there

1. **Call `list_connections`.** If the agent has no such tool, stop and say: *this needs the Agent Companion MCP plugin running in XrmToolBox and registered with the agent client.* Do not fall back to another route. If the list is empty, ask the person to open the plugin and connect a tab.
2. **Choose the connection.** With one bound connection, use it and name it to the person. With several, ask which one. Never switch connection within a task without saying so.
3. **`get_connection_info`.** Note `devEnvironmentConfirmed`. Write tools refuse on an unconfirmed connection; only the person can confirm it, in the plugin tab. A build environment is bound and deliberately left unconfirmed (A2).
4. **Match PAC.** Run `pac org who` and compare its organization URL with `organization.url`. If they differ, stop; the person selects the right profile (`pac auth list`, `pac auth select`). Repeat this check whenever another skill is about to run PAC after a connection change.

## 2. Establish the facts (T1)

Read in this order, and stop looking for a fact once it is settled:

1. **Recorded facts** — a delivery-facts record in the project's `<client>-Context/` repository, or next to the segmentation map in `<client>-d365/` (T5). A record is checked against the environments before use; it is never taken on trust.
2. **The project's repositories** — the segmentation map, the solutions folder, pipeline definitions.
3. **The bound environments** — `list_solutions`, `list_publishers`, a second tab for a build environment.
4. **The person** — only for what the first three do not show.

| Fact | Used for | Where to look first |
|---|---|---|
| Development connection and PAC profile | Every call and every PAC command | Section 1 |
| Build environment | Strategy A; where transport homes are looked up | A second bound tab; pipeline definitions naming it |
| Publisher of each transport solution | WIP publisher (W3) | `list_solutions` → each transport solution's publisher; `list_publishers` |
| Segmentation map and transport order | Grouping (W1), order (W5, W6) | The map next to the solutions in `<client>-d365/` |
| Transport solutions present | Bootstrap for missing ones; transport homes (W2) | `list_solutions` on development and on build; `Other/Solution.xml` files in the repository |
| Transport source of each transport solution | Close-out strategy; where homes are looked up (T4); when an in-place WIP ends | Extraction pipeline definitions; whether a build environment exists |
| Close-out strategy of each WIP | `agent-companion-wip-close-out` | The transport source of its destination |
| Pipelines — promotion, extraction and commit, build, deployment | Close-out steps | Pipeline definitions: triggers and parameters |
| Protected prefixes | WIP names (W4) | The promotion pipeline's configuration |
| Work item ID | WIP names | The task |
| Automated runs between close-outs | Partial close-out (X8) | Pipeline triggers |
| Others working in the development environment | Shared documents (X1); publish-all on import (X11) | The person |

[delivery-facts.md](./references/delivery-facts.md) says what to search for in each source, how to read a pipeline definition for its source environment and trigger, and how to record the answers.

**Check for a managed project solution in development (R16).** While listing solutions, look for a managed solution whose publisher is one of the project's transport publishers. If there is one, stop and tell the person — it locks the project's own components under a managed layer, and removing it deletes them. Never propose importing a managed project solution into development.

## 3. Ask with a recommendation (T2)

A question carries the recommended answer, why, and what the alternatives change. Never fill a gap with a default without saying so.

```
I could not find where the Base transport solution is extracted from. The repository has an
extract-and-commit pipeline pointing at the development environment, and no build environment
is bound.

Recommendation: treat Base as extracted from development (strategy B). Close-out then adds
new components to Base in development, and edits to components already in Base ship with the
next extract.

If Base is in fact extracted from a build environment (strategy A), close-out instead needs a
promotion run per WIP, and the build environment should be bound in a second plugin tab, left
unconfirmed. Which is it?
```

## 4. State the steps (T3)

Before the first Write call of a task, and again before close-out, list the concrete steps and have the person confirm them. A change of plan during the task is stated again before acting on it. The steps name:

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

## 6. Record confirmed facts (T5)

When the person wants the answers kept, write them as a delivery-facts record:

- in `<client>-Context/` when the project has one;
- otherwise in `<client>-d365/`, next to the segmentation map.

Never in a repository that is not the client project's (R7). The agent writes the file; **a person commits and pushes it** (T6). The shape is in [delivery-facts.md](./references/delivery-facts.md#recording).

## 7. Pipelines, commits and pushes are a person's (T6)

Prepare them — the pipeline and its parameters, the files and the commit message — and hand them to the person. Run one only when the person explicitly delegates that specific run, commit or push. Never start a deployment to a downstream environment, and never one that deploys a WIP (R11).

---

## Working with the other agent-companion skills

| When | Hand to |
|---|---|
| A transport solution or publisher the work needs is missing, or a first-party app was installed | `agent-companion-transport-bootstrap` |
| A change touches a component | `agent-companion-wip-shaping` |
| A WIP is ready for edits | `agent-companion-wip-edit-loop` |
| A WIP is imported and validated, or the person asks to end one | `agent-companion-wip-close-out` |

Skills in the repository that do not depend on the plugin — for example a flow-editing or planning skill — keep their own procedures. Where one covers a step, the agent-companion skills defer to it for that step (K7). If two active skills give different instructions for the same step, follow neither silently: tell the person, and follow the stricter one only if they agree (K8).

---

## Guardrails — what a person confirms

| Moment | The person confirms |
|---|---|
| Choosing the connection | The tab is the intended development environment; the PAC profile targets the same organization |
| Each fact the agent could not derive | The answer, after reading the recommendation |
| The steps, before the first Write call and before close-out | Every WIP, route, ending and pipeline run; who runs each run, commit and push |
| A managed project solution found in development | How to proceed — the agent does not continue (R16) |
| Recording facts | Where they are written; the person commits them |
