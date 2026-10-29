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

The same loop serves every moment above: one small WIP solution per user story or task, with discovery used whenever the work reaches beyond what the transport solutions already hold.

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
| Delete WIP solution after close-out | PAC `solution delete` | Deleting an unmanaged solution keeps its components |

### Two paths for adding components

Both are specified so the choice can be made per component type in the skills, once the spikes in [Phase 0](#phase-0--spikes) have run.

| | Path A — MCP `add_solution_components` | Path B — PAC `add-solution-component` |
|---|---|---|
| Required components | Forced off, cannot be overridden | Off unless `--AddRequiredComponents` is passed — **never pass it** |
| Table subcomponents | Forced off (table added as metadata-only or shell) | **No switch exists** in the PAC reference[^1]; behaviour for tables must be confirmed in spike S2. Until proven safe, **tables must not be added through PAC** |
| Authorization | Plugin approval dialog per call | Agent client's own terminal-command approval |
| Batch | One call, one approval, a list of components | One command per component |
| Visible in plugin log | Yes | No |

Whichever path is used, `list_solution_components` runs afterwards (R1).

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
| **Connection ID** | The XrmToolBox connection's ID. Agents pass it as `connection` |

| # | Behaviour |
|---|---|
| H1 | The host is a process-wide singleton, created when the first instance opens and shared by every later instance. No instance owns it: closing the tab that created it does not stop it |
| H2 | Opening the first instance starts the host — no connection is needed and there is no separate Start step. If the port is taken (for example by a second XrmToolBox process), the host does not start and every instance shows the error and the port setting |
| H3 | The host stops when the last instance closes or XrmToolBox exits. **Stop server** / **Start server** on any instance acts on the shared host and is labelled as affecting all connections. Stopping answers pending calls with `server_stopping` and revokes every session allowance |
| H4 | Port, bearer token, response cap, maximum page size and approval timeout are host-level: one value each, shown and editable on every instance. Changing the port or token affects every connection |
| C1 | An instance binds at most one connection — the XrmToolBox connection of its tab. XrmToolBox's additional-connections feature is not used |
| C2 | Binding happens when the instance receives a connection: opened with one already selected, connected later, or changed. Only bound connections are returned by `list_connections` and accept calls. The person is expected to connect every instance they open |
| C3 | **One instance per connection.** Before binding, the instance checks the host's registry. The connection is already active when another instance is bound to the same XrmToolBox connection ID, **or** to the same organization (same organization ID) through a different saved connection. In that case the instance does not bind and shows the *Connection already active* dialog ([§8.5](#85-connection-already-active-dialog)): **Close this tab** closes the instance; **Choose another connection** re-opens the XrmToolBox connection dialog for this tab, and the new choice goes through the same check. Cancelling the connection dialog leaves the instance unbound |
| C4 | An unbound instance shows the host status and a prompt to connect. No calls are routed to it |
| C5 | When a bound instance's connection changes, the old binding ends before the new one is checked: queued and pending calls for it fail with `connection_closed`, its session allowances and dev-environment confirmation are cleared, and it disappears from `list_connections`. The new connection then goes through C3 |
| C6 | Closing a bound instance ends its binding the same way. If it was the last instance, H3 applies |
| C7 | Every tool except `list_connections` takes `connection` as its mandatory first parameter. The host routes the call to the instance bound to it; an unknown ID fails at once with `connection_not_found`, without a dialog |
| C8 | Approvals, session allowances, the dev-environment confirmation, the record-query and publisher-creation settings, the approval queue and the call log are **per connection**, held by its instance. An approval in one tab never covers a call to another connection |
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
| Authentication | Random bearer token generated each time the host starts, shared by all connections, shown masked on every instance with a copy button. Regenerate on demand. |
| Origin checks | Reject requests with a non-local `Origin` header (DNS-rebinding protection). |
| Port | Configurable; default fixed so the agent's MCP configuration stays stable. |
| Lifecycle | Singleton; starts with the first instance, stops with the last — H1 to H3 in [Host and connection model](#host-and-connection-model). |
| Connection binding | One instance per connection; every call names its connection — C1 to C10. The bound connection is shown on every approval dialog. |
| Environment guard | Per connection: when a tab is bound, the user confirms the connection is a development environment. Write tools refuse with `dev_environment_not_confirmed` without that confirmation. |
| Threading | Requests are queued per connection; SDK calls run on a background worker using a clone of that connection's service; approvals marshal to the UI thread. |
| Approval timeout | Configurable (default 120 s). Timeout returns `denied_no_response` to the agent. |

### JSON-RPC and MCP layer

The plugin must not ship a DLL next to itself (see [§10](#10-implementation-constraints)). The MCP layer is therefore either:

- **hand-rolled** — JSON-RPC 2.0 plus the MCP methods in [§6.1](#61-protocol-surface), serialized with a JSON library already loaded by XrmToolBox; or
- **source-included** from an MCP SDK, if one exists that compiles on `net48` without a private `System.Text.Json`.

Spike S1 decides. The protocol revision is pinned at build time and reported in `initialize`.

### Serialization

| Payload | Shape |
|---|---|
| Records | Human-readable JSON with typed wrappers for references, money, option sets and aliased values, plus formatted values — the value rules under [`query`](#query). Library choice and source-inclusion confirmed in spike S1. |
| Metadata | Trimmed projection (see each tool in [§6](#6-mcp-interface)) — never the raw `EntityMetadata` graph. Labels in the organization's base language. |
| XML payloads (FormXml, ribbon XML) | Returned as strings. Ribbon responses are decompressed server-side. |
| Size cap | Every response is capped (default 1 MB). Oversized list responses are cut at a whole element with `"truncated": true`; the agent narrows the request. XML payloads are never cut — they fail with `response_too_large`. |

---

## 6. MCP interface

This section is the contract the host implements and the agent skills code against. Tiers are defined in [§7](#7-authorization-model); connection routing in [Host and connection model](#host-and-connection-model).

### 6.1 Protocol surface

| Method | Behaviour |
|---|---|
| `initialize` | Returns `serverInfo` `{ "name": "agent-companion-mcp", "version": "<plugin version>" }`, the pinned protocol revision, and `capabilities: { "tools": { "listChanged": false } }`. No resources, prompts or sampling |
| `tools/list` | Every tool in [§6.3](#63-tool-summary), each with `name`, `description`, `inputSchema` and `outputSchema`. The list does not depend on which connections are bound; `create_publisher` is listed even when disabled |
| `tools/call` | Runs one tool, routed by its `connection` argument (C7) |
| `ping` | Answered by the host without approval |

The pinned revision must support `outputSchema` and `structuredContent` on tool results. Spikes S1 and S4 confirm the revision and that the target clients read structured content; the text fallback below covers clients that do not.

### 6.2 Conventions

**Connection.** Every tool except `list_connections` takes `connection` — the `connectionId` of an entry returned by `list_connections` — as a required parameter. It is the first property in each `inputSchema` and first in `required`, so clients that order arguments by schema present it first; on the wire, presence is what matters. There is no default: a call without `connection` fails with `invalid_argument` even when only one connection is bound. Agent-side rule: when exactly one connection is bound, the agent may use it and names it to the person; when more than one is bound, the agent asks which to use; it never switches connection within a task without saying so.

**Agent reason.** Every tool except `list_connections` also accepts an optional `reason` (string, at most 500 characters). It is shown in the *Agent-supplied — not verified* box of the approval dialog (R8) and recorded in the log. It never influences what the tool does. The per-tool input tables below omit it.

**Value formats**

| Kind | Format |
|---|---|
| GUID | Lowercase, hyphenated, no braces — `00000000-0000-0000-0000-000000000000`. Inputs accept any case, with or without braces |
| Date-time | ISO 8601 in UTC with `Z` — `2026-10-25T14:03:31Z` |
| Labels | Organization base language; `null` when the label is empty |
| Row versions | `versionNumber` as a decimal string — values can exceed JSON's safe integer range |
| Missing values | Omitted from records; `null` in fixed-shape metadata projections |
| Logical names | As stored (lowercase) |

**Successful result.** `structuredContent` holds the result object matching the tool's `outputSchema`; `content` holds one text block with the same JSON, for clients that do not read structured content. `isError` is `false`.

**Errors**

| Level | When | Shape |
|---|---|---|
| HTTP | Missing or wrong bearer token → `401`; non-local `Origin` → `403` | No body |
| JSON-RPC | Malformed request, unknown method, unknown tool name | Standard JSON-RPC `error` object |
| Tool | Everything else — invalid arguments, unknown connection, denial, Dataverse fault | `isError: true`; one text block holding a `ToolError` as JSON; no `structuredContent` |

| Code | Meaning | Agent's next step |
|---|---|---|
| `invalid_argument` | Missing, wrongly typed or out-of-range argument, or a rule broken (for example `<all-attributes />`). `details.argument` names it | Fix the call |
| `connection_not_found` | No plugin tab is bound to `connection` | `list_connections`; ask the person |
| `connection_closed` | The tab closed or changed connection while the call was queued or awaiting approval | `list_connections`; ask the person |
| `dev_environment_not_confirmed` | Write or Publisher tool on a connection not confirmed as development | Ask the person |
| `denied_by_user` | The person denied the call | Do not retry without new instructions |
| `denied_no_response` | Approval timed out | Do not retry without new instructions |
| `tool_disabled` | `create_publisher`, or a Record-read `query`, switched off for this connection | Ask the person |
| `not_found` | A named solution, publisher, table, column, option set, relationship or component does not exist. `details` names it | Check the name |
| `already_exists` | Unique name already taken | Choose another name, or reuse the existing one |
| `managed_solution` | Add or remove against a managed solution | Use an unmanaged solution |
| `response_too_large` | XML payload over the response cap. `details.sizeBytes`, `details.capBytes` | Narrow the request, or ask the person to raise the cap |
| `server_stopping` | The host is stopping; the call did not run | Wait for the person |
| `dataverse_error` | The platform returned a fault. `details.errorCode` (hex string, for example `0x80040217`) and `details.message` | Report to the person |

**Paging** (`query`, `list_solution_components`)

| Input | Type | Notes |
|---|---|---|
| `page` | integer | Default 1 |
| `pageSize` | integer | Default per tool; maximum is the host's max page size (default 500) |
| `pagingCookie` | string | From the previous page; omit on page 1 |

The result carries `page`, `pageSize`, `moreRecords` and `pagingCookie` (`null` when there are no more records).

**Truncation.** List results that would exceed the response cap are cut at a whole element and carry `"truncated": true`. XML payloads fail with `response_too_large` instead.

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

interface PageInfo {
  page: number;
  pageSize: number;
  moreRecords: boolean;
  pagingCookie: string | null;
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
| `list_publishers` | Read | Publishers with prefix and option value prefix |
| `list_solutions` | Read | Solutions with version, managed state and publisher; filterable |
| `list_tables` | Read | Tables with logical name, display name, custom/managed flags; filterable |
| `get_table_metadata` | Read | Columns, keys and identifiers for one table |
| `get_relationships` | Read | 1:N, N:1 and N:N relationships for one table |
| `get_option_sets` | Read | Global choices with values and labels |
| `get_entity_ribbon` | Read | Full classic ribbon for a table |
| `get_application_ribbon` | Read | Full classic application ribbon |
| `query` | Read or Record-read | FetchXML, paged; tier depends on the tables queried |
| `list_solution_components` | Read | Components in a solution, with type, ID and root behaviour; paged |
| `get_component_state` | Read | Version and modified-on for row-backed components |
| `create_solution` | Write | Empty unmanaged solution |
| `add_solution_components` | Write | Add existing components, no subcomponents, no required components |
| `remove_solution_component` | Write | Remove a component from a solution (the component itself is kept) |
| `create_publisher` | Publisher | New publisher — disabled by default |

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
    connectionId: string;            // pass as `connection`
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
  user: UserRef;                     // the user the XrmToolBox connection runs as
  devEnvironmentConfirmed: boolean;
  settings: {
    recordQueriesEnabled: boolean;   // per connection
    publisherCreationEnabled: boolean; // per connection
    maxPageSize: number;             // host
    responseCapBytes: number;        // host
    approvalTimeoutSeconds: number;  // host
  };
}
```

#### `list_publishers`

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
  truncated: boolean;
}
```

#### `list_solutions`

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
  truncated: boolean;
}
```

#### `list_tables`

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
  truncated: boolean;
}
```

#### `get_table_metadata`

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
  columns?: Column[];                // present when include has "columns"
  keys?: AlternateKey[];             // present when include has "keys"
  truncated: boolean;
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

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `logicalName` | string | ✓ | |
| `kinds` | `("oneToMany" \| "manyToOne" \| "manyToMany")[]` | | Default all three |

```ts
interface RelationshipsResult {
  logicalName: string;
  relationships: Array<OneToManyRelationship | ManyToManyRelationship>;
  truncated: boolean;
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
  notFound: string[];                // requested names that do not exist
  truncated: boolean;
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
  ribbonXml: string;                 // decompressed: RibbonDefinitions, CommandDefinitions, RuleDefinitions
  sizeBytes: number;
}
```

These responses are large. Over the response cap they fail with `response_too_large`; they are never cut.

#### `query`

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `fetchXml` | string | ✓ | `page`, `count` and `paging-cookie` attributes are set by the server and ignored if supplied |
| `page` | integer | | [Paging](#62-conventions); default 1 |
| `pageSize` | integer | | Default 50 |
| `pagingCookie` | string | | From the previous page |
| `includeFormattedValues` | boolean | | Default `true` |

```ts
interface QueryResult extends PageInfo {
  entityName: string;                // root entity of the FetchXML
  tier: "read" | "recordRead";       // the tier the call was approved under
  records: Array<Record<string, Value>>;
  truncated: boolean;
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
  "page": 1,
  "pageSize": 50,
  "moreRecords": true,
  "pagingCookie": "<cookie>",
  "entityName": "systemform",
  "tier": "read",
  "records": [
    {
      "formid": "00000000-0000-0000-0000-000000000000",
      "name": "[FormName]",
      "type": { "_option": 2, "label": "Main" },
      "objecttypecode": "account"
    }
  ],
  "truncated": false
}
```

Rules:

- **Paging from the first release.** Development tables are expected to be small, but every query is paged and the agent continues with `pagingCookie` only when it needs more.
- **Explicit columns.** `<all-attributes />` is rejected with `invalid_argument`; the agent must name columns.
- **Tier by table.** The root entity and every `link-entity` are checked against the configuration allow-list. All on the list → Read tier. Anything else → Record-read tier, or `tool_disabled` when record queries are switched off for the connection.
- **Values.** Columns without a value are omitted. Aliased values (link-entity columns) are keyed by the name the platform returns (`alias.column`) and serialized by the same rules, without the `AliasedValue` wrapper. `label`, `labels`, `name` and `formatted` are present only when `includeFormattedValues` is true.

Configuration allow-list (v1): `publisher`, `solution`, `solutioncomponent`, `systemform`, `savedquery`, `sitemap`, `appmodule`, `appmodulecomponent`, `webresource`, `ribboncustomization`, `organization`.

#### `list_solution_components`

Used after every add to compare intended against actual (R1); the comparison reads every page.

| Input | Type | Required | Notes |
|---|---|---|---|
| `connection` | string | ✓ | |
| `solutionUniqueName` | string | ✓ | Managed or unmanaged |
| `componentTypes` | integer[] | | Only these component type codes |
| `page` | integer | | [Paging](#62-conventions); default 1 |
| `pageSize` | integer | | Default 250 |
| `pagingCookie` | string | | From the previous page |

```ts
interface SolutionComponentsResult extends PageInfo {
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
  truncated: boolean;
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
| `tableBehavior` | `"metadata" \| "shell"` | | Tables only; default `metadata`. Exact mapping confirmed in spike S2 |

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
5. **Read back** and compute `unexpected` = components present now − snapshot − requested. Each unexpected component is also raised as a warning in the call log.

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

| Tier | Tools | Allow once | Allow for session | Notes |
|---|---|---|---|---|
| **Host** | `list_connections` | — | — | No dialog; logged on every tab (R2) |
| **Read** | Connection info, publishers, solutions, metadata, ribbons, configuration queries, component state, solution components | ✓ | ✓ (per tool) | |
| **Record-read** | `query` touching non-configuration tables | ✓ | ✓ (per tool) | Record queries can be switched off per connection |
| **Write** | `create_solution`, `add_solution_components`, `remove_solution_component` | ✓ | ✗ | Requires dev-environment confirmation |
| **Publisher** | `create_publisher` | ✓ with typed confirmation | ✗ | Disabled by default (R3) |

Rules:

- **Per connection.** Each tab approves only the calls to its own connection; allowances granted in one tab never cover another connection (C8).
- **Deny is a normal result.** The agent receives a tool error, `denied_by_user` or `denied_no_response`, and must not retry the same call without new instructions from the person.
- **Session allowances end** when the host stops, the connection's binding ends (tab closed or connection changed), or the user clicks **Revoke all** in that tab.
- **Arguments shown verbatim.** The dialog shows the full arguments (FetchXML pretty-printed, component lists resolved to names). The agent-supplied `reason`, if present, is shown in a separate box labelled *Agent-supplied — not verified* (R8).
- **One dialog per call.** Concurrent calls to one connection queue; the queue length is visible in its tab.

---

## 8. User experience

### 8.1 Main panel

Each tab shows its own connection on top and the shared host below it.

```
┌─ Agent Companion MCP ─────────────────────────────────────────────┐
│ This tab   : [ConnectionName] → [ClientName] DEV                  │
│              https://<org>.crm.dynamics.com                       │
│ Dev env    : [x] I confirm this is a development environment      │
│ Settings   : [x] Record queries   [ ] Publisher creation          │
│ Session allowances: get_table_metadata, query (config)            │
│                                                     [Revoke all]  │
├─ Host — shared by all tabs ───────────────────────────────────────┤
│ Server     : ● Running   http://127.0.0.1:47321/mcp               │
│ Token      : ●●●●●●●●●●●●●●●●  [Copy] [Regenerate]                │
│              [Stop server — all connections] [Copy client config] │
│ Bound      : 2 — [ClientName] DEV (this tab), [ClientName] DEV2   │
│ Limits     : Max page size [500]  Response cap [1 MB]  Timeout    │
│              [120 s]                                              │
├─ Call log — this connection ──────────────────────────────────────┤
│ Time     Tool                     Tier     Result     Duration    │
│ 14:02:11 list_publishers          Read     Allowed     0.3 s      │
│ 14:02:40 get_table_metadata       Read     Session     1.1 s      │
│ 14:03:05 create_solution          Write    Allowed     0.8 s      │
│ 14:03:31 add_solution_components  Write    Allowed  ⚠  2.4 s      │
│ 14:04:02 query (records)          Record   Denied        —        │
├─ Host log ────────────────────────────────────────────────────────┤
│ 14:01:58 list_connections                  2 bound                │
│                                                                   │
│ Pending approvals: 0          [Export log…]  [Clear]              │
└───────────────────────────────────────────────────────────────────┘
```

`⚠` marks a call that returned unexpected components or was truncated. An unbound tab shows only the host section and a **Connect** prompt.

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

For Read and Record-read calls the dialog adds **Allow for this session**. The dialog is owned by the XrmToolBox main window, so it appears whichever tab is active (C9).

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
│ Response size 2 KB · not truncated                                  │
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

### 9.2 Per user story — WIP solution

1. `create_solution` named by convention (see [Open questions](#13-open-questions)), after `list_solutions` confirms the name is free.
2. Discover the components the story touches (metadata, queries, ribbon reads).
3. Add only those components — Path A or Path B per the skill — then `list_solution_components` (R1).
4. `pac solution export` and `pac solution unpack`; record a baseline of `get_component_state` for every row-backed component in the solution.
5. Edit locally; run validation scripts.
6. **Pre-import overwrite check** — `get_component_state` again, compared with the baseline. Any difference: stop, tell the person, re-export and merge before continuing. For table and column metadata, re-export and diff instead.
7. `pac solution pack`, `pac solution import --publish-changes`.
8. Close-out: components move to the transport solution (same add rules), then `pac solution delete` removes the WIP solution while keeping its components.

### 9.3 A first-party app joins the solution

Sales, Customer Service, Field Service or another first-party app brings a new set of out-of-box components. None of them are in a transport solution yet, so this is a smaller cold start.

1. **Prerequisite, outside the plugin:** a person installs the app in the development environment. Every downstream environment needs the same app installed before any transport solution that references its components is imported.
2. `list_solutions` with `managed: "managed"` finds the app's managed solutions; `list_solution_components` on each, plus `query` over `appmodule`, `sitemap`, `systemform` and `savedquery`, shows what the app added.
3. A person decides which of those components the project will customize now. Only those are pulled in; the rest stay out until a story needs them.
4. Each change then follows the per-story loop ([§9.2](#92-per-user-story--wip-solution)). Close-out places the new components in the transport solutions that own them, under the same add rules (R1).

### 9.4 Why the overwrite check matters

Forms, views and sitemaps are imported as whole documents: an unmanaged import replaces the environment's definition with the one in the solution. If someone changed the same form after the WIP export, the import silently discards their change. The baseline comparison detects that before it happens.

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
| Tier classifier (tool + FetchXML tables → tier) | Unit tests over FetchXML samples, including link-entities and aliases |
| FetchXML rewriting (page, count, cookie; reject `all-attributes`) | Unit tests |
| Approval policy (once, session, revoke, timeout, connection change, per-connection isolation) | Unit tests with a fake clock and fake UI |
| Add-components guard (resolution before approval, fixed flags, stop on first failure, unexpected-component detection) | Unit tests with mocked `IOrganizationService` |
| Serializers (records, metadata projections, ribbon decompression) | Unit tests with recorded fixtures — synthetic, never client data |
| Tool schemas (every `inputSchema` has `connection` first and required, except `list_connections`; outputs validate against `outputSchema`) | Unit tests over `tools/list` and recorded results |
| JSON-RPC / MCP layer, error mapping | Unit tests on request/response pairs; manual test with each supported client |

---

## 11. Plan

Each phase ends with a usable, reviewable increment.

### Phase 0 — Spikes

Short, throwaway investigations against a development environment. Each produces a written finding that updates this spec.

| ID | Question | Exit criterion |
|---|---|---|
| S1 | Can an MCP host run inside XrmToolBox on `net48` without shipping a DLL? Which JSON library is safely available? Which record serializer can be source-included? Which protocol revision supports `outputSchema` and `structuredContent`? | A hello-world tool callable from the target agent clients, installed and uninstalled cleanly via the Tool Library |
| S2 | What exactly does `AddSolutionComponentRequest` produce for an out-of-box table with `DoNotIncludeSubcomponents = true`, and how is a shell-only table achieved? Does the export then carry the entity's ribbon diff? Which type code is the application ribbon? | Documented mapping of `tableBehavior` → root component behaviour → exported XML, for `account` and one custom table |
| S3 | What does PAC `add-solution-component` do for each v1 component type without `--AddRequiredComponents` — in particular for tables? | Table per component type: safe for Path B yes/no |
| S4 | Do the target agent clients (Claude Code, VS Code agent mode) connect to a local HTTP MCP server with a bearer header, read `structuredContent`, and how do they surface errors and long waits during approval? | Client configuration snippets and timeout behaviour documented |
| S5 | Which of the Read tools does the Microsoft Dataverse MCP server already provide, and could it replace any of them? Requires a dated, sourced check. | Overlap table; decision recorded |
| S6 | How does XrmToolBox host several tabs of one plugin? Is a static singleton shared across tabs (one assembly load per process)? Which hooks report a connection being set and changed, how does a plugin close its own tab, and how does it re-open the connection dialog? Is the connection ID stable across restarts, and can a dialog be owned by the main window? | Two tabs bound to different connections share one host; a duplicate is detected and both dialog actions work; closing tabs in any order leaves the registry correct |

### Phase 1 — Host, connections, approvals and log

- Singleton host: start with the first instance, stop with the last, port-in-use handling, shared token and limits.
- Connection registry: binding, the *Connection already active* dialog, unbinding on close and change, routing by `connection`.
- Main panel, per-connection dev-environment confirmation and settings.
- Approval queue and dialogs (Read and Write tiers), session allowances, revoke, timeout — per connection.
- Call log, host log and log detail; export to a user-chosen folder.
- Error model and result envelope ([§6.2](#62-conventions)).
- Tools: `list_connections`, `get_connection_info`, `list_publishers`.

**Exit:** with two tabs on different connections, `list_connections` returns both and calls route to the right tab; a third tab on an already bound connection gets the dialog and both actions work; denial, timeout and unknown connection reach the agent as tool errors; closing or changing a tab's connection revokes its allowances.

### Phase 2 — Discovery

- `list_solutions`, `list_tables`, `get_table_metadata`, `get_relationships`, `get_option_sets`.
- `query` with paging, tier classification, response cap; Record-read tier and its per-connection switch.
- `get_entity_ribbon`, `get_application_ribbon`.
- `list_solution_components`, `get_component_state`.

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
| WIP story skill | Create, add, verify, export, baseline, edit, overwrite check, import, close-out |
| Component-adding guidance | Path A vs Path B per type; R1 restated |
| Validation scripts | Schema check of `Customizations.xml` and entity files against the customization XSD; form checks (controls bound to existing columns, unique control IDs, GUID format); ribbon checks (referenced commands, rules and web resources exist) |
| Baseline and overwrite-check script | Records and compares `get_component_state` results |

**Exit:** a WIP story touching a column, a form, a view and a ribbon button is delivered through the full loop, with the platform import succeeding and no unexpected components.

### Phase 5 — Release

- Tooling README with installation, client configuration, tool table, the one-tab-per-connection behaviour, and settings.
- CI workflow, NuGet package, Tool Library listing.
- Repository updates that sit outside this plugin are listed under [Open questions](#13-open-questions); they need an editorial decision.

**Exit:** installable from the Tool Library; README matches the shipped tool surface.

---

## 12. Human review checkpoints

| Moment | What a person checks |
|---|---|
| Binding a tab to a connection | The connection is the intended **development** environment, confirmed in that tab |
| Choosing the connection for a task | With more than one bound, the agent asked which to use; the PAC profile targets the same organization |
| Publisher selection | The existing publisher is reused; a new one only with explicit certainty (R3) |
| Every Write approval | Connection, solution name, component list, fixed flags shown as OFF |
| After every add | `list_solution_components` shows nothing unexpected (R1) |
| Record-read approval | The tables and columns are needed for the task; no more |
| Before import | The overwrite check passed or a re-export was merged |
| After import | Components behave as intended in the app; the WIP solution contains only what the story touched |
| Ribbon changes | Button rules and commands reviewed against the base ribbon, not only the diff |

---

## 13. Open questions

| # | Question |
|---|---|
| Q1 | WIP solution naming convention (for example `[prefix]_WIP_<work item id>`) and friendly-name format |
| Q2 | Should large payloads (ribbons) optionally be written to an exchange folder and returned as a path? This breaks if the agent runs in WSL or a container |
| Q3 | Should the dev-environment confirmation be remembered per connection, or asked each time a tab binds? |
| Q4 | Default for record queries: enabled, or off until enabled per connection? |
| Q5 | Repository follow-ups outside the plugin — a tooling-conventions section for this plugin in the instructions file, and whether a workflow guide or matrix entry is needed. Both change shared documents and need an editorial decision |

---

## 14. References

[^1]: Microsoft Learn — *Microsoft Power Platform CLI solution command group*, <https://learn.microsoft.com/power-platform/developer/cli/reference/solution> (last updated 2026-02-25, checked 2026-10-25). Supports: `add-solution-component` options are limited to `--AddRequiredComponents`, `--async` and `--environment`; no create command exists; `unpack` defaults to the XML layout while `clone` and Git integration use the YAML layout.

SDK message names and behaviours used in this spec (`AddSolutionComponentRequest`, `RemoveSolutionComponentRequest`, `RetrieveEntityRibbonRequest`, `RetrieveApplicationRibbonRequest`, root component behaviour values, component type codes) are from working knowledge and are confirmed in spikes S2 and S3 before implementation. MCP protocol details (`outputSchema`, `structuredContent`, `isError`) are confirmed in spikes S1 and S4; XrmToolBox multi-tab and connection-handling behaviour in spike S6.
