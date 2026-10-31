---
name: agent-companion-wip-shaping
description: Used together with the Agent Companion MCP - the XrmToolBox plugin that gives a coding agent person-approved access to Dynamics 365 CE / Dataverse environments - and needs a plugin tab bound to the development environment. Shapes segmented work-in-progress (WIP) solutions for a change - classifies each component a change touches with the project's segmentation map, finds its existing transport home, routes it (placement for a component with no transport home, in place for one that has one), names and creates one WIP per transport group, adds components with no required components and no subcomponents (MCP or PAC path per component type), verifies every add by reading the solution back, removes strays and orphaned table shells, and checks overlap with other open WIPs and dependencies against the transport order. Use when a change touches a Dataverse component - new, out-of-box, or already in a transport solution - and it is to be edited through a WIP solution with the Agent Companion MCP.
---

# Shaping WIP solutions with the Agent Companion MCP

A WIP solution holds exactly the components a change touches, so they can be exported, edited locally and imported back. This skill decides which WIP each component goes into and puts it there safely. It applies to every change — a new column, an out-of-box form touched for the first time, or a fix to a form already in production.

**Precondition.** The delivery context is established and the connection matched to PAC — apply `agent-companion-context` first if it is not (K2). Without the plugin's tools, stop and say the Agent Companion MCP is needed.

---

## The decisions this skill owns

| Decision | Rule |
|---|---|
| A component's group | The segmentation map, including subtype rules (W1) — or its existing transport home, which always wins (W2) |
| A component's route | **Placement** when it has no transport home; **in place** when it has one (W9) |
| A WIP's lifetime | Lives until close-out if it holds any placement component; otherwise ends once its edit reaches the transport source (W9). Carried out by `agent-companion-wip-close-out` |
| A second WIP for one group | Only when the person chooses to end part of the group's work early (W12) |
| MCP or PAC to add a component | By component type — table below |
| The WIP's name | `<initials>_Agent_WIP_<work item id>_<Group>[_<n>]` |

Changing a component's route never changes its group (W11). A component is in at most one of a story's WIPs (W12).

### Adding: MCP or PAC

| Component | Type code | Add with | Notes |
|---|---|---|---|
| Table | 1 | **MCP only** — `add_solution_components` with `tableBehavior` | **Never through PAC**: PAC adds a table with all its subcomponents |
| Column, view, form (and dashboard), web resource | 2, 26, 60, 61 | MCP (preferred) or PAC | Added alone, the platform adds the table as a shell to hold it |
| Global choice, relationship, sitemap, model-driven app | 9, 10, 62, 80 | MCP | PAC behaviour for these is not verified; do not use it |
| Anything else — workflows and cloud flows, plug-in assemblies and steps, security roles, environment variables, custom APIs, connection references, canvas apps, charts | other | PAC, one command per component | The plugin refuses these types. Never pass `--AddRequiredComponents` |

MCP is preferred where it is accepted: one approval for a list, required components and subcomponents forced off, parent shells recognized, and every call in the plugin's log. Type codes and the subtype rules the map uses are in [component-types.md](./references/component-types.md).

Whichever path, **every add is followed by a read-back** (R1, step 8).

---

## Procedure

Work through the task's components in transport order (W5): Resources before Base, Base before Assemblies, and so on.

### 1. Discover

Find each component and its ID: `list_tables`, `get_table_metadata`, `get_relationships`, `get_option_sets`, and `query` over `systemform`, `savedquery`, `webresource`, `sitemap`, `appmodule`, `workflow` and the others in [queries.md](./references/queries.md). For a form, note its `type` and `formactivationstate`; for a workflow, its `category`. `objecttypecode` and `returnedtypecode` filter on the table's type code (`objectTypeCode` from `list_tables`), not its logical name.

### 2. Classify and find the transport home

For each component:

1. **Look for a transport home (W2, T4)** — `query` over `solutioncomponent` for the component's ID, joined to `solution`, on the environment its transport solutions are extracted from: development under B, the build environment's read-only tab under A. A column, form, view or relationship can also be at home through its **table**, when the table sits in a transport solution with root behaviour `includeSubcomponents` (0): query the table's `metadataId` too. [queries.md](./references/queries.md#transport-home-and-overlap) has the FetchXML.
2. **Group.** A component with a transport home stays in that group, even if the map changed since (W2). Otherwise apply the map, with its subtype rules — business rules and business process flows to Base, dashboards to Apps (W1). A type the map does not cover is asked, with a recommendation.
3. **Route (W9).** No transport home → **placement**. A transport home → **in place**.

Record each in the ledger's Components table.

### 3. Checks before shaping

Stop and ask the person when any of these finds something:

| Check | How | Rule |
|---|---|---|
| Never in a WIP | Record creation and update rules and the flows they generated; a managed table with all subcomponents; a custom component carrying a prefix other than its WIP's publisher's — typically one created by mistake under the environment's default publisher (`new_`, `cr<…>_`) or a partner's. Out-of-box and installed-app components are not affected; the shared-library WIP uses the library's publisher (W3) | W8 |
| Inactive form | `formactivationstate` is 0. It will not export, so there is nothing to edit; the person activates it or picks another form | X10 |
| Overlap with other open WIPs | For every whole document — form, view, sitemap, web resource, workflow, app module, **and every table, for its ribbon** — the same `solutioncomponent` query: any other unmanaged solution that is neither a transport solution nor this task's WIP. Report it; the person sequences or reviews | W7, X1, X6 |
| Dependencies against the order | `get_required_components` for the components. A requirement is in place when it is managed and from an installed app, in a transport solution of the same or an earlier group, or in this story's WIP for the same or an earlier group. Held only by a later group → stop for a placement decision. Held by no solution → it needs placing too (X5) | W6, X5 |

### 4. Decide the WIPs

- One WIP per group the change touches (R10).
- A group holding both routes shares one WIP by default; that WIP lives until close-out. When the person wants the in-place part to end first, propose a second WIP for the group, named with a suffix (W12).
- The WIP's publisher is the publisher of the transport solution it feeds (W3) — from the delivery context.

Hand the list — WIP, group, route, publisher, components — to the steps for the person to confirm before the first Write call (T3, owned by `agent-companion-context`).

### 5. Name

1. `who_am_i`. If `firstName` or `lastName` is empty, `isApplicationUser` is true or `impersonating` is true, ask the person for their initials — the WIP belongs to the person doing the work.
2. Build the name with the script, which also checks length, characters and protected prefixes (W4):

   ```powershell
   .\scripts\Get-WipName.ps1 -FirstName '<firstName>' -LastName '<lastName>' -WorkItemId 1234 -Group Base -ProtectedPrefixes 'new','msdyn'
   .\scripts\Get-WipName.ps1 -Initials 'XY' -WorkItemId 1234 -Group Base -Sequence 2
   ```

3. `list_solutions` with `nameContains` set to the unique name: it must be free. A clash — two people with the same initials on the same work item and group — is resolved by the person.

### 6. Create

`create_solution` with the unique name, the friendly name the script returned, the destination's `publisherUniqueName`, and a `reason` (R12) — for example *"WIP for work item 1234, Base group: new column new_rating (placement) and an edit to the account main form (in place)."* The reason states intent concretely; the person compares it with the arguments.

### 7. Add

**MCP** — `add_solution_components` with every accepted component for that WIP in one call, and a `reason`:

- `tableBehavior: "shell"` when the table is there only to hold its column, form or view, or to carry a ribbon change. This is also what the platform does on its own when a child is added alone.
- `tableBehavior: "metadata"` when the table's own properties change, or a new table is being placed.

Read the result: `failed` stops the run; `notAttempted` entries follow the first failure; a parent shell shows with the `index` of the child it holds. Any `unexpected` entry stops the workflow for review (R1).

**PAC** — one command per component, never `--AddRequiredComponents`, never for a table:

```powershell
pac solution add-solution-component --solutionUniqueName <wip> --component <id or name> --componentType <code>
```

Check the PAC profile matches the connection first (`agent-companion-context`, section 1).

### 8. Read back (R1)

`list_solution_components` on the WIP, every page. What is there must be exactly:

- the components requested, under their tables;
- plus, for a column, form or view added without its table, that table as a shell (`includeAsShellOnly`).

Anything else — a table with `includeSubcomponents`, a required component nobody asked for, a shell for a table no added entry belongs to — stops the work until the person reviews it. A new table the person created inside the WIP in the maker portal also arrives with all subcomponents; it was not added by the agent, so report it and let the person decide.

### 9. Remove

`remove_solution_component` with a `reason`. Then read the WIP back: when a removed column, form or view leaves its parent table shell holding nothing, remove the shell too — the platform keeps it, and with it the table's whole ribbon diff (W13).

### 10. Update the ledger and hand on

Mark the WIP `shaped`, with its components, and hand to `agent-companion-wip-edit-loop`.

---

## When a call fails

| Result | Do |
|---|---|
| `denied_by_user`, `denied_no_response` | Do not retry. Ask the person what to change |
| `dev_environment_not_confirmed` | Ask the person to confirm the tab as development — only if it is |
| `already_exists` | The name is taken: `list_solutions` and ask |
| `not_found` | Check the name or ID in `details`; rediscover |
| `managed_solution` | Wrong solution — a WIP is always unmanaged |
| `invalid_argument` on a type | That type is not accepted by the plugin: use PAC (table above) |
| `connection_not_found`, `connection_closed` | `list_connections`; ask the person |

---

## Guardrails — what a person confirms

| Moment | The person confirms |
|---|---|
| Classification | Each component's group follows the map; one already in a transport solution stays there (W1, W2) |
| Routing | Placement when there is no transport home, in place when there is one; a second WIP for a group only by choice (W9, W12) |
| Overlap and dependencies | Shared whole documents — tables' ribbons included — are sequenced or reviewed; no dependency points up the order (W6, W7) |
| Each Write approval in the plugin | Connection, WIP name, component list and fixed flags; the reason matches the arguments (R12) |
| After every add | The read-back shows nothing unexpected (R1) |
