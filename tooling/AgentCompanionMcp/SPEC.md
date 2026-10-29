# Agent Companion MCP — specification and plan

> **Status:** Draft · **Date:** 2026-10-25 · **Plugin:** `AgentCompanionMcp` · **Intended path:** `tooling/AgentCompanionMcp/SPEC.md`

An XrmToolBox plugin that exposes a local Model Context Protocol (MCP) endpoint over the active XrmToolBox connection. It gives a coding agent the few Dataverse operations PAC CLI cannot perform, so the agent can bootstrap and maintain small, unmanaged work-in-progress (WIP) solutions locally. Every call is shown in a visible log and authorized by a person before it runs.

---

## Contents

1. [Problem](#1-problem)
2. [Non-negotiable rules](#2-non-negotiable-rules)
3. [Scope](#3-scope)
4. [Responsibility split — MCP vs PAC CLI](#4-responsibility-split--mcp-vs-pac-cli)
5. [Architecture](#5-architecture)
6. [Tool catalogue](#6-tool-catalogue)
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

- discover publishers, out-of-box tables, relationships, option sets, forms, views, ribbons and records;
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
| R2 | **Every call is visible and authorized by a person** before it runs | Prompt injection; agent error |
| R3 | **Publisher creation is always confirmed individually** — never allowed for a session, and disabled by default | A second publisher, even with the same prefix, creates layering problems that cannot be solved |
| R4 | **Modern commanding (Power Fx command bar) is out of scope** — classic ribbon only | Creates chicken-and-egg dependencies between base solutions and app solutions |
| R5 | **Unpacked format is the XML layout produced by `pac solution unpack`** — not the YAML source-control layout | One format for all skills and scripts |
| R6 | **Late-bound SDK access only** — no early-bound classes | Tables change too fast to keep generated classes current |
| R7 | **No client data in the repository** — logs, exchange files and samples stay outside the working tree | Client data boundary |
| R8 | **Agent-supplied text is untrusted** — never rendered as instructions, always labelled as coming from the agent | Prompt injection |

---

## 3. Scope

### In scope (v1)

| Area | Capability |
|---|---|
| Discovery | Connection info, publishers, solutions' components, table metadata, relationships, global option sets, classic ribbons, FetchXML queries over configuration tables and records (paged) |
| Solution shaping | Create publisher (gated), create empty unmanaged solution, add and remove existing components (no subcomponents, no required components) |
| Concurrency check | Version and modified-on state of row-backed components (forms, views, sitemaps, web resources, app modules) |
| Host | Local MCP endpoint, approval dialogs, visible call log, connection binding |

### Out of scope (v1)

| Excluded | Handled by / reason |
|---|---|
| Solution export, unpack, pack, import, publish, delete | PAC CLI |
| Creating or changing components directly (columns, forms, views, ribbons) | Authored locally in the unpacked solution and imported through PAC CLI |
| A general SDK facade (arbitrary `OrganizationRequest` execution) | Not needed for the workflow; large risk surface |
| Record create, update, delete | Not needed for the workflow |
| Modern commanding | R4 |
| Managed solutions, patches, holding solutions | Not part of the WIP workflow |

---

## 4. Responsibility split — MCP vs PAC CLI

| Step | Owner | Notes |
|---|---|---|
| Find publishers | **MCP** `list_publishers` | |
| Create publisher | **MCP** `create_publisher` | R3; rarely used |
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
| List solutions | PAC `solution list --json` | |

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

---

## 5. Architecture

```
┌──────────────────────── Developer workstation ────────────────────────┐
│                                                                       │
│  ┌───────────────────┐   MCP over HTTP    ┌────────────────────────┐  │
│  │ Coding agent      │  127.0.0.1:<port>  │ XrmToolBox             │  │
│  │ (MCP client)      │ ─────────────────▶ │ ┌────────────────────┐ │  │
│  │                   │  Bearer <token>    │ │ Agent Companion MCP│ │  │
│  │                   │ ◀───────────────── │ │ plugin             │ │  │
│  │                   │   JSON results     │ │  • MCP host        │ │  │
│  │                   │                    │ │  • approval queue  │ │  │
│  │  runs PAC CLI ──┐ │                    │ │  • call log        │ │  │
│  └─────────────────┼─┘                    │ └─────────┬──────────┘ │  │
│                    │                      │  active connection     │  │
│  ┌─────────────────▼─┐                    └───────────┼────────────┘  │
│  │ Unpacked WIP      │                                │               │
│  │ solutions (XML)   │                                │               │
│  └───────────────────┘                                │               │
└────────────────────┬──────────────────────────────────┼───────────────┘
                     │ pac auth profile                 │ XrmToolBox auth
                     ▼                                  ▼
            ┌──────────────────────── Dataverse (Dev) ────────────────┐
            └─────────────────────────────────────────────────────────┘
```

### Host

| Concern | Decision |
|---|---|
| Transport | MCP Streamable HTTP on `HttpListener`, bound to `127.0.0.1` only. stdio is not possible: the agent cannot launch a process that lives inside XrmToolBox. |
| Authentication | Random bearer token generated per server start, shown masked in the plugin with a copy button. Regenerate on demand. |
| Origin checks | Reject requests with a non-local `Origin` header (DNS-rebinding protection). |
| Port | Configurable; default fixed so the agent's MCP configuration stays stable. |
| Lifecycle | Server runs only while the plugin tab is open and the user has pressed **Start**. Closing the tab stops it. |
| Connection binding | Bound to the connection active at start. On connection change the server pauses, all session allowances are revoked, and the user must restart it. The bound connection is shown on every approval dialog. |
| Environment guard | At start the user confirms the connection is a development environment. Write tools refuse to run without that confirmation. |
| Threading | Requests are queued; SDK calls run on a background worker using a clone of the connection's service; approvals marshal to the UI thread. |
| Approval timeout | Configurable (default 120 s). Timeout returns a "denied: no response" error to the agent. |

### JSON-RPC and MCP layer

The plugin must not ship a DLL next to itself (see [§10](#10-implementation-constraints)). The MCP layer is therefore either:

- **hand-rolled** — JSON-RPC 2.0 plus the MCP `initialize`, `tools/list` and `tools/call` methods, serialized with a JSON library already loaded by XrmToolBox; or
- **source-included** from an MCP SDK, if one exists that compiles on `net48` without a private `System.Text.Json`.

Spike S1 decides. The protocol revision is pinned at build time and reported in `initialize`.

### Serialization

| Payload | Shape |
|---|---|
| Records | Human-readable JSON with typed wrappers for references, money, option sets and aliased values, plus formatted values. Library choice and source-inclusion confirmed in spike S1. |
| Metadata | Trimmed projection (see each tool) — never the raw `EntityMetadata` graph. Labels in the organization's base language. |
| XML payloads (FormXml, ribbon XML) | Returned as strings. Ribbon responses are decompressed server-side. |
| Size cap | Every response is capped (default 1 MB). Oversized responses are truncated with `"truncated": true` and a hint to narrow the request. |

---

## 6. Tool catalogue

Tiers are defined in [§7](#7-authorization-model).

| Tool | Tier | Purpose |
|---|---|---|
| `get_connection_info` | Read | Bound organization: friendly name, URL, version, base language, user, dev-environment confirmation |
| `list_publishers` | Read | All publishers with prefix and option value prefix |
| `list_tables` | Read | Tables with logical name, display name, custom/managed flags; filterable |
| `get_table_metadata` | Read | Columns, keys and identifiers for one table |
| `get_relationships` | Read | 1:N, N:1 and N:N relationships for one table |
| `get_option_sets` | Read | Global choices with values and labels |
| `get_entity_ribbon` | Read | Full classic ribbon for a table |
| `get_application_ribbon` | Read | Full classic application ribbon |
| `query` | Read or Record-read | FetchXML, paged; tier depends on the tables queried |
| `list_solution_components` | Read | Components in a solution, with type, ID and root behaviour |
| `get_component_state` | Read | Version and modified-on for row-backed components |
| `create_solution` | Write | Empty unmanaged solution |
| `add_solution_components` | Write | Add existing components, no subcomponents, no required components |
| `remove_solution_component` | Write | Remove a component from a solution (the component itself is kept) |
| `create_publisher` | Publisher | New publisher — disabled by default |

### Read tools

**`list_tables`**

| Input | Type | Notes |
|---|---|---|
| `filter` | `"all" \| "custom" \| "system"` | Default `all` |
| `nameContains` | string | Optional |

Returns `logicalName`, `schemaName`, `displayName`, `metadataId`, `objectTypeCode`, `isCustom`, `isManaged`.

**`get_table_metadata`**

| Input | Type | Notes |
|---|---|---|
| `logicalName` | string | Required |
| `include` | `["columns","keys"]` | Default both |
| `columnFilter` | `"all" \| "custom" \| "system"` | Default `all` |

Returns table identity (`schemaName`, `metadataId`, `primaryIdAttribute`, `primaryNameAttribute`, `ownershipType`, `isCustom`, `isManaged`) and per column: `logicalName`, `schemaName`, `metadataId`, `type`, `requiredLevel`, `isCustom`, `isManaged`, `maxLength` (strings), `targets` (lookups), `optionSet` (name and whether global), `isValidForForm`.

**`get_relationships`** — input `logicalName`, optional `kind` (`oneToMany`, `manyToOne`, `manyToMany`). Returns `schemaName`, `metadataId`, referenced/referencing tables and attributes, intersect table for N:N, cascade configuration, `isCustom`, `isManaged`.

**`get_option_sets`** — input optional `names[]`. Returns `name`, `metadataId`, `isCustom`, `isManaged`, and options (`value`, `label`).

**`get_entity_ribbon`** / **`get_application_ribbon`**

| Input | Type | Notes |
|---|---|---|
| `logicalName` | string | Entity ribbon only |
| `location` | `"form" \| "homepageGrid" \| "subGrid" \| "all"` | Default `all`; entity ribbon only |

Returns the decompressed ribbon XML (`RibbonDefinitions`, `CommandDefinitions`, `RuleDefinitions`). These responses are large; the size cap applies.

**`query`**

| Input | Type | Notes |
|---|---|---|
| `fetchXml` | string | Required. `page`, `count` and `paging-cookie` attributes are set by the server and ignored if supplied |
| `page` | integer | Default 1 |
| `pageSize` | integer | Default 50; maximum 500 (configurable) |
| `pagingCookie` | string | From the previous page |
| `includeFormattedValues` | boolean | Default true |

```json
{
  "page": 1,
  "pageSize": 50,
  "moreRecords": true,
  "pagingCookie": "<cookie>",
  "records": [
    {
      "systemformid": "00000000-0000-0000-0000-000000000000",
      "name": "[FormName]",
      "type": { "_option": 2, "label": "Main" }
    }
  ],
  "truncated": false
}
```

Rules:

- **Paging from the first release.** Development tables are expected to be small, but every query is paged and the agent continues with `pagingCookie` only when it needs more.
- **Explicit columns.** `<all-attributes />` is rejected; the agent must name columns.
- **Tier by table.** The root entity and every `link-entity` are checked against the configuration allow-list. All on the list → Read tier. Anything else → Record-read tier.

Configuration allow-list (v1): `publisher`, `solution`, `solutioncomponent`, `systemform`, `savedquery`, `sitemap`, `appmodule`, `appmodulecomponent`, `webresource`, `ribboncustomization`, `organization`.

**`list_solution_components`** — input `solutionUniqueName`. Returns per component: `componentType` (code and name), `objectId`, resolved display name where cheap to resolve, `rootComponentBehavior`, and `isMetadata`. Used after every add to compare intended vs actual (R1).

**`get_component_state`**

| Input | Type | Notes |
|---|---|---|
| `components` | `[{ "type": 60, "id": "<guid>" }]` | Forms, views, sitemaps, web resources, app modules |

Returns `versionNumber`, `modifiedOn`, `modifiedBy` per component. Table and column metadata carry no comparable row version; for those, the overwrite check falls back to a PAC re-export and diff.

### Write tools

**`create_solution`**

| Input | Type | Notes |
|---|---|---|
| `uniqueName` | string | Required; validated against naming rules |
| `friendlyName` | string | Required |
| `publisherUniqueName` | string | Required; must exist |
| `version` | string | Default `1.0.0.0` |
| `description` | string | Optional |

Fails if the unique name exists.

**`add_solution_components`**

| Input | Type | Notes |
|---|---|---|
| `solutionUniqueName` | string | Required; must be unmanaged |
| `components` | array | `{ "type": <code>, "id": "<guid>" }` or, for tables and columns, `{ "type": 1, "logicalName": "account" }` / `{ "type": 2, "table": "account", "logicalName": "<column>" }` |
| `tableBehavior` | `"metadata" \| "shell"` | Tables only; default `metadata`. Exact mapping confirmed in spike S2 |

Fixed behaviour, not exposed to the caller:

- `AddRequiredComponents = false`
- `DoNotIncludeSubcomponents = true` for tables
- After adding, the tool reads the solution back and returns any component present that was not requested, flagged `"unexpected": true`. The plugin also raises a warning in its log.

Component type codes accepted in v1 (confirmed against the `solutioncomponent` type option set in spike S2): table `1`, column `2`, global option set `9`, relationship `10`, view `26`, form `60`, web resource `61`, sitemap `62`, app module `80`, application ribbon customization (code to confirm).

**`remove_solution_component`** — input `solutionUniqueName`, `type`, `id`. Removes the component from the solution only.

**`create_publisher`**

| Input | Type | Notes |
|---|---|---|
| `uniqueName` | string | Required |
| `friendlyName` | string | Required |
| `prefix` | string | Required; 2–8 alphanumerics, starts with a letter, not `mscrm` |
| `optionValuePrefix` | integer | Required; 10000–99999 |

Disabled unless the user enables it in settings. When disabled, the tool returns an error telling the agent that a person must create or choose the publisher. When enabled, it raises the publisher dialog ([§8.3](#83-publisher-creation-dialog)).

---

## 7. Authorization model

The plugin's approval is the authoritative gate. The agent client's own tool-approval prompt is a convenience and is not relied on.

| Tier | Tools | Allow once | Allow for session | Notes |
|---|---|---|---|---|
| **Read** | Metadata, ribbons, configuration queries, component state, solution components | ✓ | ✓ (per tool) | |
| **Record-read** | `query` touching non-configuration tables | ✓ | ✓ (per tool) | Record queries can be switched off entirely in settings |
| **Write** | `create_solution`, `add_solution_components`, `remove_solution_component` | ✓ | ✗ | Requires dev-environment confirmation |
| **Publisher** | `create_publisher` | ✓ with typed confirmation | ✗ | Disabled by default (R3) |

Rules:

- **Deny is a normal result.** The agent receives a structured error, `"denied by user"` or `"denied: no response"`, and must not retry the same call without new instructions from the person.
- **Session allowances end** when the server stops, the connection changes, or the user clicks **Revoke all**.
- **Arguments shown verbatim.** The dialog shows the full arguments (FetchXML pretty-printed, component lists resolved to names). An agent-supplied `reason` field, if present, is shown in a separate box labelled *Agent-supplied — not verified* (R8).
- **One dialog per call.** Concurrent calls queue; the queue length is visible.

---

## 8. User experience

### 8.1 Main panel

```
┌─ Agent Companion MCP ───────────────────────────────────────────────┐
│ Connection : [ClientName] DEV  (https://<org>.crm.dynamics.com)     │
│ Dev env    : [x] I confirm this is a development environment        │
│                                                                     │
│ Server     : ● Running   http://127.0.0.1:47321/mcp                 │
│ Token      : ●●●●●●●●●●●●●●●●  [Copy] [Regenerate]                  │
│              [Stop]  [Copy client config]                           │
│                                                                     │
│ Settings   : [x] Record queries   [ ] Publisher creation            │
│              Max page size [500]  Response cap [1 MB]  Timeout [120s]│
│                                                                     │
│ Session allowances: get_table_metadata, query (config)  [Revoke all]│
├─ Call log ──────────────────────────────────────────────────────────┤
│ Time     Tool                     Tier     Result     Duration      │
│ 14:02:11 list_publishers          Read     Allowed     0.3 s        │
│ 14:02:40 get_table_metadata       Read     Session     1.1 s        │
│ 14:03:05 create_solution          Write    Allowed     0.8 s        │
│ 14:03:31 add_solution_components  Write    Allowed  ⚠  2.4 s        │
│ 14:04:02 query (records)          Record   Denied        —          │
│                                                                     │
│ Pending approvals: 0          [Export log…]  [Clear]                │
└─────────────────────────────────────────────────────────────────────┘
```

`⚠` marks a call that returned unexpected components or was truncated.

### 8.2 Approval dialog

```
┌─ Approve tool call ─────────────────────────────────────────────────┐
│ Environment : [ClientName] DEV                                      │
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

For Read and Record-read calls the dialog adds **Allow for this session**.

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
│   { "solutionUniqueName": "[prefix]_WIP_1234", ... }                │
│ Result                                                              │
│   added 3 · unexpected 1                                            │
│   ⚠ unexpected: Relationship [prefix]_account_contact               │
│ Response size 2 KB · not truncated                                  │
└─────────────────────────────────────────────────────────────────────┘
```

The persisted log records arguments and result summaries (counts, sizes, warnings), not result payloads. It is written to a user-chosen folder outside any repository (R7).

---

## 9. Workflows

### 9.1 Cold start — first base solution

```
 Agent                              Companion (MCP)            PAC CLI
 ─────                              ───────────────            ───────
 get_connection_info ─────────────▶ confirm DEV env
 list_publishers ─────────────────▶ person confirms which
                                    publisher to use
   (create_publisher only if none
    exists and the person agrees)
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

1. `create_solution` named by convention (see [Open questions](#13-open-questions)).
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
2. `list_solution_components` on the app's managed solutions, plus `query` over `appmodule`, `sitemap`, `systemform` and `savedquery`, shows what the app added.
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
| Versioning | `AssemblyVersion` / `FileVersion` / `Version` in the `.csproj` and `<version>` in the `.nuspec`, kept identical; scheme `1.YYYY.M.N` |
| CI | Build, test and pack on push and PR to `main` under this plugin's path; trusted publishing, no stored credentials |
| No client data | No organization URLs, tenant IDs, GUIDs from client environments, or record data in code, tests, samples or commit messages. Placeholders: `[ClientName]`, `[TableName]`, `[FieldName]`, `[prefix]` |

### Testable units

| Unit | Tested by |
|---|---|
| Tier classifier (tool + FetchXML tables → tier) | Unit tests over FetchXML samples, including link-entities and aliases |
| FetchXML rewriting (page, count, cookie; reject `all-attributes`) | Unit tests |
| Approval policy (once, session, revoke, timeout, connection change) | Unit tests with a fake clock and fake UI |
| Add-components guard (fixed flags, unexpected-component detection) | Unit tests with mocked `IOrganizationService` |
| Serializers (records, metadata projections, ribbon decompression) | Unit tests with recorded fixtures — synthetic, never client data |
| JSON-RPC / MCP layer | Unit tests on request/response pairs; manual test with each supported client |

---

## 11. Plan

Each phase ends with a usable, reviewable increment.

### Phase 0 — Spikes

Short, throwaway investigations against a development environment. Each produces a written finding that updates this spec.

| ID | Question | Exit criterion |
|---|---|---|
| S1 | Can an MCP host run inside XrmToolBox on `net48` without shipping a DLL? Which JSON library is safely available? Which record serializer can be source-included? | A hello-world tool callable from the target agent clients, installed and uninstalled cleanly via the Tool Library |
| S2 | What exactly does `AddSolutionComponentRequest` produce for an out-of-box table with `DoNotIncludeSubcomponents = true`, and how is a shell-only table achieved? Does the export then carry the entity's ribbon diff? Which type code is the application ribbon? | Documented mapping of `tableBehavior` → root component behaviour → exported XML, for `account` and one custom table |
| S3 | What does PAC `add-solution-component` do for each v1 component type without `--AddRequiredComponents` — in particular for tables? | Table per component type: safe for Path B yes/no |
| S4 | Do the target agent clients (Claude Code, VS Code agent mode) connect to a local HTTP MCP server with a bearer header, and how do they surface errors and long waits during approval? | Client configuration snippets and timeout behaviour documented |
| S5 | Which of the Read tools does the Microsoft Dataverse MCP server already provide, and could it replace any of them? Requires a dated, sourced check. | Overlap table; decision recorded |

### Phase 1 — Host, approvals and log

- Plugin shell, main panel, start/stop, token, connection binding, dev-environment confirmation.
- Approval queue and dialogs (Read and Write tiers), session allowances, revoke, timeout.
- Call log and log detail; export to a user-chosen folder.
- Tools: `get_connection_info`, `list_publishers`.

**Exit:** an agent lists publishers through an approved call; denial and timeout reach the agent as errors; connection change revokes allowances.

### Phase 2 — Discovery

- `list_tables`, `get_table_metadata`, `get_relationships`, `get_option_sets`.
- `query` with paging, tier classification, response cap; Record-read tier and its settings switch.
- `get_entity_ribbon`, `get_application_ribbon`.
- `list_solution_components`, `get_component_state`.

**Exit:** an agent can describe `account` (columns, relationships, forms, views, ribbon) and page through a small record set, with every call approved and logged.

### Phase 3 — Solution shaping

- `create_solution`, `add_solution_components` (fixed flags, unexpected-component detection), `remove_solution_component`.
- `create_publisher` behind its setting and dialog.

**Exit:** the cold-start workflow ([§9.1](#91-cold-start--first-base-solution)) runs end to end on a clean development environment, and `list_solution_components` shows exactly the intended components.

### Phase 4 — Agent skills and scripts

Agent-side work that makes the local edits safe. Skills decide between Path A and Path B per component type using the S2 and S3 findings.

| Deliverable | Purpose |
|---|---|
| Cold-start skill | Publisher choice, base solutions, first components |
| WIP story skill | Create, add, verify, export, baseline, edit, overwrite check, import, close-out |
| Component-adding guidance | Path A vs Path B per type; R1 restated |
| Validation scripts | Schema check of `Customizations.xml` and entity files against the customization XSD; form checks (controls bound to existing columns, unique control IDs, GUID format); ribbon checks (referenced commands, rules and web resources exist) |
| Baseline and overwrite-check script | Records and compares `get_component_state` results |

**Exit:** a WIP story touching a column, a form, a view and a ribbon button is delivered through the full loop, with the platform import succeeding and no unexpected components.

### Phase 5 — Release

- Tooling README with installation, client configuration, tool table and settings.
- CI workflow, NuGet package, Tool Library listing.
- Repository updates that sit outside this plugin are listed under [Open questions](#13-open-questions); they need an editorial decision.

**Exit:** installable from the Tool Library; README matches the shipped tool surface.

---

## 12. Human review checkpoints

| Moment | What a person checks |
|---|---|
| Server start | The bound connection is the intended **development** environment |
| Publisher selection | The existing publisher is reused; a new one only with explicit certainty (R3) |
| Every Write approval | Solution name, component list, fixed flags shown as OFF |
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
| Q3 | Should the dev-environment confirmation be remembered per connection, or asked at every start? |
| Q4 | Default for record queries: enabled, or off until enabled per session? |
| Q5 | Repository follow-ups outside the plugin — a tooling-conventions section for this plugin in the instructions file, and whether a workflow guide or matrix entry is needed. Both change shared documents and need an editorial decision |

---

## 14. References

[^1]: Microsoft Learn — *Microsoft Power Platform CLI solution command group*, <https://learn.microsoft.com/power-platform/developer/cli/reference/solution> (last updated 2026-02-25, checked 2026-10-25). Supports: `add-solution-component` options are limited to `--AddRequiredComponents`, `--async` and `--environment`; no create command exists; `unpack` defaults to the XML layout while `clone` and Git integration use the YAML layout.

SDK message names and behaviours used in this spec (`AddSolutionComponentRequest`, `RemoveSolutionComponentRequest`, `RetrieveEntityRibbonRequest`, `RetrieveApplicationRibbonRequest`, root component behaviour values, component type codes) are from working knowledge and are confirmed in spikes S2 and S3 before implementation.
