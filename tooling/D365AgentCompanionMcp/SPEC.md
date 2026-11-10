# Agent Companion MCP — specification and plan

> **Status:** Draft · **Date:** 2026-10-25 · **Updated:** 2026-11-03 · **Plugin:** `D365AgentCompanionMcp` · **Intended path:** `tooling/D365AgentCompanionMcp/SPEC.md`

An XrmToolBox plugin that exposes a local Model Context Protocol (MCP) endpoint over one or more XrmToolBox connections. It gives a coding agent the few Dataverse operations PAC CLI cannot perform, so the agent can bootstrap and maintain small, unmanaged work-in-progress (WIP) solutions locally. One MCP host serves the whole XrmToolBox process; each open plugin tab binds one connection and is the window where a person sees and authorizes every call to it.

---

## Contents

1. [Problem](#1-problem)
2. [Non-negotiable rules](#2-non-negotiable-rules)
3. [Scope](#3-scope)
4. [Responsibility split — MCP vs PAC CLI](#4-responsibility-split--mcp-vs-pac-cli)
5. [Architecture](#5-architecture)
6. [MCP interface](#6-mcp-interface)
7. [Authorization model](#7-authorization-model)
8. [User experience](#8-user-experience)
9. [Workflows](#9-workflows)
10. [Implementation constraints](#10-implementation-constraints)
11. [Plan](#11-plan)
12. [Human review checkpoints](#12-human-review-checkpoints)
13. [Open questions](#13-open-questions)
14. [References](#14-references)

---

## 1. Problem

An agent working on an unpacked solution can see everything that is in flight: it reads the exported transport solutions and edits them locally. What it cannot see is anything not yet in a solution — above all, out-of-box components that have never been customized. Their IDs and current definitions exist only in the environment, and nothing exports them until they are added to a solution.

The need to reach those components is greatest at the start of a project, but it does not go away — it recurs, less often, for as long as the project runs:

| Moment | What has to be pulled in |
|---|---|
| **Project start** | Everything — there are no transport solutions yet. Publisher, base solutions, and the first out-of-box tables, forms, views, ribbons and sitemaps to customize |
| **During delivery** | Each out-of-box component a story touches for the first time — a table, form, view or ribbon not yet in any transport solution |
| **A first-party app joins the solution** | Sales, Customer Service, Field Service or another app brings a new set of out-of-box tables, forms, views, sitemaps and ribbons, none of them in a transport solution |
| **After go-live — maintenance and support** | Usually nothing new: most fixes change a component that already has a transport home, and a short-lived WIP only carries the edit. A fix that touches an out-of-box component for the first time pulls it in as during delivery |

At each of these moments the agent has no ground truth for the components it needs: it cannot see them, their IDs, or their current definitions.

These moments are conditions, not project phases. They recur and overlap. A project in support still installs a new first-party app, and a story during delivery mostly edits components that already have a transport home. One task can meet several of them at once. The workflows and the skills key on the condition the work is in, never on the phase the project is said to be in ([§9.9](#99-routes--placing-a-component-or-editing-it-in-place), K1).

The plugin closes that gap. It lets the agent:

- discover publishers, solutions, out-of-box tables, relationships, option sets, forms, views, ribbons and records;
- create empty unmanaged solutions in Dataverse and add existing components to them **without** subcomponents or required components;
- check, before re-import, whether a component changed in the environment since it was exported.

From there, PAC CLI exports and unpacks the solution, the agent edits it locally, and PAC CLI packs and imports it. The platform's import is the final validation.

The same loop serves every moment above: a small set of WIP solutions per user story or task — one for each transport solution the story touches ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) — with discovery used whenever the work reaches beyond what the transport solutions already hold. For each component, a WIP either places it in its transport solution for the first time, or only carries an edit to a component that already has one. The two routes mix freely within a project and within a story ([§9.9](#99-routes--placing-a-component-or-editing-it-in-place)).

---

## 2. Non-negotiable rules

> [!IMPORTANT]
> **Required components and subcomponents are never included when adding components to a solution.**
>
> Including them has caused solution bloat and, worse, **layering problems that cannot be undone**: components the story never touched become part of the solution, and once exported as managed they create layers in downstream environments that block other solutions' updates and uninstalls.
>
> - The MCP `add_solution_components` tool hard-codes `AddRequiredComponents = false` and, for tables, adds them **without subcomponents**. Neither can be overridden by the caller.
> - The PAC CLI path must never use `--AddRequiredComponents`.
> - Every add is followed by `list_solution_components` and a comparison against what was intended. Any unexpected component stops the workflow until a person reviews it.

| # | Rule | Why |
|---|---|---|
| R1 | **No required components, no subcomponents** — see above | Bloat and unrecoverable layering |
| R2 | **Every call is visible and authorized by a person** before it runs: in its own dialog, or in advance by a grant the person gave in a dialog — a scoped Write approval reaches no further than one environment, and a read approval no further than the scope the person chose ([§7](#phase-6--scopes-and-grants)). A grant never skips the reason (R12), the dev-environment confirmation, the fixed add flags (R1) or the log (R13). The one exception is `list_connections`, which returns only the connections the person bound to plugin tabs and is logged but not individually approved | Prompt injection; agent error |
| R3 | **Publisher creation is always confirmed individually** — never allowed for a session, and disabled by default | A second publisher, even with the same prefix, creates layering problems that cannot be solved |
| R4 | **Modern commanding (Power Fx command bar) is out of scope** — classic ribbon only | Creates chicken-and-egg dependencies between base solutions and app solutions |
| R5 | **Unpacked format is the XML layout produced by `pac solution unpack`** — not the YAML source-control layout | One format for all skills and scripts |
| R6 | **Late-bound SDK access only** — no early-bound classes | Tables change too fast to keep generated classes current |
| R7 | **No client data in the repository** — logs, exchange files and samples stay outside the working tree | Client data boundary |
| R8 | **Agent-supplied text is untrusted** — never rendered as instructions, always labelled as coming from the agent | Prompt injection |
| R9 | **One plugin tab per connection; every call names its connection** — no default connection | Approvals for an environment happen in one place; a call never lands on the wrong environment |
| R10 | **One transport group per WIP solution; one transport home per component** — placement follows the project's segmentation map ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) | A component in the wrong transport solution is costly to move once deployed (X4) |
| R11 | **WIP solutions are never deployed downstream** — to test, UAT, production or any other environment that receives managed solutions; only transport solutions are. Promotion to a build environment (strategy A, [§9.7](#97-close-out-strategies)) is not a downstream deployment: it is how a WIP's components reach their transport solution, and the normal end of a WIP under A | A deployed WIP becomes a managed layer that ties its components' lifetime to the WIP (D1) |
| R12 | **Write and Publisher calls must state a reason.** `reason` is required and must not be blank. A call without one fails before any dialog. The reason is still unverified agent text (R8) | The person approving a change sees the agent's stated intent next to the arguments, and a mismatch between them is grounds to deny |
| R13 | **Every call is logged in the plugin window as a call line and an outcome line**, including denied, rejected and host calls ([§8.6](#86-call-log-format)). The log reaches disk only when the person uses **Save as…**, unless the person turns on **Write call log to disk** ([6a.2](#6a2--automatic-call-log-on-disk)) | Gives each environment a reviewable record of what the agent asked for and what it got |
| R14 | **A transport solution is never the edit vehicle.** Local edits reach development only through a WIP holding just the components being changed. An unpacked transport solution is never edited locally and imported into development. The one import of a transport solution into development is the unmanaged alignment import from the build environment under strategy A, which carries no local edits (A8) | An unmanaged import replaces every whole document in the solution with the imported copy ([§9.4](#94-why-the-overwrite-check-matters)). As an edit vehicle, that discards every change made in development since the copy was taken, across a whole transport solution |
| R15 | **Delivery facts are derived or asked, never assumed.** The close-out strategy, which transport solutions exist, where each is extracted from, whether there is a build environment, and how pipelines run all come from the project, its environments or the person. Before shaping, and again before close-out, the agent states the steps it will take and the person confirms them ([§9.8](#98-delivery-context)) | A wrong assumption about the transport source or about who runs which pipeline puts work where it never ships, or ships it unreviewed |
| R16 | **No managed solution of the project is ever imported into development.** A managed build of a transport solution, from any downstream environment or pipeline artifact, is never imported into a development environment — by the agent, by a script, or as a step the agent proposes. When a skill finds a managed copy of a project solution in development, it stops and tells the person | The managed layer locks the project's own components in development under a managed solution. Removing it means uninstalling that solution, which deletes its components — for tables and columns, with their data |

---

## 3. Scope

### In scope (v1)

| Area | Capability |
|---|---|
| Discovery | Bound connections, connection info, publishers, solutions, solutions' components, table metadata, relationships, global option sets, classic ribbons, FetchXML queries over configuration tables and records (paged) |
| Solution shaping | Create publisher (gated), create empty unmanaged solution, add and remove existing components (no subcomponents, no required components) |
| Concurrency check | Version and modified-on state of row-backed components (forms, views, sitemaps, web resources, app modules) |
| Host | Singleton local MCP endpoint per XrmToolBox process, one plugin tab per connection, approval dialogs, visible call log |

### Out of scope (v1)

| Excluded | Handled by / reason |
|---|---|
| Solution export, unpack, pack, import, publish, delete | PAC CLI |
| Creating or changing components directly (columns, forms, views, ribbons) | Authored locally in the unpacked solution and imported through PAC CLI. Phase 6 makes one exception for speed in development — web resource content ([§6.8](#68-direct-update-tools-phase-6), [§9.11](#911-direct-updates-in-development)). [Phase 7a](#phase-7a--power-pages-authoring) proposes a second, for the source of Power Pages site components |
| Registering plug-in assemblies, plug-in types, steps and images, custom APIs and their parameters | A person, in the Plugin Registration Tool. Plug-in authoring by the agent — through a WIP, with `pac plugin push`, and by mirroring and switching steps — is deferred to [Phase 7](#phase-7--plug-in-authoring) ([§6.9](#69-plug-in-step-tools-phase-7), [§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7)) |
| A general SDK facade (arbitrary `OrganizationRequest` execution) | Not needed for the workflow; large risk surface |
| Record create, update, delete | Not needed for the workflow |
| Modern commanding | R4 |
| Managed solutions, patches, holding solutions | Not part of the WIP workflow |
| XrmToolBox's additional-connections feature | One tab binds one connection (R9) |

---

## 4. Responsibility split — MCP vs PAC CLI

| Step | Owner | Notes |
|---|---|---|
| Establish the delivery context | Agent, from the project repository, the bound environments and the person | Before any other step; [§9.8](#98-delivery-context), R15 |
| Choose the environment | **MCP** `list_connections`, `get_connection_info` | One connection per task; the PAC profile is matched to it — see below |
| Identify the user for WIP naming | **MCP** `who_am_i` | First and last names give the initials in WIP names ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) |
| Find publishers | **MCP** `list_publishers` | |
| Create publisher | **MCP** `create_publisher` | R3; rarely used |
| List solutions | **MCP** `list_solutions` | `pac solution list --json` still works but runs against the PAC profile, not the connection |
| Create empty solution | **MCP** `create_solution` | PAC has no create command; `pac solution init` is local only |
| Find components to add (IDs, names) | **MCP** metadata and query tools | |
| Add existing components | **MCP** `add_solution_components` **or** PAC `add-solution-component` | Both offered; skills decide per component type — see below |
| Verify solution contents | **MCP** `list_solution_components` | Mandatory after every add (R1) |
| Remove stray components | **MCP** `remove_solution_component` | |
| Read base ribbon definitions | **MCP** `get_entity_ribbon`, `get_application_ribbon` | An export contains only the ribbon diff |
| Export, unpack | PAC `solution export`, `solution unpack` | XML layout (R5) |
| Local edits and validation | Agent, skills, scripts | |
| Pre-import overwrite check | **MCP** `get_component_state` **or** PAC re-export and diff | |
| Pack, import, publish | PAC `solution pack`, `solution import --publish-changes` | Platform validates on import |
| Close-out — promote, add to transport, or commit | Depends on the WIP's route and on where its destination is extracted from — [§9.9](#99-routes--placing-a-component-or-editing-it-in-place), [§9.7](#97-close-out-strategies) | Each WIP feeds one transport solution (R10) |
| Delete WIP solution | PAC `solution delete` | After close-out. A WIP holding only in-place edits is deleted as soon as its edit has reached the transport source (W10). Deleting an unmanaged solution keeps its components |

### Two paths for adding components

Both are specified so the choice can be made per component type in the skills, once the spikes in [Phase 0](#phase-0--spikes) have run.

| | Path A — MCP `add_solution_components` | Path B — PAC `add-solution-component` |
|---|---|---|
| Required components | Forced off, cannot be overridden | Off unless `--AddRequiredComponents` is passed — **never pass it** |
| Table subcomponents | Forced off (table added as metadata-only or shell) | **No switch exists** in the PAC reference[^1], and spike S3 showed PAC adds a table with **all its subcomponents** (root behaviour 0). **Tables must never be added through PAC.** Columns, forms and views added through PAC on their own are safe: the platform adds their table as a shell (behaviour 2), exactly as the SDK does. Global choices, relationships and sitemaps through PAC give the same rows as the SDK; PAC 2.14.1 cannot add a model-driven app (Phase 4 live check, E1–E4 in [notes](notes.md#live-run--i31-add-paths-2026-10-31)) |
| Authorization | Plugin approval dialog per call | Agent client's own terminal-command approval |
| Batch | One call, one approval, a list of components | One command per component |
| Visible in plugin log | Yes | No |

Whichever path is used, `list_solution_components` runs afterwards (R1).

Projects that promote WIP solutions to a build environment have a third path: the promotion pipeline's component copy ([§9.7](#97-close-out-strategies), A1). It runs outside the plugin and its approvals; R1's check still follows it, against the build environment.

### Keeping PAC CLI on the same environment

The PAC auth profile and the plugin's connections are independent, and with several connections bound the mismatch is easy to make. Before any PAC command, the agent compares the active profile (`pac org who`) with the `organization.url` returned by `get_connection_info` for the connection it is using, and stops if they differ.

---

## 5. Architecture

```
┌──────────────────────────── Developer workstation ─────────────────────────────┐
│                                                                                │
│  ┌───────────────────┐   MCP over HTTP   ┌─ XrmToolBox ─────────────────────┐  │
│  │ Coding agent      │  127.0.0.1:<port> │ ┌──────────────────────────────┐ │  │
│  │ (MCP client)      │ ────────────────▶ │ │ MCP host (singleton)         │ │  │
│  │                   │  Bearer <token>   │ │ • endpoint and token         │ │  │
│  │  every call names │ ◀──────────────── │ │ • connection registry        │ │  │
│  │  its connection   │   JSON results    │ │ • routing by connection      │ │  │
│  │                   │                   │ └───────┬──────────────┬───────┘ │  │
│  │  runs PAC CLI ──┐ │                   │ ┌───────▼──────┐┌──────▼───────┐ │  │
│  └─────────────────┼─┘                   │ │ Plugin tab 1 ││ Plugin tab 2 │ │  │
│                    │                     │ │ approvals    ││ approvals    │ │  │
│  ┌─────────────────▼─┐                   │ │ call log     ││ call log     │ │  │
│  │ Unpacked WIP      │                   │ └───────┬──────┘└──────┬───────┘ │  │
│  │ solutions (XML)   │                   └─────────┼──────────────┼─────────┘  │
│  └───────────────────┘                             │              │            │
└────────────────────┬───────────────────────────────┼──────────────┼────────────┘
                     │ pac auth profile              │ connection A │ connection B
                     ▼                               ▼              ▼
          ┌────────────── Dataverse (Dev A) ─────────────┐   ┌─ Dataverse (Dev B) ─┐
          └──────────────────────────────────────────────┘   └─────────────────────┘
```

### Host and connection model

> [!IMPORTANT]
> **One MCP host per XrmToolBox process; one plugin tab per connection.** The host is a process-wide singleton shared by every open tab of the plugin. Each tab binds one XrmToolBox connection and is the window through which a person sees and approves the calls for that connection. A connection already bound to a tab cannot be bound to a second one — the plugin is an approval window, and two windows for one environment would split its approvals and its log.

| Term | Meaning |
|---|---|
| **Host** | The MCP endpoint — listener, token, routing and connection registry. One per XrmToolBox process |
| **Instance** | One open tab of the plugin. XrmToolBox allows several |
| **Bound connection** | The XrmToolBox connection an instance has registered with the host. An instance has zero or one |
| **Connection ID** | The XrmToolBox connection's ID, or an ID the host mints when XrmToolBox has none (its connection ID is optional). Agents pass it as `connection` |

| # | Behaviour |
|---|---|
| H1 | The host is a process-wide singleton, created when the first instance opens and shared by every later instance. No instance owns it: closing the tab that created it does not stop it. An instance attaches on whichever comes first, its connection (`UpdateConnection`) or its `Load` event — XrmToolBox delivers the connection of a tab opened already connected **before** `Load` (spike S6) |
| H2 | Opening the first instance starts the host — no connection is needed and there is no separate Start step. If the port is taken (for example by a second XrmToolBox process), the host does not start and every instance shows the error and the port setting |
| H3 | The host stops when the last instance closes or XrmToolBox exits. **Stop server** / **Start server** on any instance acts on the shared host and is labelled as affecting all connections. Stopping answers pending calls with `server_stopping` and ends every session-scoped approval and confirmation; environment and always scopes persist ([§7](#7-authorization-model)) |
| H4 | Port, bearer token, response cap, maximum page size and approval timeout are host-level: one value each, shown and editable on every instance, and kept across restarts. Changing the port or regenerating the token affects every connection and every client configuration |
| C1 | An instance binds at most one connection — the XrmToolBox connection of its tab. XrmToolBox's additional-connections feature is not used |
| C2 | Binding happens when the instance receives a connection: opened with one already selected, connected later, or changed. Only bound connections are returned by `list_connections` and accept calls. The person is expected to connect every instance they open |
| C3 | **One instance per connection.** Before binding, the instance checks the host's registry. The connection is already active when another instance is bound to the same XrmToolBox connection ID, **or** to the same organization (same organization ID) through a different saved connection. In that case the instance does not bind and shows the *Connection already active* dialog ([§8.5](#85-connection-already-active-dialog)): **Close this tab** closes the instance; **Choose another connection** re-opens the XrmToolBox connection dialog for this tab, and the new choice goes through the same check. Cancelling the connection dialog leaves the instance unbound. One connect from the XrmToolBox toolbar can apply the same connection to several open tabs; XrmToolBox updates them one after another. So that the first tab in that order binds and the others get the dialog, the instance **claims the connection ID synchronously** inside `UpdateConnection`, and checks the organization ID only after the asynchronous identity call returns. Spike S6 showed that binding everything after the asynchronous call makes the winner a race |
| C4 | An unbound instance shows the host status and a prompt to connect. No calls are routed to it |
| C5 | When a bound instance's connection changes, the old binding ends before the new one is checked: queued and pending calls for it fail with `connection_closed`, any once-scoped dev-environment confirmation ends, open paging cursors are released, and it disappears from `list_connections`. Session, environment and always scopes stay with their organization ([§7](#7-authorization-model)). The new connection then goes through C3 |
| C6 | Closing a bound instance ends its binding the same way. If it was the last instance, H3 applies |
| C7 | Every tool except `list_connections` takes `connection` as its mandatory first parameter. The host routes the call to the instance bound to it; an unknown ID fails at once with `connection_not_found`, without a dialog |
| C8 | Read approvals and dev-environment confirmations are scoped by organization ([§7](#7-authorization-model)). The record-query and publisher-creation settings, the approval queue and the call log are **per connection**, held by its instance. An approval given for one organization never covers a call to another, except an approval the person explicitly scopes as *always*. In Phase 6, grants are scoped by environment, tenant or globally, and the record-query setting is removed ([§7](#phase-6--scopes-and-grants)) |
| C9 | Approval dialogs are owned by the XrmToolBox main window, so they appear whichever tab is active, and name the connection in the title and on the Environment line. Each connection has its own queue; calls for different connections can await approval at the same time |
| C10 | `list_connections` calls and calls naming an unknown connection are written to a host section of the call log, shown on every instance |

```
 Person                          Instance (tab)                 Host (singleton)
 ──────                          ──────────────                 ────────────────
 opens plugin (tab 1) ─────────▶ created ─────────────────────▶ created, listening
 connects tab 1 to Dev A ──────▶ check registry ──────────────▶ A not bound
                                 bind A ──────────────────────▶ registry: A
 opens tab 2, connects Dev B ──▶ check, bind B ───────────────▶ registry: A, B
 opens tab 3, connects Dev A ──▶ check registry ──────────────▶ A already bound
                                 "Connection already active"
 chooses Close this tab ───────▶ tab 3 closed                   registry: A, B
 closes tab 1 ─────────────────▶ unbind A ────────────────────▶ registry: B
 closes tab 2 ─────────────────▶ unbind B ────────────────────▶ registry: empty
                                 last instance closed ────────▶ stopped
```

### Host

| Concern | Decision |
|---|---|
| Transport | MCP Streamable HTTP on `HttpListener`, bound to `127.0.0.1` only. stdio is not possible: the agent cannot launch a process that lives inside XrmToolBox. |
| Authentication | Random bearer token, shared by all connections, shown masked on every instance with a copy button. Generated on first use and **kept across host starts and XrmToolBox restarts** — stored in the plugin settings, encrypted with DPAPI for the current Windows user — so saved client configurations keep working. Changed only by **Regenerate**, which every client must then pick up. A rejected token is answered as described in [§6.2](#62-conventions) (D8, spike S4). |
| Origin checks | Reject requests with a non-local `Origin` header (DNS-rebinding protection). |
| Port | Configurable; default fixed so the agent's MCP configuration stays stable. |
| Lifecycle | Singleton; starts with the first instance, stops with the last — H1 to H3 in [Host and connection model](#host-and-connection-model). |
| Connection binding | One instance per connection; every call names its connection — C1 to C10. The bound connection is shown on every approval dialog. |
| Environment guard | Per connection: when a tab is bound, the user confirms the connection is a development environment where the agent may shape solutions. Write tools refuse with `dev_environment_not_confirmed` without that confirmation. Environments the agent should only read — such as a build environment fed by a promotion pipeline — are bound and left unconfirmed. How long a confirmation lasts is chosen by the person ([§7](#7-authorization-model)). |
| Threading | Requests are queued per connection; SDK calls run on a background worker using a clone of that connection's service; approvals marshal to the UI thread. |
| Approval timeout | Configurable (default 120 s). Timeout returns `denied_no_response` to the agent. |

### JSON-RPC and MCP layer

The plugin must not ship a DLL next to itself (see [§10](#10-implementation-constraints)). The MCP layer is **hand-rolled**: JSON-RPC 2.0 plus the MCP methods in [§6.1](#61-protocol-surface), on `HttpListener`, serialized with **`Newtonsoft.Json` 13** — referenced at build time and loaded from XrmToolBox's own copy, never shipped with the plugin.

The official MCP C# SDK cannot be used in-process: every stable release needs `System.Text.Json` 10.x and other 10.x libraries, while XrmToolBox ships and redirects to 9.0.0.7, and its HTTP server package targets .NET 8 or later only (spike S1, [notes](notes.md#s1--mcp-host-inside-xrmtoolbox-on-net48)). The protocol revision is pinned at build time and reported by `server/discover`.

### Serialization

| Payload | Shape |
|---|---|
| Records | Human-readable JSON with typed wrappers for references, money, option sets and aliased values, plus formatted values — the value rules under [`query`](#query). Library choice and source-inclusion confirmed in spike S1. |
| Metadata | Trimmed projection (see each tool in [§6](#6-mcp-interface)) — never the raw `EntityMetadata` graph. Labels in the organization's base language. |
| XML payloads (FormXml, ribbon XML) | Returned as strings. Ribbon responses are decompressed server-side. |
| Size cap | Every response is capped (default 40,000 characters, measured on the text content block — the JSON copy that clients count and may persist; the structured copy duplicates it on the wire). Clients may save larger results to disk — Claude Code does above 50,000 characters — so the cap keeps every page in memory end to end. Large results are always paged, never truncated and never written to a file — lists by item, XML payloads in chunks ([§6.2](#62-conventions)). |

---

## 6. MCP interface

This section is the contract the host implements and the agent skills code against. Tiers are defined in [§7](#7-authorization-model); connection routing in [Host and connection model](#host-and-connection-model).

### 6.1 Protocol surface

The host is **dual-era** on one endpoint, as revision 2026-07-28 allows. Revision **2026-07-28** is primary: stateless, with the protocol version in every request's `_meta`. Clients that still open with the `initialize` handshake are served through a fallback under **2025-11-25** or **2025-06-18** — the handshake revisions with structured tool output. Older revisions are not supported. Spike S4 showed both kinds of client in use: Claude Code 2.1.312 speaks 2026-07-28, while VS Code 1.144.0 and Claude Code 2.1.72 only open with `initialize`.

The host chooses the era per request:

| Request carries | Era |
|---|---|
| `_meta["io.modelcontextprotocol/protocolVersion"]` | 2026-07-28 — stateless |
| Method `initialize` | Handshake — opens a session |
| `Mcp-Session-Id` header and no `_meta` version | Handshake — continues that session |
| None of these | Rejected as 2026-07-28 with an unsupported version |

**2026-07-28 methods**

| Method | Behaviour |
|---|---|
| `server/discover` | Returns `supportedVersions: ["2026-07-28"]`, `capabilities: { "tools": { "listChanged": false } }`, `_meta["io.modelcontextprotocol/serverInfo"]` `{ "name": "d365-agent-companion-mcp", "version": "<plugin version>" }`, short `instructions`, and `ttlMs` with `cacheScope: "private"`. No resources, prompts or sampling. Answered without approval |
| `tools/list` | Every tool in [§6.3](#63-tool-summary), in a fixed order, each with `name`, `title`, `description`, `inputSchema` and `outputSchema`, plus `ttlMs` and `cacheScope: "private"`. The list does not depend on which connections are bound; `create_publisher` is listed even when disabled |
| `tools/call` | Runs one tool, routed by its `connection` argument (C7) |

Every 2026-07-28 result carries `resultType: "complete"` and `_meta["io.modelcontextprotocol/serverInfo"]`.

**Handshake-era fallback**

| Method | Behaviour |
|---|---|
| `initialize` | Agrees the client's `protocolVersion` when it is `2025-11-25` or `2025-06-18`; otherwise offers `2025-11-25`, and the client decides whether to continue. Returns `protocolVersion`, `capabilities: { "tools": { "listChanged": false } }`, `serverInfo` and `instructions`, and issues a session in the `Mcp-Session-Id` response header |
| `notifications/initialized` | HTTP `202` |
| `notifications/cancelled` | HTTP `202`; cancels the session's pending call with that `requestId`, if it is still queued or awaiting approval |
| `ping` | Empty result |
| `tools/list`, `tools/call` | The same tools, arguments, approvals and results as 2026-07-28, without the fields that revision added (`resultType`, `ttlMs`, `cacheScope`, result `_meta`) |
| HTTP `DELETE` with the session header | Ends the session |

Sessions hold only the agreed revision and a last-used time, in memory; they expire after 30 minutes idle and when the host stops. They carry no approvals, connections or cursors — those stay where they are in 2026-07-28.

**Transport and protocol rules**

| Situation | Response |
|---|---|
| No `_meta` version, no `initialize`, no session — or a `_meta` version other than `2026-07-28` | HTTP `400`, JSON-RPC error `-32022` (`UnsupportedProtocolVersion`) with `data.supported: ["2026-07-28"]` and `data.requested` |
| 2026-07-28: `MCP-Protocol-Version`, `Mcp-Method` or `Mcp-Name` header missing, or not matching the body | HTTP `400`, JSON-RPC error `-32020` (`HeaderMismatch`) |
| Handshake era: unknown or expired session | HTTP `404`, JSON-RPC error `-32001`; the client starts again with `initialize` |
| Handshake era: `MCP-Protocol-Version` header not matching the session's revision | HTTP `400`, JSON-RPC error `-32600` |
| Unknown method | 2026-07-28: HTTP `404`, JSON-RPC error `-32601`. Handshake era: JSON-RPC error `-32601` |
| Unknown tool name | JSON-RPC error `-32602` |
| HTTP `GET` on the endpoint, or `DELETE` without a session | HTTP `405` — no notification stream is offered in either era |
| `Mcp-Session-Id` on a 2026-07-28 request, or `Last-Event-ID` | Ignored |
| JSON-RPC notification | HTTP `202`, no body |

Responses are single `application/json` bodies; the host never opens SSE streams. A call awaiting approval holds its HTTP request open until the person answers or the approval times out. The client closing that request cancels the call and withdraws its dialog.

**Keep-alive and cancellation (D9).** `HttpListener` reports no client disconnect, so the host probes for one. When a `tools/call` reply is not ready within 2 seconds — the call is queued, awaiting approval, or waiting on Dataverse — the host commits HTTP `200` with a chunked `application/json` body and writes one space every 2 seconds until the reply follows. JSON allows whitespace before a value, so the body is still one JSON document. Every check that can answer with another HTTP status runs before this point. A failed write means the client closed the request: a call still queued or awaiting approval is cancelled, its dialog is withdrawn, and it is logged as `cancelled_by_client`. A call already running completes — a Dataverse request cannot be recalled — and is logged as usual. In a handshake session, `notifications/cancelled` naming a pending request cancels it the same way.

State that spans calls — the `connection` and paging `cursor` handles — travels as ordinary tool arguments in both eras, as revision 2026-07-28 recommends.

### 6.2 Conventions

**Connection.** Every tool except `list_connections` takes `connection` — the `connectionId` of an entry returned by `list_connections` — as a required parameter. It is the first property in each `inputSchema` and first in `required`, so clients that order arguments by schema present it first; on the wire, presence is what matters. There is no default: a call without `connection` fails with `invalid_argument` even when only one connection is bound. Agent-side rule: when exactly one connection is bound, the agent may use it and names it to the person; when more than one is bound, the agent asks which to use; it never switches connection within a task without saying so.

**Agent reason.** Every tool except `list_connections` also accepts `reason` (string, at most 500 characters). It is optional for Read tools. For Write and Publisher tools it is **required** (R12). Their `inputSchema` lists it in `required` with `minLength: 1`, and a call whose `reason` is missing, or holds only whitespace, fails with `invalid_argument` (`details.argument: "reason"`). That check runs before every other check and before any dialog. The reason is shown in the *Agent-supplied — not verified* box of the approval dialog (R8) and in full on the call's log line ([§8.6](#86-call-log-format)). It never influences what the tool does. The per-tool input tables below omit `reason`, and they also omit the paging inputs `cursor` and `pageSize`. Paged tools are marked *Paged*.

**Value formats**

| Kind | Format |
|---|---|
| GUID | Lowercase, hyphenated, no braces — `00000000-0000-0000-0000-000000000000`. Inputs accept any case, with or without braces |
| Date-time | ISO 8601 in UTC with `Z` — `2026-10-25T14:03:31Z` |
| Labels | Organization base language; `null` when the label is empty |
| Row versions | `versionNumber` as a decimal string — values can exceed JSON's safe integer range |
| Missing values | Omitted from records; `null` in fixed-shape metadata projections |
| Logical names | As stored (lowercase) |

**Successful result.** `resultType` is `"complete"`; `structuredContent` holds the result object matching the tool's `outputSchema`; `content` holds one text block with the same JSON, for clients that do not read structured content. `isError` is `false`.

**Errors**

| Level | When | Shape |
|---|---|---|
| HTTP | Missing or wrong bearer token → `401`; non-local `Origin` → `403`; `GET`, or `DELETE` without a session → `405` | `401` carries a plain-text body: *"Agent Companion MCP: bearer token missing or not current. Copy the token from the plugin panel and update the client."* Other statuses have no body |
| JSON-RPC | Malformed request, unsupported protocol version, header mismatch, unknown session, unknown method, unknown tool name — with the HTTP status in [§6.1](#61-protocol-surface) | Standard JSON-RPC `error` object |
| Tool | Everything else — invalid arguments, unknown connection, denial, Dataverse fault | `isError: true`; one text block holding a `ToolError` as JSON; no `structuredContent` |

**A `401` looks like OAuth to clients.** MCP clients treat `401` as the start of an OAuth flow. In spike S4, VS Code answered a stale token by probing for OAuth metadata and offering manual client registration. The host does not use OAuth: it serves only the MCP endpoint, so the well-known OAuth paths answer `404`, and the `401` body names the real cause. The README tells users what to do when a client shows an OAuth prompt: update the token (in VS Code, **MCP: Reset Cached Inputs**, then restart the server) and cancel any client registration. Keeping the token across restarts (D8) makes this rare.

| Code | Meaning | Agent's next step |
|---|---|---|
| `invalid_argument` | Missing, wrongly typed or out-of-range argument, or a rule broken (for example `<all-attributes />`). `details.argument` names it | Fix the call |
| `connection_not_found` | No plugin tab is bound to `connection` | `list_connections`; ask the person |
| `connection_closed` | The tab closed or changed connection while the call was queued or awaiting approval | `list_connections`; ask the person |
| `dev_environment_not_confirmed` | Write or Publisher tool on a connection not confirmed as development | Ask the person |
| `denied_by_user` | The person denied the call | Do not retry without new instructions |
| `denied_no_response` | Approval timed out | Do not retry without new instructions |
| `tool_disabled` | `create_publisher` switched off for this connection. In v1 a `query` that touched records could also be switched off; from Phase 6 it asks as a record read instead | Ask the person |
| `not_found` | A named solution, publisher, table, column, option set, relationship or component does not exist. `details` names it | Check the name |
| `already_exists` | Unique name already taken | Choose another name, or reuse the existing one |
| `managed_solution` | Add or remove against a managed solution | Use an unmanaged solution |
| `version_conflict`, `managed_component`, `not_in_solution` | `update_web_resources` (Phase 6, built) and the *Phase 7* step tools — see [§6.8](#68-direct-update-tools-phase-6) | See §6.8 |
| `invalid_cursor` | The cursor is unknown, expired, or belongs to another connection or tool | Start again from the first page |
| `server_stopping` | The host is stopping; the call did not run | Wait for the person |
| `cancelled_by_client` | The client closed the request, or cancelled it, before the call ran ([§6.1](#61-protocol-surface)). Written to the call log only — the agent never receives it | — |
| `dataverse_error` | The platform returned a fault. `details.errorCode` (hex string, for example `0x80040217`) and `details.message` | Report to the person |

**Paging.** Large results are always paged — never truncated, and never written to a file. Every tool whose result can grow pages the same way:

| Input | Type | Notes |
|---|---|---|
| `cursor` | string | Omit on the first call. On later calls pass the previous result's `nextCursor` with the same `connection`; other arguments are ignored |
| `pageSize` | integer | First call only. Items per page; default per tool; maximum is the host's max page size (default 500). Ignored for XML payloads |

Every paged result carries `nextCursor`, which is `null` when there is nothing more.

- **Cursors are host-side handles**, opaque to the agent and bound to the connection, tool and arguments of the first call. Continuation pages run under that call's approval; they raise no new dialog.
- **Pages respect the response cap** (default 40,000 characters of text content). A page that would exceed it ends early at a whole item and the next page carries on. XML payloads are split into chunks of at most the cap, which the agent concatenates in order.
- **One snapshot per paged call.** Record-backed tools page with Dataverse paging cookies. Metadata and XML results are read once, on the first call, and held in memory for the cursor's lifetime, so every page comes from the same snapshot. Nothing is written to disk.
- **Cursors expire** after 10 minutes without use, when the connection's binding ends, or when the host stops. An expired or unknown cursor fails with `invalid_cursor`.

**Schema notation.** Inputs are given as tables, outputs in TypeScript notation. They map to JSON Schema as follows: `T | null` → a type array including `"null"`; `?` → not listed in `required`; string literal unions → `enum`; `Guid` → `{ "type": "string", "format": "uuid" }`; `DateTime` → `{ "type": "string", "format": "date-time" }`. Input schemas set `additionalProperties: false`. Output schemas do not: fields may be added in later versions, but none is removed or renamed within a major version.

```ts
type Guid = string;
type DateTime = string;

interface ToolError {
  error: {
    code: string;                    // a code from the table above
    message: string;                 // plain text, for the person and the agent
    details?: Record<string, unknown>;
  };
}

interface Paged {
  nextCursor: string | null;         // null when there is nothing more
}

interface ComponentType {
  code: number;                      // solutioncomponent.componenttype value
  name: string;                      // for example "Entity", "Attribute", "SystemForm"
}

interface UserRef {
  id: Guid;
  name: string | null;
}

type RootComponentBehavior =
  | "includeSubcomponents"           // 0
  | "doNotIncludeSubcomponents"      // 1
  | "includeAsShellOnly";            // 2
```

Example `inputSchema`, showing `connection` first and required:

```json
{
  "type": "object",
  "properties": {
    "connection": { "type": "string", "description": "connectionId from list_connections" },
    "managed": { "type": "string", "enum": ["all", "unmanaged", "managed"], "default": "all" },
    "includeHidden": { "type": "boolean", "default": false },
    "nameContains": { "type": "string" },
    "publisherUniqueName": { "type": "string" },
    "reason": { "type": "string", "maxLength": 500 }
  },
  "required": ["connection"],
  "additionalProperties": false
}
```

### 6.3 Tool summary

| Tool | Tier | Purpose |
|---|---|---|
| `list_connections` | Host | Connections bound to plugin tabs — the values accepted as `connection` |
| `get_connection_info` | Read | One connection: organization, version, base language, user, dev-environment confirmation, settings |
| `who_am_i` | Read | The connected user's ID, first name, last name and full name — for WIP naming |
| `list_publishers` | Read | Publishers with prefix and option value prefix |
| `list_solutions` | Read | Solutions with version, managed state and publisher; filterable |
| `list_tables` | Read | Tables with logical name, display name, custom/managed flags; filterable |
| `get_table_metadata` | Read | Columns, keys and identifiers for one table |
| `get_relationships` | Read | 1:N, N:1 and N:N relationships for one table |
| `get_option_sets` | Read | Global choices with values and labels |
| `get_entity_ribbon` | Read | Full classic ribbon for a table |
| `get_application_ribbon` | Read | Full classic application ribbon |
| `query` | Read | FetchXML, paged; flags queries that touch records |
| `list_solution_components` | Read | Components in a solution, with type, ID and root behaviour; paged |
| `get_component_state` | Read | Version and modified-on for row-backed components |
| `get_required_components` | Read | What components require, and which solutions already hold each requirement |
| `create_solution` | Write | Empty unmanaged solution |
| `add_solution_components` | Write | Add existing components, no subcomponents, no required components |
| `remove_solution_component` | Write | Remove a component from a solution (the component itself is kept) |
| `create_publisher` | Publisher | New publisher — disabled by default |
| `update_web_resources` | Write | *Phase 6.* Create or update web resources from local files in a WIP, and publish them |
| `mirror_steps` | Write | *Phase 7.* Copy plug-in steps and their images to the same plug-in types in another assembly, disabled |
| `disable_steps` | Write | *Phase 7.* Disable plug-in steps |
| `enable_steps` | Write | *Phase 7.* Enable plug-in steps |
| `rebind_custom_apis` | Write | *Phase 7.* Point custom APIs at the plug-in type of the same name in another assembly |
| `update_site_components` | Write | *Phase 7a, proposed; shaped by the Phase 6b spikes.* Update the source of Power Pages site components in a WIP from local files: web templates, page copy, page CSS and JavaScript, content snippets and web files |

The Read tools were checked against the Microsoft Dataverse MCP server in spike S5 (2026-10-29); none is replaced. Findings are in [notes.md](notes.md#s5--overlap-with-the-dataverse-mcp-server).

### 6.4 Host tool

#### `list_connections`

Lists the connections bound to plugin tabs (C2). Unbound tabs, and tabs showing the *Connection already active* dialog, are not listed. Runs without an approval dialog (R2) and is logged in the host section of every tab's call log (C10).

Input: none — `{}`.

```ts
interface ListConnectionsResult {
  host: {
    version: string;                 // plugin version
    protocolVersion: string;         // pinned MCP revision
  };
  connections: Array<{
    connectionId: string;            // pass as `connection`; minted by the host when XrmToolBox has none
    connectionName: string;          // as named in the XrmToolBox connection manager
    organization: {
      id: Guid;
      uniqueName: string;
      friendlyName: string;
      url: string;                   // web application URL
    };
    devEnvironmentConfirmed: boolean;
    boundOn: DateTime;
  }>;                                // ordered by boundOn; empty when no tab is bound
}
```

An empty list means the person has not connected a tab; the agent asks them to open the plugin and connect.

### 6.5 Read tools

#### `get_connection_info`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | [§6.2](#62-conventions) |

```ts
interface ConnectionInfo {
  connectionId: string;
  connectionName: string;
  organization: {
    id: Guid;
    uniqueName: string;
    friendlyName: string;
    url: string;
    version: string;                 // platform version, for example "9.2.x"
    baseLanguageCode: number;        // LCID
    environmentId: string | null;    // Phase 6; null when the connection does not report it
    tenantId: Guid | null;           // Phase 6; null when the connection does not report it
  };
  user: UserRef;                     // the user the XrmToolBox connection runs as; full name only —
                                     // first and last names come from who_am_i
  devEnvironmentConfirmed: boolean;
  devEnvironmentScope: "once" | "session" | "environment" | "always" | null;
                                     // Phase 6: "always" is withdrawn
  readApprovalScope: "session" | "environment" | "always" | null;  // null: every Read call prompts
                                     // Phase 6: adds "tenant"; "always" becomes "global"
  recordReadScope: "session" | "environment" | "tenant" | "global" | null;  // Phase 6
  writeScope: "session" | "environment" | null;                             // Phase 6
  settings: {
    recordQueriesEnabled: boolean;   // per connection; removed in Phase 6 (record reads are grants)
    publisherCreationEnabled: boolean; // per connection
    fileRoot: string | null;         // Phase 6, §6.8; null: not set yet
    fileRootScope: "environment" | "tenant" | null;  // Phase 6: the level the File root was set at
    maxPageSize: number;             // host
    responseCapBytes: number;        // host
    approvalTimeoutSeconds: number;  // host
  };
}
```

#### `who_am_i`

The connected user's identity, read from Dataverse. XrmToolBox's connection object carries the user's ID and sign-in name but not their first and last names, so the plugin runs `WhoAmIRequest` and then reads the user's `systemuser` row. The agent uses the names for WIP solution naming ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)).

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |

```ts
interface WhoAmIResult {
  userId: Guid;
  businessUnitId: Guid;
  organizationId: Guid;
  firstName: string | null;          // systemuser.firstname
  lastName: string | null;           // systemuser.lastname
  fullName: string | null;           // systemuser.fullname, in the organization's name format
  domainName: string | null;         // systemuser.domainname — the sign-in name
  isApplicationUser: boolean;        // true for an application (service principal) user
  impersonating: boolean;            // true when the XrmToolBox connection impersonates another user;
                                     // every other field then describes the impersonated user
}
```

#### `list_publishers`

*Paged* — default page size 100.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `nameContains` | string | | Matches unique or friendly name |

```ts
interface ListPublishersResult {
  publishers: Array<{
    publisherId: Guid;
    uniqueName: string;
    friendlyName: string;
    customizationPrefix: string;
    optionValuePrefix: number;
    isReadonly: boolean;             // system publishers
    description: string | null;
  }>;
  nextCursor: string | null;
}
```

#### `list_solutions`

*Paged* — default page size 100.

Completes solution handling on the MCP side: the agent finds transport solutions, WIP solutions, and the managed solutions a first-party app installed ([§9.3](#93-a-first-party-app-joins-the-solution)) on the same connection it shapes them through.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `managed` | `"all" \| "unmanaged" \| "managed"` | | Default `all` |
| `includeHidden` | boolean | | Default `false`. Hidden solutions (`isvisible = false`), such as the system's internal solutions, are excluded unless set |
| `nameContains` | string | | Matches unique or friendly name |
| `publisherUniqueName` | string | | Only solutions of this publisher |

```ts
interface ListSolutionsResult {
  solutions: Array<{
    solutionId: Guid;
    uniqueName: string;
    friendlyName: string;
    version: string;
    isManaged: boolean;
    isVisible: boolean;
    publisher: {
      publisherId: Guid;
      uniqueName: string;
      customizationPrefix: string;
    };
    installedOn: DateTime | null;
    modifiedOn: DateTime;
    description: string | null;
  }>;                                // ordered by friendlyName
  nextCursor: string | null;
}
```

#### `list_tables`

*Paged* — default page size 250.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `filter` | `"all" \| "custom" \| "system"` | | Default `all` |
| `nameContains` | string | | Matches logical, schema or display name |

```ts
interface ListTablesResult {
  tables: Array<{
    logicalName: string;
    schemaName: string;
    displayName: string | null;
    metadataId: Guid;
    objectTypeCode: number;
    isCustom: boolean;
    isManaged: boolean;
    isCustomizable: boolean;
    isIntersect: boolean;
  }>;
  nextCursor: string | null;
}
```

#### `get_table_metadata`

*Paged* over columns — default page size 100. `table` and `keys` come on the first page.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `logicalName` | string | ✓ | |
| `include` | `("columns" \| "keys")[]` | | Default both |
| `columnFilter` | `"all" \| "custom" \| "system"` | | Default `all` |

```ts
interface TableMetadataResult {
  table: {
    logicalName: string;
    schemaName: string;
    displayName: string | null;
    metadataId: Guid;
    objectTypeCode: number;
    primaryIdAttribute: string;
    primaryNameAttribute: string | null;
    ownershipType: "none" | "userOwned" | "teamOwned" | "businessOwned"
                 | "organizationOwned" | "businessParented";
    isCustom: boolean;
    isManaged: boolean;
    isCustomizable: boolean;
  };
  columns?: Column[];                // present when include has "columns"; the paged items
  keys?: AlternateKey[];             // present when include has "keys"; first page only
  nextCursor: string | null;
}

interface Column {
  logicalName: string;
  schemaName: string;
  displayName: string | null;
  metadataId: Guid;
  type: string;                      // AttributeTypeName, e.g. "StringType", "LookupType", "PicklistType"
  requiredLevel: "none" | "systemRequired" | "applicationRequired" | "recommended";
  isCustom: boolean;
  isManaged: boolean;
  isValidForForm: boolean;
  attributeOf: string | null;        // set on companion columns, e.g. a lookup's name column
  maxLength: number | null;          // string and memo columns
  targets: string[] | null;          // lookup columns
  optionSet: {
    name: string;
    isGlobal: boolean;
    options: Array<{ value: number; label: string | null }> | null;
                                     // local choice, Yes/No, status and state columns;
                                     // null for global choices — use get_option_sets
  } | null;
}

interface AlternateKey {
  logicalName: string;
  schemaName: string;
  displayName: string | null;
  metadataId: Guid;
  keyAttributes: string[];
  isManaged: boolean;
}
```

#### `get_relationships`

*Paged* — default page size 100.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `logicalName` | string | ✓ | |
| `kinds` | `("oneToMany" \| "manyToOne" \| "manyToMany")[]` | | Default all three |

```ts
interface RelationshipsResult {
  logicalName: string;
  relationships: Array<OneToManyRelationship | ManyToManyRelationship>;
  nextCursor: string | null;
}

interface OneToManyRelationship {
  kind: "oneToMany" | "manyToOne";   // relative to the requested table;
                                     // a self-referencing relationship appears once as each
  schemaName: string;
  metadataId: Guid;
  referencedEntity: string;
  referencedAttribute: string;
  referencingEntity: string;
  referencingAttribute: string;
  cascade: {
    assign: CascadeType;
    delete: CascadeType;
    merge: CascadeType;
    reparent: CascadeType;
    share: CascadeType;
    unshare: CascadeType;
  };
  isCustom: boolean;
  isManaged: boolean;
}

type CascadeType = "noCascade" | "cascade" | "active" | "userOwned" | "removeLink" | "restrict";

interface ManyToManyRelationship {
  kind: "manyToMany";
  schemaName: string;
  metadataId: Guid;
  entity1LogicalName: string;
  entity1IntersectAttribute: string;
  entity2LogicalName: string;
  entity2IntersectAttribute: string;
  intersectEntityName: string;
  isCustom: boolean;
  isManaged: boolean;
}
```

#### `get_option_sets`

*Paged* — default page size 50.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `names` | string[] | | Omit for all global choices |
| `filter` | `"all" \| "custom" \| "system"` | | Default `all` |
| `includeOptions` | boolean | | Default `true` |

```ts
interface OptionSetsResult {
  optionSets: Array<{
    name: string;
    displayName: string | null;
    metadataId: Guid;
    optionSetType: string;           // OptionSetType name, e.g. "Picklist", "Boolean"
    isCustom: boolean;
    isManaged: boolean;
    options: Array<{ value: number; label: string | null }> | null;  // null when includeOptions is false
  }>;
  notFound: string[];                // requested names that do not exist; first page only
  nextCursor: string | null;
}
```

#### `get_entity_ribbon`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `logicalName` | string | ✓ | |
| `location` | `"form" \| "homepageGrid" \| "subGrid" \| "all"` | | Default `all` |

#### `get_application_ribbon`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |

Both ribbon tools return:

```ts
interface RibbonResult {
  logicalName: string | null;        // null for the application ribbon
  location: "form" | "homepageGrid" | "subGrid" | "all" | null;  // null for the application ribbon
  ribbonXml: string;                 // this page's chunk of the decompressed XML
                                     // (RibbonDefinitions, CommandDefinitions, RuleDefinitions)
  sizeBytes: number;                 // size of the whole document
  nextCursor: string | null;
}
```

*Paged* in chunks. Ribbon XML is large: each page's `ribbonXml` is the next part of one document, split at the response cap. The agent concatenates the pages in order; the document is complete when `nextCursor` is `null`.

Building a ribbon is slow: the whole ribbon of a large table can take the platform longer than the SDK's default two-minute request timeout. Both ribbon requests run with a timeout of up to **5 minutes**, set on the call's service clone for that request only and restored afterwards; the process-wide `CrmServiceClient.MaxConnectionTimeout` is not changed. The call's keep-alive ([§6.1](#61-protocol-surface), D9) holds the client's request open meanwhile. When the whole ribbon still times out, the agent asks for `form`, `homepageGrid` and `subGrid` one at a time.

#### `query`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `fetchXml` | string | ✓ | `page`, `count` and `paging-cookie` attributes are set by the server and ignored if supplied. `top` is rejected with `invalid_argument` — Dataverse refuses `top` together with paging; use `pageSize` |
| `includeFormattedValues` | boolean | | Default `true` |

*Paged* — default page size 50.

```ts
interface QueryResult extends Paged {
  entityName: string;                // root entity of the FetchXML
  touchesRecords: boolean;           // true when a table outside the configuration allow-list is queried
  records: Array<Record<string, Value>>;
}

type Value =
  | string                           // text, GUID, date-time
  | number                           // whole number, decimal, float
  | boolean                          // Yes/No
  | OptionValue
  | OptionValues
  | Reference
  | MoneyValue;

interface OptionValue  { _option: number;   label?: string | null }
interface OptionValues { _options: number[]; labels?: (string | null)[] }
interface Reference    { _ref: { entity: string; id: Guid }; name?: string | null }
interface MoneyValue   { _money: number;    formatted?: string | null }
```

```json
{
  "nextCursor": "<cursor>",
  "entityName": "systemform",
  "touchesRecords": false,
  "records": [
    {
      "formid": "00000000-0000-0000-0000-000000000000",
      "name": "[FormName]",
      "type": { "_option": 2, "label": "Main" },
      "objecttypecode": "account"
    }
  ]
}
```

Rules:

- **Paging from the first release.** Development tables are expected to be small, but every query is paged and the agent continues with `nextCursor` only when it needs more.
- **Explicit columns.** `<all-attributes />` is rejected with `invalid_argument`; the agent must name columns.
- **Record detection.** The root entity and every `link-entity` are checked against the configuration allow-list. Anything outside it marks the query as touching records: the approval dialog and the log say so, and the call fails with `tool_disabled` when record queries are switched off for the connection. Approval is the same single Read approval as every other read ([§7](#7-authorization-model)). In Phase 6 the switch is removed, and such a query needs a `RecordRead` grant or its own per-call approval ([§7](#phase-6--scopes-and-grants)).
- **Values.** Columns without a value are omitted. Aliased values (link-entity columns) are keyed by the name the platform returns (`alias.column`) and serialized by the same rules, without the `AliasedValue` wrapper. `label`, `labels`, `name` and `formatted` are present only when `includeFormattedValues` is true.

Configuration allow-list (v1): `publisher`, `solution`, `solutioncomponent`, `systemform`, `savedquery`, `sitemap`, `appmodule`, `appmodulecomponent`, `webresource`, `ribboncustomization`, `organization`, `workflow`, `customcontrol`, `environmentvariabledefinition`, `connectionreference`, `pluginassembly`, `plugintype`, `sdkmessage`, `sdkmessagefilter`, `sdkmessageprocessingstep`, `sdkmessageprocessingstepimage`, `customapi`, `customapirequestparameter`, `customapiresponseproperty`, `role`, `fieldsecurityprofile`. The entries from `workflow` onwards let the agent read the subtype and name columns that the segmentation map uses ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)). The plug-in and custom API tables let it read what is registered on a table — steps by message and table, their images, the plug-in types of an assembly, a custom API's parameters — for planning and research; they hold registration metadata, not business records.

#### `list_solution_components`

Used after every add to compare intended against actual (R1); the comparison reads every page.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `solutionUniqueName` | string | ✓ | Managed or unmanaged |
| `componentTypes` | integer[] | | Only these component type codes |

*Paged* — default page size 250.

```ts
interface SolutionComponentsResult extends Paged {
  solution: {
    solutionId: Guid;
    uniqueName: string;
    isManaged: boolean;
  };
  components: Array<{
    solutionComponentId: Guid;
    componentType: ComponentType;
    objectId: Guid;
    name: string | null;             // resolved where cheap (tables, columns, forms, views, web resources,
                                     // sitemaps, app modules); otherwise null
    rootComponentBehavior: RootComponentBehavior | null;  // null when the platform stores none
    rootSolutionComponentId: Guid | null;  // set for components added under a root, e.g. a column under its table
    isMetadata: boolean;
  }>;
}
```

#### `get_component_state`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `components` | `{ "type": number, "id": Guid }[]` | ✓ | 1–200 entries. Types: view `26`, form `60`, web resource `61`, sitemap `62`, app module `80`. Any other type fails with `invalid_argument` naming the entries |

```ts
interface ComponentStateResult {
  components: Array<{
    type: number;
    id: Guid;
    found: boolean;                  // false when deleted — itself a change the overwrite check must catch
    name: string | null;
    versionNumber: string | null;    // row version, decimal string
    modifiedOn: DateTime | null;     // null for forms: systemform has no modifiedon (Phase 2, live)
    modifiedBy: UserRef | null;      // null for forms: systemform has no modifiedby
  }>;                                // in request order
}
```

The overwrite check compares `versionNumber`, which every accepted type has; `modifiedOn` and `modifiedBy` are for the person. Table and column metadata carry no comparable row version; for those, the overwrite check falls back to a PAC re-export and diff.

#### `get_required_components`

What the given components require, and where each requirement already lives — the check behind W6 and X5 before close-out ([§9.5](#95-transport-solutions-and-segmented-wip-solutions), [§9.6](#96-issues-common-to-every-close-out-strategy)). Runs `RetrieveRequiredComponentsRequest` for each component, then reads `solutioncomponent` for the solutions holding each required component.

*Paged* — default page size 250; one item per dependency.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `components` | `ComponentSelector[]` | ✓ | 1–50 entries; the selector of [`add_solution_components`](#add_solution_components) |

```ts
interface RequiredComponentsResult extends Paged {
  dependencies: Array<{
    dependent: {                     // one of the requested components
      type: number;
      id: Guid;
      name: string | null;
    };
    required: {
      componentType: ComponentType;
      objectId: Guid;
      name: string | null;
      isManaged: boolean | null;     // null when the platform does not report it
      solutions: Array<{             // solutions holding it, excluding the default solution;
        uniqueName: string;          // empty when it is in none
        isManaged: boolean;
      }>;
    };
    dependencyType: "solutionInternal" | "published" | "unpublished";
  }>;
}
```

The agent judges each requirement; the plugin does not. A requirement is in place when it is managed and comes from an installed app, when one of its solutions is a transport solution earlier in the order, or when it is in a WIP of the same story for an earlier group. Anything else — above all a requirement held only by a later transport group, or by no solution — is reported to the person (W6, X5).

### 6.6 Write tools

Every Write tool requires `reason` (R12). A call without one fails with `invalid_argument` ([§6.2](#62-conventions)). Every Write tool also fails with `dev_environment_not_confirmed` on a connection not confirmed as development. Both checks run before any dialog is shown.

#### `create_solution`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `uniqueName` | string | ✓ | Letters, digits and underscores, starting with a letter; at most 65 characters |
| `friendlyName` | string | ✓ | |
| `publisherUniqueName` | string | ✓ | Must exist; the dialog shows its prefix |
| `version` | string | | Default `1.0.0.0`; four numeric parts |
| `description` | string | | |

```ts
interface CreateSolutionResult {
  solutionId: Guid;
  uniqueName: string;
  friendlyName: string;
  version: string;
  publisher: {
    publisherId: Guid;
    uniqueName: string;
    customizationPrefix: string;
  };
}
```

Fails with `already_exists` if the unique name is taken, `not_found` if the publisher does not exist.

#### `add_solution_components`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `solutionUniqueName` | string | ✓ | Must be unmanaged, else `managed_solution` |
| `components` | `ComponentSelector[]` | ✓ | 1–50 entries |
| `tableBehavior` | `"metadata" \| "shell"` | | Tables only; default `metadata`. `metadata` → `DoNotIncludeSubcomponents = true` with `IncludedComponentSettingsValues` unset → root behaviour 1, exporting the table's own properties. `shell` → the same flag with an empty `IncludedComponentSettingsValues` array → root behaviour 2, exporting an empty table shell. **Both behaviours also export the table's whole unmanaged ribbon diff** — including a shell the platform adds as the parent of a column, form or view. Confirmed in spike S2 |

```ts
type ComponentSelector =
  | { type: number; id: Guid }                          // any accepted type, by ID
  | { type: 1; logicalName: string }                    // table
  | { type: 2; table: string; logicalName: string }     // column
  | { type: 9; name: string }                           // global option set
  | { type: 10; schemaName: string }                    // relationship
  // Phase 6 — plug-in components (below)
  | { type: 91; name: string; version?: string }        // plug-in assembly
  | { componentTable: PluginComponentTable; id: Guid }  // custom API, request parameter, response property
  | { componentTable: "customapi"; uniqueName: string }
  | { componentTable: "customapirequestparameter" | "customapiresponseproperty"; customApi: string; uniqueName: string };

type PluginComponentTable = "customapi" | "customapirequestparameter" | "customapiresponseproperty";
```

Component type codes accepted in v1 (confirmed against the `solutioncomponent` type option set in spike S2): table `1`, column `2`, global option set `9`, relationship `10`, view `26`, form `60`, web resource `61`, sitemap `62`, app module `80`. Any other type fails with `invalid_argument` naming the entry. The application ribbon customization is not accepted until its type code is confirmed live; S2 did not settle it.

**Plug-in components (Phase 6).** Accepting them is not plug-in authoring, which stays in [Phase 7](#phase-7--plug-in-authoring): each component must already be registered in development, by a person in the Plugin Registration Tool, and the tool only puts it in a WIP. From plugin version 1.2026.11.2 they are accepted; on an earlier plugin they are refused as above and are added with PAC (Path B).

| Component | Type code | Selected by |
|---|---|---|
| Plug-in assembly | `91` | ID, or `name` — with `version` when several versions of the name are registered; an ambiguous name fails with `invalid_argument` listing the versions |
| Plug-in step | `92` | ID only — step names are not unique |
| Custom API | Per environment | `componentTable: "customapi"` with ID or `uniqueName` |
| Custom API request parameter, response property | Per environment | `componentTable` with ID, or `customApi` (the custom API's unique name) and the parameter's or property's `uniqueName` |

- **Codes that vary by environment.** Custom API types have codes of 10000 and above, assigned per organization (10038 in S9). The tool reads the code for the connection's organization from `solutioncomponentdefinition` by table name and reports it in `results`. A numeric `type` of 10000 or above is refused with `invalid_argument`, because the same number means something else elsewhere.
- **Fixed behaviour is unchanged (R1).** Adding a step adds neither its plug-in type nor its assembly; `get_required_components` checks that they are in place (W6).
- **What travels with what.** Adding an assembly does not add its steps[^5]: every step the WIP should carry is added on its own. A step's images are not separate solution components and travel with the step (S9); its secure configuration is never exported. Whether adding a custom API also adds its request parameters and response properties is checked at Phase 6 exit. Until it is, the read-back shows it, and anything added that was not requested is reported as `unexpected`, with a code of 10000 or above named by its table (`customapirequestparameter`, say) rather than as `ComponentType10038`.
- **How the tool resolves them (as built).** An assembly selected by name is matched in `pluginassembly`; a name that matches several versions fails with `invalid_argument` and `details.versions` lists them, and a `version` that matches none is `not_found`. A custom API component selected by `uniqueName` is read from its table, a request parameter or response property by the `customapiid` of the named API plus its own `uniqueName`. The code is read with `solutioncomponentdefinition.primaryentityname`; an environment with no definition for the table fails the call with `not_found`. Steps, assemblies and the three custom API tables are named in the dialog; a selector by `componentTable` has no numeric `type`, and one that carries both is `invalid_argument`.
- **The same selectors** apply to `get_required_components` and `remove_solution_component`.

```ts
interface AddComponentsResult {
  solution: { solutionId: Guid; uniqueName: string };
  results: Array<{
    index: number;                   // position in the request
    type: number;
    id: Guid;                        // resolved ID
    name: string | null;
    status: "added" | "alreadyPresent" | "failed" | "notAttempted";
    error?: ToolError["error"];      // when status is "failed"
  }>;
  unexpected: Array<{
    componentType: ComponentType;
    objectId: Guid;
    name: string | null;
    rootComponentBehavior: RootComponentBehavior | null;
  }>;
  addedCount: number;
  unexpectedCount: number;
}
```

Sequence:

1. **Resolve** every selector to an ID. Any failure fails the whole call with `not_found`; nothing is added. Resolution comes first so the approval dialog shows names, not bare IDs.
2. **Approve** — Write tier, one dialog for the whole list.
3. **Snapshot** the solution's components.
4. **Add** in request order with the fixed behaviour below. A component already in the solution is `alreadyPresent` and is not added again — a second add could change how the platform holds it. The first failure stops the run; later entries are `notAttempted`.
5. **Read back** and compute `unexpected` = components present now − snapshot − requested. Each unexpected component is also raised as a warning in the call log. One addition is expected, not unexpected: when a column, form or view is added without its table, the platform adds the table as a shell (root behaviour 2) to hold it (spike S2). Such a parent shell is reported in `results` with status `added`, the `index` of the column, form or view it holds, and `name` noting it was added as the parent. It is not counted in `unexpected` or in `addedCount` — unless it arrives with any behaviour other than 2, or for a table none of the added entries belongs to, when it is unexpected.

   A **relationship** brings parents the same way (Phase 4 live check, E2): for a 1:N, the referencing table as a shell with the lookup column under it; for an N:N, the first table as a shell. These are expected and are reported like a column's parent shell, with the relationship's `index` (Q12). *Not yet built — until it is, the tool reports them as `unexpected`.* Unless the change customizes them, the agent removes these auto-added components afterwards and reads the WIP back to check the relationship is still held; when in doubt it asks the person.

Steps 1 and 2 run before the dialog, inside the connection's queue: a solution that does not exist or is managed, and any selector that does not resolve, fail before the person is asked. `create_solution` resolves its publisher, and `remove_solution_component` its component, the same way.

Fixed behaviour, not exposed to the caller:

- `AddRequiredComponents = false`
- `DoNotIncludeSubcomponents = true` for tables

A partial run is a successful tool result: the agent reads `results`, and any `unexpected` entry stops the workflow for review (R1).

#### `remove_solution_component`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `solutionUniqueName` | string | ✓ | Must be unmanaged, else `managed_solution` |
| `component` | `ComponentSelector` | ✓ | |

```ts
interface RemoveComponentResult {
  solution: { solutionId: Guid; uniqueName: string };
  component: {
    componentType: ComponentType;
    objectId: Guid;
    name: string | null;
  };
}
```

Removes the component from the solution only; the component itself stays in the environment. Fails with `not_found` when the component is not in the solution.

### 6.7 Publisher tool

#### `create_publisher`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `uniqueName` | string | ✓ | |
| `friendlyName` | string | ✓ | |
| `prefix` | string | ✓ | 2–8 alphanumerics, starts with a letter, not `mscrm` |
| `optionValuePrefix` | integer | ✓ | 10000–99999 |
| `description` | string | | |

```ts
interface CreatePublisherResult {
  publisherId: Guid;
  uniqueName: string;
  friendlyName: string;
  customizationPrefix: string;
  optionValuePrefix: number;
}
```

Disabled unless the user enables it for the connection. When disabled, the tool returns `tool_disabled`, telling the agent that a person must create or choose the publisher. When enabled, it raises the publisher dialog ([§8.3](#83-publisher-creation-dialog)); a wrong typed confirmation counts as `denied_by_user`. Requires `reason` (R12) and dev-environment confirmation. Fails with `already_exists` if the unique name is taken; a prefix shared with an existing publisher is warned about in the dialog, not refused.

### 6.8 Direct-update tools (Phase 6)

> [!NOTE]
> **Built (2026-11-05, version 1.2026.11.2).** Specified from the analysis of 2026-11-03 ([notes](notes.md#phase-6--preparation)); the workflows that use the tool are in [§9.11](#911-direct-updates-in-development). Live exit checks are pending ([Phase 6](#phase-6--planned-enhancements)).

These tools change components in development without a solution export and import. They are Write tier: each needs a `reason` (R12), dev-environment confirmation and a per-call approval, and each is logged (R13). They take over two jobs the import loop does today:

- **The overwrite check runs inside the tool.** Each item carries the version the agent baselined, and the tool refuses an item whose current version differs, before the dialog and again just before writing.
- **The WIP still records the work.** A component the tool creates is created in the named WIP (`SolutionUniqueName`) and an existing one must already be in it, so W7 overlap checks, W10 and close-out see it. Creating in a solution adds no required components (R1); the read-back still follows.

**Files are passed by path, never by content.** The host runs on the person's machine, so the agent passes a local path and the plugin reads the file. Every path is resolved against the organization's **File root** ([below](#file-root)). The dialog shows the File root once and each item's path relative to it, with its size and SHA-256, so the person approves the bytes that will be written.

#### File root

The **File root** is the root folder of the git repository that holds the source files the agent is working on. A client project usually has one repository and many environments, so a File root is set at one of two levels:

| Level | Applies to | Use when |
|---|---|---|
| **Environment** | This environment only | The environment works from a repository of its own, or the connection reports no tenant ID |
| **Tenant** | Every environment in this tenant, including ones bound later | The tenant's environments all work from the same repository — the usual case |

A File root set for an environment takes precedence over its tenant's. It belongs to the environment or tenant, not to a tab, so every tab bound there, now or later, uses the same folder. Environments and tenants are matched by the same IDs as grants ([§7](#phase-6--scopes-and-grants)). The File root is kept in the plugin settings, returned by `get_connection_info` as `settings.fileRoot` and `settings.fileRootScope`, and shown and changed in the Active Approvals window ([§8.8](#88-active-approvals-window-phase-6)).

A file item's `path` takes one of two forms:

| Form | Example | The file read is | Accepted when |
|---|---|---|---|
| Relative | `Webresources/scripts/account.form.js` | File root + `path` | The result is inside the File root |
| Absolute | `C:\Repos\[ClientName]-d365\Webresources\scripts\account.form.js` | `path` | `path` is inside the File root |

The check runs while the call is preparing, before any dialog:

- **Normalized first.** `/` and `\` are both accepted. The path is made full — `.` and `..` resolved — before it is compared, so a relative path that climbs out of the root with `..` is outside it.
- **Inside means under the root.** The full path must start with the File root followed by a separator, compared without regard to case. A path on another folder, drive or share is outside.
- **No links.** The file, and every folder between the File root and the file, must not be a symbolic link or junction. A link can lead outside the root while the path text stays inside it.
- **The file must exist** and be readable.

A path that fails any of these fails the whole call with `invalid_argument`. `details.argument` names the item, and `details` gives the File root and the full path it resolved to.

**When no File root is set.** If neither the environment nor its tenant has a File root when a call that takes files is preparing, the plugin asks the person to choose one before the approval dialog. Like the approval dialog, the prompt is owned by the XrmToolBox main window (C9). It explains the rules above:

- the folder should be the root of the git repository that holds the source files the agent edits;
- a relative path from the agent is read from that folder;
- an absolute path must lie inside it;
- the choice is kept and can be changed in Active Approvals.

The prompt asks for the folder and the level:

- **Every environment in this tenant** is pre-selected. It is disabled, with the reason shown, when the connection reports no tenant ID.
- **This environment only** is the other choice.

The prompt lists the paths in the call. When they are absolute, the folder picker starts at the root of the git working tree that holds the first one, if there is one. A folder that is not the root of a git working tree (no `.git` entry) gets a warning but is not refused. Once a folder and a level are chosen, the plugin saves them and preparation continues. **Cancel** fails the call with `denied_by_user`. No choice within the approval timeout fails it with `denied_no_response`. In both cases nothing is written and the File root stays unset.

The prompt appears even when a scoped Write grant would skip the approval dialog, because the File root is a setting, not an approval. A change to the File root applies from the next call. A call already running finishes under the root it resolved its paths with.

New error codes for these tools, added to [§6.2](#62-conventions):

| Code | Meaning | Agent action |
|---|---|---|
| `version_conflict` | A component's current `versionNumber` differs from the `expectedVersion` passed. `details` lists each one with both versions and `modifiedBy` | Re-baseline, merge, ask the person |
| `managed_component` | The component is managed — a first-party web resource, step or custom API | Do not customize it this way |
| `not_in_solution` | An existing component is not in the named WIP | Add it with `add_solution_components` or PAC first |

#### `update_web_resources`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `solutionUniqueName` | string | ✓ | The WIP. Unmanaged, else `managed_solution` |
| `items` | `WebResourceItem[]` | ✓ | 1–50 entries |
| `publish` | boolean | | Default `true`: one `PublishXmlRequest` for every resource written, and for every unchanged one whose content was written but never published (step 3) |

```ts
interface WebResourceItem {
  path: string;              // relative to the File root, or absolute inside it
  name: string;              // web resource name, e.g. "[prefix]_/scripts/account.form.js"
  displayName?: string;      // new resources only; default: the file name
  expectedVersion?: string;  // versionNumber from get_component_state; required for an existing resource
}

interface UpdateWebResourcesResult {
  solution: { solutionId: Guid; uniqueName: string };
  results: Array<{
    index: number;
    name: string;
    id: Guid | null;
    status: "created" | "updated" | "unchanged" | "failed" | "notAttempted";
    versionBefore: string | null;
    versionAfter: string | null;
    sha256: string;
    published: boolean;      // this resource was in the call's publish, and the publish succeeded
    error?: ToolError["error"];
  }>;
  published: boolean;
  publishError: string | null;       // the publish failed after the writes: they are kept, and the reason is here
  unexpected: Array<{ componentType: ComponentType; objectId: Guid; name: string | null; rootComponentBehavior: RootComponentBehavior | null }>;
  unexpectedCount: number;           // the read-back of step 5, as add_solution_components reports it
}
```

Sequence:

1. **Prepare** (before the dialog). If the organization has no File root, prompt for one ([File root](#file-root)). Read every file and resolve every name. The web resource type comes from the file extension (`.htm`/`.html` 1, `.css` 2, `.js` 3, `.xml` 4, `.png` 5, `.jpg`/`.jpeg` 6, `.gif` 7, `.xsl`/`.xslt` 9, `.ico` 10, `.svg` 11, `.resx` 12). An unknown extension, or a type different from an existing resource's, is `invalid_argument`. Fail the whole call before the dialog on: a path the File root does not accept; a file over the organization's maximum upload size; an existing resource that is managed (`managed_component`), outside the WIP (`not_in_solution`), or without `expectedVersion` (`invalid_argument`), or whose `versionNumber` differs from it (`version_conflict`); a new resource whose name does not start with the WIP publisher's prefix and `_` (`invalid_argument`).
2. **Approve.** One dialog listing each item: create or update, name, path, size, SHA-256 and versions.
3. **Write** in request order. An item whose content matches the environment's *unpublished* content (`RetrieveUnpublishedRequest`) byte for byte is `unchanged` and is not written: every update raises `versionNumber`, identical content included (S10). When `publish` is true, an `unchanged` item's *published* content (`Retrieve`) is also compared: if it differs, the content was written earlier without a publish — by a call with `publish: false`, or in the maker portal — and the item is published in step 4 without being written again (decided 2026-11-09, from the live test of every web resource type). Before each update the version is read again; a change since preparation fails the item with `version_conflict`. The first failure stops the run; later entries are `notAttempted`.
4. **Publish**, when `publish` is true, the resources written and the `unchanged` ones whose published content differs, in one targeted request; nothing when there are none. An item's `published` says whether it was in that request and the request succeeded. Publishing raises `versionNumber` again (S10), so `versionAfter` is read after the publish and is the baseline for the next call.
5. **Read back** the WIP and report anything unexpected, as `add_solution_components` does.

Measured in S10 on a near-empty trial environment: an update takes 0.3–0.5 s and a targeted publish about 3 s. Exporting and importing a one-resource WIP takes 14–21 s before its publish-all (11–31 s), and before PAC's unpack and pack.

Not covered: registering a script on a form, a command or an app. Those are edits to a form, ribbon diff or app module and stay on the import path.

### 6.9 Plug-in step tools (Phase 7)

> [!NOTE]
> **Deferred to [Phase 7](#phase-7--plug-in-authoring), not built.** Specified with Phase 6 on 2026-11-03 and moved out of it the same day, with the rest of plug-in authoring ([§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7)). Phase 7's spike may reshape them. They follow §6.8's conventions: Write tier, `reason`, dev-environment confirmation, per-call approval, the log, a WIP that records the work, and §6.8's error codes.

#### `mirror_steps`

Copies plug-in steps from one assembly to another that contains the same plug-in types — a developer's side-loaded build, or the new registration after a major or minor version change. Mirrors **start disabled**. Switching which copy runs is a separate, deliberate call to `disable_steps` and `enable_steps`.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `solutionUniqueName` | string | ✓ | The WIP that will hold the mirrors. Unmanaged. Steps are not added to a solution with their assembly, so the tool creates each mirror in this WIP |
| `steps` | Guid[] | ✓ | 1–50 source `sdkmessageprocessingstepid`s |
| `targetAssembly` | string \| Guid | ✓ | Name or ID of a registered, unmanaged `pluginassembly` |

```ts
interface MirrorStepsResult {
  solution: { solutionId: Guid; uniqueName: string };
  targetAssembly: { pluginAssemblyId: Guid; name: string; version: string };
  results: Array<{
    index: number;
    sourceStepId: Guid;
    sourceName: string;
    pluginTypeName: string;     // typename, matched in the target assembly
    mirrorStepId: Guid | null;
    mirrorName: string | null;  // "<source name> (<target assembly name>)"
    images: number;
    secureConfiguration: boolean;
    state: "disabled" | null;
    status: "created" | "failed" | "notAttempted";
    error?: ToolError["error"];
  }>;
}
```

- **Matching.** Each source step's plug-in type is matched by `typename` in the target assembly. A type missing from the target fails the whole call with `not_found` before the dialog, listing the missing type names. Registering the target assembly and its types is the person's job, in the Plugin Registration Tool, which registers every type in the assembly.
- **What is copied.** Message, message filter (table), stage, mode, rank, deployment, filtering attributes, unsecure configuration, impersonating user, async auto-delete and description, each image with its type, alias, name, attributes and message property, and the secure configuration as a new `sdkmessageprocessingstepsecureconfig` row. A secure configuration the connection cannot read fails the step.
- **Marker.** The mirror's description records `Mirror of <source step ID> from <source assembly>`. That is how the tool, the leftover check and the person find mirrors.
- **Refused before the dialog:** a source step already mirrored to the target assembly (`already_exists`); a target assembly that is managed or is the source step's own assembly (`invalid_argument`).
- **Disabled straight after creation.** A step cannot be created disabled (S9): `statecode` 1 on create is ignored, and with `statuscode` 2 the create is refused. The tool creates each mirror and disables it in the next request. For that moment both copies run, and the dialog says so.
- **What the platform adds.** Creating a step in the WIP adds only the step (92, behaviour 0); its images and secure configuration are not separate solution components (S9). The same `typename` may be registered in two assemblies (S9).

#### `disable_steps` and `enable_steps`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `steps` | Guid[] | ✓ | 1–50 `sdkmessageprocessingstepid`s |

```ts
interface StepStateResult {
  results: Array<{
    index: number;
    stepId: Guid;
    name: string;
    assembly: string;
    stateBefore: "enabled" | "disabled";
    stateAfter: "enabled" | "disabled" | null;
    status: "changed" | "unchanged" | "failed" | "notAttempted";
    error?: ToolError["error"];
  }>;
}
```

A managed step fails the whole call with `managed_component` before the dialog. The dialog lists each step with its message, table, stage, assembly and current state. For each step, the dialog also shows every other step with the same message, table and stage and its state, so a switch that would leave both copies running, or neither, is visible before approval. A step already in the requested state is `unchanged`. These tools change no solution membership. State alone is not reliable across imports ([§9.10](#step-state-is-not-transported), M3).

#### `rebind_custom_apis`

A custom API's main operation is its `plugintypeid`, not a step, so it is switched by pointing it at another plug-in type. The platform allows that column to change after creation[^5], and the change takes effect on the next call — within a second in S9.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `customApis` | string[] | ✓ | 1–50 unique names |
| `targetAssembly` | string \| Guid | ✓ | Name or ID of a registered, unmanaged `pluginassembly` |

```ts
interface RebindCustomApisResult {
  results: Array<{
    index: number;
    uniqueName: string;
    pluginTypeName: string;
    assemblyBefore: string;
    assemblyAfter: string | null;
    status: "rebound" | "unchanged" | "failed" | "notAttempted";
    error?: ToolError["error"];
  }>;
}
```

The current plug-in type's `typename` is matched in the target assembly; a missing one fails the call with `not_found` before the dialog. A managed custom API fails with `managed_component`. Rebinding back is the same call with the original assembly. Steps registered on a custom API's message are ordinary steps and use `mirror_steps`.

---

## 7. Authorization model

The plugin's approval is the authoritative gate. The agent client's own tool-approval prompt is a convenience and is not relied on.

> [!NOTE]
> **Phase 6 changes this model (built in 1.2026.11.2).** The tiers, scopes and saved settings immediately below describe v1. [Phase 6 — scopes and grants](#phase-6--scopes-and-grants) at the end of this section replaces them:
> - a Tenant scope is added, and *Always* is shown as Global;
> - record reads and Write calls become grants;
> - Write and the dev-environment confirmation are capped at one environment;
> - grants also record environment and tenant IDs.

| Tier | Tools | Approval | Notes |
|---|---|---|---|
| **Host** | `list_connections` | None | No dialog; logged on every tab (R2) |
| **Read** | Every read tool — connection info, identity, publishers, solutions, metadata, ribbons, queries (configuration and records), component state, solution components, required components | One prompt for all reads, scoped *once*, *session*, *environment* or *always* | Record queries can be switched off per connection |
| **Write** | `create_solution`, `add_solution_components`, `remove_solution_component`; in Phase 6 also `update_web_resources`; in Phase 7 `mirror_steps`, `disable_steps`, `enable_steps`, `rebind_custom_apis` | Once, per call | Requires dev-environment confirmation and a `reason` (R12) |
| **Publisher** | `create_publisher` | Once, with typed confirmation | Disabled by default (R3); requires a `reason` (R12) |

### Read approval

A single approval covers every Read tool; there is no per-tool allowance. The dialog offers four scopes:

| Scope | Covers | Ends |
|---|---|---|
| **Once** | This call and its continuation pages | When the call completes |
| **Session** | Every Read call to this organization | When the host stops, or **Revoke** in that tab |
| **Environment** | Every Read call to this organization, in this and later sessions | **Revoke** in that tab, or in settings |
| **Always** | Every Read call to every organization, including ones bound later | Settings |

> [!NOTE]
> **Recommended:** approve reads **per environment** for development environments and **per session** for production and other non-development environments. *Always* removes the person from reads everywhere, including environments bound later; use it only with a reason.

### Dev-environment confirmation

The confirmation that lets Write tools run on a connection ([§5](#host)) has the same four scopes:

| Scope | Applies to | Ends |
|---|---|---|
| **Once** | This binding | When the tab closes or changes connection |
| **Session** | This organization | When the host stops |
| **Environment** | This organization, in this and later sessions | Cleared in that tab or in settings |
| **Always** | Every organization — the guard is off | Cleared in settings |

*Always* is set only in settings, never from the binding prompt. It carries a warning and is shown on every tab while it is on. Write and Publisher calls still need their own per-call approval.

Environment and always scopes persist between sessions in XrmToolBox's plugin settings, which record only organization IDs, friendly names and scopes, plus the host's bearer token encrypted with DPAPI for the current Windows user — no arguments or results. Phase 6 adds environment and tenant IDs, the Tenant scope, File roots and a schema version ([below](#phase-6--scopes-and-grants)).

### Rules

- **Scoped by organization.** Session and environment scopes belong to an organization, not to a tab; a tab that rebinds to the same organization finds them in place (C8).
- **Deny is a normal result.** The agent receives a tool error, `denied_by_user` or `denied_no_response`, and must not retry the same call without new instructions from the person.
- **Arguments shown verbatim.** The dialog shows the full arguments (FetchXML pretty-printed, component lists resolved to names) and flags a query that touches records. The agent-supplied `reason` is shown in a separate box labelled *Agent-supplied — not verified* (R8). The box is always filled for Write and Publisher calls (R12), and for Read calls it appears only when a reason was given.
- **One dialog per call.** Concurrent calls to one connection queue; the queue length is visible in its tab.

### Phase 6 — scopes and grants

> [!NOTE]
> **Built (2026-11-05, version 1.2026.11.2).** Decided 2026-11-04 ([notes](notes.md#decisions), D16). This replaces the scopes and saved settings above; the text above describes v1, which an older plugin still follows.

A client project usually has one repository and many environments, often all in one tenant. Phase 6 lets a grant cover one environment, one tenant or every tenant, and lets the reach depend on what the grant allows.

**Grant kinds.** Each kind is a separate grant. A grant of one kind never covers a call of another.

| Kind (`ApprovalKind`) | Covers | Scopes offered |
|---|---|---|
| `ReadApproval` | Every Read call except a `query` that touches records | Once, Session, Environment, Tenant, Global |
| `RecordRead` (new) | A `query` that touches records ([§6.5](#query)) | Once, Session, Environment, Tenant, Global |
| `Write` (new) | Every Write call | Once, Session, Environment |
| `DevEnvironment` | The dev-environment confirmation that Write calls require | Once, Session, Environment |
| — | `create_publisher` | Once, with typed confirmation. Never a grant (R3) |

**Scopes.**

| Scope (`ApprovalScope`) | Shown as | Covers | Kept | Ends |
|---|---|---|---|---|
| `Once` | Once | This call and its continuation pages; for the dev-environment confirmation, this binding | Not stored | When the call completes or the binding ends |
| `Session` | Session only | This environment | In memory | When the host stops, or **Revoke** |
| `Environment` | Per environment | This environment | Plugin settings | **Revoke** |
| `Tenant` (new) | Per tenant | Every environment in this tenant, including ones bound later | Plugin settings | **Revoke** |
| `Always` | Global | Every environment in every tenant, including ones bound later | Plugin settings | **Revoke** |

The enum keeps the name `Always`, so saved v1 grants still load; the dialogs, the Approvals Window and the log say *Global*. A call runs under the broadest grant that covers it. Every scope is offered in the approval dialog ([§8.2](#82-approval-dialog)), and the Active Approvals window ([§8.8](#88-active-approvals-window-phase-6)) lists and revokes them.

**Rules.**

- **Writes stop at one environment.** A Write grant or a dev-environment confirmation never reaches beyond one environment: only development is written, and a broader grant would also cover environments bound later. The v1 *always* dev-environment confirmation, the "guard is off" setting, is withdrawn. A saved one is dropped on upgrade, and the person confirms each development environment again.
- **A Write grant does not replace the other checks.** Every Write call still needs the dev-environment confirmation, a non-blank reason (R12) and the fixed add flags (R1), and is logged (R13). Publisher creation stays once per call with typed confirmation (R3). Its switch stays per connection and off by default.
- **Records are their own grant.** A query that touches records runs only under a `RecordRead` grant, or under a per-call approval whose dialog says it reads records. `ReadApproval` does not cover it, and `RecordRead` covers nothing else. The per-connection **Record queries** switch is removed. The plugin exists to read, the first record query always asks, and a person who does not want a tab reading an environment closes the tab. `tool_disabled` no longer applies to record queries.
- **Broad scopes carry a warning.** The dialog warns when Per tenant or Global is chosen, because those scopes also cover environments bound later.

> [!NOTE]
> **Recommended:** Read **per tenant** when the tenant holds only the project's development and test environments; otherwise **per environment** for development and **per session** for production. The same for record reads. Global only with a reason.

**Identity.** Each grant records three IDs:

- the **organization ID**, from the identity call;
- the **environment ID** and **tenant ID**, from the connection's `CrmServiceClient`;
- plus the environment's friendly name, for display only.

The IDs are applied as follows:

- **The organization ID is enough to load a grant.** A saved grant that has only an organization ID is accepted; every v1 grant is like this. When a connection with that organization ID binds and reports its environment and tenant IDs, the plugin fills them into the grant and saves it.
- **Every ID known on both sides must match.** A session or environment grant applies when the organization IDs match, and also the environment and tenant IDs wherever both the grant and the connection know them. A grant that matches on the organization ID but differs on another ID does not apply. The Approvals Window marks it *Stale*. This is the case of an environment that was copied, restored or reset.
- **Tenant scope needs a tenant ID.** A tenant grant applies to a connection that reports the same tenant ID. A connection that reports none (`Guid.Empty`) gets no tenant grants. For that connection, the dialog and the File root prompt show the Per tenant option disabled, with the reason.
- **Global grants hold no IDs.**

**Saved settings.** `HostSettings` gains fields; no existing element or enum name changes.

| Element | Holds |
|---|---|
| `SchemaVersion` | Integer. A file without it is version 0 (v1). Phase 6 writes 1 |
| `Grants/PersistedGrant` | The v1 elements, plus `EnvironmentId` (string) and `TenantId` (Guid), each empty until known |
| `FileRoots/PersistedFileRoot` | `Scope` (`Environment` or `Tenant`), `OrganizationId`, `EnvironmentId`, `TenantId`, `Name` (the environment's friendly name), `Path` ([§6.8](#file-root)) |

How the file is carried between versions:

- **Upgrade.** When the file's `SchemaVersion` is older than the plugin's, an upgrade mapping runs on load. A grant the mapping cannot carry is dropped, and the person approves again. The port, limits, bearer token, setup version and File roots are never dropped by an upgrade, because losing the token would break every client configuration. The mapping from version 0 keeps Read grants as they are and drops *always* dev-environment confirmations.
- **Downgrade.** An older plugin ignores elements it does not know and drops grants whose kind or scope it does not know. Its next save removes them for good. This is accepted: the person approves again. The token and host settings survive, because every version knows them.

The settings still record no arguments or results.

---

## 8. User experience

### 8.1 Main panel

Each tab shows its own connection on top and the shared host below it.

```
┌─ Agent Companion MCP ─────────────────────────────────────────────┐
│ This tab   : [ConnectionName] → [ClientName] DEV                  │
│              https://<org>.crm.dynamics.com                       │
│ Dev env    : Confirmed — this environment, remembered  [Clear]    │
│ Settings   : [ ] Publisher creation                               │
│ File root  : C:\Repos\[ClientName]-d365   tenant · read-only      │
│ Reads      : Approved — this environment, remembered              │
│                                                      [Revoke]     │
├─ Host — shared by all tabs ───────────────────────────────────────┤
│ Server     : ● Running   http://127.0.0.1:47321/mcp               │
│ Token      : ●●●●●●●●●●●●●●●●  [Copy] [Regenerate]                │
│              [Stop server — all connections] [Copy client config] │
│ Bound      : 2 — [ClientName] DEV (this tab), [ClientName] DEV2   │
│ Limits     : Max page size [500]  Response cap [40K]  Timeout     │
│              [120 s]                                              │
├─ Call log — this connection ─── no wrap; scrolls horizontally ───┤
│ 2026-10-30 14:02:11  list_publishers  reason —  connection=000000 │
│   → ok  environment  0.3 s  publishers=[{"publish…  nextCursor=nu │
│ 2026-10-30 14:03:05  create_solution  reason "Base solution for s │
│   → ok  once  0.8 s  solutionId=00000000-0…  uniqueName=[prefix]_ │
│ 2026-10-30 14:03:31  add_solution_components  reason "Adding the  │
│   → ok  once  2.4 s  solution={"solution…  results=[{"index":…  u │
│   ⚠ unexpected Relationship [prefix]_account_contact              │
│ 2026-10-30 14:04:02  query  reason "Count active accounts by segm │
│   → error denied_by_user  —  9.6 s  The person denied the call.   │
├─ Host log ────────────────────────────────────────────────────────┤
│ 2026-10-30 14:01:58  list_connections  reason —                   │
│   → ok  none  0.0 s  host={"version"…  connections=[{"connect…    │
│                                                                   │
│ Pending approvals: 0   Running: 0   [Save as…]  [Clear]           │
└───────────────────────────────────────────────────────────────────┘
```

The log lines are cut at the panel's edge here only; in the plugin they scroll horizontally. Their format, and **Save as…**, are in [§8.6](#86-call-log-format). `⚠` lines name components a call added unexpectedly. An unbound tab shows only the host section and a **Connect** prompt.

The mockup shows the panel as Phase 6 changes it:

- **Settings** no longer has the v1 **Record queries** checkbox. From Phase 6, record reads are grants ([§7](#phase-6--scopes-and-grants)).
- **File root** is new. It shows the File root that applies to the tab's environment and the level it was set at ([§6.8](#file-root)), or *Not set — asked for on first use*. It is read-only here. It is set, changed and cleared in the Active Approvals window ([§8.8](#88-active-approvals-window-phase-6)), and the line updates when that happens.

### 8.2 Approval dialog

```
┌─ Approve tool call — [ClientName] DEV ──────────────────────────────┐
│ Environment : [ClientName] DEV  ([ConnectionName])                  │
│ Tool        : add_solution_components                  Tier: WRITE  │
│                                                                     │
│ Solution    : [prefix]_WIP_1234                                     │
│ Components  :                                                       │
│   Table   account            (metadata only, no subcomponents)      │
│   Column  account.[prefix]_segment                                  │
│   Form    Account — Main     {00000000-…}                           │
│                                                                     │
│ Fixed       : required components OFF · subcomponents OFF           │
│                                                                     │
│ ┌ Agent-supplied — not verified ──────────────────────────────────┐ │
│ │ Adding the account form for story 1234 (segment field).         │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                     │
│ [ Allow once ]   [ Deny ]                         Times out in 112s │
└─────────────────────────────────────────────────────────────────────┘
```

For Read calls the dialog offers **Allow once**, **Allow for this session** and **Allow for this environment**, flags a query that touches records, and shows the recommended scope for the environment ([§7](#7-authorization-model)). *Always* is set only in settings. The dialog is owned by the XrmToolBox main window, so it appears whichever tab is active (C9).

In Phase 6, every dialog except publisher creation offers the scopes its grant kind allows ([§7](#phase-6--scopes-and-grants)):

| Grant kind | Scopes offered |
|---|---|
| Read and record reads | **Once**, **Session only**, **Per environment**, **Per tenant**, **Global** |
| Write calls | **Once**, **Session only**, **Per environment** |

Per tenant and Global carry a warning. Per tenant is disabled, with the reason shown, when the connection reports no tenant ID. A query that touches records is labelled as reading records, and its scopes grant `RecordRead`, not `ReadApproval`.

### 8.3 Publisher creation dialog

```
┌─ Create publisher — read carefully ─────────────────────────────────┐
│ Environment : [ClientName] DEV                                      │
│                                                                     │
│ A second publisher, even with the same prefix, can cause layering   │
│ problems that cannot be fixed later. Reuse an existing publisher    │
│ unless you are certain.                                             │
│                                                                     │
│ Existing publishers:                                                │
│   [PublisherName]   prefix [prefix]   option prefix 12345           │
│   Default Publisher prefix new        option prefix 10000           │
│                                                                     │
│ Requested:                                                          │
│   Unique name  [PublisherName]2                                     │
│   Prefix       [prefix]      ⚠ same prefix as an existing publisher │
│   Option pfx   12345         ⚠ same as an existing publisher        │
│                                                                     │
│ Type the prefix to confirm: [__________]                            │
│                                                                     │
│ [ Create publisher ]   [ Deny ]                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 8.4 Log entry detail

```
┌─ Call detail ───────────────────────────────────────────────────────┐
│ 14:03:31  add_solution_components   WRITE   Allowed once            │
│ Arguments  (verbatim)                                [Copy]         │
│   { "connection": "…", "solutionUniqueName": "[prefix]_WIP_1234" }  │
│ Result                                                              │
│   added 3 · unexpected 1                                            │
│   ⚠ unexpected: Relationship [prefix]_account_contact               │
│ Response size 2 KB · 1 page                                         │
└─────────────────────────────────────────────────────────────────────┘
```

Double-clicking a call line opens its detail. The detail is held in memory only and is never saved, unless the person turns on **Write call log to disk** ([6a.2](#6a2--automatic-call-log-on-disk)), which writes it in full. A saved log ([§8.6](#86-call-log-format)) holds the lines as shown in the panel, with parameters and return values truncated. It never holds full arguments or result payloads, and it is written only to a file the person chooses, outside any repository (R7).

### 8.5 Connection already active dialog

Shown when a tab receives a connection that another tab already holds (C3). The tab stays unbound until the person chooses.

```
┌─ Connection already active ─────────────────────────────────────────┐
│                                                                     │
│ [ConnectionName] ([ClientName] DEV) is already active in another    │
│ Agent Companion tab.                                                │
│                                                                     │
│ Each environment has one tab, so every call to it is approved and   │
│ logged in one place. Use the existing tab, or choose another        │
│ connection for this one.                                            │
│                                                                     │
│ [ Close this tab ]   [ Choose another connection ]                  │
└─────────────────────────────────────────────────────────────────────┘
```

When the match is on organization rather than connection ID, the text names both: *[ConnectionName] connects to the same organization as [OtherConnectionName], which is active in another Agent Companion tab.* **Choose another connection** re-opens the XrmToolBox connection dialog for this tab.

### 8.6 Call log format

Every tool call is written to the log as two lines: a call line, then an outcome line (R13). This covers `list_connections`, calls naming an unknown connection, calls rejected for invalid arguments, denied and timed-out calls, and each continuation page. Calls to the tab's connection go to its call log. Host calls (C10) go to the host log on every tab.

```
2026-10-30 14:03:05  create_solution  reason "Base solution for story 1234."  connection=00000000-0…  uniqueName=[prefix]_W…  friendlyName=WIP 1234  publisherUniqueName=[Publisher…
  → ok  once  0.8 s  solutionId=00000000-0…  uniqueName=[prefix]_W…  friendlyName=WIP 1234  version=1.0.0.0  publisher={"publishe…
2026-10-30 14:03:20  remove_solution_component  reason —  connection=00000000-0…  solutionUniqueName=[prefix]_W…  component={"type":1,…
  → error invalid_argument  —  0.0 s  reason is required for Write and Publisher tools.
```

**Call line**

| Part | Content |
|---|---|
| Timestamp | When the host received the call, in local time, `yyyy-MM-dd HH:mm:ss` |
| Tool | The tool name as called |
| Reason | The agent's `reason` in full, in quotes, or `reason —` when there is none. It is agent text and is not verified (R8) |
| Parameters | Every argument the agent sent except `reason`, as `name=value`, in input-schema order with `connection` first. Each value is truncated |

**Outcome line**, indented under its call line

| Part | Content |
|---|---|
| Status | `ok`, or `error` followed by the `ToolError` code |
| Approval | How the call was authorized: `once`, `session`, `environment` or `always` — from Phase 6 `once`, `session`, `environment`, `tenant` or `global`; `none` for `list_connections`; `page` for a continuation page, which runs under its first call's approval; `—` when the call never ran because it was rejected, denied or timed out |
| Duration | From receipt to reply, including time spent awaiting approval |
| Return values | On `ok`, each top-level field of `structuredContent` as `name=value`, in output-schema order. Each value is truncated |
| Error message | On `error`, the `ToolError` message in full |

After an `add_solution_components` outcome line, one `⚠ unexpected <type> <name>` line follows for each unexpected component. The name is shown in full so that a person can find the component (R1).

**Values**

- Strings are shown as they are, without quotes. Numbers, booleans and `null` are shown as JSON literals, and objects and arrays as compact JSON.
- **Truncation.** A parameter or return value longer than 10 characters is cut to its first 10 characters, followed by `…` (U+2026). A value of 10 characters or fewer is shown whole, so an ellipsis always means something was cut. The reason and error messages are not truncated.
- **Line safety.** Before a line is written, every control character (including CR, LF and tab) and every Unicode bidirectional-formatting character is replaced by a space. This applies to the reason, values, names and messages, and happens before truncation. Agent-supplied text therefore cannot start a fake log line or reorder what the person sees (R8).

**Behaviour**

- Both lines of a pair are written together when the call ends, so another call's lines never come between them. Until then, the call shows in the approval dialog and in the **Pending approvals** and **Running** counts.
- Lines do not wrap. The log uses a monospaced font and scrolls both ways. It is kept in memory for the life of the tab, and **Clear** empties it.
- **Save as…** opens a standard Windows save dialog. The default name is `AgentCompanion-[ConnectionName]-yyyyMMdd-HHmmss.txt`. The plugin writes a UTF-8 text file holding a header line (connection name, organization friendly name and save time), then this tab's call log, then the host log under a `Host log` heading. Lines are written exactly as shown, truncation included. If the chosen folder is inside a Git working tree, meaning it or a parent folder contains `.git`, the plugin warns and asks the person to confirm (R7). The log is written to disk only through **Save as…**, unless the person turns on **Write call log to disk** ([6a.2](#6a2--automatic-call-log-on-disk)).

### 8.7 First-run setup wizard

The first time the plugin opens in an XrmToolBox installation, the tab shows a setup wizard before the main panel. It has three pages. Each page has **Next** and **Cancel**; the last page has **Finish** instead of **Next**. **Cancel** closes the plugin tab, and the wizard shows again the next time the plugin opens. **Finish** records in the plugin settings the plugin version setup was completed for — nothing else — and shows the main panel. The host starts as usual while the wizard is open (H2), so the test on the last page can reach it.

| Page | Shows | Buttons |
|---|---|---|
| **1 · Introduction** | One or two paragraphs on what the plugin is for: it gives a local coding agent a small set of person-approved Dataverse operations PAC CLI cannot perform, so the agent can discover components and shape small work-in-progress solutions; every call is approved in this tab and logged; solution export, edit and import stay with PAC CLI and the agent's skills | Next · Cancel |
| **2 · Preconditions** | The five things a working setup needs, each with its own buttons — table below | Next · Cancel |
| **3 · Test** | What the test checks, the host status and whether this tab is bound to a connection, and the button below | Finish · Cancel |

**Page 2 — preconditions**

| # | Precondition | Button |
|---|---|---|
| a | **A local agent** — GitHub Copilot, Cursor or Claude Code, at the minimum versions in the README | — |
| b | **PAC CLI installed** | **Copy setup prompt** — a prompt asking the agent to check for PAC CLI, install or update it, and confirm with `pac` that it runs |
| c | **A working `pac auth` profile for each environment to be used** | **Copy setup prompt** — a prompt asking the agent to list the profiles, create one with `pac auth create` for each environment the person names (this tab's organization URL is included when the tab is bound), and confirm with `pac org who` |
| d | **The agent-companion skills installed for the agent** | **Export skills…** — opens a folder picker and writes each `agent-companion-*` skill as its own zip file there. **Copy setup prompt** — a prompt asking the agent to unpack those zips into its client's skills location and confirm that the skills load |
| e | **The plugin's MCP endpoint registered with the agent** | **Copy setup prompt** — a prompt asking the agent to register the endpoint, holding the client configuration that **Copy client config** produces (URL and bearer token), and to restart or reload the client so the tools appear |

**Page 3 — test.** **Copy test prompt** copies a prompt that directs the agent to: confirm the agent-companion skills are in place; run `pac org who`; call `list_connections` and `get_connection_info` and compare `organization.url` with PAC's organization URL ([§4](#keeping-pac-cli-on-the-same-environment)); then call `list_solutions` as a test. The person approves that call in this tab and sees it in the call log.

| Rule | Detail |
|---|---|
| Prompts are text resources | Each prompt is an embedded text resource, so its wording can be revised without code. Prompts are plain text, addressed to the agent, and name what to report back |
| Only the MCP prompts hold a secret | The page-2 (e) prompt and the update page's MCP prompt hold the bearer token, as **Copy client config** does. The page says so: paste it only into the local agent |
| Skills ship inside the plugin | The companion skills are embedded as resources at build time from `skills/agent-companion-*`, so the exported zips always match the plugin version. No extra DLL (§10) |
| Nothing else reaches disk | The wizard writes only the skill zips, to the folder the person picks; if that folder is inside a Git working tree, it warns as **Save as…** does |

**After an update.** When the plugin opens and its version differs from the one setup was last completed for, the tab shows a single **Plugin updated** page before the main panel. It names the previous and the new version, and says that the agent-companion skills and the MCP client configuration may have changed. The reinstall is optional:

| Item | Buttons |
|---|---|
| Skills | **Export skills…** — writes the new version's `agent-companion-*` zips, as on page 2. **Copy update prompt** — asks the agent to replace its installed agent-companion skills with the new zips, leave every other skill alone, and confirm the new ones load |
| MCP configuration | **Copy update prompt** — asks the agent to check its registration of the endpoint against the client configuration the prompt holds (URL and bearer token), update it if it differs, and reload the client |
| Check | **Copy test prompt** — the page-3 test prompt |

**Done** and **Skip** both record the new version and show the main panel, so the page appears once per update; **Skip** reinstalls nothing. A first run — no version recorded — always gets the full wizard instead.

### 8.8 Active Approvals window (Phase 6)

> [!NOTE]
> **Built (2026-11-05, version 1.2026.11.2).** The behaviour is set out in [§7, Phase 6 — scopes and grants](#phase-6--scopes-and-grants), in [§6.8, File root](#file-root) and in the [Phase 6](#phase-6--planned-enhancements) table. This section is the window's layout; the tree is built by one function apart from the window and is unit-tested.

The window opens from an **Active Approvals** button on the main panel of any tab. There is one window for all tabs. It lists every environment and tenant the plugin holds a grant or a File root for, whether or not a tab is bound there now.

```
┌─ Active Approvals — every environment and tenant ─────────────────────────┐
│ Token      : ●●●●●●●●●●●●●●●●  [Copy] [Regenerate]                        │
│              One token for every client — regenerating affects all        │
│ Call log   : [x] Write call log to disk   Folder  D:\AgentLogs            │
├─ Global ──────────────────────────────────────────────────────── [Reset] ─┤
│ None                                                                      │
├─ Tenant 00000000… ────────────────────────────────────────────── [Reset] ─┤
│ File root  C:\Repos\[ClientName]-d365                    [Change] [Clear] │
│ Read     Per tenant      2026-10-30 14:02  [ConnectionName]  [Revoke]     │
│ ▸ [ClientName] DEV                                    [File root] [Reset] │
│   Write    Per environment 2026-11-04 09:15  [ConnectionName]  [Revoke]   │
│   Dev env  Per environment 2026-10-30 13:58  [ConnectionName]  [Revoke]   │
│   Records  Session only    14:06             [ConnectionName]  [Revoke]   │
│ ▸ [ClientName] SBX                                    [File root] [Reset] │
│   File root  C:\Repos\[ClientName]-poc                   [Change] [Clear] │
│ ▸ [ClientName] UAT                                    [File root] [Reset] │
│   Records  Per environment 2026-10-12 11:20  [ConnectionName]  [Revoke]   │
│     ⚠ Stale — environment ID differs from the one granted; not applied    │
├─ Tenant 11111111… ────────────────────────────────────────────── [Reset] ─┤
│ File root  Not set                                                  [Set] │
│ ▸ [ClientName2] DEV                                   [File root] [Reset] │
│   Read     Session only    15:40             [ConnectionName]  [Revoke]   │
├─ No tenant ───────────────────────────────────────────────────── [Reset] ─┤
│ ▸ [ClientName] TRN                                                [Reset] │
│   File root  Not set                                                [Set] │
│   Read     Per environment 2026-09-02 10:11  [ConnectionName]  [Revoke]   │
├───────────────────────────────────────────────────────────────────────────┤
│ [Reset all approvals]                                           [Close]   │
└───────────────────────────────────────────────────────────────────────────┘
```

- **Token.** It is shown masked, with **Copy** and **Regenerate** (H4, D8), and has moved here from the main panel.
- **Call log.** The **Write call log to disk** checkbox and its folder ([6a.2](#6a2--automatic-call-log-on-disk)). When it is off, the line shows *Off*, with the last folder if there was one. A warning about the log — a saved folder that could not be used at startup, a failed write, a file left untouched — shows under it. It is a setting, not a grant: no **Reset** touches it.
- **One tree, in a fixed order.** Grants are grouped by where they apply, not by scope:
  1. **Global** grants come first.
  2. Each **tenant** follows: its File root, its *Per tenant* grants, then each of its environments. Under an environment come its own File root, if it has one, its *Per environment* grants, then its *Session only* grants. All the entries of one tenant come before the next tenant.
  3. **No tenant** comes last. It holds the environments whose connection gave no tenant ID, ordered as if their tenant ID were empty.

  Environments are in alphabetical order of their friendly name, and tenants in the order of their first environment's name. Within a group, grants are ordered by kind: *Read*, *Records*, *Write*, *Dev env*. *Once* is not listed. An empty Global shows *None*.
- **Rows.** Each row is one grant and shows:
  - what it covers: *Read*, *Records*, *Write* or *Dev env* confirmation;
  - its scope;
  - when it was given — only the time for a session grant;
  - the tab it was given from;
  - a **Revoke** button.
- **What is listed.** A tenant is labelled by its shortened ID, and the environments listed under it tell the person which tenant it is. An environment is listed when it has a grant or a File root of its own, or when a tab is bound to it and no File root covers it, so that its *Not set* line shows. A grant is placed by the IDs it was saved with. A grant that matches on the organization ID but not on the environment or tenant ID is marked **Stale** and is not applied ([§7](#phase-6--scopes-and-grants)); it stays listed until it is revoked. A grant loaded from a v1 file knows only its organization ID, so it is listed under **No tenant** until a connection fills in its environment and tenant IDs, and then moves under its tenant.
- **Reset.** Every heading has a **Reset**:
  - on **Global**, it revokes every Global grant;
  - on a tenant, it revokes the tenant's grants and the grants of every environment under it;
  - on an environment, it revokes that environment's *Per environment* and *Session only* grants;
  - **Reset all approvals** revokes every grant.

  No reset clears a File root, because a File root is a setting, not a grant.
- **File roots.** A tenant shows its File root on the line under its heading, or *Not set* with **Set**. An environment in a tenant shows a File root line only when it has its own, which takes precedence over the tenant's ([§6.8](#file-root)). An environment under **No tenant** always shows its File root line, or *Not set*. **File root** on an environment heading opens the folder prompt with *This environment only* selected; **Set** and **Change** on a tenant line open it with *Every environment in this tenant* selected. The prompt explains the rules and lets the person change the level. **Clear** removes the File root; the environments it covered fall back to their tenant's File root, if any, or are asked again on the next call that takes files.
- **Confirmation inside the window.** **Reset**, **Reset all approvals** and **Clear** each replace the rows they affect with a question until the person answers:

  ```
  ├─ Tenant 00000000… ────────────────────────────────────────────── [Reset] ─┤
  │ Revoke the grants of tenant 00000000… and of its 3 environments?          │
  │ Calls already running finish under their approval.  [Reset] [Cancel]      │
  ├─ Tenant 11111111… ────────────────────────────────────────────── [Reset] ─┤
  ```

- **Live.** The window updates when a grant is given or revoked, or a File root is set, from any tab. A change applies from the next call; a call already running finishes under its approval and its File root.

---

## 9. Workflows

Every workflow starts by settling the connection ([§6.2](#62-conventions)); every MCP call that follows passes it.

§9.1 to §9.3 are entry conditions, not exclusive phases. One task can begin as a cold start for a missing transport solution, continue as a story, and include an in-place edit to a component already in production. Before any of them the agent establishes the delivery context ([§9.8](#98-delivery-context)), and it routes every component it touches on its own ([§9.9](#99-routes--placing-a-component-or-editing-it-in-place)).

### 9.1 Cold start — first base solution

```
 Agent                              Companion (MCP)            PAC CLI
 ─────                              ───────────────            ───────
 list_connections ────────────────▶ person confirms which
                                    connection to use
 get_connection_info ─────────────▶ confirm DEV env
                                                               org who: same
                                                                 organization?
 list_publishers ─────────────────▶ person confirms which
                                    publisher to use
   (create_publisher only if none
    exists and the person agrees)
 list_solutions ──────────────────▶ [Read] name not taken
 create_solution ─────────────────▶ [Write] approve
 list_tables / get_table_metadata ▶ [Read]
 query systemform, savedquery ────▶ [Read] form and view IDs
 add_solution_components ─────────▶ [Write] approve
 list_solution_components ────────▶ compare intended vs actual
   stop if anything unexpected
                                                               solution export
                                                               solution unpack
 edit unpacked XML locally
 run validation scripts
                                                               solution pack
                                                               solution import
                                                                 --publish-changes
 list_solution_components ────────▶ confirm final contents
```

Where the project uses segmented transport solutions ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)), cold start creates them empty, in transport order, under the project publisher — except a shared-library solution, which is imported from its own publisher — and the first components go into segmented WIP solutions rather than straight into the transport solutions.

Cold start is not only for the start of a project. It applies whenever a transport solution the work needs does not exist yet — for example, the first Assemblies change on a project that so far has only Base. Only the missing transport solutions are created, in transport order. Before creating any, `list_solutions` on development, and on the build environment where there is one, shows which already exist.

### 9.2 Per user story — WIP solutions

1. Establish the delivery context, or confirm the one already established for this task, and state the steps (R15, T3).
2. Discover the components the story touches (metadata, queries, ribbon reads).
3. Classify each with the project's segmentation map, and look up its transport home where transport is extracted from (W1, W2, T4). Route each one: **placement** when it has no transport home, **in place** when it has one (W9). Check for overlap with other open WIP solutions (W7).
4. `create_solution` once for each transport group the story touches, named `<initials>_Agent_WIP_<work item id>_<Group>` with initials from `who_am_i` ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) and under the destination's publisher (W3), after `list_solutions` confirms the names are free. A second WIP for a group only when the person chooses to end part of that group's work early (W12).
5. Add each component to its group's WIP — Path A or Path B per the skill — then `list_solution_components` on each WIP (R1). After any removal, remove an orphaned parent shell (W13).
6. `pac solution export` and `pac solution unpack` for each WIP, outside the repository; record a baseline of `get_component_state` for every row-backed component.
7. Edit locally; run validation scripts.
8. **Pre-import overwrite check** — `get_component_state` again, compared with the baseline. Any difference: stop, tell the person, re-export and merge before continuing. For table and column metadata, re-export and diff instead.
9. `pac solution pack`, `pac solution import --publish-changes` — one WIP at a time, in transport order (W5). The import publishes every pending customization in the environment, not only the WIP's (X11).
10. End each WIP by its route ([§9.9](#99-routes--placing-a-component-or-editing-it-in-place)). An in-place WIP ends once its edit has reached the transport source (W10). Placement WIPs end at close-out by the strategy for their destination ([§9.7](#97-close-out-strategies)): all of the story's placement WIPs together, in transport order (X8). Then `pac solution delete` removes each WIP from development and keeps its components.

### 9.3 A first-party app joins the solution

Sales, Customer Service, Field Service or another first-party app brings a new set of out-of-box components. None of them are in a transport solution yet, so this is a smaller cold start.

1. **Prerequisite, outside the plugin:** a person installs the app in the development environment. Every downstream environment needs the same app installed before any transport solution that references its components is imported.
2. `list_solutions` with `managed: "managed"` finds the app's managed solutions; `list_solution_components` on each, plus `query` over `appmodule`, `sitemap`, `systemform` and `savedquery`, shows what the app added.
3. A person decides which of those components the project will customize now. Only those are pulled in; the rest stay out until a story needs them.
4. Each change then follows the per-story loop ([§9.2](#92-per-user-story--wip-solutions)). Close-out places the new components in the transport solutions that own them, under the same add rules (R1).

### 9.4 Why the overwrite check matters

Forms, views and sitemaps are imported as whole documents: an unmanaged import replaces the environment's definition with the one in the solution. If someone changed the same form after the WIP export, the import silently discards their change. The baseline comparison detects that before it happens.

### 9.5 Transport solutions and segmented WIP solutions

Projects that segment components by type deliver through a fixed, ordered set of long-lived **transport solutions**. Each holds one group of component types. They are imported downstream in an order that keeps every dependency pointing at a solution imported earlier:

| Order | Transport solution | Typical contents | Publisher |
|---|---|---|---|
| 1 | Shared libraries | Reusable web resources and controls maintained outside the project | The library's own — not the project's |
| 2 | Resources | Web resources, custom controls (PCF), environment variable definitions and values | Project |
| 3 | Base | Tables, columns, keys, choices, relationships, forms, views, charts, connection roles, business rules, business process flows | Project |
| 4 | Assemblies | Plug-in assemblies, plug-in steps, custom APIs and their parameters | Project |
| 5 | Processes | Classic workflows, cloud flows, connection references | Project |
| 6 | Apps | Model-driven apps, sitemaps, dashboards, canvas apps, email templates, reports, duplicate detection rules | Project |
| 7 | Security | Security roles, field security profiles, team templates, masking rules | Project |

Projects vary — some import Security directly after Base. The fixed part is the rule: **a transport solution may depend only on solutions imported before it.** Managed upgrades are applied in the reverse order, so a component removed from a later solution is released before the solution it depends on is upgraded.

**Segmentation map.** The project keeps a map next to its solutions in source control. It maps each component type to a transport group, and for some types it maps by subtype as well. Subtype rules route business rules and business process flows (workflow categories) to Base instead of Processes, and dashboards (form types) to Apps instead of Base. Component types whose code varies between environments (codes of 10000 and above) are mapped by name. Where a promotion pipeline is used, it validates each WIP against this map before copying it ([§9.7](#97-close-out-strategies), A). Without one, the map still governs placement.

**Consequence: several WIP solutions per work item.** A WIP solution feeds exactly one transport solution (R10). A story that touches a column, a form, a web resource and a plug-in step therefore needs three WIPs — Base, Resources and Assemblies — each named for the story and its group. Only the groups the story touches get a WIP.

**WIP naming.** `<initials>_Agent_WIP_<work item id>_<Group>[_<n>]` — for example `[XY]_Agent_WIP_1234_Base`.

| Part | Rule |
|---|---|
| `<initials>` | First letter of the first name and first letter of the last name, from `who_am_i` on the connection where the WIP is created. Uppercase ASCII: accented letters lose the accent; any other character is dropped |
| `Agent_WIP` | Literal. Marks a WIP shaped by an agent through the plugin, as distinct from WIPs people create by hand |
| `<work item id>` | The work item's numeric ID |
| `<Group>` | The transport group's name in the segmentation map — `Resources`, `Base`, `Assemblies`, `Processes`, `Apps`, `Security`, or the map's name for the shared-library solution |
| `_<n>` | Optional. A sequence number starting at 2, used only for a story's second or later WIP for the same group (W12). The first WIP has no suffix |
| `_Sideload` | *Phase 7.* Only after `Assemblies`, for the WIP that holds a developer's side-loaded assembly and its mirror steps. It feeds no transport solution and is never imported (M2, [§9.10](#plug-in-assemblies--side-loading)) |
| Friendly name | The unique name with each underscore replaced by a space — `[XY] Agent WIP 1234 Base` |

**A project's own convention wins.** Where the project already names its WIPs another way — recorded in its delivery facts (T5), or visible in the WIPs earlier stories left in development — or the person names the WIP, that name is used. It still passes `create_solution`'s rules and W4. Searches for open and leftover WIPs (K6, X9) then look for the project's pattern as well as `_Agent_WIP_`.

The agent stops and asks the person for initials when `firstName` or `lastName` is empty, when `isApplicationUser` is true, or when `impersonating` is true — the WIP belongs to the person doing the work, not to the identity the connection runs as. The name must also pass `create_solution`'s rules (letters, digits and underscores, starting with a letter, at most 65 characters) and W4. Before creating, `list_solutions` confirms the name is free; a clash, such as two people with the same initials on the same work item and group, is resolved by the person.

| # | WIP rule | Why |
|---|---|---|
| W1 | **One group per WIP.** Before adding, the agent classifies every component with the project's map, including subtype rules, and adds it to the WIP for that group | A promotion validator rejects mixed WIPs; a component in the wrong group lands in the wrong transport solution, which is costly to undo (X4) |
| W2 | **A component already in a transport solution stays in that group**, even if the map has changed since. The agent looks for an existing transport home in `solutioncomponent` first, in the environment the transport solutions are extracted from (T4) | Each component has exactly one transport home |
| W3 | **Same publisher as the destination.** A WIP uses the publisher of the transport solution it feeds — the project publisher, or the shared library's publisher for that solution | Promotion validation requires matching prefixes; components created while working in the WIP take its publisher's prefix |
| W4 | **WIP names never start with a protected prefix.** A promotion pipeline can refuse WIP names that start with the project's, a partner's or the platform's prefix, so that no transport solution is promoted as if it were WIP. Leading with initials makes a clash unlikely, but the agent still checks the name against the project's protected prefixes and asks the person if one matches | A transport solution promoted as WIP would be copied into another transport solution and then deleted from the build environment |
| W5 | **Work in transport order.** Create, import and close out a story's WIPs in transport order — Resources before Base, Base before Assemblies, and so on | Each WIP excludes required components (R1), so the target must already hold what it depends on |
| W6 | **Dependencies point down the order.** No component may require one in a later group. Where one does — for example a Base form embedding a canvas app that the map places in Apps — stop and ask the person. `get_required_components` finds these. The fix is a placement decision, not a workaround | A dependency pointing up breaks the import of the earlier solution in every downstream environment |
| W7 | **Check overlap with other open WIPs.** Before adding a whole-document component (form, view, sitemap, web resource, workflow, app module, table), the agent queries `solutioncomponent` for the same object in other WIP solutions and reports any overlap | Two stories editing one document ship each other's in-flight changes (X1) |
| W8 | **Never in a WIP:** record creation and update rules and their generated cloud flows; managed tables with all subcomponents (also R1); components carrying another publisher's prefix | Known managed-deployment failures; validator rejections; layering |
| W9 | **Route per component, lifetime per WIP.** Each component is routed when it is classified: *placement* when it has no transport home yet, *in place* when it already has one (W2). A WIP holding any placement component lives until close-out. A WIP holding only in-place components ends as soon as its edit has reached the transport source ([§9.9](#99-routes--placing-a-component-or-editing-it-in-place)) | The route decides what close-out has to do. Deciding it per component lets one story mix new and existing components without forcing one onto the other's lifetime |
| W10 | **Re-check before ending an in-place WIP.** After import, run `list_solution_components` on the WIP. Every component in it must already have a transport home. A component authored locally during the edit, such as a new column or web resource, has none, so the WIP becomes a placement WIP | Deleting the WIP would leave that component in development with no transport home. It never ships, and the next downstream import that needs it fails (X5) |
| W11 | **Changing route never changes the group.** Switching a component between placement and in place, or moving it between a story's WIPs, keeps it in the group its transport home and the map give it | W1 and W2 fix placement; a route sets only a lifetime (X4) |
| W12 | **A second WIP for one group is the person's choice.** The person may want part of a group's work to end before the rest — typically in-place edits that should reach the transport source now while new components wait. The story then gets a second WIP for that group, named with a suffix. A component is in at most one of a story's WIPs — except a table, which the platform adds as a parent shell to every WIP holding one of its columns, forms or views. Each such WIP then carries the table's ribbon diff (X6), so ribbon and table-property edits go in only one of them, and the others export it unchanged (finding E9 in [notes](notes.md#live-run--exit-scenarios-2026-10-31)) | Keeps R10 and W7 intact and lets the person decide when each part ends |
| W13 | **Remove orphaned parent shells.** After removing a column, form or view from a WIP, read the WIP back. If nothing else in the WIP needs the table shell the platform added for it, remove the shell | The platform keeps the shell, and with it the table's ribbon diff (X6) — finding P1 in [notes](notes.md#phase-3--execution) |

How the plugin supports this: `list_solutions` finds open WIPs and the transport solutions with their publishers. `query` over `solutioncomponent` finds a component's existing home and any overlapping WIPs. `query` over `systemform` and `workflow` reads the subtype columns the map uses. `get_required_components` checks dependencies against the order. `create_solution` and `add_solution_components` shape one WIP per group. Where transport is extracted from a build environment, the agent reads `solutioncomponent` there through the build environment's read-only tab (A2, T4). The plugin neither reads, tracks nor enforces the segmentation map: placement is the agent's responsibility, guided by its skill.

### 9.6 Issues common to every close-out strategy

| # | Issue | Effect | Mitigation |
|---|---|---|---|
| X1 | **In-flight changes leak through whole documents.** A WIP export carries each component's current state in development, not the story's change. Forms, views, sitemaps, ribbon diffs, web resources, workflows and app modules are whole documents | Closing out story A ships story B's unfinished edits to the same form | Overlap check (W7); sequence stories that share a document; a person reviews shared documents at close-out |
| X2 | **A WIP isolates only what is not yet in a transport solution.** Where transport solutions are exported from the development environment (strategies B, C, and D sourced from development), a component already in one ships on the next export whatever its WIP state | The WIP holds back new components only; edits to existing ones go out with the next extract | Use a build environment as the source of transport (A) where isolation matters; otherwise treat every edit to an existing component as released at the next extract |
| X3 | **Deletions are not carried.** A WIP carries additions and updates only. Deleting a column, view, web resource or plug-in step in development is reflected by no WIP | Deleted components survive in the build environment or transport solution and are redeployed | A deletion is a separate, person-run task in each environment that transport is extracted from, followed by the managed upgrade that removes it downstream. The plugin does not delete ([§3](#3-scope)) |
| X4 | **Moving a component between transport solutions.** Correcting a placement means removing it from one managed solution and adding it to another | If the losing solution is upgraded first, the platform deletes the component downstream — for tables and columns, with their data | Classify right the first time (W1, W2). When a move cannot be avoided, the receiving solution must be imported before the losing one is upgraded — staged import with reverse-order apply, or a dedicated release a person plans |
| X5 | **Required components are excluded (R1).** Everything a WIP depends on must already exist where the WIP lands | The import into the build environment, or a pipeline's preflight, fails with a missing dependency | W5 ordering; before close-out, `get_required_components` shows whether each dependency sits in an earlier transport solution or an earlier WIP of the same story |
| X6 | **Table-level payload.** Any table in a WIP exports its whole unmanaged ribbon diff — metadata-only, shell, and the shell the platform adds when a story touches only a column, form or view. A metadata-only table also exports its table-level properties (spike S2) | An unmanaged import overwrites the table's ribbon diff, and for metadata-only tables its properties, with development's in-flight state — even when the story only added a column | Treat every table in a WIP, including parent shells, as a whole document for W7: check overlap on the table's ribbon as well as its forms and views |
| X7 | **Connection references and environment variables.** Each developer's flows can create their own connection reference, and environment variable current values travel with the WIP | Duplicate references downstream; development values carried forward | Reuse the project's existing connection references; deployment settings set values per environment; a promotion pipeline may consolidate references |
| X8 | **Partial close-out.** A story's WIPs close out one at a time | A build in between carries half a story — Base without the app that uses it | Close out all of a story's WIPs together, in order; make sure no automated deployment runs between them, or accept the intermediate state knowingly |
| X9 | **Leftover WIP solutions.** Unmanaged WIPs in development — and, for C, in source control — outlive their story | Stale WIPs distort overlap checks and clutter pipeline pickers | Delete each WIP at close-out (`pac solution delete` keeps components); `list_solutions` finds stragglers |
| X10 | **Inactive forms are not exported.** In spike S2, two inactive main forms in a WIP produced no form XML in the unmanaged export, while an active form did | The agent has nothing to edit locally, and an import cannot carry a change to that form | Check `formactivationstate` before adding a form; a person decides whether to activate it or work on another form |
| X11 | **An import publishes everything.** `pac solution import --publish-changes` publishes every pending customization in the environment, not only the WIP's (finding P2 in [notes](notes.md#phase-3--execution)) | Another person's unpublished change in development is published by the agent's import | In a shared development environment, tell the person before importing; where it matters, the person coordinates publishing with the team |

### 9.7 Close-out strategies

**v1 support.** The v1 skills support strategies **A** and **B**. Strategies C and D are recorded here for a later assessment with teams that use them; until then their considerations are analysis, not experience-based recommendations.

**Chosen per close-out, not per project.** A strategy follows from where the destination transport solution is extracted from (T4), so it is settled for each WIP, not once for the project. A project can extract some transport solutions from a build environment and others from development. It can add a build environment after go-live, or keep a separate development environment for production fixes. The agent derives each WIP's strategy from the delivery context ([§9.8](#98-delivery-context)). Where more than one is possible, it recommends one, explains the difference, and the person chooses (R15). In-place WIPs ([§9.9](#99-routes--placing-a-component-or-editing-it-in-place)) use the same strategies: under A an in-place edit is still promoted, because development is not the transport source.

|  | A — Promote to a build environment | B — Add to transport solutions in development | C — Add to Git-linked solutions | D — Add to solutions deployed by Power Platform pipelines |
|---|---|---|---|---|
| What happens to the WIP | Exported unmanaged, imported into the build environment, copied there into one transport solution, then deleted there | Its components are added to the transport solution in development; the WIP is deleted | Its components are added to a solution bound to Git; a person commits from the maker portal | Its components are added to a transport solution in the pipeline's source environment |
| Source of downstream artifacts | Transport solutions extracted from the build environment | Transport solutions extracted from development | The Git repository, YAML layout[^2] | The solution the pipeline exports when deployment is requested[^3] |
| Isolation from in-flight work | Yes, apart from shared whole documents (X1) | No (X2) | No (X2) | Only when the source is a build environment fed by A |
| Transport order enforced by | The person promoting; multi-solution deployment pipelines | Multi-solution deployment pipelines | The build from the repository | The person requesting deployments — one solution per deployment[^3] |
| R1 check after close-out | `list_solution_components` through a read-only build connection | `list_solution_components` on development | The same, plus review of the commit | The same, on the source environment |

**Mixing strategies.** When strategies meet in one project, the agent checks the combination before acting:

| Situation | Effect | Agent's action |
|---|---|---|
| The destination is extracted from a build environment, but the close-out adds components to the transport solution in development (B) | Nothing reaches the build environment. Development's copy of the transport solution, where there is one, drifts from the build copy | Not offered; promote instead (A). If the person asks for B anyway, the agent says the work will not ship until it is promoted |
| A build environment is added to a project that used B | Components placed in development's transport solutions before the change are missing from the build environment | A person seeds the build environment once, before the first promotion. The agent lists what development's transport solutions hold that the build environment's do not |
| A separate development environment is kept for production fixes | Its transport source and pipelines can differ from the main development environment's, and its fixes must also reach the main line, or the next release from there overwrites them | Bind it in its own tab and establish its delivery context separately. The agent asks how a fix reaches the main development environment — re-applied there, or carried by the next deployment — and puts it in the steps |
| An in-place edit where transport is extracted from a build environment | The edit sits in development until the WIP is promoted | The in-place WIP ends after promotion, not after import (W10) |

#### A — WIP promoted to a build environment

A promotion pipeline exports a WIP unmanaged from development and validates it against the segmentation map. It then imports the WIP into a build environment, copies its components into the chosen transport solution, extracts and commits that transport solution from the build environment, and deletes the WIP there. The build environment holds only promoted work, so the repository reflects finished stories.

| # | Consideration |
|---|---|
| A1 | **The copy step is a third add path.** It runs outside the plugin and its approvals. Whether it preserves root component behaviour and never adds required components or subcomponents is confirmed in spike S7. Until then, R1's check runs after every promotion, against the build environment |
| A2 | **Bind the build environment read-only.** The agent reads it through a second plugin tab that is never confirmed as development, so Write tools refuse there ([§5](#host)). Only the pipeline changes it |
| A3 | **One promotion per WIP, in transport order** (W5). Each promotion commits, and the commit may trigger a build and a test deployment before the story's next WIP is promoted (X8) |
| A4 | **Missing dependencies fail the import into build** (X5). The usual cause is a column or web resource created in development but never placed in a WIP. Check with `get_required_components` before requesting the promotion |
| A5 | **Deletions** are made by a person in the build environment (X3); the promotion pipeline never deletes |
| A6 | **The development WIP survives promotion.** Only the build copy is deleted; the development WIP is deleted at close-out (X9) |
| A7 | **Promotion validation is the backstop, not the plan.** The pipeline rejects wrong groups, wrong prefixes and managed tables with all subcomponents only after the work is done. The agent applies W1–W8 while shaping the WIP so that promotion passes the first time |
| A8 | **Aligning development from build.** A transport solution may be exported unmanaged from the build environment and imported unmanaged into development, so that development holds the same transport solutions with the same placement as build. This gives W2 and W7 a current view in development, and the project's own solutions in development stay unmanaged (R16). It is not an edit: nothing is changed locally before the import (R14). A person decides when it runs and runs it. Before it runs, the agent lists the open WIPs and the whole documents they share with the transport solution, because the import replaces those documents in development with the build copy (X1). Transport homes are still looked up in the build environment (T4) |

#### B — WIP until its components are added to transport solutions

Close-out adds the WIP's components to the transport solution in development (Path A or B, same rules) and deletes the WIP. Scheduled extraction from development carries them on.

| # | Consideration |
|---|---|
| B1 | **No pipeline validation.** Nothing checks the map at close-out; W1–W3 and W8 rest on the skill and on the person approving the add |
| B2 | **No isolation** (X2). The WIP gives scope, a local edit loop and an overwrite check — not a hold on release. Close out when the story is done; edits to components already in transport are released at the next extract regardless |
| B3 | **Verify the transport solution** with `list_solution_components` after close-out (R1); anything unexpected there goes straight into the next extract |

#### C — WIP until its components are added to Git-linked solutions

*Not supported by the v1 skills.*

Dataverse Git integration binds either a whole environment or individual solutions to a repository folder, and commits from the maker portal in the YAML source layout[^2].

| # | Consideration |
|---|---|
| C1 | **Environment binding syncs every unmanaged solution, WIPs included**[^2]. Each WIP appears in the repository with its own membership file and has to be removed by a later commit. Prefer solution binding with WIPs left unbound, or clean WIP folders up at close-out (X9) |
| C2 | **Solution binding does not allow one object in several bound solutions**[^2]. Transport solutions bound to separate folders therefore need strict single homes (W2). Whether an unbound WIP may hold objects of bound solutions is confirmed in spike S8 |
| C3 | **Two layouts.** The repository uses the YAML layout; the agent's edit loop uses the XML layout from `pac solution unpack` (R5). The agent never edits the Git-linked folder; changes reach it only through the environment and a commit by a person |
| C4 | **Commits are per object, not per story.** The source-control page lists every changed object in the solution, including another story's in-flight edit to a shared document (X1). The person committing chooses; the agent reports which objects belong to the story |
| C5 | **One writer per folder.** A pipeline that extracts solutions into the repository with Solution Packager (XML layout) must not write the same branch and folder as Git integration |
| C6 | **Managed environments.** Git integration requires the development and target environments to be managed environments[^4] |

#### D — WIP until its components are added to solutions deployed by Power Platform pipelines

*Not supported by the v1 skills.*

In-product pipelines export a solution from a source environment when a maker requests a deployment, and import that same artifact, managed, into each stage in turn[^3].

| # | Consideration |
|---|---|
| D1 | **Never deploy a WIP** (R11). A pipeline can be run from any unmanaged solution in its source environment, and from PAC CLI[^3]. A WIP deployed this way becomes a managed solution in every target, adding layers to its components and tying their lifetime to the WIP. The agent never requests a pipeline deployment; a person deploys transport solutions only. Delegated deployments with approval add a second check |
| D2 | **One solution per deployment, stage by stage**[^3]. Transport order and complete stories (X8) depend on the person requesting deployments, not on the tool |
| D3 | **Upgrade only, no staged apply**[^3]. Imports are upgrades without overwriting customizations, and that cannot be changed. The reverse-order apply that makes a component move safe is not available, so X4 needs a dedicated release that a person plans |
| D4 | **The artifact is exported when deployment is requested**[^3]. With development as the source, every in-flight edit to a component in the solution ships (X2). Using a build environment fed by strategy A as the pipeline's source restores isolation |
| D5 | **Preflight checks catch missing dependencies**[^3] — a useful late check for X5, but only after the work is done |
| D6 | **Target environments must be managed environments, and the deploying identity owns the deployed objects**[^3]. Flow ownership and connection references follow from who deploys |

### 9.8 Delivery context

Every skill that shapes, edits or closes out a WIP needs the same facts about how the project delivers. The agent establishes them once per task — from the project first and the person last — and every skill uses the same answers (R15, K2, K4). Facts recorded earlier (T5) are read first and checked like any other source.

**Planning is part of the task.** When a change that will go through WIPs is planned, the delivery context is established then, with read calls only — not deferred to a step of the plan. The close-out strategy decides the plan's tasks: under A a WIP lives until a person runs its promotion and the extract comes from the build environment; under B the agent adds placement components to the transport solution in development and the extract comes from development.

| Fact | Used for | Derived from | When it cannot be derived |
|---|---|---|---|
| Development connection and PAC profile | Every call and every PAC command | `list_connections`, `get_connection_info`, `pac org who` ([§4](#keeping-pac-cli-on-the-same-environment)) | Ask which connection; stop on a profile mismatch |
| Build environment | Strategy A; where transport homes are looked up | A second bound tab left unconfirmed (A2); pipeline definitions that name it | Ask whether one exists. If it exists but is not bound, ask the person to bind it and leave it unconfirmed |
| Publisher of each transport solution | W3; WIP creation | `list_solutions` (each transport solution's publisher), `list_publishers` | Ask; never create one without R3's confirmation |
| Segmentation map and transport order | W1, W5, W6 | The map in the project's source control, next to its solutions ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) | Ask where it is. If the project has none, recommend the order in §9.5 or a single transport solution, and the person decides. A project with one transport solution has a map with one group |
| Transport solutions present | Cold start for missing ones ([§9.1](#91-cold-start--first-base-solution)); W2 | `list_solutions` on development and on the build environment; the solutions folder in the project repository | Always derivable. Differences between the environments and the repository are reported to the person |
| Transport source of each transport solution | Close-out strategy; where W2 looks up a home; when an in-place WIP ends | Extraction pipeline definitions in the project repository; whether a build environment exists | Ask, showing what was found |
| Close-out strategy for each WIP | [§9.7](#97-close-out-strategies) | What the person has said about it (T1); the transport source of its destination; how earlier WIPs ended | Recommend one; the person chooses where more than one applies |
| How earlier WIPs ended, and how they were named | Close-out strategy; WIP naming ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)); leftover search (X9) | Unmanaged non-transport solutions in development — names such as `… - PROMOTED`, descriptions, publishers | Report what was found as evidence, not as the answer |
| Pipelines — promotion, extraction and commit, build, deployment | The close-out steps and what follows them | Pipeline definitions in the project repository: their triggers (manual, on commit, scheduled) and approvals | Ask what triggers each one and who approves it. A person runs them (T6) |
| Protected prefixes | W4 | The promotion pipeline's configuration | Ask. Until known, check against the project, partner and platform prefixes the agent can see |
| Work item ID | WIP naming | The task | Ask |
| Automated runs between close-outs | X8 | The pipeline triggers above | Ask whether a commit or a schedule can deploy between two close-outs |
| Others working in the development environment | X1, X11 | — | Ask |

| # | Rule |
|---|---|
| T1 | **Derive first, then ask.** Read the project repository and the bound environments before asking. Ask only for what neither shows, and never fill a gap with a default without saying so. What the person has already stated — in the task, in a plan they edited, or in conversation — is a source: it settles the fact unless the environments or the repositories contradict it. A contradiction is reported; the person's statement is never rewritten into another answer |
| T2 | **Ask with a recommendation.** Each question carries the agent's recommended answer, the reason for it, and what the alternatives would change |
| T3 | **Steps before action.** Before the first Write call of a task, and again before close-out, the agent lists the concrete steps: each WIP with its group, route and connection; the import order; how and when each WIP ends; each pipeline to run, who runs it and what it triggers; the checks after each step; and when each WIP is deleted. The person confirms. Any change to the steps during the task is stated again before the agent acts on it. When the task is a plan, the steps are its task list; when the plan is carried out, the agent states the steps by reference to the plan and to anything that changed since, and the person confirms |
| T4 | **Transport homes are looked up where transport is extracted from** — `solutioncomponent` on the build environment's read-only tab under A, on development under B. A project with mixed sources looks up each group in its own source |
| T5 | **Settled facts are recorded; the environments win over the record.** Each fact settled in a task — derived, stated by the person or answered — is written to the project's delivery facts (`delivery-facts.md`) unless it is already there, and a recorded fact found changed is updated. The record builds up across tasks so the next one starts from it; the agent says what it added or changed. It goes in a client repository, never this one (R7): `<client>-Context/` where the project has one, otherwise the project's solution repository (`<client>-d365/`), next to the segmentation map. The agent writes the file; a person commits it (T6). A recorded answer is checked against the environments and the repositories each time it is used: a transport solution it names must exist, and so must a pipeline it names |
| T6 | **Pipeline runs, commits and pushes are a person's.** The agent prepares them — the pipeline to run and its parameters, the files to commit and the message — and a person executes them. The agent runs one itself only when the person explicitly delegates that specific run, commit or push. It never starts a deployment to a downstream environment, and never one that deploys a WIP (R11, D1) |

### 9.9 Routes — placing a component or editing it in place

A WIP does one of two jobs for each component in it. The job is decided per component, from whether the component already has a transport home. The two routes mix freely within a project, within a story, and within one person's day.

|  | Placement | In place |
|---|---|---|
| The component | Has no transport home — a new component, or an out-of-box one touched for the first time | Is already in a transport solution (W2) — for example, a form on a table already in production |
| What the WIP does | Carries the edit and, at close-out, places the component in its transport solution | Carries the edit only |
| When the WIP ends | At close-out, together with the story's other placement WIPs (X8) | When the edit has reached the transport source: at import and validation where transport is extracted from development (B); after promotion where it is extracted from a build environment (A) |
| Close-out | Add to the transport solution (B), or promote (A) | Nothing to add (B); promote (A) |
| Checks | R1 after every add; W6, W7 and X5 before close-out | R1 after every add; the overwrite check before import; W10 before the WIP ends |

**An in-place change, transport extracted from development.** A fix changes a form on a table already in production:

1. Classify: the table and form are in the Base transport solution, so both are in place (W2, W9).
2. `create_solution` for `[XY]_Agent_WIP_1234_Base`. Add the table — as a shell, or as metadata when its properties change, never with subcomponents (R1) — and the form. `list_solution_components` shows nothing unexpected.
3. Export, unpack, baseline, edit, validate, overwrite check, pack, import ([§9.2](#92-per-user-story--wip-solutions) steps 6–9).
4. Validate: the import succeeded, the read-back passes W10, and the person confirms the form behaves as intended ([§12](#12-human-review-checkpoints)).
5. `pac solution delete` the WIP. The edit ships with the next extract from development (X2).

Under A, step 5 waits for the WIP to be promoted, and the build environment's transport solution receives the edit.

**Switching between routes**

| Case | What happens |
|---|---|
| A story mixes both routes in one group | They share the group's WIP by default, which then lives until close-out (W9). The person can split them so the in-place part ends first (W12) — for example `[XY]_Agent_WIP_1234_Base` for a new column and `[XY]_Agent_WIP_1234_Base_2` for an edit to a form already in production |
| An in-place WIP gains a new component | A new column or web resource authored during the edit makes the WIP a placement WIP. W10 catches it, and the WIP is not deleted until it is closed out |
| A placement component gets a transport home elsewhere | Another story's close-out places the same component first. The overlap check (W7) reports it; the component becomes in place, and the person decides whose edit wins (X1) |
| A person edits in the maker portal | Direct edits are neither route. The overwrite check ([§9.4](#94-why-the-overwrite-check-matters)) catches them before an import discards them; W7 does not see them |
| The edit is to a transport solution as a whole | Not a route. Edits reach development through a WIP holding only the components being changed (R14) |

**When the WIP's lifetime matters.** Under B, an in-place edit ships at the next extract from development whatever happens to its WIP (X2); ending the WIP early only removes clutter (X9). Under A, the lifetime is the release: an in-place edit ships when its WIP is promoted, so promoting early ships early.

### 9.10 Plug-in assemblies, steps and custom APIs (Phase 7)

> [!IMPORTANT]
> **Deferred to [Phase 7](#phase-7--plug-in-authoring) (2026-11-03).** Field use found that the path below does not work as written, and the quirks need a spike before anything is built or put in a skill. Until Phase 7, a person registers plug-in assemblies, plug-in types, steps, images and custom APIs in the Plugin Registration Tool, and a plan treats that work as manual (K7). The agent may still read registrations with `query` ([§6.5](#65-read-tools)), build and unit-test plug-in code, and add an assembly, step or custom API that is already registered to a WIP — with PAC (Path B), or from Phase 6 with `add_solution_components` ([§6.6](#add_solution_components)) — reading it back (R1). Spike S12 (2026-11-09, [notes](notes.md#6a3--spike-s12-2026-11-09)) answered the export question: a WIP exports each step it holds as 92, with its images, and an assembly held as 91 exports its plug-in types but no steps. S12 also verified the authoring path below live, with the corrections now in it (the table, G1, G4, G6, G7). What follows is the design Phase 7 starts from, not a procedure to follow now.

Registering plug-in code is a change to Assemblies-group components, and it travels like any other: through a WIP, authored in the unpacked solution, imported with PAC CLI (R14). The plugin reads assemblies, plug-in types, steps, images and custom APIs through `query`; it does not register them, and PAC CLI has no step-registration command. The step tools ([§6.9](#69-plug-in-step-tools-phase-7)) mirror existing steps, switch their state and rebind custom APIs, but new registrations still go through the WIP.

**A new build of a registered assembly goes through `pac plugin push`** (decided 2026-11-03, replacing the WIP path for this one case). The assembly is still held by a WIP (91, added with PAC), so W7 and close-out see it. The push skips the import's overwrite check, so the agent runs G5 first. A build that adds a plug-in type is followed by registering the type in the Plugin Registration Tool, or it takes the WIP path below: push never registers a type (S12). The platform refuses a push whose build lacks a registered type.

| Change | Held by the WIP before the edit | Authored in the unpacked WIP |
|---|---|---|
| New build of an existing assembly | The assembly (91), added with PAC | Not authored — `pac plugin push` after G5. The WIP path below remains for a build that adds plug-in types: the assembly's `.dll` replaced under `PluginAssemblies\`, keeping its name, strong name and version unless the plan changes them, and every new plug-in type listed in the assembly's `.dll.data.xml` with a new ID (G7). It works whether or not the DLL was pushed first |
| New or changed step, with its images | For a changed step, the step (92). For a new step, nothing, unless its plug-in type is new, in which case also the assembly (91). The assembly is never needed to carry a step: an export of the assembly alone has no steps | The step's file under `SdkMessageProcessingSteps\{id}.xml`, images inside it; a new step also gets a `RootComponent` of type 92 in `Other\Solution.xml`, and the `<SdkMessageProcessingSteps />` anchor (G6) |
| New assembly | Nothing; an empty WIP works | The assembly's `PluginAssemblies\<FileName>-<ID>\` folder with the `.dll` and `.dll.data.xml` (every plug-in type listed, G7), a `RootComponent` of type 91 with the assembly's full name as `schemaName`, and the `<SolutionPluginAssemblies />` anchor (G6) |
| Custom API, its request parameters and response properties | For a changed one, the custom API and the parameters and properties that change. Added by itself, an unmanaged custom API bound to a plug-in type also brings its plug-in assembly, parameters and properties, which R1 reports | `customapis\<uniquename>\customapi.xml`, with `customapirequestparameters\<uniquename>\customapirequestparameter.xml` and `customapiresponseproperties\<uniquename>\customapiresponseproperty.xml`, the plug-in type referenced by `<plugintypeexportkey>`. **No `RootComponent` and no anchor**; the import adds the custom API, its parameters and its properties to the WIP. The type may be one the same import registers |

| # | Rule |
|---|---|
| G1 | **Shapes are copied, never recalled.** Every new file, `RootComponent` entry and `Other\Customizations.xml` anchor node is modelled on an export that holds the same type — the project's extract or another WIP's export. Without one, the agent says so, and the person decides between providing an example and registering by hand. A managed component exports as an empty stub (`<customapi uniquename="…"></customapi>`) and is not a shape. For custom API parameters and properties, [Microsoft's documented solution-file format](https://learn.microsoft.com/power-apps/developer/data-platform/create-custom-api-solution) matched the export field for field (S12) and may be used where no export holds one |
| G2 | **The new build keeps every registered plug-in type.** Before import, the plug-in types in the exported `.dll.data.xml` — and every step registered against them in development, found with `query` — are compared with the types in the new build. A type missing from the build stops the import: it is in use, possibly by another story's step |
| G3 | **An assembly is a whole document** (X1). The overlap check (W7) covers it: another open WIP holding the same assembly carries a different build of it |
| G4 | **Read back after import.** `list_solution_components` on the WIP (R1), and `query` over `plugintype`, `sdkmessageprocessingstep`, `sdkmessageprocessingstepimage`, `customapi`, `customapirequestparameter` and `customapiresponseproperty` to confirm each type, message, table, stage, mode, rank, filtering attributes, images, binding, parameters and properties as planned. *Solution Imported successfully* is not evidence: an import that skipped every component reports it too (S12). The step state reflects the import's `--activate-plugins` choice, for every step the WIP carries |
| G5 | **Check before `pac plugin push`.** The PAC profile matches the connection ([§4](#keeping-pac-cli-on-the-same-environment)). `query` reads the target `pluginassembly`: its name matches the built file's assembly name; its major and minor version match the build's (a change in either is a new registration — [version change](#plug-in-assemblies--major-or-minor-version-change)); its `modifiedon` and `modifiedby` match the baseline taken when the work started, else someone else pushed and the person decides. G2 holds. For the project's shared assembly, the build is the Release configuration from merged source, not a developer branch. Afterwards `query` confirms the new `modifiedon` and version |
| G6 | **Anchors before pack.** `Other\Customizations.xml` has `<SolutionPluginAssemblies />` when the WIP has a `PluginAssemblies\` folder, and `<SdkMessageProcessingSteps />` when it has a `SdkMessageProcessingSteps\` folder. An export has only the anchors for what the WIP held, so an empty WIP or an assembly-only WIP needs one added. Model the position on an export that has it (G1): after `<CustomControls />`, before `<EntityDataProviders />`. Pack output that says *Following root components are not defined in customizations* is a failure: the named components are left out of `customizations.xml`, and the import would report success without creating them |
| G7 | **Every plug-in type in `.dll.data.xml`.** The import registers exactly the `PluginType` entries listed, with their `PluginTypeId`s; a type present in the DLL but not listed is not registered. A step or custom API whose type is neither registered nor listed fails the whole import |

Verified live by spike S12 (2026-11-09) for a new assembly, new plug-in types (with and without a prior push), new steps with images, and a new custom API with a request parameter and a response property ([notes](notes.md#6a3--spike-s12-2026-11-09)). The field failures ([Phase 7](#phase-7--plug-in-authoring)) came from the missing anchors (G6) and an unlisted type (G7). Changed steps, changed custom APIs and import into an environment other than development were not exercised.

**Faster iteration in development.** `pac plugin push` already updates a registered assembly; the step work around it is what takes time and goes wrong. The step tools ([§6.9](#69-plug-in-step-tools-phase-7)) cover it for side-loaded builds and for version changes. Where they are used, the WIP still holds the components, for overlap checks, W10 and close-out.

#### Plug-in assemblies — side-loading

Some projects give each developer a **side-loaded build**. It is the same source compiled under a different assembly name, typically `<Assembly>.<initials>` from a per-developer Debug configuration, while Release keeps the shared name. Dataverse identifies an assembly by its name, so the side-loaded build registers alongside the shared assembly. It also cannot be pushed over it by mistake.

1. **Register.** The person registers the side-loaded assembly in the Plugin Registration Tool, which registers its plug-in types. The agent creates the story's side-load WIP — `<initials>_Agent_WIP_<work item id>_Assemblies_Sideload` ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) — and adds the assembly with PAC.
2. **Mirror.** `mirror_steps` copies the steps under test from the shared assembly into the side-load WIP. The mirrors are disabled.
3. **Switch.** `disable_steps` on the originals, then `enable_steps` on the mirrors. Disabling first means the two copies never run together; between the calls, neither runs. For a custom API, `rebind_custom_apis` to the side-loaded assembly. The switch goes in the ledger (M7).
4. **Iterate.** Build the side-loaded configuration and `pac plugin push` it to the side-loaded assembly (G5, without the shared-assembly checks).
5. **Restore.** `disable_steps` on the mirrors, `enable_steps` on the originals, and `rebind_custom_apis` back to the shared assembly.
6. **Update the shared assembly.** After the source is merged: Release build, G5, `pac plugin push` to the shared assembly, which its own Assemblies WIP holds.
7. **Remove.** Delete the mirror steps and then the side-loaded assembly — an assembly cannot be deleted while steps depend on it. Then `pac solution delete` the side-load WIP. Deletion is the person's, in the Plugin Registration Tool (Q14).

**While a switch is open, it affects everyone.** Every user of the shared development environment runs the side-loaded code for the switched steps, not only the developer.

#### Plug-in assemblies — major or minor version change

A change to an assembly's major or minor version makes it a new registration next to the old one. The same tools move the steps:

1. The person registers the new version in the Plugin Registration Tool. The agent adds it to the story's Assemblies WIP, as a placement component.
2. `mirror_steps` from the old assembly to the new one, into the Assemblies WIP. Here the mirrors are the steps that will ship.
3. Switch as above; `rebind_custom_apis` to the new assembly.
4. Remove the old steps and the old assembly. This is a deletion (X3): a person-run task in each environment transport is extracted from, followed by the managed upgrade that removes them downstream.

#### Step state is not transported

An import sets the state of every step the solution carries. With plug-in activation (`ImportSolutionRequest.PublishWorkflows`, PAC's `--activate-plugins`) they all end enabled; without it, they all end **disabled**. Their state at export, and in the target before the import, play no part: the exported step XML has no state element. Steps the solution does not carry are left alone. All of this was confirmed in spike S9, reproducing what field use had already run into. The edit-loop helper passes `--activate-plugins`, and deployment pipelines commonly set it. A step's state is therefore never carried by a solution — in development or downstream.

| # | Rule | Why |
|---|---|---|
| M1 | **Mirrors start disabled.** The tool disables each mirror straight after creating it (a step cannot be created disabled — S9). Which copy runs changes only through an explicit `disable_steps` / `enable_steps` call | Switching is a decision, not a side effect of mirroring |
| M2 | **A side-load WIP is never exported, imported, closed out or promoted.** It feeds no transport solution, and it ends by deletion after the restore. It is the one WIP outside R10 | Imported with activation it would enable its mirrors next to the originals — S9 saw both copies run. Without activation it would disable them all |
| M3 | **Re-check open switches after every import into development** that carries a switched step — a WIP, or the alignment import under A (A8). The agent re-reads every step in the ledger's open switches and restores the intended state with the tools. A WIP carrying steps is imported with activation unless the person decides otherwise | An import with activation re-enables the disabled originals, and both copies run; one without disables every step it carries |
| M4 | **No open switch at an extract or a close-out.** Before transport is extracted from development (B) or a WIP is promoted (A), every switch is restored and every custom API is bound to the shared assembly | A custom API's plug-in type is exported: bound to a side-loaded type, it makes the transport solution depend on an assembly that never ships. Step state is not exported, but an open switch at close-out is one nobody is watching any more |
| M5 | **Retire a step by deleting it, not by disabling it** | No import carries a disabled state: the next one enables the step or, without activation, disables every step it carries (X3 for the deletion) |
| M6 | **One switch per step at a time.** When a step's original is already disabled, or a mirror of it is enabled, the agent stops and asks the person | Two side-loads of one step would both run |
| M7 | **Open switches are in the WIP ledger** with source and mirror step IDs and custom API bindings. The leftover check (X9) also looks for mirrors by their description marker and for disabled originals | A forgotten switch leaves a shared step disabled for everyone |

### 9.11 Direct updates in development

> [!NOTE]
> **Phase 6.** Decided 2026-11-03 from field use; the analysis and decisions are in [notes](notes.md#phase-6--preparation). `update_web_resources` is built (1.2026.11.2); the PCF guidance is in the `agent-companion-wip-edit-loop` skill. Plug-ins were part of this analysis and moved to Phase 7 the same day ([§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7)).

Export, unpack, pack and import are the slowest part of the edit loop. For four kinds of component, updating development in place was weighed against that loop. Where it is adopted, the WIP still holds the components, for overlap checks, W10 and close-out. It is no longer how the edit gets into development.

| Component | Decision | Path |
|---|---|---|
| Web resources | **Direct update** — one row per resource, content plus a targeted publish, and no PAC command does it | `update_web_resources` ([§6.8](#68-direct-update-tools-phase-6)) |
| Plug-in assemblies, steps, custom APIs | **Deferred to Phase 7** with the rest of plug-in authoring. The proposal: push the build, and mirror and switch steps with tools | [§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7) |
| Code components (PCF) | **Guidance only** — PAC already updates a control in place and can put it in the WIP | `pac pcf push` (below) |
| Power Pages | **Reopened** (2026-11-10). Not pursued on 2026-11-03 (D13); now [Phase 7a](#phase-7a--power-pages-authoring), after the [Phase 6b](#phase-6b--power-pages-spikes) spikes | `update_site_components`, proposed (below) |

#### Web resources

1. Shape as usual: the web resource is in the story's Resources WIP, placed or in place (W9). A new one is created by the tool in that WIP.
2. Baseline its `versionNumber` with `get_component_state`.
3. Edit the file in source.
4. `update_web_resources` with `expectedVersion`. It publishes the resources it wrote.
5. Test, and repeat steps 3–4 with `versionAfter` from the result as the new baseline.
6. End the WIP as its route says: W10 then deletion for an in-place WIP under B; close-out for a placement WIP; promotion under A.

Adding a script to a form's libraries or events is a form edit and stays on the import path.

#### Code components (PCF)

`pac pcf push` updates a control without a solution build. With `--incremental` it sends only the files that changed, as entity updates, and `--solution-unique-name` puts the control in a named solution[^6]. Spike S11 (PAC 2.14.1, trial environment):

| Push | What it did | Time |
|---|---|---|
| First, `--solution-unique-name <WIP>` | Built and imported a temporary wrapper solution, added the control to the WIP, published all customizations. No `PowerAppsTools_<prefix>` solution was left behind | 48 s |
| Later, `--solution-unique-name <WIP> --incremental` | Updated only the control's changed resource web resource (`cc_<namespace>.<control>/bundle.js`), then published all customizations | 24 s |

`--solution-unique-name` and `--publisher-prefix` cannot be combined; with the solution named, the prefix comes from its publisher. The solution must already exist — push does not create it.

1. **Iterate without deploying where possible.** The test harness (`npm start watch`), and inside the app, a Fiddler AutoResponder rule that serves the local build output[^6].
2. **Build.** `npm run build`. A push that other people will test uses a production build (`<PcfBuildMode>production</PcfBuildMode>` in the `.pcfproj`).
3. **Version.** Push bypasses the version requirement, but a later solution import of the control needs the patch version raised. Raise it before the WIP is closed out with `pac pcf version --strategy manifest`, keeping the manifest and solution versions in step as the project's PCF convention says.
4. **Push.** After the PAC profile check ([§4](#keeping-pac-cli-on-the-same-environment)), the first push is `pac pcf push --solution-unique-name <Resources WIP>` — the WIP exists already and gives the publisher — followed by `list_solution_components` on the WIP (R1). Later pushes add `--incremental`. Without `--solution-unique-name`, the push creates a `PowerAppsTools_<prefix>` solution that holds the control: a stray solution that the overlap check (W7) then reports.
5. **Publishing is part of the push.** Every push publishes all customizations — in a shared development environment, others' pending changes too (X11). No separate publish is needed; if the change does not show, hard-refresh the browser.

#### Power Pages — reopened for Phase 7a

> [!NOTE]
> **Reopened 2026-11-10 at the person's choice**, with a new fact: on the enhanced data model, the default for new sites, site configuration is solution-aware[^8]. The spikes are in [Phase 6b](#phase-6b--power-pages-spikes) and the proposed design in [Phase 7a](#phase-7a--power-pages-authoring). The decision of 2026-11-03 (D13) and its reasons are kept below.

The decision of 2026-11-03, not to pursue Power Pages:

- With the standard data model, site configuration is `adx_*` records, not solution components, so a WIP cannot hold it. *(2026-11-10: true for the standard data model only. On the enhanced data model, site components are `powerpagecomponent` rows that a WIP can hold.)*
- `pac pages upload` already updates in place, and only what changed: it compares local files with the environment manifest written by the last download[^7]. Edits that span several page components travel together.
- The gap is concurrency. The comparison is against the last download, not against the environment, so a design-studio edit made since then is overwritten silently. A read-only drift check would close it: map the changed files to record IDs through the manifest and compare `modifiedon`. It is a candidate if Power Pages is taken up. *(2026-11-10: the drift check is part of Phase 7a, and spike S16 tests the overwrite. The enhanced data model has now been assessed from the documentation; Phase 6b confirms the behaviour.)*
- The environment manifest (`<org-url>-manifest.yml`) is meant to be git-ignored[^7]. When it is tracked, each developer's upload is computed against someone else's download.

---

## 10. Implementation constraints

| Constraint | Detail |
|---|---|
| Platform | XrmToolBox plugin, `net48`, WinForms, `Nullable` enabled |
| Analyzers | StyleCop.Analyzers enforced via `stylecop.json` |
| Tests | NUnit + Moq |
| No private DLLs | Do not add package references that ship a DLL next to the plugin — a private DLL can be locked by XrmToolBox and break Tool Library update and uninstall. Unavoidable dependencies are source-included (`IncludeAssets="Build"` with a source-include package) |
| SDK access | Late-bound only (R6) |
| Singleton host | Process-wide static, thread-safe; created by the first instance, disposed after the last. No instance holds the only reference |
| Versioning | `AssemblyVersion` / `FileVersion` / `Version` in the `.csproj` and `<version>` in the `.nuspec`, kept identical; scheme `1.YYYY.M.N` |
| CI | Build, test and pack on push and PR to `main` under this plugin's path; trusted publishing, no stored credentials |
| No client data | No organization URLs, tenant IDs, GUIDs from client environments, or record data in code, tests, samples or commit messages. Placeholders: `[ClientName]`, `[TableName]`, `[FieldName]`, `[prefix]` |

### Testable units

| Unit | Tested by |
|---|---|
| Host lifecycle (first instance starts, last stops, port in use, stop/start from any instance) | Unit tests with fake instances and a fake listener |
| Connection registry (bind, duplicate by connection ID, duplicate by organization ID, unbind on close and on change, routing, unknown connection) | Unit tests with fake instances |
| Record-query detector (FetchXML tables against the configuration allow-list) | Unit tests over FetchXML samples, including link-entities and aliases |
| FetchXML rewriting (page, count, cookie; reject `all-attributes`) | Unit tests |
| Approval policy (read scopes and dev-confirmation scopes — once, session, environment, always; persistence; revoke; timeout; per-organization isolation). Phase 6: four kinds with their allowed scopes; tenant scope; Write and dev-environment confirmation capped at Environment; matching on organization, environment and tenant IDs, filling missing IDs, *Stale* on a mismatch; no tenant ID disables Tenant; `SchemaVersion` upgrade that keeps the token and host settings; a downgraded file read without failing; the Active Approvals tree order (Global, tenants by first environment name, environments alphabetically, environment grants before session grants, kind order, *No tenant* last, a v1 grant under *No tenant* until its IDs are filled) and reset of a tenant or an environment | Unit tests with a fake clock, fake UI and fake settings store |
| Paging (cursor binding to connection, tool and arguments; cap-limited pages; XML chunking; snapshot consistency; expiry) | Unit tests with recorded fixtures and a fake clock |
| Add-components guard (resolution before approval, fixed flags, stop on first failure, unexpected-component detection) | Unit tests with mocked `IOrganizationService` |
| Serializers (records, metadata projections, ribbon decompression) | Unit tests with recorded fixtures — synthetic, never client data |
| Tool schemas (every `inputSchema` has `connection` first and required, except `list_connections`; Write and Publisher tools require a non-blank `reason`, checked before any dialog; outputs validate against `outputSchema`) | Unit tests over `tools/list` and recorded results |
| Log formatter (line pair per call; parameter and return-value truncation at 10 characters with `…`; reason and error messages in full; control and bidirectional characters replaced; pair written together; **Save as…** content and the Git working-tree warning) | Unit tests over synthetic calls, including values of exactly 10 and 11 characters and a reason containing line breaks |
| JSON-RPC / MCP layer, error mapping, both protocol eras | Unit tests on request/response pairs — the thirty-one cases of [Invoke-ProtocolChecks.ps1](D365AgentCompanionMcp.ConsoleHarness/Invoke-ProtocolChecks.ps1), which also runs against the console harness; manual test with Claude Code (current and a handshake-era release) and VS Code |
| Handshake sessions (issue, continue, expire, `DELETE`, unknown session) | Unit tests with a fake clock |
| Keep-alive and cancellation (fast reply unchunked; slow reply chunked with leading spaces and still one JSON document; client disconnect withdraws a pending approval; `notifications/cancelled` in a session) | Tests against a real `HttpListener` on a loopback port |
| Token store (generate once, DPAPI round trip, regenerate, missing or corrupt setting) | Unit tests over a fake settings store |
| File root (Phase 6): relative and absolute paths inside; `..` escape; another folder, drive or share; a root that is a prefix of a sibling folder's name (`C:\repo` against `C:\repo2`); case; mixed separators; a symbolic link or junction on the path; a missing file; prompt when unset, with its level, cancel and timeout; environment File root over tenant File root; tenant level disabled without a tenant ID; isolation between environments and tenants; change applied from the next call | Unit tests over a fake file system, fake UI and fake settings store |
| Binding order (connection ID claimed synchronously in hook order; organization check after the identity call; `UpdateConnection` before `Load`) | Unit tests with fake instances and a controllable identity call |

---

## 11. Plan

Each phase ends with a usable, reviewable increment.

### Phase 0 — Spikes

Short, throwaway investigations against a development environment. Each produces a written finding that updates this spec. Tracks, sequencing, shared setup and findings are kept in [notes.md](notes.md); the spike code is in [Spikes/](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/).

**Status (2026-10-30): done for v1.** S1–S6 are complete and applied to this spec. **S7 is in cold storage** — no build environment with the promotion pipeline is available; until it runs, A1's R1 check after every promotion stays mandatory. S8 stays deferred with strategy C. The spike solutions and the test ribbon button have been removed from the sandbox.

| ID | Question | Exit criterion |
|---|---|---|
| S1 | Can an MCP host run inside XrmToolBox on `net48` without shipping a DLL? Which JSON library is safely available? Which record serializer can be source-included? Which protocol revision supports `outputSchema` and `structuredContent`? | A hello-world tool callable from the target agent clients, installed and uninstalled cleanly via the Tool Library |
| S2 | What exactly does `AddSolutionComponentRequest` produce for an out-of-box table with `DoNotIncludeSubcomponents = true`, and how is a shell-only table achieved? Does the export then carry the entity's ribbon diff? Which type code is the application ribbon? | Documented mapping of `tableBehavior` → root component behaviour → exported XML, for `account` and one custom table |
| S3 | What does PAC `add-solution-component` do for each v1 component type without `--AddRequiredComponents` — in particular for tables? | Table per component type: safe for Path B yes/no |
| S4 | Do the target agent clients (Claude Code, VS Code agent mode) connect to a local HTTP MCP server with a bearer header, read `structuredContent`, and how do they surface errors and long waits during approval? | Client configuration snippets and timeout behaviour documented |
| S5 | Which of the Read tools does the Microsoft Dataverse MCP server already provide, and could it replace any of them? Requires a dated, sourced check. | Overlap table; decision recorded |
| S6 | How does XrmToolBox host several tabs of one plugin? Is a static singleton shared across tabs (one assembly load per process)? Which hooks report a connection being set and changed, how does a plugin close its own tab, and how does it re-open the connection dialog? Is the connection ID stable across restarts, and can a dialog be owned by the main window? | Two tabs bound to different connections share one host; a duplicate is detected and both dialog actions work; closing tabs in any order leaves the registry correct |
| S7 | *Cold storage — no build environment.* Does the promotion pipeline's component copy preserve each component's root component behaviour, and does it ever add required components or subcomponents? | `list_solution_components` on the build environment before and after promoting a WIP holding a metadata-only table, a column, a form and a web resource matches what was intended *(Moved to [Phase 6a](#6a3--pending-spikes) on 2026-11-08, then to [Phase 8](#phase-8--promotion-and-git-integration-spikes) on 2026-11-10.)* |
| S8 | *Deferred with strategy C.* With Dataverse Git integration, does environment binding commit WIP solutions as solutions in their own right, and can an unbound WIP hold objects of solutions bound under solution binding? | Documented behaviour, and the recommended binding for projects that use WIP solutions *(Moved to [Phase 6a](#6a3--pending-spikes) on 2026-11-08, then to [Phase 8](#phase-8--promotion-and-git-integration-spikes) on 2026-11-10.)* |

### Phase 1 — Host, connections, approvals and log

**Status (2026-10-30): done.** Built and unit-tested, and all twelve live exit checks passed in XrmToolBox. Code, implementation choices and the exit-criteria status are in [notes.md](notes.md#phase-1--execution). Cancellation is decided (D9).

**Carried from Phase 0.** The spikes settled these points; Phase 1 builds on them rather than re-deciding them. Details and evidence are in [notes.md](notes.md#phase-0-outcome--handover-to-phase-1).

| Area | What Phase 1 must do | Source |
|---|---|---|
| Protocol layer | Hand-rolled on `HttpListener` and XrmToolBox's `Newtonsoft.Json` — no MCP SDK, no shipped DLL. Dual-era: 2026-07-28 primary, 2025-11-25 / 2025-06-18 through `initialize`. Start from the spike's [McpLayer](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/McpLayer) and its thirty-one protocol checks | S1, S4, D1, D3, D7 |
| Host lifecycle | Process-wide static singleton. Attach on whichever comes first, `UpdateConnection` or `Load`; stop when the last tab closes. Static state survives a host stop and restart within one XrmToolBox session | S6, H1–H3 |
| Binding | Claim the connection ID synchronously inside `UpdateConnection`, in hook order; resolve the organization ID with the asynchronous identity call afterwards. Mint a connection ID when XrmToolBox has none | S6, C3, D4 |
| Dialogs | Own every dialog by XrmToolBox's main window (`TopLevelControl`), so it appears whichever tab is active. Do not rely on the control's `Visible` — it reports `True` for a background tab | S6, C9 |
| Token | Persist across restarts with DPAPI; plain-text `401` body; README guidance for OAuth prompts | S4, D8 |
| Response cap | 40,000 characters, measured on the text content block | S4, D2, D6 |
| Cancellation | `HttpListener` gives no signal when a client abandons a request. Decided (D9): a reply not ready within 2 seconds is answered as a chunked `application/json` body with a space written every 2 seconds; a failed write cancels a queued or pending call and withdraws its dialog. No SSE ([§6.1](#61-protocol-surface)) | S1, D9 |
| Clients | Supported and tested: Claude Code 2.1.312 (2026-07-28), Claude Code 2.1.72 and VS Code 1.144.0 (handshake fallback) | S4 |

- Singleton host: start with the first tab, stop with the last, port-in-use handling, shared limits, persisted token (D8).
- Dual-era MCP layer ([§6.1](#61-protocol-surface)), including handshake sessions.
- Connection registry: synchronous claim, binding, the *Connection already active* dialog, unbinding on close and change, routing by `connection`.
- Main panel, per-connection dev-environment confirmation and settings.
- Approval queue and dialogs (Read and Write tiers); read-approval and dev-confirmation scopes with their persistence; revoke; timeout. Keep every grant in one listable approval store, and build the token as one self-contained control, so that Phase 6 fits without rework ([Phase 6](#phase-6--planned-enhancements)).
- Call log and host log in the line-pair format ([§8.6](#86-call-log-format)), log detail, and **Save as…** to a text file.
- Error model and result envelope ([§6.2](#62-conventions)).
- Tools: `list_connections`, `get_connection_info`, `who_am_i`, `list_publishers`.

**Exit:** with two tabs on different connections, `list_connections` returns both and calls route to the right tab; a third tab on an already bound connection gets the dialog and both actions work; a toolbar connect applied to several tabs binds the first in XrmToolBox's order; denial, timeout and unknown connection reach the agent as tool errors; every one of those calls appears as a line pair in the right log and survives **Save as…** unchanged; a once-scoped approval ends with its call, a session scope ends with the host, and Revoke clears environment scopes; Claude Code (2026-07-28) and VS Code (handshake fallback) both call a tool, and both still connect after an XrmToolBox restart without reconfiguration.

### Phase 2 — Discovery

- `list_solutions`, `list_tables`, `get_table_metadata`, `get_relationships`, `get_option_sets`.
- Cursor paging for every list and XML tool, within the response cap.
- `query` with record-query detection and its per-connection switch.
- `get_entity_ribbon`, `get_application_ribbon`.
- `list_solution_components`, `get_component_state`, `get_required_components`.

**Exit:** an agent can find the solutions in an environment, describe `account` (columns, relationships, forms, views, ribbon) and page through a small record set, with every call approved and logged.

### Phase 3 — Solution shaping

- `create_solution`, `add_solution_components` (resolution, fixed flags, unexpected-component detection), `remove_solution_component`.
- `create_publisher` behind its setting and dialog.

**Exit:** the cold-start workflow ([§9.1](#91-cold-start--first-base-solution)) runs end to end on a clean development environment, and `list_solution_components` shows exactly the intended components. A Write or Publisher call with no reason, or a blank one, fails with `invalid_argument` and raises no dialog (R12).

### Phase 4 — Agent skills and scripts

**Status (2026-10-31): built; live exit checks pending.** The five skills and their scripts are in [skills/](../../skills/); the scripts' self-tests pass. Implementation choices and the exit-check steps are in [notes.md](notes.md#phase-4--execution).

Agent-side work that helps an agent use the plugin safely. Skills decide between Path A and Path B per component type using the S2 and S3 findings, and carry the Phase 3 findings P1 and P2 (W13, X11).

**Companion skills, separate from the existing ones.** The Phase 4 skills live in [skills/](../../skills/) with the repository's other skills, one folder each, in the layout the instructions file sets (`SKILL.md` with `name` and `description`, plus `references/` and `scripts/`). They are new skills that help an agent use this plugin. The existing skills are not rebuilt around it (K7). Every companion skill's frontmatter says so plainly:

- `name` starts with `agent-companion-`.
- `description` opens by stating that the skill is used together with the Agent Companion MCP — the XrmToolBox plugin — and needs a plugin tab bound to the environment. It then names the conditions the skill applies to (K1).
- `SKILL.md` checks that a bound connection exists before anything else. When none does, it stops and says the plugin is needed, rather than falling back to another route.

**Composable, not phased.** Each skill owns one concern. Any of them can be active at the same time, in any task. A cold start for one missing transport solution, a story, and an in-place fix to a component already in production can happen in one session, and the person moves between them without announcing a phase. Loaded together, the skills never give the agent two different instructions for the same step.

| Skill (working name) | Owns | Applies when | Hands to |
|---|---|---|---|
| `agent-companion-context` | The delivery context and the steps ([§9.8](#98-delivery-context), T1–T6): connection and PAC match, publishers, segmentation map, transport solutions present and their sources, each WIP's close-out strategy, pipelines, protected prefixes. Also the WIP ledger's shape (K5) | A task will shape, edit or end a WIP, a change that will go through WIPs is being planned, a plan or a question needs a fact from the environment, or another skill needs a delivery fact it does not have | Any |
| `agent-companion-transport-bootstrap` | Creating the transport solutions the work needs that do not exist yet, in order and under the right publisher; pulling in a first-party app's components ([§9.1](#91-cold-start--first-base-solution), [§9.3](#93-a-first-party-app-joins-the-solution)) | A transport solution or publisher the work needs is missing, or a first-party app has been installed | `agent-companion-wip-shaping` |
| `agent-companion-wip-shaping` | Classification (W1, W2, T4); routes and WIP lifetimes (W9–W12); WIP names; create; add by Path A or B; read-back (R1); removal with shell cleanup (W13); overlap and dependency checks (W6, W7, X5) | A change touches a component — new, out-of-box, or already in transport | `agent-companion-wip-edit-loop` |
| `agent-companion-wip-edit-loop` | Export; unpack outside the repository; baseline; local edits; validation scripts; overwrite check; pack; import in transport order (W5, X11) | A WIP holds its components and they need editing | `agent-companion-wip-close-out` |
| `agent-companion-wip-close-out` | Ending each WIP by its route: W10 for in-place WIPs; close-out by A or B with the X issues; promotion requests under T6; R1 after close-out; deletion; stragglers (X9) | A WIP has been imported and validated, or the person asks to end a WIP or a story | — |

**Decision ownership.** Each decision belongs to one skill (K2):

| Decision | Owned by |
|---|---|
| Connection; PAC profile match | `agent-companion-context` |
| Publisher of each transport solution | `agent-companion-context`; creating one, if ever, in `agent-companion-transport-bootstrap` under R3 |
| Segmentation map, transport order, transport source of each transport solution | `agent-companion-context` |
| A WIP's close-out strategy | `agent-companion-context` derives it; `agent-companion-wip-close-out` carries it out |
| Which transport solutions to create | `agent-companion-transport-bootstrap` |
| A component's group, transport home and route; a WIP's lifetime; a second WIP for a group | `agent-companion-wip-shaping`, confirmed by the person under T3 |
| Path A or B for a component type | `agent-companion-wip-shaping` |
| WIP name | `agent-companion-wip-shaping` |
| Import order and import flags | `agent-companion-wip-edit-loop` |
| Overwrite-check outcome | `agent-companion-wip-edit-loop` |
| When a WIP ends; which pipeline run to request | `agent-companion-wip-close-out`, within the steps confirmed under T3 |

**Coexistence rules**

| # | Rule |
|---|---|
| K1 | **Conditions, not phases.** A skill applies because of the state of the work — a missing transport solution, a component to change, a WIP ready for edits, a WIP ready to end. It never applies because the project is said to be starting, in delivery or in support. Skill descriptions name conditions, so loading one never rules out another |
| K2 | **One owner per decision** (table above). A skill that needs a decision it does not own uses the owner's result. When there is none yet, it applies the owner skill instead of deciding locally |
| K3 | **Rules come from this spec, by ID.** A skill cites R, W, X, T, A and B rules by ID and keeps their meaning. It may add detail in its `references/`, but never restates a rule more loosely, more strictly, or with a default the spec does not give |
| K4 | **No skill-local defaults for delivery facts.** No skill assumes a strategy, a build environment, a pipeline or a transport source; they come from the delivery context (R15) |
| K5 | **One shared record of the work.** A task's WIPs are kept in one shape that every skill reads and updates — the *WIP ledger*: for each WIP its name, connection, group, route, components, baseline, import state and how it will end — next to the delivery context and the confirmed steps. The shape is defined once, in `agent-companion-context`'s references. The ledger lives in the conversation and, where the person wants it kept, in the task's implementation plan |
| K6 | **The environment wins over the ledger.** A skill entering a task already under way — a hand-off from another skill, or a new session — re-reads the WIPs with `list_solutions` and `list_solution_components` and reconciles the ledger before acting |
| K7 | **Existing skills keep their procedures and keep working without the plugin; when it is connected, they use it.** `power-automate-flow-editing` and the other existing skills gain at most a short note that the agent may use it — for example to create the WIP. `d365ce-planning` goes further, because a plan made without the environment or the delivery context is wrong in ways the plan cannot show: when the plugin is connected, research reads the development environment through it; environment changes the agent can carry out through a WIP with the plugin and PAC CLI are agentic tasks, while plug-in registration stays manual until [Phase 7](#phase-7--plug-in-authoring); and before writing the tasks for WIP work it applies `agent-companion-context` for the delivery context, the close-out strategy first (§9.8). Without the plugin it uses the tooling that is available — another MCP server with access to the environment, PAC CLI, the extract — and plans as agentic only what that tooling can carry out and verify. Otherwise, fitting together is the companion skills' job. Where an existing skill covers a step, the companion skills defer to it for that step and supply only what it leaves open. For example, `power-automate-flow-editing` owns a cloud flow's edit and its import flags for that WIP, while `agent-companion-wip-shaping` can supply the WIP (cloud flows are type 29, not among `add_solution_components`' v1 types, so Path B). A plan from `d365ce-planning` that names WIPs or routes is taken as a proposal, still subject to classification |
| K8 | **When two instructions still differ, stop.** If active skills give different instructions for the same step, the agent follows neither silently. It tells the person and follows the stricter one if the person agrees. The difference is then a defect to fix in the companion skills |

**Scripts**

| Deliverable | Purpose |
|---|---|
| Validation scripts | Schema check of `Customizations.xml` and entity files against the customization XSD; form checks (controls bound to existing columns, unique control IDs, GUID format); ribbon checks (referenced commands, rules and web resources exist) |
| Baseline and overwrite-check script | Records and compares `get_component_state` results; for table and column metadata, re-exports and diffs |
| WIP naming script | Builds WIP names from the naming rule: initials without accents, length, characters, protected prefixes |
| Edit-loop driver | Runs each PAC and validation step of the loop with paths built from the work folder and WIP name, so no step depends on hand-quoted paths |
| Tool-call helper | Calls a plugin tool over the same MCP endpoint when the agent client has not loaded the tools; the plugin still approves and logs every call |

Scripts ship inside the skill that uses them. A companion skill may call a script from a repository skill when that skill is installed, and works without it.

**Exit:** on the sandbox, without a build environment — S7 is in cold storage, so strategy A is checked as steps, not run:

1. **Placement story.** At the start the Resources transport solution is missing, so the bootstrap creates only that one. A story touching a new column, a form, a view, a web resource and a ribbon button then runs the full loop: two WIPs, Resources and Base, imported and closed out by B in order, with the platform import succeeding and no unexpected components.
2. **In-place change.** An edit to a form already in a transport solution goes through an in-place WIP that is imported, validated (W10) and deleted, with no close-out add.
3. **Both routes in one story.** A story adds a column and edits a form already in transport. The person splits the Base work (W12); the in-place WIP ends at import while the placement WIP waits for close-out.
4. **Route change.** A new column authored in an in-place WIP is caught by W10, and the WIP is not deleted until it is closed out as placement.
5. **Gaps in the delivery context.** With the segmentation map or the transport source withheld, the agent asks with a recommendation (T1, T2) and states the steps before its first Write call and again before close-out (T3). Given a described build environment and promotion pipeline, it produces the steps for A — promotion order, R1 on the build connection, promotion of the in-place WIP — and runs none of them (T6).
6. **Coexistence.** With every companion skill and `power-automate-flow-editing` loaded, review finds each decision in the ownership table owned by one skill and no step carrying two instructions (K2, K8). A cloud flow edit inside a story WIP follows one set of import flags. Each repository skill still works with the plugin absent (K7).

### Phase 5 — Release

**Status (2026-11-03): released.** The plugin is published in the XrmToolBox Tool Library. The column-3 guide changes listed below were to follow the release and are still to be made. Implementation choices and the exit-check steps are in [notes.md](notes.md#phase-5--execution).

- Tooling README with installation, client configuration (snippets in [Spikes/README.md](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/README.md#client-registration-s4)), minimum client versions, what to do when a client shows an OAuth prompt, tool table, the one-tab-per-connection behaviour, settings, and the companion skills in [skills/](../../skills/) with how to install them for an agent client.
- First-run setup wizard ([§8.7](#87-first-run-setup-wizard)): introduction, preconditions with copy-prompt and skill-export buttons, and a test prompt; the companion skills embedded as resources for export. After an update, a **Plugin updated** page offers the optional reinstall of the skills and the MCP configuration through agent prompts.
- Tool Library install and uninstall from the NuGet package — the part of S1's exit criterion the spike could not cover.
- CI workflow, NuGet package, Tool Library listing. The package manifest, [D365AgentCompanionMcp.nuspec](D365AgentCompanionMcp/D365AgentCompanionMcp.nuspec), follows the Context Exporter's — the format the Tool Library accepts: `lib\net472\Plugins` target, `XrmToolBox` dependency and tag, `iconUrl`, MIT licence expression, readme and licence at the package root, no loose DLLs. A test `nuget pack` built it (with a placeholder readme) with the exporter's two warnings, NU5128 and NU5048. The [tooling README](README.md) and the icons (`d365-agent-companion-16px.png` to `-256px.png`, `iconUrl` pointing at the 128 px one) are in place, and the package packs with them. The 16 px and 32 px icons are embedded as the plugin's `SmallImageBase64` and `BigImageBase64`, as the exporter does. The GitHub workflow below and the setup wizard are built.
- GitHub workflow `.github/workflows/build-and-pack-d365-agent-companion-mcp.yml`, following [build-and-pack-d365-context-exporter.yml](../../.github/workflows/build-and-pack-d365-context-exporter.yml) job for job:

  | Part | As the exporter's | For this plugin |
  |---|---|---|
  | Triggers | Push and pull request to `main`, filtered by path | `tooling/D365AgentCompanionMcp/**` and `skills/agent-companion-*/**` — the skills are embedded in the plugin for the setup wizard, so a skill change rebuilds the package |
  | `build` job | `windows-latest`; .NET 8.x SDK; NuGet; restore, build Release, test; `nuget pack` of the `.nuspec` into `nupkg`; the package name as a job output; upload the artifact | `D365AgentCompanionMcp.sln`, `D365AgentCompanionMcp.Tests.csproj`, `D365AgentCompanionMcp.nuspec`; artifact `D365AgentCompanionMcp-nupkg`. One step added: the skills' script self-tests (`scripts/tests/Invoke-ScriptTests.ps1` of each skill), in Windows PowerShell; the schema tests skip without the schema bundle |
  | `publish` job | Push to `main` only; downloads the artifact; NuGet API key from GitHub OIDC (`id-token: write`), masked; `nuget push` to nuget.org | Unchanged. Trusted publishing — no stored credentials. A person adds a trusted-publishing policy on nuget.org for the new package ID before the first push |

  The two-file version rule (csproj and nuspec identical) holds for every push that publishes: nuget.org refuses a version it already has.
- Repository updates outside this plugin: the instructions file's tooling-conventions section, the codebase map, the root README and the matrix's build-backlog row already describe the plugin as specified, not built — change them to describe it as shipped. Add the column-3 guide changes then, not before: registering the plugin's MCP endpoint in [C.3 §7](../../workflows/3-coding-assistants/c.3-project-setup.md#7-wire-up-mcp-and-finish-grounding), and the segmented WIP loop where [4.3](../../workflows/3-coding-assistants/4.3-implementation.md) covers solution changes. Adding a guide also updates the matrix, the root README table and the column README together.

**Exit:** installable from the Tool Library; README matches the shipped tool surface. On a fresh install the wizard shows; **Cancel** closes the tab and the wizard returns on the next open; every copy button puts its prompt on the clipboard; **Export skills…** writes one zip per companion skill; and an agent set up from the prompts alone passes the test prompt. After an upgrade to a new version, the **Plugin updated** page shows once — **Skip** and **Done** both record the version — and an agent following its prompts ends with the new skills installed and the endpoint registered.

### Phase 6 — Planned enhancements

> [!NOTE]
> **Phase 6 is built (2026-11-05), as version 1.2026.11.2; the live exit checks are pending.** Phase 5 shipped on 2026-11-03, and Phase 6 was shaped from real use before it was built. Spikes S10 and S11 ran on 2026-11-03; their results are written into [§6.8](#68-direct-update-tools-phase-6) and [§9.11](#911-direct-updates-in-development). Plug-in authoring, step mirroring and switching included, moved to [Phase 7](#phase-7--plug-in-authoring) the same day. Phase 6 builds as version **1.2026.11.2** (two-file rule); the `.nuspec` release notes are rewritten for it.

**Status (2026-11-05): built; live exit checks pending.** Every enhancement below is built and unit-tested, and the skills carry the direct-update and plug-in component guidance. Implementation choices, what the build found, and the exit-check steps are in [notes.md](notes.md#phase-6--execution).

| Enhancement | Behaviour | What v1 does to leave room |
|---|---|---|
| **Active Approvals** | A button on the main panel opens one window showing every current grant across all environments and tenants. Grants are shown as one tree, in a fixed order: **Global** first, then each tenant with its *Per tenant* grants followed by its environments, each environment with its *Per environment* grants and then its *Session only* grants, and environments without a tenant ID last. Environments are in alphabetical order, and tenants in the order of their first environment. Each entry shows what the grant covers (Read, record reads, Write or dev-environment confirmation, [§7](#phase-6--scopes-and-grants)), its scope, and when and from which tab it was given. A grant that no longer matches its environment is marked *Stale*. A person can **Revoke** a single entry, use **Reset** on a heading to clear Global, a tenant with its environments, or one environment, or use **Reset all approvals** to clear everything. Each reset asks for confirmation inside the window. A revocation applies from the next call, and a call that is already running finishes under its approval. *Once* scopes are not listed, because they end with their call or binding. Each File root shows under the tenant or environment it is set for, with its level ([§6.8](#file-root)), **Change**, which opens the folder prompt that explains the File root rules, and **Clear**. A tenant without a File root, or a bound environment that no File root covers, shows *Not set*, with **Set**. The reset buttons do not clear File roots, because a File root is a setting, not a grant. The layout and ordering rules are in [§8.8](#88-active-approvals-window-phase-6) | Keep every grant in one approval store. Each entry records its kind, scope, organization, environment and tenant IDs, friendly name, grant time and originating tab. The store can list its entries, remove one entry, and clear Global, one tenant with its environments, or one environment. Order the tree in one testable function, apart from the window. v1's **Revoke** in a tab, and the *always* setting, become calls on that store rather than separate paths |
| **Token in the Active Approvals window** | The bearer token moves off the main panel into the Active Approvals window, so the credentials and grants that decide what an agent can do sit in one place. The window shows the token masked, with **Copy** and **Regenerate**. **Copy client config** stays on the main panel for setup | Build the token display, **Copy** and **Regenerate** as one self-contained control backed by the token store (D8), so it can move without change. H4 is unchanged: one host-level token, and regenerating it affects every client |
| **Scopes and grants** | The model in [§7, Phase 6 — scopes and grants](#phase-6--scopes-and-grants). The scopes are Once, Session, Environment, a new Tenant, and *Always*, shown as Global. Record reads and Write calls become grants, and the **Record queries** switch is removed. Write grants and the dev-environment confirmation go no further than Environment, and the *always* dev-environment confirmation is withdrawn. Grants record organization, environment and tenant IDs, and a grant that no longer matches is *Stale*. The settings file gets a `SchemaVersion` and an upgrade mapping. Publisher creation stays once, with typed confirmation, and never for a session (R3). A scoped Write grant still leaves every Write call subject to the dev-environment confirmation, a non-blank reason (R12) and the fixed add flags (R1), and every call is still logged (R13) | The approval store is already one store keyed by kind, scope and organization. Its enums and the settings elements gain values and fields, and nothing is renamed, so v1 grants load as they are. A scope other than *Once* authorizes later Write calls in advance — and that remains a person's decision, made in the dialog for one environment. It stays within R2 the way read scopes do today; when this is built, R2 and the instructions file's fixed safety behaviour are reworded to name scoped Write approvals explicitly |
| **Direct web resource updates** | `update_web_resources` ([§6.8](#68-direct-update-tools-phase-6)): create or update web resources from local files in a WIP, with the overwrite check inside the tool and a targeted publish; the **File root**, set per environment or per tenant ([§6.8](#file-root)), asked for on first use and editable in Active Approvals. An edit-and-push loop calls it often, so scoped Write approvals make it practical | Specified after v1 was built (2026-11-03); v1 is unchanged |
| **Plug-in components in `add_solution_components`** | Plug-in assemblies, steps, custom APIs and their request parameters and response properties accepted, already registered, with the same fixed behaviour, one approval for the list, read-back and log entry as the other types ([§6.6](#add_solution_components)). Custom API codes resolved per environment. Registration stays with a person until [Phase 7](#phase-7--plug-in-authoring) | v1 refuses these types with `invalid_argument`; PAC adds them (Path B). `get_required_components` and `remove_solution_component` take the wider selector too |
| **Skill changes for direct updates** | `agent-companion-wip-edit-loop`: web resources through `update_web_resources`, with paths relative to the File root that `get_connection_info` returns; the PCF push guidance. `agent-companion-wip-shaping`: the MCP path for plug-in assemblies, steps, custom APIs, request parameters and response properties, each step added on its own | — |

**Spikes before the direct-update tools.** Run 2026-11-03 on a data-free trial environment; details in [notes](notes.md#spikes-s9s11--2026-11-03). S9, on plug-in steps, ran with them and now belongs to [Phase 7](#phase-7--plug-in-authoring).

| ID | Question | Result | Feeds |
|---|---|---|---|
| S10 | Web resources: version behaviour, targeted publish, and the time against a WIP round trip | `versionNumber` rises on every update, identical content included, and again on publish. A targeted publish makes the content current. Direct ≈ 3.5 s against 25–50 s for export, import and publish-all | `update_web_resources`; the gain justifies it |
| S11 | PCF: `--incremental` with `--solution-unique-name`; does push publish? | Both work together; the solution must exist and excludes `--publisher-prefix`. Every push publishes all customizations. First push 48 s, incremental 24 s | §9.11 PCF guidance |

S10 ran through a throwaway SDK console with its own sign-in, not through the plugin. It establishes platform behaviour only: the tool itself is verified through the plugin at Phase 6 exit. Later spikes use the plugin and the person's PAC profile only.

**Exit:** with grants of every scope in place for two environments in one tenant and one in another, each grant is listed under its tenant and environment in the order of [§8.8](#88-active-approvals-window-phase-6), revoking one or resetting a tenant or an environment takes effect on the next call, and the main panel no longer shows the token. A Write call approved *Per environment* runs the next Write to that environment without a dialog but with its log line pair; Write and dev-environment confirmation offer nothing broader; a publisher creation still prompts every time. A Read granted *Per tenant* covers a second environment of that tenant bound afterwards, but not the environment in the other tenant. A record query prompts on its own even with a Read grant, and a `RecordRead` grant does not cover other reads. A v1 settings file loads: its Read grants are kept and gain environment and tenant IDs on the next binding, an *always* dev-environment confirmation is dropped, and the token is unchanged. A grant whose environment ID no longer matches is shown *Stale* and not applied. For the direct-update tools: on the sandbox, a web resource edit is written, published and visible in the app without an import, and a stale `expectedVersion` is refused. With no File root set, the first call prompts for one and its level, and Cancel fails it with `denied_by_user`. A tenant File root serves a second environment of that tenant without a prompt, and an environment File root takes precedence over it. A relative path and an absolute path inside the root are both accepted. A `..` escape, an absolute path outside the root and a path through a junction are refused before the dialog. The File root is shown, changed and cleared in Active Approvals, and a change applies from the next call. For plug-in components: an assembly, two of its steps — one with an image — and a custom API with a request parameter and a response property, all registered beforehand, are added to a WIP by name, unique name or ID in one call. The read-back shows what the platform added with each, recorded in [notes](notes.md); nothing unexpected is left unreported. An export of the WIP carries the steps with their image and the custom API with its parameter and property. This also answers part of spike S12.

### Phase 6a — Refinements

> [!NOTE]
> **Phase 6a (opened 2026-11-08) collects follow-up work on 1.2026.11.2 before its release.** The version does not change: 1.2026.11.2 is unreleased, so the two-file rule is not triggered. Tasks are added here as they come up. Progress and findings are in [notes.md](notes.md#phase-6a--refinements).

| # | Task | Status |
|---|---|---|
| 6a.1 | **Tool Library icon resolution.** The embedded icons were half the size XrmToolBox expects, so they looked blurry next to other tools | **Done** (2026-11-08) |
| 6a.2 | **Automatic call log on disk.** An opt-in setting that writes every call and its outcome, in full, to a JSON file per connection and day | **Built** (2026-11-09); unit-tested, live check pending |
| 6a.3 | **Pending spikes.** S7, S8 and S12 move here from Phase 0 and Phase 7. S7 and S8 move on to [Phase 8](#phase-8--promotion-and-git-integration-spikes) (2026-11-10) | **Done** (2026-11-09) — S12 run |
| 6a.4 | **Live test of `update_web_resources`** for every web resource type, checking that every update is published. It changed §6.8: content written earlier without a publish is now published ([notes](notes.md#6a4--live-test-of-update_web_resources-2026-11-09)) | **Done** (2026-11-09) |

#### 6a.1 — Tool Library icon resolution

XrmToolBox reads a tool's icons from the `BigImageBase64` and `SmallImageBase64` metadata on the plugin class, and the Tool Library list uses the package's `iconUrl`. The plugin embedded a 32×32 Big and a 16×16 Small icon; XrmToolBox expects 80×80 and 32×32, which is what FetchXML Builder embeds, so it stretched the plugin's icons.

| Source | Before | Now |
|---|---|---|
| `BigImageBase64` | 32×32 | 80×80 (`d365-agent-companion-80px.png`) |
| `SmallImageBase64` | 16×16 | 32×32 (`d365-agent-companion-32px.png`) |
| `.nuspec` `iconUrl` | `…-128px.png` | `…-150px.png` |

**Check:** after a build, the tool tile and tool list in XrmToolBox show the sharper icons. The Tool Library shows the 150px icon once `d365-agent-companion-150px.png` is on `main`.

#### 6a.2 — Automatic call log on disk

> [!NOTE]
> **Built (2026-11-09), still version 1.2026.11.2.** Unit-tested; the check below has not run in XrmToolBox yet. Details the build settled are under *Settled when built*; progress is in [notes.md](notes.md#6a2--automatic-call-log-on-disk).

Before 6a.2, a call log reached disk only through **Save as…**, as the truncated text shown in the panel ([§8.6](#86-call-log-format), R13). 6a.2 adds an opt-in setting that writes each call to disk as it happens, with full details, so a session can be reviewed or handed over without remembering to save.

| Setting | Level | Default | Behaviour |
|---|---|---|---|
| **Write call log to disk** | Host (all connections and tabs) | Off | A checkbox in the Active Approvals window ([§8.8](#88-active-approvals-window-phase-6)). While it is on, every call to every connection, and every host call, is written to its log file when the call ends |
| **Log folder** | Host | Not set | The folder the files are written to. It is asked for each time the checkbox goes from off to on, and the window shows the folder next to the checkbox |

**Turning it on.** Ticking the checkbox from off opens a folder prompt, which explains the rules below and starts at the previous folder if there was one. The checkbox ends up ticked only when the person chooses a folder that passes the Git working tree check ([Client data](#6a2--automatic-call-log-on-disk)). **Cancel**, or a refused folder, leaves it off, with the reason shown. **Unticking** turns the log off at once; a call that is already running is still written when it ends. To use another folder, untick and tick again.

**Saved settings.** Both values are saved with the rest of the plugin settings, so the log is on again, writing to the same folder, the next time XrmToolBox opens and for every connection bound afterwards, until the person unticks it. If the saved folder is missing or no longer passes the Git check at startup, the log stays off for that session and a warning shows in the Active Approvals window and in the log panel; the saved values are kept. **The agent is never told.** `get_connection_info` and every other tool response leave out whether the log is on, its folder and the names of its files, and no tool can read or change the setting. It is the person's record of what the agent did, and only the person sets it, in the Active Approvals window. A warning about the log (a missing folder, a failed write) shows only in the plugin window and the log panel, never in a tool response, and a call's timing and result are the same with the log on or off. Both values have defaults, so the settings file needs no upgrade mapping and `SchemaVersion` stays 1.

Because the setting is global, it is not changed from a connection's tab, and Reset all approvals does not touch it: like a File root, it is a setting, not a grant.

**File.** One file per connection and day, named `AgentCompanion-[ConnectionName]-yyyyMMdd.json`, in the log folder. The date is the local date the host received the call, so a call received after midnight starts a new file. Characters that are invalid in a file name are replaced with `_`. The first call of a day creates the file; later calls, including after XrmToolBox restarts, append to it. Nothing is deleted or rotated by the plugin.

**Valid JSON at all times.** The file is a single JSON document, never a stream of separate lines, and it is complete and parseable after every write:

```json
{
  "schemaVersion": 1,
  "connection": {
    "connectionId": "…", "connectionName": "…",
    "organization": { "id": "…", "uniqueName": "…", "friendlyName": "…", "environmentId": "…", "tenantId": "…" }
  },
  "date": "2026-11-08",
  "entries": [
    {
      "receivedAt": "2026-11-08T14:03:05.123+01:00",
      "tool": "create_solution",
      "tier": "write",
      "reason": "Base solution for story 1234.",
      "callLine": "2026-11-08 14:03:05  create_solution  reason \"…\"  connection=00000000-0…",
      "arguments": { "…": "verbatim, as the agent sent them, without reason" },
      "outcome": {
        "status": "ok",
        "errorCode": null,
        "errorMessage": null,
        "errorDetails": null,
        "approval": "once",
        "durationMs": 800,
        "outcomeLine": "  → ok  once  0.8 s  solutionId=00000000-0…",
        "pages": 1,
        "responseBytes": 2048
      },
      "result": { "…": "the full structuredContent, untruncated" },
      "unexpected": []
    }
  ]
}
```

The file ends with `]}` and a newline, and the plugin appends by overwriting that trailer with `,` and the new entry followed by the trailer again. Each entry is written in one write and flushed. Rules:

- **A call and its outcome are one entry**, written together when the call ends, as the two lines are in the panel ([§8.6](#86-call-log-format)). Entries are in the order calls ended. A continuation page is its own entry, with `approval: "page"`.
- **`callLine` and `outcomeLine`** hold the lines as the panel shows them, truncated and sanitised. **`arguments` and `result` hold the full detail**, with nothing truncated, so the file is a superset of the panel and of the call detail ([§8.4](#84-log-entry-detail)).
- **Every call is written**, including denied, rejected, timed-out and host calls. When a call never ran, `result` is `null`.
- **Escaping.** JSON escapes control characters, so agent text cannot break the structure. The plugin also escapes Unicode bidirectional-formatting characters as `\uXXXX`, so a file opened in a viewer cannot be made to read differently from what it holds (R8).
- **Never written:** the bearer token, protected settings values, and anything the plugin holds that the agent did not send or receive.
- **A file that is not in this shape** (a trailer that is not `]}`, a parse failure on opening) is never modified. The plugin starts `AgentCompanion-[ConnectionName]-yyyyMMdd-2.json` (then `-3`, and so on) and shows a warning in the log panel.
- **A failed write never fails or delays a call.** The panel warns once, the checkbox stays ticked, and the next call tries again.

**Client data (R7).** The files hold full arguments and results, so they can hold client data, including record values from record reads. The folder is therefore checked more strictly than **Save as…**: the plugin **refuses** a log folder that is, or is inside, a Git working tree (it or a parent folder contains `.git`), rather than asking for confirmation, because nobody is there to confirm each write. The check runs when the folder is chosen, at startup, and again each time a file is opened. The folder prompt says plainly that the files can contain client data and are not encrypted.

**Reworded when built (2026-11-09).** R13, §8.4 and §8.6 now say that a log reaches disk only through **Save as…** and that a call's detail is never saved, "unless the person turns on **Write call log to disk**". R7's wording stands. §8.8 has the checkbox and the folder line.

**Settled when built (2026-11-09).**

- *A failed call.* A call that failed has no `structuredContent`, so `result` is `null` for every error, and the error is in `outcome`: `errorCode`, `errorMessage` and `errorDetails` (the `ToolError` details, or `null`). The file stays a superset of the call detail.
- *A call that never ran.* `approval` is `null` where the panel shows `—`; `outcomeLine` keeps the `—`.
- *Size.* `responseBytes` is the UTF-8 size of the text content block returned to the agent. `pages` is 1 for a result and 0 for an error; a continuation page is its own entry, as in the panel.
- *`unexpected`.* The panel's `⚠ unexpected <type> <name>` lines for the call, as shown; the components themselves are in `result.unexpected`.
- *A file already written by another connection of the same name.* A plain-named file whose `connection.connectionId` is another connection's is never appended to, even when that connection is no longer bound; the connection writes to its `[id8]` file instead. The host file is checked the same way.
- *When a write happens.* The entry is queued when the call ends and written on a background task, one at a time, so the reply to the agent never waits for the disk. A call received while the log was on is written even if the log is turned off before it ends.
- *Warn once.* After a failed write, further failures are not reported until a write succeeds. A file left untouched is reported once per file.

**Decided (2026-11-08).**

- *Host calls.* Calls that name no connection or an unknown one (`list_connections` and rejected calls) have no connection file. They are written to `AgentCompanion-host-yyyyMMdd.json`, in the same shape, with `connection` set to `null`, while the checkbox is ticked.
- *Two connections with one name.* They would share a file, so the first 8 characters of the connection ID are added to the file name (`AgentCompanion-[ConnectionName]-[id8]-yyyyMMdd.json`) for both of them while two bound connections have the same name. A name that is unique when the first call of the day is written keeps the plain name for that day's file.

**Check:** with the checkbox ticked, a Read call and a denied Write call each produce an entry, and the file parses with a standard JSON parser after each. The file name has the connection name and the date, with no time. After XrmToolBox restarts on the same day, new calls append to the same file. A call after midnight creates the next day's file. Ticking the checkbox from off asks for the folder; Cancel leaves it off, and a folder inside a Git working tree is refused. Once on, two connections in different tabs each get their own file, and a call naming no connection goes to the host file. Closing and reopening XrmToolBox keeps the checkbox ticked and the folder, and logging resumes for a connection bound afterwards. With the folder made read-only, a call still completes and the panel warns once. A truncated or hand-edited file is left untouched and a `-2` file starts. Two connections with one name get the `[id8]` in their file names. After unticking, nothing more is written, and ticking again asks for the folder again. `get_connection_info` and the other tool responses are identical with the checkbox ticked and unticked, including when a log write fails.

#### 6a.3 — Pending spikes

Spikes that had not been run moved into this phase on 2026-11-08, so that open questions were in one place. They were unchanged except for where they were tracked. The original rows in Phase 0 and Phase 7 point here.

S12 ran here. **S7 and S8 moved on to [Phase 8](#phase-8--promotion-and-git-integration-spikes) on 2026-11-10**, at the person's choice: S7 needs a build environment with the promotion pipeline and S8 needs Dataverse Git integration, and neither will be ready before Phase 7 is built.

| ID | Question | Status | Feeds |
|---|---|---|---|
| S12 | Registration round trip, starting from the field findings in [Phase 7](#phase-7--plug-in-authoring). Does `pac plugin push` ever register new plug-in types, and if not, which route does — a WIP import with new `PluginType` entries in `.dll.data.xml`, or only the Plugin Registration Tool? What must a WIP hold for its export to carry steps, images and custom APIs — each step added as 92, the assembly with its subcomponents, or something else? Does importing a WIP with a new step file and its `RootComponent` create the step, with which import flags, and what makes the packager or the import skip it silently — a missing `Customizations.xml` anchor node, an unregistered plug-in type? The same for a custom API with request parameters and response properties. Through the plugin and the person's PAC profile only, on a data-free environment | **Run 2026-11-09** ([notes](notes.md#6a3--spike-s12-2026-11-09)). Push never registers types, and refuses a build that drops a registered one. A WIP import registers exactly the types listed in `.dll.data.xml`, and creates steps with images and custom APIs with parameters and properties, all in one import. The U10 skip: without the `<SolutionPluginAssemblies />` and `<SdkMessageProcessingSteps />` anchors in `Customizations.xml`, pack only warns and the import "succeeds" creating nothing — and an export has only the anchors for what the WIP holds. A step on an unregistered type fails the import. Each step is held as 92; the assembly (91) exports its types but no steps. Custom APIs need no `RootComponent`; adding an unmanaged one brings its assembly, parameters and properties | §9.10 (updated: G1, G4, G6, G7), the skill changes. **Phase 7 starts from its result** |

Each spike, when run, gets a written finding in [notes](notes.md) and updates the sections it feeds, as in Phase 0.

### Phase 6b — Power Pages spikes

> [!NOTE]
> **Opened 2026-11-10 at the person's choice, ahead of [Phase 7a](#phase-7a--power-pages-authoring).** D13 decided on 2026-11-03 not to pursue Power Pages, on two premises: site configuration is records outside solutions, and `pac pages upload` already updates in place. The first holds only for the standard data model. On the enhanced data model, the default for new sites, site configuration is solution-aware[^8]. These spikes settle what Phase 7a builds. Nothing is built in this phase, and the version does not change.

**What the documentation already settles** (Microsoft Learn, checked 2026-11-10[^8]):

| Fact | Consequence for the design |
|---|---|
| The enhanced data model is on by default, and new sites from most templates use it. Its configuration is in three solution-aware system tables: `powerpagesite`, `powerpagesitelanguage` and `powerpagecomponent` | A WIP can hold site components, so the overlap check (W7), W10 and close-out apply to them as to any component |
| Each site component is a `powerpagecomponent` row with a `powerpagecomponenttype` (2 web page, 3 web file, 7 content snippet, 8 web template, 9 site setting, among others). Its properties are a JSON document in the `content` column, up to 1 MB. A web file's bytes are in the `filecontent` file column, up to 128 MB. Rows have `versionnumber` and `modifiedon` | The overwrite check can use `versionnumber`, as for web resources. A direct update changes one property inside a JSON document, not a column of its own |
| The solution component type codes of the three tables differ per environment and are read from `solutioncomponentdefinition` | The resolution the plugin already does for custom APIs |
| New site components are not added to the solution that holds the site | A component created during a story is added to the WIP, as any new component is |
| The standard data model keeps configuration in `adx_*` tables, outside solutions. Four templates still create standard sites. A migration utility moves a site to the enhanced model | Standard sites stay outside WIPs. Migrating a site is the project's decision |
| The site caches its configuration. A change is served within 15 minutes, or at once after a cache clear: design studio's **Preview**, the site's `/_services/about` page for a user whose web role holds every website access permission, or a restart in the admin center | Fast iteration needs a cache clear, and none of the documented routes is a Dataverse request |
| `pac pages upload --modelVersion 2` uploads what changed since the last download in the same environment, using the environment manifest. It compares with that download, not with the environment | A design-studio edit made since the download is overwritten without warning ([§9.11](#power-pages--reopened-for-phase-7a)) |
| Single-page application sites are uploaded with `pac pages upload-code-site` | Not in these spikes. Guidance only, as for PCF |

**Method.** On a data-free environment with an enhanced-model site created from a starter template, through the plugin and the person's PAC profile only. Site components are added to WIPs with PAC (`pac solution add-solution-component` with the resolved code) until the plugin's selectors take them. The agent never uses an SDK connection of its own.

**Writes go through a throwaway spike tool** (the person's choice, 2026-11-10). The plugin has no write for site components, so a development build of the plugin carries one spike tool, `spike_write_site_component`:

| | |
|---|---|
| What it does | Writes one `powerpagecomponent` row: either one named top-level property of `content`, read and written back with every other property kept, or the bytes of `filecontent`. The value is a file under the File root, as in §6.8. It returns `versionnumber` and `modifiedon` before and after. It never creates or deletes a row, and touches no other table |
| What it keeps | Write tier: `reason` (R12), dev-environment confirmation, a per-call approval dialog showing the row, its type and site, the property, the file's size and SHA-256, and the log (R13). No grant covers it: it prompts every time, like publisher creation |
| Where it lives | A spike branch, built locally in Debug. It is never merged, packed or released, and the release build never lists it. It is removed when Phase 6b ends, and the spike branch is deleted. What it teaches goes into `update_site_components` |
| What stays with the person | The design-studio edits that S16 needs, to play a maker working beside the agent, and every cache clear done in a browser for S15 |

| ID | Question | Exit | Feeds |
|---|---|---|---|
| S13 | **Site components in a WIP.** Add to an empty WIP, one at a time: a web template, a web page with its localized content page, a web file, a content snippet and a site setting. What does the platform add with each — the site, the site language, a parent page, the page's content pages, table permissions — and with which root behaviour? Can the site record be held without its components? Export and unpack the WIP: where does each component land in the XML layout? Does importing the WIP with one component edited change only that component? | `list_solution_components` after each add, recorded. The unpacked layout per component type. An import that changes one web template and nothing else | N2, the selectors, the shaping and edit-loop skills, the segmentation map |
| S14 | **What a direct update writes.** For each component type in scope: which property of `content` holds the source a developer edits, and how `pac pages download --modelVersion 2` maps files and YAML to rows and properties. Update one property and keep the others: does the design studio still open the component, and does a fresh download give back the same files with the change? For a web file: is writing `filecontent` enough, or must its name, MIME type or partial URL change with it? Does `versionnumber` rise on every update, identical content included? | A table per component type: the file in the download, the row, the property or column, and whether a one-property update round-trips | The `update_site_components` item model and its overwrite check |
| S15 | **When the site shows a change.** After updating a web template, a page's copy and a web file: how long until the site serves the change without a cache clear? Does **Preview** clear it? Can any route the agent's tools reach clear it — a Dataverse request or a PAC command? How long do a direct update, a `pac pages upload` of one file and an import of a one-component WIP each take? | Measured times. The route the edit loop uses to make a change visible, or a statement that the person does it | Whether the tool is worth building, as S10 was for web resources. The step after an update |
| S16 | **`pac pages upload` against the environment.** After a download, change one component in the design studio and another file locally, then upload. Is the studio change overwritten, and does the upload report anything? Does it write only the changed file's row, or raise `versionnumber` on others too? Does it add anything to a solution? What does the drift check need from the environment manifest to map a changed file to its row? | The overwrite reproduced or ruled out. The rows an upload writes. The mapping the drift check uses | The drift check (N3), and when the edit loop uploads rather than calling the tool |

Each spike, when run, gets a written finding in [notes](notes.md) and updates the sections it feeds.

**Exit:** every spike has a finding; §9.11 and Phase 7a are updated; Phase 7a's design is confirmed, changed or dropped. If S15 finds no route to make a change visible quickly, the tool's case rests on the overwrite check and the WIP alone, and Phase 7a says whether that is enough.

### Phase 7 — Plug-in authoring

> [!NOTE]
> **Deferred (2026-11-03), at the person's choice, to prioritize other work.** Starts from the result of spike S12, run on 2026-11-09 in [Phase 6a](#6a3--pending-spikes). Until then plug-in registration is a person's, in the Plugin Registration Tool: the companion skills do not carry it, and `d365ce-planning` plans it as manual ([§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7)).

Specified with Phase 6 and split off when field use showed that plug-in authoring has quirks the design does not account for. On a project, an agent added plug-ins to an existing assembly with `pac plugin push`:

| Seen | Against the design |
|---|---|
| `pac plugin push` did not register the new plug-in types in the assembly | §9.10 already sends a build that adds types to the Plugin Registration Tool or to the WIP path; nothing in the agent's path did either |
| Steps were missing from the solutions exported | Steps are not added to a solution with their assembly[^5]; whether they had been added on their own is not known |
| After importing, the steps were not created | The WIP path (G1–G4) has not been verified live |

| Enhancement | Behaviour |
|---|---|
| **Plug-in registration through a WIP** | New and changed assemblies, plug-in types, steps, images and custom APIs authored in the unpacked WIP and imported with PAC; rules G1–G4, G6 and G7 ([§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7)) |
| **`pac plugin push` for a registered assembly** | A new build pushed after G5, with the assembly still held by its WIP; a build that adds plug-in types needs them registered by another route |
| **Step mirroring and switching** | `mirror_steps`, `disable_steps`, `enable_steps` and `rebind_custom_apis` ([§6.9](#69-plug-in-step-tools-phase-7)); side-load WIP naming (`_Sideload`, [§9.5](#95-transport-solutions-and-segmented-wip-solutions)); rules M1–M7 |
| **Skill changes** | `agent-companion-wip-edit-loop`: registration through the WIP, G1–G7, `pac plugin push`, M3 after every import. `agent-companion-wip-shaping`: what a WIP must hold for registration, plug-in assemblies in the overlap check (G3), side-load WIPs and `Get-WipName.ps1`'s `_Sideload` suffix. `agent-companion-context`: the G and M rules in its rule index, open switches in the ledger (M7). `agent-companion-wip-close-out`: M4 before close-out, mirrors and disabled originals in the leftover check. `d365ce-planning`: registration as agentic work once the path is verified (K7). What spike S12 adds to each, file by file, is in [Skill changes from spike S12](#skill-changes-from-spike-s12) |

**Spikes.** S9 ran on 2026-11-03 with Phase 6's spikes. S12 moved to [Phase 6a](#6a3--pending-spikes) on 2026-11-08 and ran there on 2026-11-09, through the plugin and the person's PAC profile only, on a data-free environment. It explains all three field observations above: push does not register types (a WIP import with the types in `.dll.data.xml` does); an assembly-only WIP exports no steps; and step files added to an export without the `<SdkMessageProcessingSteps />` anchor are dropped at pack with a warning, so the import reports success and creates nothing.

| ID | Question | Result | Feeds |
|---|---|---|---|
| S9 | Steps: created disabled? Same `typename` in two assemblies? Images and secure configuration copied? What does an import do to step state? Custom API rebinding? | Not creatable disabled — create, then disable. Same `typename` allowed. Images, unsecure and secure configuration copied, and the mirror ran with identical inputs. An import sets every step it carries — enabled with activation, disabled without — and leaves others alone; no state in the export. Custom API rebinding effective within a second. Ran through a throwaway SDK console, not the plugin ([notes](notes.md#spikes-s9s11--2026-11-03)) | `mirror_steps`, M1–M5, `rebind_custom_apis` |
| S12 | *Moved to [Phase 6a](#6a3--pending-spikes) (2026-11-08).* Registration round trip, starting from the field findings above | Run 2026-11-09 — see [6a.3](#6a3--pending-spikes) and [notes](notes.md#6a3--spike-s12-2026-11-09) | §9.10, G1–G7, the skill changes |

**Exit:** on the sandbox, through the plugin and PAC only: a story adds a plug-in type and a step with an image to an existing assembly, and a new custom API, by the path S12 settles; the read-back shows each as planned (G4); an export of the WIP carries them, and importing that export where they do not exist creates them. A step is mirrored to a side-loaded assembly disabled, switched, restored and its mirror removed. An import with `--activate-plugins` of a WIP holding a switched original is caught by M3.

#### Skill changes from spike S12

This subsection says how the findings of spike S12 ([notes](notes.md#6a3--spike-s12-2026-11-09)) go into the companion skills when Phase 7 is built. It covers registration through a WIP and `pac plugin push`. The step tools and the M rules have their own skill changes in the table above, and those are unchanged. Nothing here is written into a skill before Phase 7 starts. Until then plug-in registration stays manual, and the current skills say so.

**How to apply it.**

- Rules are cited by ID with their meaning kept (K3). A skill names G6 and states what to do; it does not restate the reason or the spike. Platform behaviour goes in each skill's `references/platform-behaviour.md`. The `SKILL.md` carries the decision and the procedure ([INSTRUCTIONS.md — Skills](../../INSTRUCTIONS.md#skills)).
- The companion skills are embedded in the plugin's DLL, so these changes ship with a Phase 7 build and its version bump.
- Every script change comes with fixtures in the script's `tests\Invoke-ScriptTests.ps1`, and the self-tests must pass. Fixtures are synthetic: made-up assembly names, public key tokens and GUIDs, and no IDs from the spike environment (R7).
- Shapes in the skills are described by element and file name, never pasted from an environment's export (G1 applies to the agent at run time, not to the skill text).

**The findings, numbered for the instructions below.**

| # | Finding |
|---|---|
| S12.1 | `pac plugin push` updates a registered assembly's content and never registers a plug-in type. The platform refuses a push whose build lacks a registered type: *PluginType [&lt;type&gt;] not found in PluginAssembly [&lt;assembly&gt;] which has a total of [n] plugin/workflow activity types* |
| S12.2 | A WIP import registers exactly the `PluginType` entries in `.dll.data.xml`, with their `PluginTypeId`s. A type in the DLL but not listed is not registered. It works for a new assembly, for an assembly whose content already holds the type through a push, and in the same import as the steps and custom APIs that use the type |
| S12.3 | `pac solution pack` merges `PluginAssemblies\` only where `Other\Customizations.xml` has `<SolutionPluginAssemblies />`, and `SdkMessageProcessingSteps\` only where it has `<SdkMessageProcessingSteps />`. Without the node, pack prints *Following root components are not defined in customizations*, completes, and zips the files loose. The import then reports *Solution Imported successfully* and creates nothing. An export has a node only for a type the WIP held |
| S12.4 | A step whose plug-in type is neither registered nor listed fails the whole import: *SDK Message Processing Steps import: FAILURE: Entity 'plugintype' With Id = 00000000-… Does Not Exist*. Nothing is left behind |
| S12.5 | A new step is created from `SdkMessageProcessingSteps\{id}.xml` plus a `RootComponent` of type 92 plus the anchor. It keeps the authored ID. Its images are elements inside the step file, and its message filter is resolved from `SdkMessageId` and `PrimaryEntity` |
| S12.6 | An import sets the state of every step it carries: enabled with `--activate-plugins`, disabled without, whatever the state before (also S9) |
| S12.7 | A custom API travels as `customapis\<uniquename>\customapi.xml`, with `customapirequestparameters\<uniquename>\customapirequestparameter.xml` and `customapiresponseproperties\<uniquename>\customapiresponseproperty.xml`. It has no `RootComponent` and no anchor. Its plug-in type is referenced by `<plugintypeid><plugintypeexportkey>`, and may be a type registered by the same import. The import adds the custom API, its parameters and its properties to the WIP as separate components |
| S12.8 | A WIP holding the assembly (91) exports the DLL and every registered type, but no steps. A WIP holding a step (92) exports the step and its images, but not the assembly, and lists the plug-in type (90) as a missing dependency |
| S12.9 | `add_solution_components` with an unmanaged custom API bound to a plug-in type also adds the plug-in assembly, the request parameters and the response properties (behaviour 0). A managed custom API adds nothing else, and exports as an empty stub (`<customapi uniquename="…"></customapi>`) |
| S12.10 | `query` returns `sdkmessageprocessingstep.plugintypeid` with `_ref.entity` `sdkmessagefilter`; the ID and name are the plug-in type's |
| S12.11 | Importing a WIP with one assembly took 16–18 s without publishing; `pac plugin push` took about 5 s |

##### `agent-companion-context`

| File | Where | Change | From |
|---|---|---|---|
| `references/rules.md` | New section *Plug-in registration (G)* after *Delivery context (T)* | G1–G7 as in [§9.10](#910-plug-in-assemblies-steps-and-custom-apis-phase-7), one line each. Applied by: G1, G6, G7 — edit loop; G2, G5 — edit loop; G3 — shaping; G4 — edit loop and close-out | §9.10 |
| `references/rules.md` | R1 row | Add: *the platform still adds the plug-in assembly, parameters and properties with an unmanaged custom API; the read-back reports them* | S12.9 |
| `references/rules.md` | K7 row | Replace the plan-as-manual clause with: registration through a WIP is agentic when the plugin is connected; deletions, and registering a side-loaded assembly, stay manual | S12.2, S12.5, S12.7 |
| `SKILL.md` | *Reading the environment for research*, the plug-in row | Add: in `query` results, read a step's `plugintypeid` by ID and name; its `_ref.entity` says `sdkmessagefilter` | S12.10 |
| `SKILL.md` | *4. State the steps (T3)* | For a WIP that carries steps, the steps say which steps the import will enable or disable, and whether it runs with `--activate-plugins`. The person confirms that choice | S12.6 |
| `references/wip-ledger.md` | *WIPs* table | Add a column *Step activation* (`on`, `off`, `n/a`), recorded when the steps are confirmed, and read by the edit loop's import | S12.6 |

##### `agent-companion-wip-shaping`

| File | Where | Change | From |
|---|---|---|---|
| `SKILL.md` | *Adding: MCP or PAC*, the plug-in row | Split it in two. **Registered components:** as today. Change "Read the add back: what the platform brings with a custom API is reported as unexpected" to name what it brings: the assembly, the parameters and the properties. **New registrations** are not added here; they are authored in the edit loop. The WIP needs only what the table below lists | S12.8, S12.9 |
| `SKILL.md` | *4. Decide the WIPs*, new bullet | **What an Assemblies WIP holds before a registration edit**, as a small table: a new assembly — nothing (an empty WIP is fine); a new type in a registered assembly — the assembly (91); a new step on a registered type — nothing; a new step on a new type — the assembly; a changed step — that step (92); a new custom API — nothing; a changed custom API — the custom API and the parameters and properties that change. *Never add the assembly just to carry steps: its export carries none* | §9.10 table, S12.8 |
| `SKILL.md` | *3. Checks before shaping*, overlap row | Add the plug-in assembly as a whole document (G3): another open WIP holding the same assembly carries a different build of it | G3 |
| `SKILL.md` | *8. Read back (R1)*, *Two platform additions are known* → three | **An unmanaged custom API** brings its plug-in assembly, request parameters and response properties. Keep the parameters and properties when the change touches them. Name the assembly to the person: unless the change rebuilds or registers into it, remove it and read back that the custom API and its parameters are still there. If removing it takes them with it, or it is not clear, ask. *That removal was not exercised by S12; confirm it during the Phase 7 build and update this line* | S12.9 |
| `references/component-types.md` | Rows 90, 93 and the custom API rows | 90 and 93: *Travel inside their assembly's `.dll.data.xml` and their step's file; never added on their own.* Custom API: *Has no `RootComponent` in an export; its parameters and properties are separate components* | S12.2, S12.5, S12.7 |
| `references/platform-behaviour.md` | *Adding and removing* | Rows: assembly alone → nothing else; step alone → nothing else, image inside the step; unmanaged custom API → assembly, parameters, properties; managed custom API → nothing else | S12.9 |
| `references/platform-behaviour.md` | *Reading what is there* | Rows: an assembly-only WIP exports no steps; a step-only WIP exports no assembly and lists the type as a missing dependency; a managed custom API exports as a stub, so it is not a shape (G1) | S12.8, S12.9 |
| `references/queries.md` | *Plug-in steps, custom APIs, …* — new sub-section *Registration read-back (G4) and push baseline (G5)* | Three FetchXML queries, written with the plugin's rules (explicit columns, no `top`). (1) `plugintype` by assembly name: `typename`, `plugintypeid`, `plugintypeexportkey`, with the assembly's `version`, `modifiedon` and `modifiedby`. (2) `sdkmessageprocessingstep` by ID: `name`, `statecode`, `stage`, `mode`, `rank`, `filteringattributes`, `configuration`, `sdkmessagefilterid`, `plugintypeid`, outer-linked to `sdkmessageprocessingstepimage` (`name`, `imagetype`, `attributes`, `entityalias`). (3) `customapi` by unique name: `plugintypeid`, `bindingtype`, `isfunction`, outer-linked to its parameters (`uniquename`, `type`, `isoptional`) and properties (`uniquename`, `type`). Add the S12.10 caveat under (2). Add (4) `sdkmessage` by `name` → `sdkmessageid`, for authoring a step file | G4, G5, S12.5, S12.10 |

##### `agent-companion-wip-edit-loop`

| File | Where | Change | From |
|---|---|---|---|
| `SKILL.md` | Description (frontmatter) | Add plug-in assemblies, types, steps with images and custom APIs to what the loop edits; add `pac plugin push` to the direct updates | — |
| `SKILL.md` | *The decisions this skill owns* | Two rows. **Push or WIP for a build:** push when every plug-in type of the build is registered; otherwise the WIP, with the new types in `.dll.data.xml` (after the push, or instead of it). **Step activation on import:** from the ledger (context skill); without a recorded choice, ask | S12.1, S12.2, S12.6 |
| `SKILL.md` | *3. Edit*, the table | Rows with the file each change touches. **Assembly:** `PluginAssemblies\<FileName>-<ID>\<FileName>.dll` and `.dll.data.xml`; keep the name, strong name and version unless the plan changes them. **Plug-in type:** a `PluginType` element in `.dll.data.xml` (`AssemblyQualifiedName`, `PluginTypeId` as a new GUID, `Name`, `FriendlyName`). **Step:** `SdkMessageProcessingSteps\{id}.xml` (`SdkMessageId` from `query` over `sdkmessage`, `PluginTypeName`, `PluginTypeId`, `PrimaryEntity`, `FilteringAttributes`, `Stage`, `Mode`, `Rank`, `Configuration`, images inside), with a `RootComponent` of type 92. **Custom API:** the `customapis\` folders of S12.7, with no `RootComponent` | §9.10, S12.2, S12.5, S12.7 |
| `SKILL.md` | *3. Edit*, *Authoring a new component*, the anchor bullet | Generalize it to every component type, and list the plug-in anchors next to `<WebResources />`: `<SolutionPluginAssemblies />` and `<SdkMessageProcessingSteps />`, after `<CustomControls />`, before `<EntityDataProviders />` (G6). Add: *an export only has the anchor for a type the WIP held — an empty or assembly-only WIP needs it added.* Add that missing plug-in anchors give no import error: the import succeeds and creates nothing | G6, S12.3 |
| `SKILL.md` | *3. Edit*, new bullets under *Authoring a new component* | **Shapes (G1):** model a new step, assembly or custom API on an export that holds one — the project's extract or another WIP's export — never on a managed component's export. For custom API parameters and properties, Microsoft's documented format is acceptable. **Every type listed (G7):** each type a step or custom API uses is registered or listed, and every new type in the build is listed. **New IDs** are new GUIDs. **A new step or type is a placement component**, as for any new component: add it to the ledger | G1, G7, S12.2, S12.4 |
| `SKILL.md` | *4. Validate offline* | Pass the new evidence to `Test-UnpackedSolution.ps1`: `-RegisteredPluginTypes` (registered type names from query (1)) and `-BuildPluginTypes` (the `IPlugin` types in the build — from the build output or the source; say how it was obtained). Fix every `Error` | G2, G7 |
| `SKILL.md` | *5. Pack and check the schema* | *A pack that prints "Following root components are not defined in customizations" has failed* — `Invoke-EditLoopStep.ps1 -Step Pack` reports it as a failure. Fix the anchor and pack again | G6, S12.3 |
| `SKILL.md` | *6. Overwrite check* | The assembly, plug-in types, steps and custom APIs have no row version: re-export and compare (step 2 of the check). Run G2 before the import that carries a new build | G2, G3 |
| `SKILL.md` | *7. Import* | Use the WIP's *Step activation* from the ledger: `--activate-plugins` when it is `on`. Before importing, tell the person which steps will be enabled or disabled. M3 after the import, as already listed | S12.6, M3 |
| `SKILL.md` | *8. Read back and verify* | Before the person checks behaviour: G4 with the queries in shaping's `queries.md`. *"Solution Imported successfully" proves nothing for plug-in components* — a skipped component shows only in the read-back. New steps, types and custom API parts are expected in `list_solution_components`; the custom API's codes differ per environment | G4, S12.3, S12.7 |
| `SKILL.md` | *Direct updates in development* | Replace *Not covered. Plug-in assemblies, steps and custom APIs stay with the Plugin Registration Tool* with a **`pac plugin push`** block: G5 first (profile, name, major and minor version, `modifiedon` and `modifiedby` against the baseline, G2); `pac plugin push --pluginId <pluginassemblyid> --pluginFile <dll> --type Assembly`; then `query` for the new `modifiedon`. *Push never registers a type*: when the build adds one, register it through the WIP. The platform's refusal of a build missing a type is a backstop, not the check | G5, S12.1 |
| `SKILL.md` | *Guardrails* | Rows: before a registration import — the step activation choice and the steps whose state changes; after it — the G4 read-back matches the plan | S12.6, G4 |
| `references/platform-behaviour.md` | *Authoring new components* | Rows for S12.2, S12.3 (plug-in anchors, the pack warning, the silent import), S12.7 (no `RootComponent`) and the managed stub (S12.9) | S12.2, S12.3, S12.7, S12.9 |
| `references/platform-behaviour.md` | *Import* | Rows for S12.6 (state of every carried step) and S12.11 (times). Messages table: *Entity 'plugintype' With Id = 00000000-… Does Not Exist* → a step's type is neither registered nor listed in `.dll.data.xml` (G7); *PluginType [x] not found in PluginAssembly* (from push) → the build lacks a registered type (G2) | S12.1, S12.4, S12.6, S12.11 |
| `scripts/Test-UnpackedSolution.ps1` | *Packager anchors* | Add `@{ Folder = 'PluginAssemblies'; Node = 'SolutionPluginAssemblies'; Severity = 'Error' }` and `@{ Folder = 'SdkMessageProcessingSteps'; Node = 'SdkMessageProcessingSteps'; Severity = 'Error' }`. Update the comment above the list: for these two the import succeeds without creating anything | G6, S12.3 |
| `scripts/Test-UnpackedSolution.ps1` | New section *Plug-in registration* | Rules. `PluginTypeUnresolved` (Error): a step's `PluginTypeId` or a custom API's `plugintypeexportkey` is in no `.dll.data.xml` of the tree and not in `-RegisteredPluginTypes` (a Warning, *not verified*, when that evidence is not passed). `PluginTypeNotListed` (Warning): a type in `-BuildPluginTypes` is neither listed nor registered, so it will not be registered. `RegisteredTypeMissingFromBuild` (Error): a type in `-RegisteredPluginTypes` for an assembly in the tree is not in `-BuildPluginTypes` (G2). `RootComponentWithoutDefinition` (Error): a `RootComponent` of type 91 or 92 with no matching folder or file, and the reverse. `CustomApiRootComponent` (Warning): a `RootComponent` that names a custom API | G2, G7, S12.2, S12.4, S12.7 |
| `scripts/Invoke-EditLoopStep.ps1` | `Pack` | Capture PAC's output; a line *Following root components are not defined in customizations* makes the step fail, naming the components, and say *stop* as after a failed import | S12.3 |
| `scripts/Invoke-EditLoopStep.ps1` | `Import` | When `src\` has a non-empty `SdkMessageProcessingSteps\` folder, refuse to run unless `-StepActivation On` or `-StepActivation Off` is given; `On` adds `--activate-plugins`. The example in the help changes accordingly | S12.6 |
| `scripts/tests/Invoke-ScriptTests.ps1` | Fixtures | One fixture tree per new rule. Include the U10 case: an assembly-only export with a step file added and no `<SdkMessageProcessingSteps />` anchor. It must give `PackagerAnchorMissing` as an Error | S12.3 |

##### `agent-companion-wip-close-out`

| File | Where | Change | From |
|---|---|---|---|
| `SKILL.md` | *Close-out B* | When the WIP's components are added to the transport solution: each step as 92, the assembly as 91, and the custom API with each request parameter and response property — the assembly does not carry steps, and the custom API is not one component. Then B3's read-back lists each of them | S12.7, S12.8 |
| `SKILL.md` | *Close-out A* | After promotion, G4's queries on the build connection as well as A1's read-back. An import into build sets the state of every carried step by the pipeline's activation setting: say which | G4, S12.6 |
| `references/strategies.md` | The strategy A and B notes | The same two points, briefly, with the rule IDs | S12.6, S12.8 |

##### `d365ce-planning`

| File | Where | Change | From |
|---|---|---|---|
| `SKILL.md` | *Ground rules* (the manual and agentic split) and *Task list conventions* (the `M` tasks) | Plug-in registration moves from *Manual* to *Agentic* when the Agent Companion MCP is connected and the Phase 7 companion skills are installed (K7). It stays manual otherwise. Deletions, and registering a side-loaded assembly, stay manual | K7 |
| `SKILL.md` | *Task list conventions*, for plug-in work | A registration task names, for each step: message, table, stage, mode, rank, filtering attributes, images (name, type, attributes) and unsecure configuration — what the step file needs. It names each new plug-in type, and each custom API with its parameters and properties (unique name, type, optional). It records the step activation choice. A build that adds a type gets a WIP task, not only a push | S12.2, S12.5, S12.6, S12.7 |
| `SKILL.md` | *Quality bar* checklist | Rows: every type the build adds has a registration task; the step activation choice is in the plan | S12.2, S12.6 |

**Plugin changes that go with these** (Phase 7 build, not skills):

- `add_solution_components` description: *subcomponents always off* holds for tables only. State what the platform adds with a custom API (S12.9).
- `list_solution_components`: resolve names for the custom API types (`customapi`, `customapirequestparameter`, `customapiresponseproperty`) through `solutioncomponentdefinition`, as `add_solution_components` already does for their codes.

**Check that the skill changes work.** Part of the Phase 7 exit, on the sandbox:

1. Shaping a registration story produces Assemblies WIPs that hold only what the table in *4. Decide the WIPs* lists.
2. The validator stops the U10 tree before pack.
3. A pack with a missing anchor fails in `Invoke-EditLoopStep.ps1`.
4. An import without a step activation choice is refused.
5. The G4 read-back matches the plan.
6. A plan from `d365ce-planning` for a story with a new type, a step with an image and a custom API lists them as agentic tasks, with every field the step file needs.

### Phase 7a — Power Pages authoring

> [!NOTE]
> **Opened 2026-11-10 at the person's choice; not started.** Reopens D13 ([§9.11](#power-pages--reopened-for-phase-7a)). The design below is provisional: it starts from the spikes in [Phase 6b](#phase-6b--power-pages-spikes) and changes with their findings. It does not depend on Phase 7 and can be built before or after it.

**Scope.** Power Pages sites on the enhanced data model, authored by the agent in development: Liquid web templates, page copy, page CSS and JavaScript, content snippets and web files, held in WIPs like any other component. Standard-model sites and single-page application sites get guidance only.

**Why a tool, when `pac pages upload` already updates in place.** For the same reasons as `update_web_resources`:

- The overwrite check is against the environment, not against the last download.
- The upload writes only the components the story names, not everything changed since the last download.
- Each component is held in the story's WIP.
- Every write is approved and logged in the plugin tab.

`pac pages upload` stays for what the tool does not cover.

| Enhancement | Behaviour |
|---|---|
| **Site components in the selectors** | `add_solution_components`, `remove_solution_component` and `get_required_components` take `powerpagesite`, `powerpagesitelanguage` and `powerpagecomponent`, with codes resolved per environment as for custom APIs. `list_solution_components` resolves their names and, for a site component, its type (web page, web template and so on). What the platform adds with each is reported as S13 found |
| **`get_component_state` for site components** | `versionNumber`, `modifiedOn` and `modifiedBy` of a `powerpagecomponent`, with its type and site: the baseline for the overwrite check |
| **`update_site_components`** | A Write tool on [§6.8](#68-direct-update-tools-phase-6)'s conventions: files by path under the File root, `expectedVersion` per item, each component already in the named WIP, one dialog listing each item with path, size, SHA-256 and versions, the first failure stops the run, then a read-back. Each item names one component and one source field — a web template's source, a page's copy, CSS or JavaScript, a content snippet's value, or a web file's content — mapped to the property or column S14 found. For a property in `content`, the tool reads the JSON, replaces that property only and writes the document back, keeping every other property as it was. A web file's bytes go to `filecontent`. It creates nothing in this phase: a new component is created in the design studio or with `pac pages upload`, then added to the WIP. After writing, it makes the change visible by the route S15 settles |
| **Drift check before `pac pages upload`** | For what the tool does not cover — component metadata, new components, deletions — the edit loop uploads with PAC after a read-only check. Each changed file is mapped to its row through the environment manifest, and its `versionnumber` is compared with the one recorded at download (N3). This was the candidate D13 named, now a skill script using `query` |
| **Skill changes** | `agent-companion-context`: Power Pages in the segmentation map — which transport solution holds each site and its components — and each site's data model, recorded in the delivery facts. `agent-companion-wip-shaping`: site components in WIPs, what the platform adds with them (S13), N1 and N2. `agent-companion-wip-edit-loop`: the Power Pages loop — a download as the baseline, local edits, `update_site_components` or the checked upload, then making the change visible. `agent-companion-wip-close-out`: site components added to the transport solution like any other. `d365ce-planning`: Power Pages tasks planned as agentic when the site is on the enhanced data model and the plugin is connected. The `ai-dcrm-pages` naming rules apply to every component the agent creates |

**Rules.** Provisional, until Phase 6b settles them.

| # | Rule | Why |
|---|---|---|
| N1 | **WIPs and direct updates are for enhanced-model sites only.** The agent reports a standard-model site to the person, and its changes go through `pac pages upload` after the drift check (N3). Migrating a site is the project's decision, never a step the agent proposes within a story | Standard-model configuration is not solution-aware, and a migration changes the site for everyone |
| N2 | **A WIP holds the site components a story touches, never the whole site.** The site record is added only when the change edits the site record itself. Anything the platform brings with a component is handled as R1 says | A site added with all its objects is the whole site. Its import overwrites every component, and its close-out carries everyone's changes |
| N3 | **Every write to a site component is checked against the environment, not against the last download.** `update_site_components` checks `expectedVersion`; an upload runs the drift check first | `pac pages upload` compares with the last download, so a design-studio edit made since then is lost without a message |
| N4 | **The environment manifest (`<org-url>-manifest.yml`) stays out of git** (U8) | It records one developer's last download from one environment[^7] |

**Exit:** on the sandbox, with an enhanced-model site, a story changes a web template, one page's copy and a web file:

- Each component is added to the story's WIP and read back as N2 expects.
- Each is updated with `update_site_components` against its baseline, and the site serves the change by the route S15 settled.
- After an edit in the design studio, a stale `expectedVersion` is refused.
- A metadata change goes through `pac pages upload`, and the drift check catches a design-studio edit made after the download.
- The WIP closes out into the site's transport solution, and an export of that solution carries exactly those components.

### Phase 8 — Promotion and Git integration spikes

> [!NOTE]
> **Opened 2026-11-10 at the person's choice.** S7 and S8 moved here from [Phase 6a](#6a3--pending-spikes) because the environments they need will not be ready before Phase 7 is built: a build environment with the promotion pipeline for S7, and Dataverse Git integration for S8. Phase 7 does not depend on either spike. Until S7 runs, A1's R1 check after every promotion stays mandatory. Until S8 runs, strategy C stays deferred.

| ID | Question | Needs | Status | Feeds |
|---|---|---|---|---|
| S7 | Does the promotion pipeline's component copy preserve each component's root component behaviour, and does it ever add required components or subcomponents? **Exit:** `list_solution_components` on the build environment before and after promoting a WIP holding a metadata-only table, a column, a form and a web resource matches what was intended | A build environment with the promotion pipeline configured. A second plugin tab is bound to it read-only | Not run | A1: whether the R1 check after promotion stays permanent. [§9.7](#97-close-out-strategies) close-out strategy A |
| S8 | With Dataverse Git integration, does environment binding commit WIP solutions as solutions in their own right, and can an unbound WIP hold objects of solutions bound under solution binding? **Exit:** documented behaviour, and the recommended binding for projects that use WIP solutions | A development environment connected to Git, both bindings tried. It must be a managed environment[^4] | Not run; waits for strategy C | [§9.7](#97-close-out-strategies), C1, C2 |

Each spike, when run, gets a written finding in [notes](notes.md) and updates the sections it feeds, as in Phase 0.

**Exit:** both spikes have a written finding, and A1, C1 and C2 say what was found.

---

## 12. Human review checkpoints

| Moment | What a person checks |
|---|---|
| Binding a tab to a connection | The connection is the intended **development** environment, confirmed in that tab |
| Choosing the connection for a task | With more than one bound, the agent asked which to use; the PAC profile targets the same organization |
| Delivery context and steps | Each fact was derived or confirmed, not assumed. The steps name every WIP with its route and how it ends, and every pipeline run with who runs it (R15, T3) |
| Publisher selection | The existing publisher is reused; a new one only with explicit certainty (R3) |
| Classifying a story's components | Each component's group follows the segmentation map; components already in a transport solution stay there (W1, W2) |
| Routing a component | Placement when it has no transport home, in place when it has one — looked up where transport is extracted from (W9, T4). A second WIP for a group only by the person's choice (W12) |
| Overlap with other open WIPs | Shared whole documents are sequenced or reviewed (W7, X1) |
| Every Write approval | Connection, solution name, component list, fixed flags shown as OFF; the agent's reason matches what the arguments do (R12) |
| Reviewing or handing over a session | The call log shows the expected calls and outcomes; a saved log is kept outside any repository (R13, R7) |
| After every add | `list_solution_components` shows nothing unexpected (R1) |
| Read approval scope | Per environment only for development environments; per session for production; *always* only with a reason ([§7](#7-authorization-model)). From Phase 6: per tenant when the tenant holds only the project's development and test environments; Global only with a reason ([§7](#phase-6--scopes-and-grants)) |
| A query that touches records | The tables and columns are needed for the task; no more. From Phase 6, the scope of a `RecordRead` grant follows the Read recommendation |
| Before import | The overwrite check passed or a re-export was merged; in a shared development environment, others' pending changes will be published too (X11) |
| After import | Components behave as intended in the app; the WIP solution contains only what the story touched |
| Ribbon changes | Button rules and commands reviewed against the base ribbon, not only the diff |
| A direct web resource update *(Phase 6)* | Each path, size and SHA-256 is the file intended; the WIP is the story's Resources WIP ([§6.8](#68-direct-update-tools-phase-6)) |
| A direct site component update *(Phase 7a)* | Each path, size and SHA-256 is the file intended, and each item names the component and field intended; the components are in the story's WIP, and the site is on the enhanced data model (N1–N3) |
| A step switch *(Phase 7)* | Exactly one copy of each step will run after the switch, and the people sharing the development environment know their steps run side-loaded code meanwhile (M6, [§9.10](#step-state-is-not-transported)) |
| Before an extract from development, a promotion or a close-out *(Phase 7)* | No open switch: originals enabled, mirrors disabled or removed, custom APIs bound to the shared assembly (M4) |
| `pac plugin push` to a shared assembly *(Phase 7)* | Release build from merged source; G2 and G5 passed |
| Ending an in-place WIP | Every component in it already has a transport home; under A it has been promoted (W10) |
| Aligning development from build | The transport solution was exported unmanaged from the build environment and carries no local edits; the open WIPs' shared whole documents were reviewed; nothing managed from the project is imported into development (A8, R14, R16) |
| Close-out | All of the story's placement WIPs, in transport order; the strategy fits each destination's transport source ([§9.7](#97-close-out-strategies)); no dependency points up the order; deletions handled by hand; no WIP deployed downstream (W5, W6, X3, X8, R11) |

---

## 13. Open questions

| # | Question | Raised by |
|---|---|---|
| Q13 | A model-driven app in a WIP exports with an empty `AppModuleComponents` even when the app has components. What does importing that WIP do to the app's component list? To be tested on a test app, never a real one | E4 |

### Resolved

| # | Decision | Applied in |
|---|---|---|
| Q1 | WIP friendly name is the unique name with each underscore replaced by a space | [§9.5](#95-transport-solutions-and-segmented-wip-solutions) |
| Q2 | No exchange files: large payloads are always paged | [§6.2](#62-conventions) |
| Q3 | Dev-environment confirmation scopes: once, session, environment, always | [§7](#7-authorization-model) |
| Q4 | One approval prompt for all reads, scoped once, session, environment or always; recommended per environment for development and per session for production | [§7](#7-authorization-model) |
| Q6 | `get_required_components` added | [§6.5](#65-read-tools) |
| Q7 | The plugin does not enforce or track the segmentation map; the agent does, with skill guidance | [§9.5](#95-transport-solutions-and-segmented-wip-solutions) |
| Q5 | Repository follow-ups made with the spec: a tooling-conventions section in the instructions file, the codebase map, the root README's repository map, and a build-backlog row in the matrix. Workflow guides change at release, when the plugin is usable | [Phase 5](#phase-5--release) |
| Q8 | v1 skills support close-out strategies A and B; C and D are assessed later with teams that use them | [§9.7](#97-close-out-strategies) |
| Q9 | The close-out strategy is chosen per WIP from where its destination is extracted from, not once per project; A and B can coexist in one project. Delivery facts the agent cannot derive are asked with a recommendation, and the steps are confirmed before acting. Confirmed facts are recorded in `<client>-Context/` where it exists. Pipeline runs, commits and pushes are a person's (T6) | [§9.7](#97-close-out-strategies), [§9.8](#98-delivery-context) |
| Q10 | Two routes per component — placement and in place — mixed freely. An in-place WIP is deleted once its edit has reached the transport source; a transport solution is never the edit vehicle, though under A it may be imported unmanaged from build to align development (A8); no managed project solution is ever imported into development (R16) | [§9.9](#99-routes--placing-a-component-or-editing-it-in-place), R14 |
| Q11 | Phase 4 skills are new `agent-companion-*` skills in `skills/`, whose frontmatter states they are used with this plugin; they are split by concern, apply on conditions rather than project phases, and share one delivery context and WIP ledger. Existing skills keep their procedures and are not made to depend on the plugin | [Phase 4](#phase-4--agent-skills-and-scripts) |
| Q12 | A relationship's parent table shells, and a 1:N's referencing lookup column, are expected platform additions: the tool reports them as the relationship's parents rather than as unexpected (plugin change, not yet built). Unless the change customizes them, the agent removes them afterwards; when in doubt it asks the person | [§6.6](#add_solution_components) step 5; shaping skill |
| Q14 | No delete tool. Removing mirror steps and a side-loaded assembly at the end of a side-load stays with the person, in the Plugin Registration Tool; the plugin never deletes (X3) | [§9.10](#plug-in-assemblies--side-loading) step 7 |

---

## 14. References

[^1]: Microsoft Learn — *Microsoft Power Platform CLI solution command group*, <https://learn.microsoft.com/power-platform/developer/cli/reference/solution> (last updated 2026-02-25, checked 2026-10-25). Supports: `add-solution-component` options are limited to `--AddRequiredComponents`, `--async` and `--environment`; no create command exists; `unpack` defaults to the XML layout while `clone` and Git integration use the YAML layout.

[^2]: Microsoft Learn — *Connect to Git*, <https://learn.microsoft.com/power-platform/alm/git-integration/connecting-to-git>, and *Solution YAML source control format reference*, <https://learn.microsoft.com/power-platform/alm/solution-source-control-yaml-format> (checked 2026-10-29). Supports: environment binding syncs every unmanaged solution in the environment to one folder; solution binding does not allow the same object in more than one solution; Git integration commits in the YAML layout.

[^3]: Microsoft Learn — *Overview of pipelines in Power Platform*, <https://learn.microsoft.com/power-platform/alm/pipelines>, and *Run pipelines in Power Platform*, <https://learn.microsoft.com/power-platform/alm/run-pipeline> (checked 2026-10-29). Supports: one solution per deployment; managed deployments only; import is an upgrade without overwriting customizations and cannot be changed to an update; the solution is exported when deployment is requested and the same artifact passes through the stages in order; pipelines run from unmanaged solutions in the source environment and from PAC CLI; preflight checks for missing dependencies; environments other than the host and developer environments must be managed environments; the deploying identity owns deployed objects.

[^4]: Microsoft Learn — *Connect to Azure DevOps* (Dataverse Git integration), <https://learn.microsoft.com/power-platform/alm/git-integration/connecting-to-azure-devops> (checked 2026-10-29). Supports: development and target environments must be managed environments.

[^5]: Microsoft Learn — *CustomAPI tables*, <https://learn.microsoft.com/power-apps/developer/data-platform/custom-api-tables>, and *Create a custom API in Power Apps*, <https://learn.microsoft.com/power-apps/developer/data-platform/create-custom-api-maker-portal> (checked 2026-11-03). Supports: `plugintypeid` holds the plug-in type that provides the custom API's main operation and can be changed after creation, unlike `uniquename`, `bindingtype`, `boundentitylogicalname`, `isfunction`, `allowedcustomprocessingsteptype` and `workflowsdkstepenabled`. *Register a plug-in*, <https://learn.microsoft.com/power-apps/developer/data-platform/register-plug-in> (checked 2026-11-03), supports: steps registered on an assembly are not added to the assembly's unmanaged solution and must be added separately; an assembly cannot be deleted while steps depend on it.

[^6]: Microsoft Learn — *pac pcf*, <https://learn.microsoft.com/power-platform/developer/cli/reference/pcf#pac-pcf-push>, *Package a code component*, <https://learn.microsoft.com/power-apps/developer/component-framework/import-custom-controls#deploy-code-components>, and *Code components ALM*, <https://learn.microsoft.com/power-apps/developer/component-framework/code-components-alm> (checked 2026-11-03). Supports: `--incremental` pushes only changed files using entity updates; `--solution-unique-name` names the solution the component is added to; without it, push creates a `PowerAppsTools_<prefix>` solution; push bypasses the versioning requirement; an update by solution import needs at least the patch version raised; debugging with the test harness and a Fiddler AutoResponder rule.

[^7]: Microsoft Learn — *Microsoft Power Platform CLI support for Power Pages*, <https://learn.microsoft.com/power-pages/configure/power-platform-cli#manifest-files> (checked 2026-11-03). Supports: `pac pages upload` uploads only content changed since the last download in the same environment, tracked by the environment manifest `<org-url>-manifest.yml`, which is meant for local development and should be git-ignored; `--forceUploadAll` uploads everything.

[^8]: Microsoft Learn — *Enhanced data model*, <https://learn.microsoft.com/power-pages/admin/enhanced-data-model>; *Use solutions with Power Pages*, <https://learn.microsoft.com/power-pages/configure/power-pages-solutions>; *Power Platform CLI solution support for Power Pages (preview)*, <https://learn.microsoft.com/power-pages/configure/power-platform-cli-solution-management>; *Site Component (powerpagecomponent) table/entity reference*, <https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/powerpagecomponent>; *How server-side caching works in Power Pages*, <https://learn.microsoft.com/power-pages/admin/clear-server-side-cache>; *Standard to enhanced data model migration overview*, <https://learn.microsoft.com/power-pages/admin/migrate-enhanced-data-model>; *Create and deploy a single-page application in Power Pages*, <https://learn.microsoft.com/power-pages/configure/create-code-sites> (all checked 2026-11-10). Supports: the enhanced data model is enabled by default and used by new sites from most templates, while four templates still create standard sites; its system tables `powerpagesite`, `powerpagesitelanguage` and `powerpagecomponent` are solution-aware; site components can be added to a solution one by one, and new ones are not added to the site's solution automatically; component type codes are read from `solutioncomponentdefinition`; `powerpagecomponenttype` values; `content` is a memo of up to 1,048,576 characters holding the component's properties, `filecontent` a file column of up to 128 MB for web files, and the table has `versionnumber`; configuration changes reach the site within 15 minutes, or at once after a cache clear from design studio Preview, `/_services/about` or a site restart; standard-model sites can be migrated with `pac pages migrate-datamodel`; single-page application sites use `pac pages upload-code-site`.

SDK message names and behaviours used in this spec (`WhoAmIRequest`, `RetrieveRequiredComponentsRequest`, `AddSolutionComponentRequest`, `RemoveSolutionComponentRequest`, `RetrieveEntityRibbonRequest`, `RetrieveApplicationRibbonRequest`, root component behaviour values, component type codes) are from working knowledge; root component behaviours, the `AddSolutionComponentRequest` flags and PAC's add behaviour were confirmed in spikes S2 and S3 (2026-10-29, [notes](notes.md#s2--addsolutioncomponentrequest-flags-and-exports)). MCP protocol details follow revisions 2026-07-28 (primary) and 2025-11-25 / 2025-06-18 (handshake fallback) of the Model Context Protocol specification (<https://modelcontextprotocol.io/specification/2026-07-28>, <https://modelcontextprotocol.io/specification/2025-11-25>, checked 2026-10-29); client behaviour is confirmed in spike S4; XrmToolBox multi-tab and connection-handling behaviour in spike S6. The transport-solution order, segmentation map and promotion pipeline in [§9.5](#95-transport-solutions-and-segmented-wip-solutions)–[§9.7](#97-close-out-strategies) are generalized from existing delivery pipeline templates; the promotion copy step's behaviour is confirmed in spike S7.
