# Agent Companion MCP — specification and plan

> **Status:** Draft · **Date:** 2026-10-25 · **Updated:** 2026-10-29 · **Plugin:** `AgentCompanionMcp` · **Intended path:** `tooling/AgentCompanionMcp/SPEC.md`

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

At each of these moments the agent has no ground truth for the components it needs: it cannot see them, their IDs, or their current definitions.

The plugin closes that gap. It lets the agent:

- discover publishers, solutions, out-of-box tables, relationships, option sets, forms, views, ribbons and records;
- create empty unmanaged solutions in Dataverse and add existing components to them **without** subcomponents or required components;
- check, before re-import, whether a component changed in the environment since it was exported.

From there, PAC CLI exports and unpacks the solution, the agent edits it locally, and PAC CLI packs and imports it. The platform's import is the final validation.

The same loop serves every moment above: a small set of WIP solutions per user story or task — one for each transport solution the story touches ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) — with discovery used whenever the work reaches beyond what the transport solutions already hold.

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
| R2 | **Every call is visible and authorized by a person** before it runs. The one exception is `list_connections`, which returns only the connections the person bound to plugin tabs and is logged but not individually approved | Prompt injection; agent error |
| R3 | **Publisher creation is always confirmed individually** — never allowed for a session, and disabled by default | A second publisher, even with the same prefix, creates layering problems that cannot be solved |
| R4 | **Modern commanding (Power Fx command bar) is out of scope** — classic ribbon only | Creates chicken-and-egg dependencies between base solutions and app solutions |
| R5 | **Unpacked format is the XML layout produced by `pac solution unpack`** — not the YAML source-control layout | One format for all skills and scripts |
| R6 | **Late-bound SDK access only** — no early-bound classes | Tables change too fast to keep generated classes current |
| R7 | **No client data in the repository** — logs, exchange files and samples stay outside the working tree | Client data boundary |
| R8 | **Agent-supplied text is untrusted** — never rendered as instructions, always labelled as coming from the agent | Prompt injection |
| R9 | **One plugin tab per connection; every call names its connection** — no default connection | Approvals for an environment happen in one place; a call never lands on the wrong environment |
| R10 | **One transport group per WIP solution; one transport home per component** — placement follows the project's segmentation map ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) | A component in the wrong transport solution is costly to move once deployed (X4) |
| R11 | **WIP solutions are never deployed downstream** — only transport solutions are | A deployed WIP becomes a managed layer that ties its components' lifetime to the WIP (D1) |

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
| Creating or changing components directly (columns, forms, views, ribbons) | Authored locally in the unpacked solution and imported through PAC CLI |
| A general SDK facade (arbitrary `OrganizationRequest` execution) | Not needed for the workflow; large risk surface |
| Record create, update, delete | Not needed for the workflow |
| Modern commanding | R4 |
| Managed solutions, patches, holding solutions | Not part of the WIP workflow |
| XrmToolBox's additional-connections feature | One tab binds one connection (R9) |

---

## 4. Responsibility split — MCP vs PAC CLI

| Step | Owner | Notes |
|---|---|---|
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
| Close-out — promote, add to transport, or commit | Depends on the project's strategy — [§9.7](#97-close-out-strategies) | Each WIP feeds one transport solution (R10) |
| Delete WIP solution after close-out | PAC `solution delete` | Deleting an unmanaged solution keeps its components |

### Two paths for adding components

Both are specified so the choice can be made per component type in the skills, once the spikes in [Phase 0](#phase-0--spikes) have run.

| | Path A — MCP `add_solution_components` | Path B — PAC `add-solution-component` |
|---|---|---|
| Required components | Forced off, cannot be overridden | Off unless `--AddRequiredComponents` is passed — **never pass it** |
| Table subcomponents | Forced off (table added as metadata-only or shell) | **No switch exists** in the PAC reference[^1], and spike S3 showed PAC adds a table with **all its subcomponents** (root behaviour 0). **Tables must never be added through PAC.** Columns, forms and views added through PAC on their own are safe: the platform adds their table as a shell (behaviour 2), exactly as the SDK does |
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
| C8 | Read approvals and dev-environment confirmations are scoped by organization ([§7](#7-authorization-model)). The record-query and publisher-creation settings, the approval queue and the call log are **per connection**, held by its instance. An approval given for one organization never covers a call to another, except an approval the person explicitly scopes as *always* |
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
| `server/discover` | Returns `supportedVersions: ["2026-07-28"]`, `capabilities: { "tools": { "listChanged": false } }`, `_meta["io.modelcontextprotocol/serverInfo"]` `{ "name": "agent-companion-mcp", "version": "<plugin version>" }`, short `instructions`, and `ttlMs` with `cacheScope: "private"`. No resources, prompts or sampling. Answered without approval |
| `tools/list` | Every tool in [§6.3](#63-tool-summary), in a fixed order, each with `name`, `title`, `description`, `inputSchema` and `outputSchema`, plus `ttlMs` and `cacheScope: "private"`. The list does not depend on which connections are bound; `create_publisher` is listed even when disabled |
| `tools/call` | Runs one tool, routed by its `connection` argument (C7) |

Every 2026-07-28 result carries `resultType: "complete"` and `_meta["io.modelcontextprotocol/serverInfo"]`.

**Handshake-era fallback**

| Method | Behaviour |
|---|---|
| `initialize` | Agrees the client's `protocolVersion` when it is `2025-11-25` or `2025-06-18`; otherwise offers `2025-11-25`, and the client decides whether to continue. Returns `protocolVersion`, `capabilities: { "tools": { "listChanged": false } }`, `serverInfo` and `instructions`, and issues a session in the `Mcp-Session-Id` response header |
| `notifications/initialized` | HTTP `202` |
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

State that spans calls — the `connection` and paging `cursor` handles — travels as ordinary tool arguments in both eras, as revision 2026-07-28 recommends.

### 6.2 Conventions

**Connection.** Every tool except `list_connections` takes `connection` — the `connectionId` of an entry returned by `list_connections` — as a required parameter. It is the first property in each `inputSchema` and first in `required`, so clients that order arguments by schema present it first; on the wire, presence is what matters. There is no default: a call without `connection` fails with `invalid_argument` even when only one connection is bound. Agent-side rule: when exactly one connection is bound, the agent may use it and names it to the person; when more than one is bound, the agent asks which to use; it never switches connection within a task without saying so.

**Agent reason.** Every tool except `list_connections` also accepts an optional `reason` (string, at most 500 characters). It is shown in the *Agent-supplied — not verified* box of the approval dialog (R8) and recorded in the log. It never influences what the tool does. The per-tool input tables below omit it, and omit the paging inputs `cursor` and `pageSize`; paged tools are marked *Paged*.

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
| `tool_disabled` | `create_publisher`, or a `query` that touches records, switched off for this connection | Ask the person |
| `not_found` | A named solution, publisher, table, column, option set, relationship or component does not exist. `details` names it | Check the name |
| `already_exists` | Unique name already taken | Choose another name, or reuse the existing one |
| `managed_solution` | Add or remove against a managed solution | Use an unmanaged solution |
| `invalid_cursor` | The cursor is unknown, expired, or belongs to another connection or tool | Start again from the first page |
| `server_stopping` | The host is stopping; the call did not run | Wait for the person |
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
  };
  user: UserRef;                     // the user the XrmToolBox connection runs as; full name only —
                                     // first and last names come from who_am_i
  devEnvironmentConfirmed: boolean;
  devEnvironmentScope: "once" | "session" | "environment" | "always" | null;
  readApprovalScope: "session" | "environment" | "always" | null;  // null: every Read call prompts
  settings: {
    recordQueriesEnabled: boolean;   // per connection
    publisherCreationEnabled: boolean; // per connection
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
- **Record detection.** The root entity and every `link-entity` are checked against the configuration allow-list. Anything outside it marks the query as touching records: the approval dialog and the log say so, and the call fails with `tool_disabled` when record queries are switched off for the connection. Approval is the same single Read approval as every other read ([§7](#7-authorization-model)).
- **Values.** Columns without a value are omitted. Aliased values (link-entity columns) are keyed by the name the platform returns (`alias.column`) and serialized by the same rules, without the `AliasedValue` wrapper. `label`, `labels`, `name` and `formatted` are present only when `includeFormattedValues` is true.

Configuration allow-list (v1): `publisher`, `solution`, `solutioncomponent`, `systemform`, `savedquery`, `sitemap`, `appmodule`, `appmodulecomponent`, `webresource`, `ribboncustomization`, `organization`, `workflow`, `customcontrol`, `environmentvariabledefinition`, `connectionreference`, `pluginassembly`, `sdkmessageprocessingstep`, `customapi`, `role`, `fieldsecurityprofile`. The entries from `workflow` onwards let the agent read the subtype and name columns that the segmentation map uses ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)).

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
    modifiedOn: DateTime | null;
    modifiedBy: UserRef | null;
  }>;                                // in request order
}
```

Table and column metadata carry no comparable row version; for those, the overwrite check falls back to a PAC re-export and diff.

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

All Write tools fail with `dev_environment_not_confirmed` on a connection not confirmed as development, before any dialog is shown.

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
  | { type: 10; schemaName: string };                   // relationship
```

Component type codes accepted in v1 (confirmed against the `solutioncomponent` type option set in spike S2): table `1`, column `2`, global option set `9`, relationship `10`, view `26`, form `60`, web resource `61`, sitemap `62`, app module `80`, application ribbon customization (code to confirm).

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
4. **Add** in request order with the fixed behaviour below. The first failure stops the run; later entries are `notAttempted`.
5. **Read back** and compute `unexpected` = components present now − snapshot − requested. Each unexpected component is also raised as a warning in the call log. One addition is expected, not unexpected: when a column, form or view is added without its table, the platform adds the table as a shell (root behaviour 2) to hold it (spike S2). Such a parent shell is reported in `results` with status `added` and `name` noting it was added as the parent, and is not counted in `unexpected` — unless it arrives with any behaviour other than 2.

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

Disabled unless the user enables it for the connection. When disabled, the tool returns `tool_disabled`, telling the agent that a person must create or choose the publisher. When enabled, it raises the publisher dialog ([§8.3](#83-publisher-creation-dialog)); a wrong typed confirmation counts as `denied_by_user`. Requires dev-environment confirmation. Fails with `already_exists` if the unique name is taken; a prefix shared with an existing publisher is warned about in the dialog, not refused.

---

## 7. Authorization model

The plugin's approval is the authoritative gate. The agent client's own tool-approval prompt is a convenience and is not relied on.

| Tier | Tools | Approval | Notes |
|---|---|---|---|
| **Host** | `list_connections` | None | No dialog; logged on every tab (R2) |
| **Read** | Every read tool — connection info, identity, publishers, solutions, metadata, ribbons, queries (configuration and records), component state, solution components, required components | One prompt for all reads, scoped *once*, *session*, *environment* or *always* | Record queries can be switched off per connection |
| **Write** | `create_solution`, `add_solution_components`, `remove_solution_component` | Once, per call | Requires dev-environment confirmation |
| **Publisher** | `create_publisher` | Once, with typed confirmation | Disabled by default (R3) |

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

Environment and always scopes persist between sessions in XrmToolBox's plugin settings, which record only organization IDs, friendly names and scopes, plus the host's bearer token encrypted with DPAPI for the current Windows user — no arguments or results.

### Rules

- **Scoped by organization.** Session and environment scopes belong to an organization, not to a tab; a tab that rebinds to the same organization finds them in place (C8).
- **Deny is a normal result.** The agent receives a tool error, `denied_by_user` or `denied_no_response`, and must not retry the same call without new instructions from the person.
- **Arguments shown verbatim.** The dialog shows the full arguments (FetchXML pretty-printed, component lists resolved to names) and flags a query that touches records. The agent-supplied `reason`, if present, is shown in a separate box labelled *Agent-supplied — not verified* (R8).
- **One dialog per call.** Concurrent calls to one connection queue; the queue length is visible in its tab.

---

## 8. User experience

### 8.1 Main panel

Each tab shows its own connection on top and the shared host below it.

```
┌─ Agent Companion MCP ─────────────────────────────────────────────┐
│ This tab   : [ConnectionName] → [ClientName] DEV                  │
│              https://<org>.crm.dynamics.com                       │
│ Dev env    : Confirmed — this environment, remembered  [Clear]    │
│ Settings   : [x] Record queries   [ ] Publisher creation          │
│ Reads      : Approved — this environment, remembered              │
│                                                      [Revoke]     │
├─ Host — shared by all tabs ───────────────────────────────────────┤
│ Server     : ● Running   http://127.0.0.1:47321/mcp               │
│ Token      : ●●●●●●●●●●●●●●●●  [Copy] [Regenerate]                │
│              [Stop server — all connections] [Copy client config] │
│ Bound      : 2 — [ClientName] DEV (this tab), [ClientName] DEV2   │
│ Limits     : Max page size [500]  Response cap [40K]  Timeout     │
│              [120 s]                                              │
├─ Call log — this connection ──────────────────────────────────────┤
│ Time     Tool                     Tier     Result     Duration    │
│ 14:02:11 list_publishers          Read     Allowed     0.3 s      │
│ 14:02:40 get_table_metadata       Read     Env         1.1 s      │
│ 14:03:05 create_solution          Write    Allowed     0.8 s      │
│ 14:03:31 add_solution_components  Write    Allowed  ⚠  2.4 s      │
│ 14:04:02 query (records)          Read     Denied        —        │
├─ Host log ────────────────────────────────────────────────────────┤
│ 14:01:58 list_connections                  2 bound                │
│                                                                   │
│ Pending approvals: 0          [Export log…]  [Clear]              │
└───────────────────────────────────────────────────────────────────┘
```

`⚠` marks a call that returned unexpected components. An unbound tab shows only the host section and a **Connect** prompt.

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

The persisted log records arguments and result summaries (counts, sizes, warnings), not result payloads. It is written to a user-chosen folder outside any repository (R7).

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

---

## 9. Workflows

Every workflow starts by settling the connection ([§6.2](#62-conventions)); every MCP call that follows passes it.

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

### 9.2 Per user story — WIP solutions

1. Discover the components the story touches (metadata, queries, ribbon reads).
2. Classify each with the project's segmentation map, and check whether it already has a transport home (W1, W2). Check for overlap with other open WIP solutions (W7).
3. `create_solution` once for each transport group the story touches, named `<initials>_Agent_WIP_<work item id>_<Group>` with initials from `who_am_i` ([§9.5](#95-transport-solutions-and-segmented-wip-solutions)) and under the destination's publisher (W3), after `list_solutions` confirms the names are free.
4. Add each component to its group's WIP — Path A or Path B per the skill — then `list_solution_components` on each WIP (R1).
5. `pac solution export` and `pac solution unpack` for each WIP; record a baseline of `get_component_state` for every row-backed component.
6. Edit locally; run validation scripts.
7. **Pre-import overwrite check** — `get_component_state` again, compared with the baseline. Any difference: stop, tell the person, re-export and merge before continuing. For table and column metadata, re-export and diff instead.
8. `pac solution pack`, `pac solution import --publish-changes` — one WIP at a time, in transport order (W5).
9. Close-out using the project's strategy ([§9.7](#97-close-out-strategies)): all of the story's WIPs together, in transport order (X8). Then `pac solution delete` removes each WIP from development and keeps its components.

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

**WIP naming.** `<initials>_Agent_WIP_<work item id>_<Group>` — for example `[XY]_Agent_WIP_1234_Base`.

| Part | Rule |
|---|---|
| `<initials>` | First letter of the first name and first letter of the last name, from `who_am_i` on the connection where the WIP is created. Uppercase ASCII: accented letters lose the accent; any other character is dropped |
| `Agent_WIP` | Literal. Marks a WIP shaped by an agent through the plugin, as distinct from WIPs people create by hand |
| `<work item id>` | The work item's numeric ID |
| `<Group>` | The transport group's name in the segmentation map — `Resources`, `Base`, `Assemblies`, `Processes`, `Apps`, `Security`, or the map's name for the shared-library solution |
| Friendly name | The unique name with each underscore replaced by a space — `[XY] Agent WIP 1234 Base` |

The agent stops and asks the person for initials when `firstName` or `lastName` is empty, when `isApplicationUser` is true, or when `impersonating` is true — the WIP belongs to the person doing the work, not to the identity the connection runs as. The name must also pass `create_solution`'s rules (letters, digits and underscores, starting with a letter, at most 65 characters) and W4. Before creating, `list_solutions` confirms the name is free; a clash, such as two people with the same initials on the same work item and group, is resolved by the person.

| # | WIP rule | Why |
|---|---|---|
| W1 | **One group per WIP.** Before adding, the agent classifies every component with the project's map, including subtype rules, and adds it to the WIP for that group | A promotion validator rejects mixed WIPs; a component in the wrong group lands in the wrong transport solution, which is costly to undo (X4) |
| W2 | **A component already in a transport solution stays in that group**, even if the map has changed since. The agent looks for an existing transport home in `solutioncomponent` first | Each component has exactly one transport home |
| W3 | **Same publisher as the destination.** A WIP uses the publisher of the transport solution it feeds — the project publisher, or the shared library's publisher for that solution | Promotion validation requires matching prefixes; components created while working in the WIP take its publisher's prefix |
| W4 | **WIP names never start with a protected prefix.** A promotion pipeline can refuse WIP names that start with the project's, a partner's or the platform's prefix, so that no transport solution is promoted as if it were WIP. Leading with initials makes a clash unlikely, but the agent still checks the name against the project's protected prefixes and asks the person if one matches | A transport solution promoted as WIP would be copied into another transport solution and then deleted from the build environment |
| W5 | **Work in transport order.** Create, import and close out a story's WIPs in transport order — Resources before Base, Base before Assemblies, and so on | Each WIP excludes required components (R1), so the target must already hold what it depends on |
| W6 | **Dependencies point down the order.** No component may require one in a later group. Where one does — for example a Base form embedding a canvas app that the map places in Apps — stop and ask the person. `get_required_components` finds these. The fix is a placement decision, not a workaround | A dependency pointing up breaks the import of the earlier solution in every downstream environment |
| W7 | **Check overlap with other open WIPs.** Before adding a whole-document component (form, view, sitemap, web resource, workflow, app module, table), the agent queries `solutioncomponent` for the same object in other WIP solutions and reports any overlap | Two stories editing one document ship each other's in-flight changes (X1) |
| W8 | **Never in a WIP:** record creation and update rules and their generated cloud flows; managed tables with all subcomponents (also R1); components carrying another publisher's prefix | Known managed-deployment failures; validator rejections; layering |

How the plugin supports this: `list_solutions` finds open WIPs and the transport solutions with their publishers. `query` over `solutioncomponent` finds a component's existing home and any overlapping WIPs. `query` over `systemform` and `workflow` reads the subtype columns the map uses. `get_required_components` checks dependencies against the order. `create_solution` and `add_solution_components` shape one WIP per group. The plugin neither reads, tracks nor enforces the segmentation map: placement is the agent's responsibility, guided by its skill.

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

### 9.7 Close-out strategies

**v1 support.** The v1 skills support strategies **A** and **B**. Strategies C and D are recorded here for a later assessment with teams that use them; until then their considerations are analysis, not experience-based recommendations.

|  | A — Promote to a build environment | B — Add to transport solutions in development | C — Add to Git-linked solutions | D — Add to solutions deployed by Power Platform pipelines |
|---|---|---|---|---|
| What happens to the WIP | Exported unmanaged, imported into the build environment, copied there into one transport solution, then deleted there | Its components are added to the transport solution in development; the WIP is deleted | Its components are added to a solution bound to Git; a person commits from the maker portal | Its components are added to a transport solution in the pipeline's source environment |
| Source of downstream artifacts | Transport solutions extracted from the build environment | Transport solutions extracted from development | The Git repository, YAML layout[^2] | The solution the pipeline exports when deployment is requested[^3] |
| Isolation from in-flight work | Yes, apart from shared whole documents (X1) | No (X2) | No (X2) | Only when the source is a build environment fed by A |
| Transport order enforced by | The person promoting; multi-solution deployment pipelines | Multi-solution deployment pipelines | The build from the repository | The person requesting deployments — one solution per deployment[^3] |
| R1 check after close-out | `list_solution_components` through a read-only build connection | `list_solution_components` on development | The same, plus review of the commit | The same, on the source environment |

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
| Approval policy (read scopes and dev-confirmation scopes — once, session, environment, always; persistence; revoke; timeout; per-organization isolation) | Unit tests with a fake clock, fake UI and fake settings store |
| Paging (cursor binding to connection, tool and arguments; cap-limited pages; XML chunking; snapshot consistency; expiry) | Unit tests with recorded fixtures and a fake clock |
| Add-components guard (resolution before approval, fixed flags, stop on first failure, unexpected-component detection) | Unit tests with mocked `IOrganizationService` |
| Serializers (records, metadata projections, ribbon decompression) | Unit tests with recorded fixtures — synthetic, never client data |
| Tool schemas (every `inputSchema` has `connection` first and required, except `list_connections`; outputs validate against `outputSchema`) | Unit tests over `tools/list` and recorded results |
| JSON-RPC / MCP layer, error mapping, both protocol eras | Unit tests on request/response pairs — start from the thirty-one cases in the spike's [Invoke-ProtocolChecks.ps1](Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1); manual test with Claude Code (current and a handshake-era release) and VS Code |
| Handshake sessions (issue, continue, expire, `DELETE`, unknown session) | Unit tests with a fake clock |
| Token store (generate once, DPAPI round trip, regenerate, missing or corrupt setting) | Unit tests over a fake settings store |
| Binding order (connection ID claimed synchronously in hook order; organization check after the identity call; `UpdateConnection` before `Load`) | Unit tests with fake instances and a controllable identity call |

---

## 11. Plan

Each phase ends with a usable, reviewable increment.

### Phase 0 — Spikes

Short, throwaway investigations against a development environment. Each produces a written finding that updates this spec. Tracks, sequencing, shared setup and findings are kept in [notes.md](notes.md); the spike code is in [Spikes/](Spikes/).

**Status (2026-10-30): done for v1.** S1–S6 are complete and applied to this spec. **S7 is in cold storage** — no build environment with the promotion pipeline is available; until it runs, A1's R1 check after every promotion stays mandatory. S8 stays deferred with strategy C. The spike solutions and the test ribbon button have been removed from the sandbox.

| ID | Question | Exit criterion |
|---|---|---|
| S1 | Can an MCP host run inside XrmToolBox on `net48` without shipping a DLL? Which JSON library is safely available? Which record serializer can be source-included? Which protocol revision supports `outputSchema` and `structuredContent`? | A hello-world tool callable from the target agent clients, installed and uninstalled cleanly via the Tool Library |
| S2 | What exactly does `AddSolutionComponentRequest` produce for an out-of-box table with `DoNotIncludeSubcomponents = true`, and how is a shell-only table achieved? Does the export then carry the entity's ribbon diff? Which type code is the application ribbon? | Documented mapping of `tableBehavior` → root component behaviour → exported XML, for `account` and one custom table |
| S3 | What does PAC `add-solution-component` do for each v1 component type without `--AddRequiredComponents` — in particular for tables? | Table per component type: safe for Path B yes/no |
| S4 | Do the target agent clients (Claude Code, VS Code agent mode) connect to a local HTTP MCP server with a bearer header, read `structuredContent`, and how do they surface errors and long waits during approval? | Client configuration snippets and timeout behaviour documented |
| S5 | Which of the Read tools does the Microsoft Dataverse MCP server already provide, and could it replace any of them? Requires a dated, sourced check. | Overlap table; decision recorded |
| S6 | How does XrmToolBox host several tabs of one plugin? Is a static singleton shared across tabs (one assembly load per process)? Which hooks report a connection being set and changed, how does a plugin close its own tab, and how does it re-open the connection dialog? Is the connection ID stable across restarts, and can a dialog be owned by the main window? | Two tabs bound to different connections share one host; a duplicate is detected and both dialog actions work; closing tabs in any order leaves the registry correct |
| S7 | *Cold storage — no build environment.* Does the promotion pipeline's component copy preserve each component's root component behaviour, and does it ever add required components or subcomponents? | `list_solution_components` on the build environment before and after promoting a WIP holding a metadata-only table, a column, a form and a web resource matches what was intended |
| S8 | *Deferred with strategy C.* With Dataverse Git integration, does environment binding commit WIP solutions as solutions in their own right, and can an unbound WIP hold objects of solutions bound under solution binding? | Documented behaviour, and the recommended binding for projects that use WIP solutions |

### Phase 1 — Host, connections, approvals and log

**Carried from Phase 0.** The spikes settled these points; Phase 1 builds on them rather than re-deciding them. Details and evidence are in [notes.md](notes.md#phase-0-outcome--handover-to-phase-1).

| Area | What Phase 1 must do | Source |
|---|---|---|
| Protocol layer | Hand-rolled on `HttpListener` and XrmToolBox's `Newtonsoft.Json` — no MCP SDK, no shipped DLL. Dual-era: 2026-07-28 primary, 2025-11-25 / 2025-06-18 through `initialize`. Start from the spike's [McpLayer](Spikes/McpLayer/) and its thirty-one protocol checks | S1, S4, D1, D3, D7 |
| Host lifecycle | Process-wide static singleton. Attach on whichever comes first, `UpdateConnection` or `Load`; stop when the last tab closes. Static state survives a host stop and restart within one XrmToolBox session | S6, H1–H3 |
| Binding | Claim the connection ID synchronously inside `UpdateConnection`, in hook order; resolve the organization ID with the asynchronous identity call afterwards. Mint a connection ID when XrmToolBox has none | S6, C3, D4 |
| Dialogs | Own every dialog by XrmToolBox's main window (`TopLevelControl`), so it appears whichever tab is active. Do not rely on the control's `Visible` — it reports `True` for a background tab | S6, C9 |
| Token | Persist across restarts with DPAPI; plain-text `401` body; README guidance for OAuth prompts | S4, D8 |
| Response cap | 40,000 characters, measured on the text content block | S4, D2, D6 |
| Cancellation | `HttpListener` gives no signal when a client abandons a request; the spike only noticed when the reply failed to write. Phase 1 must choose how to withdraw a pending approval dialog when the client goes away — for example answering long calls as an SSE stream with periodic keep-alive comments, whose failed write signals the disconnect. This touches the "no SSE" rule in §6.1 and is a decision for Phase 1 | S1 |
| Clients | Supported and tested: Claude Code 2.1.312 (2026-07-28), Claude Code 2.1.72 and VS Code 1.144.0 (handshake fallback) | S4 |

- Singleton host: start with the first tab, stop with the last, port-in-use handling, shared limits, persisted token (D8).
- Dual-era MCP layer ([§6.1](#61-protocol-surface)), including handshake sessions.
- Connection registry: synchronous claim, binding, the *Connection already active* dialog, unbinding on close and change, routing by `connection`.
- Main panel, per-connection dev-environment confirmation and settings.
- Approval queue and dialogs (Read and Write tiers); read-approval and dev-confirmation scopes with their persistence; revoke; timeout.
- Call log, host log and log detail; export to a user-chosen folder.
- Error model and result envelope ([§6.2](#62-conventions)).
- Tools: `list_connections`, `get_connection_info`, `who_am_i`, `list_publishers`.

**Exit:** with two tabs on different connections, `list_connections` returns both and calls route to the right tab; a third tab on an already bound connection gets the dialog and both actions work; a toolbar connect applied to several tabs binds the first in XrmToolBox's order; denial, timeout and unknown connection reach the agent as tool errors; a once-scoped approval ends with its call, a session scope ends with the host, and Revoke clears environment scopes; Claude Code (2026-07-28) and VS Code (handshake fallback) both call a tool, and both still connect after an XrmToolBox restart without reconfiguration.

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

**Exit:** the cold-start workflow ([§9.1](#91-cold-start--first-base-solution)) runs end to end on a clean development environment, and `list_solution_components` shows exactly the intended components.

### Phase 4 — Agent skills and scripts

Agent-side work that makes the local edits safe. Skills decide between Path A and Path B per component type using the S2 and S3 findings.

| Deliverable | Purpose |
|---|---|
| Cold-start skill | Connection choice, PAC profile match, publisher choice, base solutions, first components |
| WIP story skill | Classify by the segmentation map, create one WIP per group, add, verify, export, baseline, edit, overwrite check, import in transport order, close-out by strategy A or B ([§9.7](#97-close-out-strategies)) |
| Component-adding guidance | Path A vs Path B per type; R1 and R10 restated; W1–W8 |
| Overlap and dependency checks | W6, W7 and X5 before close-out: overlapping open WIPs, dependencies pointing up the order, dependencies not yet placed |
| Validation scripts | Schema check of `Customizations.xml` and entity files against the customization XSD; form checks (controls bound to existing columns, unique control IDs, GUID format); ribbon checks (referenced commands, rules and web resources exist) |
| Baseline and overwrite-check script | Records and compares `get_component_state` results |

**Exit:** a WIP story touching a column, a form, a view, a web resource and a ribbon button is delivered through the full loop — two WIPs, Resources and Base, imported and closed out in order — with the platform import succeeding and no unexpected components.

### Phase 5 — Release

- Tooling README with installation, client configuration (snippets in [Spikes/README.md](Spikes/README.md#client-registration-s4)), minimum client versions, what to do when a client shows an OAuth prompt, tool table, the one-tab-per-connection behaviour, and settings.
- Tool Library install and uninstall from the NuGet package — the part of S1's exit criterion the spike could not cover.
- CI workflow, NuGet package, Tool Library listing.
- Repository updates outside this plugin: the instructions file's tooling-conventions section, the codebase map, the root README and the matrix's build-backlog row already describe the plugin as specified, not built — change them to describe it as shipped. Add the column-3 guide changes then, not before: registering the plugin's MCP endpoint in [C.3 §7](../../workflows/3-coding-assistants/c.3-project-setup.md#7-wire-up-mcp-and-finish-grounding), and the segmented WIP loop where [4.3](../../workflows/3-coding-assistants/4.3-implementation.md) covers solution changes. Adding a guide also updates the matrix, the root README table and the column README together.

**Exit:** installable from the Tool Library; README matches the shipped tool surface.

---

## 12. Human review checkpoints

| Moment | What a person checks |
|---|---|
| Binding a tab to a connection | The connection is the intended **development** environment, confirmed in that tab |
| Choosing the connection for a task | With more than one bound, the agent asked which to use; the PAC profile targets the same organization |
| Publisher selection | The existing publisher is reused; a new one only with explicit certainty (R3) |
| Classifying a story's components | Each component's group follows the segmentation map; components already in a transport solution stay there (W1, W2) |
| Overlap with other open WIPs | Shared whole documents are sequenced or reviewed (W7, X1) |
| Every Write approval | Connection, solution name, component list, fixed flags shown as OFF |
| After every add | `list_solution_components` shows nothing unexpected (R1) |
| Read approval scope | Per environment only for development environments; per session for production; *always* only with a reason ([§7](#7-authorization-model)) |
| A query that touches records | The tables and columns are needed for the task; no more |
| Before import | The overwrite check passed or a re-export was merged |
| After import | Components behave as intended in the app; the WIP solution contains only what the story touched |
| Ribbon changes | Button rules and commands reviewed against the base ribbon, not only the diff |
| Close-out | All of the story's WIPs, in transport order; no dependency points up the order; deletions handled by hand; no WIP deployed downstream (W5, W6, X3, X8, R11) |

---

## 13. Open questions

None open.

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

---

## 14. References

[^1]: Microsoft Learn — *Microsoft Power Platform CLI solution command group*, <https://learn.microsoft.com/power-platform/developer/cli/reference/solution> (last updated 2026-02-25, checked 2026-10-25). Supports: `add-solution-component` options are limited to `--AddRequiredComponents`, `--async` and `--environment`; no create command exists; `unpack` defaults to the XML layout while `clone` and Git integration use the YAML layout.

[^2]: Microsoft Learn — *Connect to Git*, <https://learn.microsoft.com/power-platform/alm/git-integration/connecting-to-git>, and *Solution YAML source control format reference*, <https://learn.microsoft.com/power-platform/alm/solution-source-control-yaml-format> (checked 2026-10-29). Supports: environment binding syncs every unmanaged solution in the environment to one folder; solution binding does not allow the same object in more than one solution; Git integration commits in the YAML layout.

[^3]: Microsoft Learn — *Overview of pipelines in Power Platform*, <https://learn.microsoft.com/power-platform/alm/pipelines>, and *Run pipelines in Power Platform*, <https://learn.microsoft.com/power-platform/alm/run-pipeline> (checked 2026-10-29). Supports: one solution per deployment; managed deployments only; import is an upgrade without overwriting customizations and cannot be changed to an update; the solution is exported when deployment is requested and the same artifact passes through the stages in order; pipelines run from unmanaged solutions in the source environment and from PAC CLI; preflight checks for missing dependencies; environments other than the host and developer environments must be managed environments; the deploying identity owns deployed objects.

[^4]: Microsoft Learn — *Connect to Azure DevOps* (Dataverse Git integration), <https://learn.microsoft.com/power-platform/alm/git-integration/connecting-to-azure-devops> (checked 2026-10-29). Supports: development and target environments must be managed environments.

SDK message names and behaviours used in this spec (`WhoAmIRequest`, `RetrieveRequiredComponentsRequest`, `AddSolutionComponentRequest`, `RemoveSolutionComponentRequest`, `RetrieveEntityRibbonRequest`, `RetrieveApplicationRibbonRequest`, root component behaviour values, component type codes) are from working knowledge; root component behaviours, the `AddSolutionComponentRequest` flags and PAC's add behaviour were confirmed in spikes S2 and S3 (2026-10-29, [notes](notes.md#s2--addsolutioncomponentrequest-flags-and-exports)). MCP protocol details follow revisions 2026-07-28 (primary) and 2025-11-25 / 2025-06-18 (handshake fallback) of the Model Context Protocol specification (<https://modelcontextprotocol.io/specification/2026-07-28>, <https://modelcontextprotocol.io/specification/2025-11-25>, checked 2026-10-29); client behaviour is confirmed in spike S4; XrmToolBox multi-tab and connection-handling behaviour in spike S6. The transport-solution order, segmentation map and promotion pipeline in [§9.5](#95-transport-solutions-and-segmented-wip-solutions)–[§9.7](#97-close-out-strategies) are generalized from existing delivery pipeline templates; the promotion copy step's behaviour is confirmed in spike S7.
