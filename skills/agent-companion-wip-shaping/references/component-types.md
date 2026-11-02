# Component types, add paths and subtype rules

## Codes the plugin accepts

Confirmed against the `solutioncomponent` type option set in the plugin's spikes. `add_solution_components`, `remove_solution_component` and `get_required_components` accept these; anything else fails with `invalid_argument`.

| Code | Component | Selector | Path | Notes |
|---|---|---|---|---|
| 1 | Table | `{ "type": 1, "logicalName": "account" }` | MCP only | `tableBehavior` `shell` (behaviour 2, an empty shell) or `metadata` (behaviour 1, the table's own properties). Both export the table's whole unmanaged ribbon diff (X6). PAC adds a table with all subcomponents — never |
| 2 | Column | `{ "type": 2, "table": "account", "logicalName": "new_rating" }` | MCP or PAC | Added alone, its table arrives as a shell |
| 9 | Global choice | `{ "type": 9, "name": "new_priority" }` | MCP or PAC | Root, behaviour 0, nothing else added |
| 10 | Relationship | `{ "type": 10, "schemaName": "new_account_contact" }` | MCP or PAC | Parent table shells added — the referencing table and its lookup column for a 1:N, the first table for an N:N — reported as unexpected. A 1:N exports the lookup column's whole definition. An out-of-box N:N with a non-customizable intersect table is held but not exported |
| 26 | View | `{ "type": 26, "id": "<savedqueryid>" }` | MCP or PAC | Table arrives as a shell |
| 60 | Form or dashboard | `{ "type": 60, "id": "<formid>" }` | MCP or PAC | Inactive forms do not export (X10) |
| 61 | Web resource | `{ "type": 61, "id": "<webresourceid>" }` | MCP or PAC | |
| 62 | Sitemap | `{ "type": 62, "id": "<sitemapid>" }` | MCP or PAC | Root, behaviour 0 |
| 80 | Model-driven app | `{ "type": 80, "id": "<appmoduleid>" }` | MCP only | PAC 2.14.1 does not know the type. The export's `AppModuleComponents` can be empty while the app has components; compare before importing |

Paths for 9, 10, 62 and 80 were verified live on 2026-10-31 (plugin notes, Phase 4).

`get_component_state` accepts the row-backed types 26, 60, 61, 62 and 80.

The application ribbon customization is not accepted until its code is confirmed.

## Other types — PAC only

The plugin refuses them, so they are added with `pac solution add-solution-component`, one per command, never with `--AddRequiredComponents`, and read back after each (R1). The codes below are working knowledge; confirm one under 10000 with `get_option_sets` and `names: ["componenttype"]`.

**Codes of 10000 and above** vary between environments and are **not** in that choice. `list_solution_components` shows them as `ComponentType<code>`; identify the component by querying its candidate table by ID (`connectionreference`, `customapi`, `environmentvariabledefinition`, …). PAC 2.14.1 refuses such a code as a number; pass the table's **logical name** instead — `--componentType connectionreference`. A display name such as `Connection Reference` resolves to the wrong type.

| Code | Component | Typical group |
|---|---|---|
| 14 | Alternate key | Base |
| 20 | Security role | Security |
| 29 | Process — classic workflow, action, business rule, business process flow, cloud flow | By category, below |
| 36 | Email template | Apps |
| 44 | Duplicate detection rule | Apps |
| 59 | Chart | Base |
| 63 | Connection role | Base |
| 66 | Custom control (PCF) | Resources |
| 70 | Field security profile | Security |
| 90, 91, 92, 93 | Plug-in type, assembly, step, step image | Assemblies |
| 300 | Canvas app | Apps |
| 380, 381 | Environment variable definition, value | Resources — values travel with the WIP (X7) |
| ≥ 10000 | Custom API and its parameters (Assemblies), connection reference (Processes), team template and masking rule (Security), others | By name, per the map |

A cloud flow being edited is often better served by a flow-editing skill if one is installed; it still gets its WIP from this skill (K7).

## Typical groups — the project's map wins

| Group | Typical contents |
|---|---|
| Shared libraries | Reusable web resources and controls maintained outside the project, under the library's own publisher |
| Resources | Web resources, custom controls, environment variable definitions and values |
| Base | Tables, columns, keys, choices, relationships, forms, views, charts, connection roles, business rules, business process flows |
| Assemblies | Plug-in assemblies, steps and images, custom APIs and their parameters |
| Processes | Classic workflows, actions, cloud flows, connection references |
| Apps | Model-driven apps, sitemaps, dashboards, canvas apps, email templates, reports, duplicate detection rules |
| Security | Security roles, field security profiles, team templates, masking rules |

## Subtype rules

The map may route some types by subtype. The columns to read:

**`systemform.type`** — read with `query`:

| Value | Form type | Typical group |
|---|---|---|
| 2 | Main | Base |
| 6 | Quick view | Base |
| 7 | Quick create | Base |
| 11 | Card | Base |
| 0 | Dashboard | **Apps** |
| 10 | Interactive dashboard | **Apps** |
| 103 | Power BI dashboard | **Apps** |

**`workflow.category`** — only `type` 1 (definition) rows are solution components:

| Value | Process | Typical group |
|---|---|---|
| 0 | Classic workflow | Processes |
| 3 | Action | Processes |
| 5 | Cloud flow | Processes |
| 2 | Business rule | **Base** |
| 4 | Business process flow | **Base** |

A business process flow also has its own table (type 1); it goes with the flow.

**Record creation and update rules** (convert rules) and the cloud flows they generated are never put in a WIP (W8). A flow whose name or description says the rule created it is asked about.
