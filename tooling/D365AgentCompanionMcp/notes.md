# Agent Companion MCP — progress notes

> Working notes for building the plugin specified in [SPEC.md](SPEC.md). The spec is the source of truth: these notes track progress and hold findings until they are written back into it. **No client data** (R7) — no organization URLs, tenant IDs, environment GUIDs, user names or records from a client environment. Use the spec's placeholders.

---

## Status

| Phase | Status | Exit criterion |
|---|---|---|
| [0 — Spikes](SPEC.md#phase-0--spikes) | **Done for v1** (2026-10-30). S7 in cold storage (no build environment); S8 deferred with strategy C. Sandbox and workstation cleaned up | Every v1 spike has a written finding, and the spec is updated |
| [1 — Host, connections, approvals and log](SPEC.md#phase-1--host-connections-approvals-and-log) | **Done** (2026-10-30) — built, unit-tested, and all twelve live exit checks passed in XrmToolBox. See [Phase 1 — execution](#phase-1--execution) | See spec |
| [2 — Discovery](SPEC.md#phase-2--discovery) | **Done** (2026-10-31) — built, unit-tested (176 tests), and the live exit checks passed against the sandbox; four findings (L1–L4) fixed on the way. See [Phase 2 — execution](#phase-2--execution) | See spec |
| [3 — Solution shaping](SPEC.md#phase-3--solution-shaping) | **Done** (2026-10-31) — built, unit-tested (207 tests), and the cold-start workflow (§9.1) ran end to end on the sandbox; two findings (P1, P2) for the Phase 4 skills. See [Phase 3 — execution](#phase-3--execution) | See spec |
| [4 — Agent skills and scripts](SPEC.md#phase-4--agent-skills-and-scripts) | **In progress** (2026-10-31) — five companion skills and their scripts built; script self-tests pass (32 + 11), including the schema check against Microsoft's XSD bundle. Live exit checks not run yet. See [Phase 4 — execution](#phase-4--execution) | See spec |
| [5 — Release](SPEC.md#phase-5--release) | Not started | See spec |

Status values: *Not started* · *In progress* · *Blocked* (say on what) · *Done*.

---

## Phase 4 — execution

### What is built

Five skills in [skills/](../../skills/), each a folder with `SKILL.md`, `references/` and, where needed, `scripts/`. Every `name` starts with `agent-companion-`, and every `description` opens by saying the skill is used together with the Agent Companion MCP and needs a bound plugin tab (SPEC §11 Phase 4).

| Skill | Owns | References and scripts |
|---|---|---|
| [agent-companion-context](../../skills/agent-companion-context/SKILL.md) | Delivery context, steps (T3), ledger shape; R15, R16 detection, T1–T6 | `rules.md` (rule index by ID), `delivery-facts.md` (where each fact is found; recording format), `wip-ledger.md` (context, steps, components, WIPs, states, reconcile) |
| [agent-companion-transport-bootstrap](../../skills/agent-companion-transport-bootstrap/SKILL.md) | Missing transport solutions, publisher choice (R3), first-party apps (§9.3) | — |
| [agent-companion-wip-shaping](../../skills/agent-companion-wip-shaping/SKILL.md) | Classification, transport home, routes, naming, create, add (Path A/B), read-back, removal with shell cleanup, overlap and dependency checks | `component-types.md`, `queries.md`; `Get-WipName.ps1` |
| [agent-companion-wip-edit-loop](../../skills/agent-companion-wip-edit-loop/SKILL.md) | Export, baseline, edit, validate, pack, overwrite check, import order and flags | `Test-UnpackedSolution.ps1`, `Test-CustomizationsSchema.ps1`, `Compare-ComponentState.ps1`, `Compare-SolutionExport.ps1` |
| [agent-companion-wip-close-out](../../skills/agent-companion-wip-close-out/SKILL.md) | Ending WIPs by route and strategy, W10, close-out A and B, A8 alignment, stragglers | `strategies.md` |

Existing skills (K7): one optional sentence each in [power-automate-flow-editing](../../skills/power-automate-flow-editing/SKILL.md) (an MCP can create the WIP) and [d365ce-planning](../../skills/d365ce-planning/SKILL.md) (an MCP with environment read access can check a component's absence). Neither depends on the plugin.

Script tests: `scripts/tests/Invoke-ScriptTests.ps1` in the edit-loop and shaping skills — plain runners for Windows PowerShell 5.1, no Pester, fixtures synthetic and built in a temp folder. The edit-loop runner takes `-SchemaPath` for the schema tests; it passed against Microsoft's schema bundle (9.0.0.2090), downloaded to the session scratchpad and not committed.

### Implementation choices to review

| # | Choice | Why |
|---|---|---|
| I27 | The schema check runs on the **packed** zip and fails only on findings absent from the untouched export (`-BaselinePackagePath`) | `pac solution unpack` splits `customizations.xml`, and the published XSD bundle is older than the platform, so an untouched export can already report findings |
| I28 | `Test-UnpackedSolution.ps1` reports a reference outside the tree as an **Error** when evidence is passed (columns, web resources, base ribbon) and a **Warning** saying *not verified* when it is not | A WIP holds only part of each table; without evidence the script cannot tell missing from absent-from-the-WIP |
| I29 | WIP naming is a script (`Get-WipName.ps1`): accent stripping, length, characters, protected prefixes. A first letter with no ASCII form (for example `Ø`) stops and asks for initials | Deterministic rules are not left to the model. The spec's "any other character is dropped" could leave one initial; asking is the safer reading |
| I30 | The overwrite check re-exports and compares the **whole** tree whenever the WIP holds anything without a row version — in practice always, since nearly every WIP holds a table shell | `get_component_state` does not cover tables, columns, flows or steps, and X6 makes every table's ribbon diff a whole document |
| I31 | Global choices, relationships, sitemaps and apps (9, 10, 62, 80) are added through MCP only | S3 verified PAC for columns, forms, views and web resources only |
| I32 | The rule index lives in `agent-companion-context/references/rules.md`; the other skills cite IDs and name the context skill as their precondition | One place to keep in step with the spec (K3) |
| I33 | Under strategy A, the bootstrap prepares the transport solutions for a person to create in build, and development receives them by alignment (A8) | The build tab is read-only (A2) |
| I34 | The transport-home query joins `solutioncomponent` to `solution` and also asks for the table's `metadataId`, so a child at home under an include-all table is found | A component under a table with root behaviour 0 has no row of its own (S3) |

### Live exit checks — steps

Exit criterion (SPEC §11 Phase 4): six scenarios on the sandbox, without a build environment. Use the data-free sandbox, its default publisher (R3) and invented work item IDs. Install the five skills in the agent client, plus `power-automate-flow-editing`.

1. **Setup.** Two transport solutions exist under the sandbox publisher — Base and Apps, say — and **Resources does not**. Delivery facts: transport extracted from development (B), no build environment, not shared.
2. **Placement story.** A story touching a new column, an `account` form, a view, a web resource and a ribbon button. Expect: the bootstrap creates only Resources; shaping creates two WIPs (Resources, Base), the steps are stated before the first Write call; edit, validate, overwrite check, import Resources then Base; close-out B adds to both transport solutions in order; read-backs show nothing unexpected; both WIPs deleted.
3. **In-place change.** Edit a form already in Base. Expect one in-place WIP; import, W10 read-back, the person confirms; deleted with no close-out add.
4. **Both routes.** A story adds a column and edits a form already in Base. The person asks to split: `_Base` and `_Base_2`; the in-place one ends at import while the other waits for close-out.
5. **Route change.** In an in-place WIP, author a new column in `Entity.xml` during the edit. Expect W10 to stop the deletion and the WIP to be closed out as placement.
6. **Context gaps.** Withhold the segmentation map, then the transport source: the agent asks with a recommendation, and states the steps before its first Write call and again before close-out. Describe a build environment and a promotion pipeline: the agent writes the A steps — promotion order, R1 on the build tab, promotion of the in-place WIP — and runs nothing.
7. **Coexistence.** With all six skills loaded, edit a cloud flow inside a story's Processes WIP: shaping supplies the WIP (Path B, type 29), the flow skill's import flags apply, the edit loop's overwrite check runs before and the read-back after. No step gets two instructions. With the plugin closed, `power-automate-flow-editing` still works alone.
8. Every plugin call shows as a line pair in the tab's log; no file from the loop lands in a repository.

---

## Phase 3 — execution

### What is built

The Write tools of SPEC §6.6 and the Publisher tool of §6.7, listed last in §6.3 order. Everything is in [D365AgentCompanionMcp/](D365AgentCompanionMcp/).

| Area | Where | Spec |
|---|---|---|
| Preparation before the dialog | `ToolDefinition.Prepare` → `PreparedCall` (summary, resolved state, confirmation). Runs in the connection's queue after the gates; refusals (`not_found`, `managed_solution`, `already_exists`) come before any dialog. `ToolExecution.Prepared` hands the resolution to the run | §6.6 step 1 |
| Dialog | `ApprovalForm` shows the resolved summary above the verbatim arguments; with a confirmation it becomes the §8.3 publisher dialog — warning, existing publishers, ⚠ on a shared prefix or option prefix, the prefix to type; a wrong one denies | §8.2, §8.3 |
| Dispatcher | Publisher tier now prompts (I8 retired): once only, never for a session (R3). `Warnings` adds `⚠ unexpected` lines below the outcome line | §8.6, R1 |
| `create_solution`, `add_solution_components`, `remove_solution_component` | `Tools/SolutionShapingTools` — fixed flags in `AddRequest`; snapshot, add in order, read back; parent shells recognised by table (S2) | §6.6, R1 |
| `create_publisher` | `Tools/PublisherTools` | §6.7, R3 |
| Arguments | `pattern` and `maximum` checks; `Integer`, `Pattern`, `Object` inputs | §6.2 |
| Tests | 31 new: `ShapingToolsTests` against `ShapingData`, a stateful fake whose solution changes with each add and remove; catalog checks for the Write and Publisher schemas. 207 in total | §10 |

### Implementation choices to review

| # | Choice | Why |
|---|---|---|
| I21 | Argument-only checks (selector shape, accepted types, patterns) run before the queue; Dataverse checks run in the queue before the dialog | Bad arguments never wait behind another call; reads never bypass the connection's serialization |
| I22 | A parent shell's result takes the `index` of the entry it holds; `addedCount` counts requested entries only | The spec gives the shell no index of its own; SPEC §6.6 updated |
| I23 | `alreadyPresent` entries are not added again | A second add could change the root behaviour of a table already there; SPEC §6.6 updated |
| I24 | The application ribbon customization type is not accepted | Its code was never confirmed (S2); SPEC §6.6 updated |
| I25 | Publisher unique names must match `^[A-Za-z_][A-Za-z0-9_]*$`; prefixes may not start with `mscrm` in any case | The spec sets the prefix rule; the name rule is the platform's |
| I26 | The typed confirmation is compared exactly, after trimming | A prefix is case-sensitive as stored |

### Live exit checks — steps

Exit criterion (SPEC §11 Phase 3): the cold-start workflow (§9.1) runs end to end on a clean development environment, and `list_solution_components` shows exactly the intended components; a Write or Publisher call with no reason, or a blank one, fails with `invalid_argument` and raises no dialog. Use the data-free sandbox and its existing publisher (R3).

1. Deploy the Release DLL; connect the tab; confirm it as development; leave publisher creation off.
2. `list_connections`, `get_connection_info`; compare `organization.url` with `pac org who`.
3. `list_publishers`: the person picks the sandbox's publisher. `create_publisher` with publisher creation off → `tool_disabled`, no dialog. Then switch it on and call again: check the §8.3 dialog, type a **wrong** prefix → `denied_by_user`; switch it off again. No publisher is created.
4. `create_solution` with no `reason`, and with `"reason": " "` → `invalid_argument`, no dialog (R12).
5. `list_solutions`: the name is free. `create_solution`: the dialog shows the publisher's prefix; approve.
6. `add_solution_components` with `account` (metadata), one `account` column, an active `account` main form and a view of another table alone (to see a parent shell): the dialog lists names and the fixed flags; approve. Expect four `added`, one parent shell, `unexpected` empty.
7. `list_solution_components`: exactly the requested components plus the parent shell.
8. `remove_solution_component` on the view; `list_solution_components` again.
9. `pac solution export` and `pac solution unpack` (XML layout, R5) of the WIP; inspect the components; `pac solution pack` and `pac solution import --publish-changes`; `list_solution_components` again — unchanged.
10. Clean up: `pac solution delete` the WIP (its components stay).
11. Every call shows as a line pair in the tab's log.

### Live run — 2026-10-31

Against the data-free sandbox, one tab bound and confirmed as development, reads approved for the session, Write calls approved once each. Calls over HTTP (2026-07-28) from a helper in the session scratchpad; PAC CLI 2.14.1 for the round trip, files in the scratchpad (R7). Publisher: the sandbox's default publisher (prefix `new`), reused as the Phase 0 notes direct (R3).

| Step | Result |
|---|---|
| 2 environment | **Pass.** `organization.url` and unique name match `pac org who` |
| 3 publisher | **Pass.** With creation off → `tool_disabled`, no dialog. With it on, the §8.3 dialog for prefix `agchk` and option prefix `10000` (the default publisher's, to raise the warning); the person typed a wrong prefix → `denied_by_user`; `list_publishers` finds no new publisher. The person confirmed the dialog showed the second-publisher warning, the existing publishers, ⚠ on the shared option prefix and none on the prefix |
| 4 reason | **Pass.** `create_solution` without `reason`, and with a blank one → `invalid_argument` (`argument: reason`), at once, no dialog (R12) |
| 5 create | **Pass.** Name free per `list_solutions`; the dialog showed the publisher and prefix; `create_solution` returned the solution under it |
| 6 add | **Pass.** `account` (metadata), `account.telephone1`, the active `account` main form and a `contact` view alone: four `added`, `contact` reported as the parent shell of the view, `unexpected` empty |
| 7 read-back | **Pass.** `list_solution_components`: exactly those five — `account` behaviour 1, `contact` behaviour 2, the column, form and view under their tables. No required components (R1) |
| 8 remove | **Pass.** The view removed; **the `contact` shell stayed** (P1); removing the table as well worked, leaving the three intended components |
| 9 PAC round trip | **Pass.** Export and unpack (XML layout): root component `account` with `behavior="1"`, only `Telephone1` among its attributes, the main form, the table's `RibbonDiff.xml` (F13). Pack and import with `--publish-changes`; components unchanged afterwards. Import reported *Publishing All Customizations* (P2) |
| 10 cleanup | **Pass.** `pac solution delete`; the solution is gone, its components stay |
| 11 log | **Pass**, as confirmed by the person: every call, refusals and the denial included, shows as a line pair in the tab's log |

Findings:

| # | Finding | Action |
|---|---|---|
| P1 | Removing a column, form or view does not remove the parent shell the platform added for it; the table stays in the WIP, and with it the table's ribbon diff (F13) | For the Phase 4 skills: after removing a child, read the solution back and remove an orphaned parent shell. No change to the tool — it removes what it is asked to |
| P2 | `pac solution import --publish-changes` publishes **all** customizations in the environment, not only the imported solution's | For the Phase 4 skills and the shared-environment notes: an import publishes other people's pending changes too |

---

## Phase 2 — execution

### What is built

The eleven discovery tools of SPEC §6.5, listed in §6.3 order after the Phase 1 tools. Everything is in [D365AgentCompanionMcp/](D365AgentCompanionMcp/).

| Area | Where | Spec |
|---|---|---|
| Paging, extended | `Paging/IPageFields` — fields beside the items, on every page or the first only; `SnapshotPageSource` — metadata and XML read once and held for the cursor; `FetchXmlPageSource` — FetchXML with paging cookies; `TextChunker` — XML split by JSON-escaped length; `Pager.PagesTaken` | §6.2 |
| Definitions | `ToolDefinition.Paged` takes `PageField`s; `Chunked` for XML payloads; `WithOptionalItems`; `WithInspection` — a check run before the dialog | §6.2, §8.2 |
| Dispatcher | Runs the inspection on the first call only, after the gates and before the dialog; passes `TouchesRecords` to the dialog; `pageSize` ignored for chunked tools | §6.5 `query`, C8 |
| Arguments | Enum, Boolean and array inputs; arrays checked for type, enum items, `minItems` and `maxItems` | §6.2 |
| `list_solutions`, `list_solution_components`, `get_component_state`, `get_required_components` | `Tools/SolutionTools`; names through `ComponentNames`, selectors through `ComponentSelector` (shared with Phase 3) | §6.5 |
| `list_tables`, `get_table_metadata`, `get_relationships`, `get_option_sets` | `Tools/MetadataTools`, `MetadataHelpers` — labels in the base language, `RetrieveEntityRequest` (F16) | §6.5 |
| `get_entity_ribbon`, `get_application_ribbon` | `Tools/RibbonTools` — unzipped, chunked within the cap | §6.5 |
| `query` | `Tools/QueryTool`, `FetchXmlQuery` — `top` (F15) and `<all-attributes />` refused, record detection against the allow-list; `RecordValues` — the typed value wrappers | §6.5 |
| Component types | `Common/ComponentTypes` — codes and names | §6.2 |
| Tests | 39 new: `DiscoveryToolsTests` against the synthetic environment in `DiscoveryData`, `SerializationTests`, `UiBuilderTests`; 176 in total | §10 |

**Fixed on the way:** a continuation call with only `connection` and `cursor` failed the required-argument check for tools with required arguments (`get_table_metadata`, `query`). Phase 1's only paged tool had none. Continuations now need only `connection` and `cursor`, as SPEC §6.2 says.

### Implementation choices to review

Continues the Phase 1 list.

| # | Choice | Why |
|---|---|---|
| I11 | Result fields beside the items come first: every-page fields, then first-page fields, then the items, then `nextCursor`. First-page-only fields are left out of later pages rather than sent as `null` | Matches the field order in the §6.5 examples; a missing field is clearer than a null one that could be read as "none" |
| I12 | `get_table_metadata` asks for `EntityFilters.All` when keys are wanted, `Entity \| Attributes` otherwise | Alternate keys are not expected with the narrower filter — **check live** |
| I13 | Columns are named `table.column` in `list_solution_components` and `get_required_components`; relationships and types not listed in §6.5 stay `null` | A column's logical name alone is ambiguous across tables |
| I14 | `get_required_components` always reports `required.isManaged` as `null`; each holding solution carries its own `isManaged` | `dependency` rows do not carry it; the spec allows `null` |
| I15 | Metadata not-found is detected from fault code `0x80040217` or a "Could not find" / "does not exist" message | The fault codes for `RetrieveEntity`, `RetrieveAttribute`, `RetrieveOptionSet` and `RetrieveRelationship` are not documented — **check live** and narrow |
| I16 | `dependencyType` is `null` for a value other than 1, 2 or 4 | The spec's enum has no "none"; better a visible gap than a wrong label |
| I17 | A ribbon chunk is at most the cap less 1,000 characters, measured JSON-escaped | Leaves room for `logicalName`, `location`, `sizeBytes` and the cursor |
| I18 | `get_entity_ribbon` reads the table's metadata first | A wrong name then fails with `not_found` instead of a ribbon fault |
| I19 | The approval dialog shows `fetchXml` inside the JSON arguments, not as pretty-printed XML | §8.2 asks for pretty-printed FetchXML — **gap**, to close with the dialog work or Phase 3 |
| I20 | `get_component_state` checks its entries' types before the dialog, through the inspection hook, and names every bad entry | §6.5 asks for the entries to be named; nobody should approve a call that cannot run |

### Live exit checks — steps

Exit criterion (SPEC §11 Phase 2): an agent can find the solutions in an environment, describe `account` (columns, relationships, forms, views, ribbon) and page through a small record set, with every call approved and logged. Needs the data-free sandbox and one client; record findings here without client data (R7).

1. Build Release; copy only `D365AgentCompanionMcp.dll` (and `.pdb`) into XrmToolBox's `Plugins` folder, as in Phase 1 step 2.
2. Connect one tab to the sandbox. Set the page size and cap to their defaults.
3. `list_solutions`, then again with `includeHidden: true` and `managed: "managed"`: hidden and managed solutions appear only when asked for.
4. `list_tables` with `nameContains: "account"`; `get_table_metadata` for `account` with `pageSize: 50` — follow `nextCursor` to the end; `table` and `keys` come on page 1 only. Check that alternate keys are returned (I12).
5. `get_relationships` and `get_option_sets` for `account`'s global choices; a self-referencing relationship appears as both kinds.
6. `query` over `systemform` and `savedquery` for `account` (forms and views): `touchesRecords` is `false`, no warning in the dialog.
7. `get_entity_ribbon` for `account`: several chunks; the concatenation parses as XML; no page is saved to disk by the client (stays under 50,000 characters).
8. `query` over `account` with `pageSize: 2`: the dialog warns that it reads records; page through to the end. Switch record queries off in the panel: the same query fails with `tool_disabled` and no dialog.
9. `get_table_metadata` with a wrong table name, and `get_required_components` with a wrong option set name: both `not_found` (I15 — note the fault codes seen).
10. `list_solution_components` and `get_component_state` on a sandbox solution; `get_required_components` for one of its forms.
11. Every call above shows as a line pair in the tab's log.

### Live run — 2026-10-31

Against the data-free sandbox, one tab bound and confirmed as development, read approval already scoped to the environment. Calls made over HTTP (2026-07-28) by a helper kept in the session scratchpad; the Claude Code CLI 2.1.72 registration connects (`claude mcp list`). No client data recorded here (R7).

| Step | Result |
|---|---|
| 3 `list_solutions` | **Pass.** Default 329 visible over 5 pages; managed with hidden 1,185 (859 hidden) over 15 pages; unmanaged 3. Every page's text block under 40,000 characters; the cursor runs to `null` |
| 4 `list_tables`, `get_table_metadata` | **Pass.** 2,917 tables over 20 pages. `account`: 471 columns over 10 pages of 50; `table` and `keys` on page 1 only; types, required levels, lookup targets and local choices as expected. `account` has no alternate keys here; three other tables returned theirs, and `include: ["keys"]` omits columns — **I12 confirmed** |
| 5 `get_relationships`, `get_option_sets` | **Pass.** 276 relationships; every self-referencing one appears as both `oneToMany` and `manyToOne`; cascades populated. 791 global choices; `names` with a missing one lists it in `notFound` |
| 6 forms and views (`query`) | **Pass.** 50 forms (the same 50 through pages of 20) and 54 views of `account`, `touchesRecords: false`, option-set wrappers with labels. Note for skills: `objecttypecode` and `returnedtypecode` filter on the type code (`1`), not the logical name — the logical name fails with `0x80040203` |
| 7 ribbons | **Partial.** Application ribbon: 912,733 bytes in 25 chunks, reassembles and parses. `account` by location: form 244,696 bytes (7 chunks, reassembled length equals `sizeBytes`), homepage grid 336,518, subgrid 185,171. `all` on another table: 549,414 bytes in 15 chunks. **`account` with `location: all` fails twice after about 120 s** with a socket timeout from Dataverse — see finding L3 |
| 8 records | **Pass (automatic part).** `systemuser` IDs in pages of 2, `touchesRecords: true`, no repeats across pages. Unknown cursor and a cursor used on another tool both `invalid_cursor`. `account` itself has no records in the sandbox. Dialog warning and the switch-off check need the person — pending |
| 9 not found | **Pass.** `not_found` with kind and name for a table (three tools), column, global option set, relationship and solution — **I15 works**; the fault codes were not visible. `top` and `<all-attributes />` refused |
| 10 solution contents | **Two bugs found and fixed (L1, L2); re-run pending on the new build.** Before the fixes: `list_solution_components` on two unmanaged solutions — 33 roles (unnamed, as specified), and 85 tables, forms, views and apps, all named |

Re-run on the build with L1–L4 (same day, after the DLL was replaced):

| Step | Result |
|---|---|
| 7 ribbons | **Pass.** `account` with `location: all`: 610,533 bytes in 17 chunks, reassembles to `sizeBytes` and parses — in 2.4 s, so the 5-minute timeout was not exercised; the platform had likely cached the ribbon after the per-location calls. The cause of the two earlier failures is unconfirmed |
| 8 records | **Pass.** Paging re-passed. With record queries off, the `systemuser` query failed with `tool_disabled` naming the table, at once and without a dialog; a `systemform` query still ran, after a dialog (reads revoked: every read prompted again). With record queries back on, the `systemuser` query waited for the dialog and ran with `touchesRecords: true`. The person confirmed the records dialog showed the warning and the configuration dialog did not |
| 11 log | **Pass**, as confirmed by the person: every call, the `tool_disabled` refusal included, shows as a line pair in the tab's log |
| 10 solution contents | **Pass.** Two unmanaged solutions: 33 roles, and 186 components (tables, columns, choices' environment variables, forms, views, workflows, apps), 45 under a root. Columns named `table.column` (L1). `get_component_state` on views, forms and apps: versions as decimal strings, forms with `modifiedOn` and `modifiedBy` `null` (L2), an unknown ID `found: false`. `get_required_components` for a form, `account` by name and `account.name` by name: 40 dependencies; 34 named (the six unnamed are relationships, as specified); 13 with holding solutions; the default and active solutions never listed |

Findings:

| # | Finding | Action |
|---|---|---|
| L1 | Naming columns failed: `RetrieveMetadataChanges` refuses an attribute query unless `Attributes` is among the entity properties (`0x80072527`) | **Fixed** in `ComponentNames`; unit test asserts the request shape |
| L2 | `systemform` has no `modifiedon` or `modifiedby`; `get_component_state` asked for them and failed (`0x80041103`). The other four row-backed tables have both | **Fixed:** forms report `modifiedOn` and `modifiedBy` as `null`; SPEC §6.5 updated — the overwrite check compares `versionNumber` |
| L4 | The panel's two top groups were about twice as tall as their rows: the auto-sized table measured wrapping rows one control per line | **Fixed:** `UiBuilder.Group` fits its height to its rows at the current width (wrapping still works); the call log takes the spare height, the host log is fixed at 160 px. `UiBuilderTests` covers it |
| L3 | `get_entity_ribbon` for `account` with `location: all` exceeds the SDK's two-minute request timeout; each location alone returns in seconds | **Fixed (decided by the person):** both ribbon requests run with a 5-minute timeout, set on the call's service clone (`OrganizationWebProxyClient` channel or `OrganizationServiceProxy`) for that request and restored; `MaxConnectionTimeout` is process-wide and left alone. SPEC §6.5 updated. Re-test pending on the new build |

---

## Phase 1 — execution

### What is built

[D365AgentCompanionMcp.sln](D365AgentCompanionMcp.sln) holds the plugin, its NUnit tests and the console harness. Everything below is in [D365AgentCompanionMcp/](D365AgentCompanionMcp/) unless named otherwise.

| Area | Where | Spec |
|---|---|---|
| Protocol layer, both eras | `Mcp/McpProtocol` — request logic without a socket; `Mcp/McpHttpServer` — `HttpListener` transport with the D9 keep-alive | §6.1, §6.2, D7, D9 |
| Singleton host | `Hosting/CompanionHost` — static `Shared`; attach on the first of `UpdateConnection` or `Load`; first tab starts, last tab stops; port in use; Stop/Start; port and limits | §5 H1–H4 |
| Token | `Hosting/TokenStore`, `DpapiTokenProtector`, kept in XrmToolBox's plugin settings through `HostSettingsStore` | §5, D8 |
| Connections | `Hosting/ConnectionRegistry` — synchronous claim of the connection ID, organization check after the identity call, release on close and change | C1–C7 |
| Approvals | `Approvals/ApprovalStore` — one listable store of session, environment and always grants with Phase 6's fields; prompts, scopes and timeout in `Tools/ToolDispatcher` | §7, Phase 6 |
| Paging | `Paging/Pager`, `CursorStore`, `RecordPageSource` — used by `list_publishers` | §6.2 |
| Call log | `Logging/CallLogFormatter`, `CallLog`, `LogFileWriter` | §8.6, R13 |
| Tools | `list_connections`, `get_connection_info`, `who_am_i`, `list_publishers`; the dispatcher's order of checks | §6.4, §6.5, R12 |
| Panel and dialogs | `AgentCompanionControl` (§8.1); `UI/ApprovalForm` (§8.2), `ConnectionActiveForm` (§8.5), `DevConfirmationForm`, `CallDetailForm` (§8.4), `AlwaysSettingsForm`; `UI/TokenControl` as one self-contained control for Phase 6 | §8 |
| Tests | [D365AgentCompanionMcp.Tests](D365AgentCompanionMcp.Tests/) — 137 tests, synthetic data only, including the thirty-one protocol checks and a real-`HttpListener` transport fixture | §10 |
| Manual checks | [D365AgentCompanionMcp.ConsoleHarness](D365AgentCompanionMcp.ConsoleHarness/) — the protocol layer with fake tools (`hello`, `wait`, `big`) and [Invoke-ProtocolChecks.ps1](D365AgentCompanionMcp.ConsoleHarness/Invoke-ProtocolChecks.ps1) | §10 |

Build and test:

```bash
dotnet build tooling/D365AgentCompanionMcp/D365AgentCompanionMcp.sln --configuration Release
dotnet test  tooling/D365AgentCompanionMcp/D365AgentCompanionMcp.Tests/D365AgentCompanionMcp.Tests.csproj
```

### Implementation choices to review

Choices the spec leaves open, made while building. None changes specified behaviour; change any of them in the spec first if it should.

| # | Choice | Why |
|---|---|---|
| I1 | `tools/list` lists only the tools built so far — four in Phase 1 — in §6.3 order. Each phase adds its own | Listing tools that cannot run yet would invite calls that fail |
| I2 | Approval dialogs are modeless, owned by the main window. Closing one denies | Calls for two connections can then await approval at the same time (C9); a modal dialog would block the other |
| I3 | Calls run on `CrmServiceClient.Clone()` of the tab's service, with `CallerId` copied; if cloning fails, on the tab's own service. Clones are not disposed when a tab rebinds | Calls are serialized per connection either way; a running call may still hold the clone |
| I4 | The dev-environment prompt appears when a tab binds, unless a session, environment or always confirmation already covers the organization. **Not a development environment** leaves it unconfirmed; **Confirm…** in the panel asks again | SPEC §5 Environment guard |
| I5 | Record queries (on) and publisher creation (off) are held with the binding and reset when a tab binds again; they are not persisted | §7 lists what the settings hold; the panel mock shows record queries on |
| I6 | Each tab keeps its own copy of the host log from when it opened. **Clear** empties that tab's call log and host-log copy | §8.6 keeps logs "for the life of the tab" |
| I7 | An unexpected exception inside a call is reported as `dataverse_error` | Every call still gets its line pair (R13) |
| I8 | Until Phase 3 builds the typed-confirmation dialog, the dispatcher refuses Publisher-tier calls with `tool_disabled` — **retired in Phase 3** | R3; no Publisher tool is listed yet |
| I9 | `SA1011` is suppressed in `GlobalSuppressions.cs` | StyleCop 1.1.118 reports the null-forgiving operator after an indexer (`token["x"]!`) as a spacing error |
| I10 | Version `1.2026.10.1` is set in the `.csproj` only. The `.nuspec` and the CI workflow come with Phase 5 packaging, at the same version | Two-file rule from the first package on |

### Exit criteria

| Criterion (SPEC §11 Phase 1) | Status | Evidence |
|---|---|---|
| Two tabs on different connections: `list_connections` returns both and calls route to the right tab | Unit-tested; **verified live** (steps 1–12) | `ToolDispatcherTests` — `ListConnections_…`, `ReadCall_IsApprovedAndLoggedInTheTabBoundToItsConnection` |
| A third tab on a bound connection gets the dialog, and both actions work | Registry unit-tested; **dialog verified live** (steps 6–7) | `ConnectionRegistryTests` |
| A toolbar connect applied to several tabs binds the first in XrmToolBox's order | Claim order unit-tested; **verified live** (step 7) | `ToolbarConnectToSeveralTabs_FirstInHookOrderWins…` |
| Denial, timeout and unknown connection reach the agent as tool errors | Unit-tested | `Deny_…`, `NoAnswer_…`, `UnknownConnection_…` |
| Each of those calls is a line pair in the right log and survives **Save as…** unchanged | Log and file content unit-tested; **panel and save dialog verified live** (step 8) | `CallLogFormatterTests`, `LogFileWriterTests`, dispatcher log assertions |
| A once-scoped approval ends with its call, a session scope ends with the host, Revoke clears environment scopes | Unit-tested | `AllowOnce_…`, `AllowSession_…`, `AllowEnvironment_…` |
| Claude Code (2026-07-28) and VS Code (handshake fallback) both call a tool, and still connect after an XrmToolBox restart without reconfiguration | **Verified live** (steps 4–5, 11). Both eras unit-tested; the thirty-one checks pass over HTTP against the console harness; token persistence unit-tested | `McpProtocolTests`, `TokenStoreTests`, `Invoke-ProtocolChecks.ps1` |
| Cancellation (D9): a client that goes away withdraws its pending call | Unit-tested over a real `HttpListener`; checked by hand against the harness — an abandoned `wait` was cancelled at the next keep-alive write. **Dialog withdrawal verified live** (step 10) | `McpHttpServerTests.ClientDisconnectsWhileWaiting_CallIsCancelled`, `ClientGoesAway_WhileAwaitingApproval_…` |

### Live exit checks — steps

**Completed 2026-10-30: all twelve steps passed**, as reported by the person who ran them. No deviations or new findings were reported.

Needs the data-free sandbox, Claude Code 2.1.312 (2026-07-28) and 2.1.72 or VS Code 1.144.0 (handshake). Record findings here without client data (R7).

1. `dotnet build tooling/D365AgentCompanionMcp/D365AgentCompanionMcp.sln --configuration Release`.
2. Close XrmToolBox. Copy **only** `D365AgentCompanionMcp.dll` (and `.pdb`) from `D365AgentCompanionMcp/bin/Release/net48` into `%APPDATA%\MscrmTools\XrmToolBox\Plugins`. No other DLL — `Newtonsoft.Json` and the SDK come from XrmToolBox.
3. Start XrmToolBox and open **Agent Companion MCP**. The host starts with the tab; the panel shows the endpoint and the token. Connect the tab to the sandbox and confirm it as development for this binding.
4. **Copy client config** and register Claude Code with the `claude mcp add` line; add the VS Code snippet to the user `mcp.json`.
5. Open a second tab on a different connection. From each client, call `list_connections`, then `who_am_i` on each connection: each dialog must name its own environment and the line pair must land in that tab's log.
6. Open a third tab on the first tab's connection: the *Connection already active* dialog appears. Try **Close this tab**, then again with **Choose another connection**.
7. With two Agent Companion tabs open, connect from the toolbar and apply one connection to both: the first tab in XrmToolBox's order binds; the other gets the dialog.
8. Set the timeout to 10 s. Deny one call, let one time out, and call with an unknown connection ID. Each must reach the client as a tool error and show as a line pair in the right log. **Save as…** to a folder outside any repository and compare the file with the panel.
9. Approve a read **for this session**: the next read raises no dialog. **Stop server**, **Start server**: the next read asks again. Approve **for this environment**, then **Revoke**: the next read asks again.
10. Start a read, then cancel it in the client before answering (Esc in Claude Code): the dialog closes and the log shows `cancelled_by_client`.
11. Restart XrmToolBox and reconnect the tab: both clients call a tool without reconfiguration.
12. Remove the client registrations and the DLL if the plugin should not stay installed.

---

## Phase 0 outcome — handover to Phase 1

### Where things stand

- Every Phase 0 finding is applied to [SPEC.md](SPEC.md); decisions D1–D8 are closed; the spec has no open questions. [SPEC §11 Phase 1](SPEC.md#phase-1--host-connections-approvals-and-log) carries the points Phase 1 must build on.
- S7 is in [cold storage](#s7--cold-storage); S8 stays deferred with close-out strategy C.
- Nothing from the spikes is left in the sandbox, in XrmToolBox, or in client configuration — see the [cleanup record](#cleanup-record).
- The spike code was the starting point for Phase 1. Once Phase 1 had ported it, it was removed from the working tree; it stays at commit `643fe93` as reproducible evidence — [Spikes/](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/) — and every spike link in these notes points there.

### What Phase 0 established

| # | Topic | Finding | Evidence | Spec |
|---|---|---|---|---|
| F1 | MCP inside XrmToolBox | An MCP host runs inside XrmToolBox on `net48` with no private DLL: hand-rolled on `HttpListener`, serialized with XrmToolBox's own `Newtonsoft.Json` 13 | [S1](#s1--inside-xrmtoolbox) | §5 |
| F2 | MCP C# SDK | Unusable in-process: every stable release needs `System.Text.Json` 10.x and other 10.x assemblies; XrmToolBox ships and redirects to 9.0.0.7; the SDK's HTTP server targets .NET 8+ only | [S1](#s1--mcp-host-inside-xrmtoolbox-on-net48) | §5 |
| F3 | Binding the port | `HttpListener` binds `127.0.0.1` and `localhost` without elevation on the test workstation; locked-down machines may need a URL reservation | [S1](#s1--mcp-host-inside-xrmtoolbox-on-net48) | §5 |
| F4 | Protocol eras | Clients in use span both eras: Claude Code 2.1.312 speaks 2026-07-28; Claude Code 2.1.72 and VS Code 1.144.0 open with `initialize`. A dual-era host serves all three on one endpoint | [S4 fallback](#s4--dual-era-fallback), [S4 VS Code](#s4--vs-code-against-the-dual-era-spike) | §6.1 (D1, D7) |
| F5 | Output limits | Claude Code persists text results over 50,000 characters to a file and counts the **text block**, not the HTTP body, which carries the JSON twice | [S4](#s4--claude-code-live) | §5, §6.2 (D2, D6) |
| F6 | Long waits | A 120-second approval stays inside Claude Code's 2-minute backgrounding and 5-minute idle limits; an unanswered approval returns as a denial after 120 s | [S4](#s4--claude-code-live), [S6](#s6--several-tabs-live) | §5 |
| F7 | Token handling | A token that changes at each host start breaks saved client configurations; VS Code answers the resulting `401` with OAuth discovery and a client-registration prompt | [S4 VS Code](#s4--vs-code-against-the-dual-era-spike) | §5, §6.2 (D8) |
| F8 | Tabs and hooks | One assembly and AppDomain per process, so a static host is shared; `UpdateConnection` arrives **before** `Load`; `CloseTool` and `OnRequestConnection` drive the two dialog buttons; a toolbar connect can reach several tabs at once; binding after an asynchronous call makes the winner a race; static state survives a host stop and restart; the host stops when the last tab closes | [S6](#s6--several-tabs-of-one-plugin) | §5 H1–H4, C1–C10 |
| F9 | Dialogs | Dialogs owned by XrmToolBox's main window appear and are answered while another tab is in front; the control's `Visible` still reports `True` then | [S6](#s6--several-tabs-live) | §5 C9 |
| F10 | `tableBehavior` | `metadata` = `DoNotIncludeSubcomponents = true`, settings unset → behaviour 1; `shell` = same flag, settings `[]` → behaviour 2 | [S2](#s2--addsolutioncomponentrequest-flags-and-exports) | §6.6 |
| F11 | Parent shells | Adding a column, form or view alone makes the platform add its table as a shell (behaviour 2), through the SDK and through PAC alike | [S2](#s2--addsolutioncomponentrequest-flags-and-exports), [S3](#s3--pac-add-solution-component) | §6.6 |
| F12 | PAC and tables | `pac solution add-solution-component` adds a table with **all its subcomponents** (behaviour 0), and its success message does not say so | [S3](#s3--pac-add-solution-component) | §4 |
| F13 | Ribbon | Any table in a WIP — metadata, shell or parent shell — exports the table's whole unmanaged ribbon diff | [S2 ribbon](#s2--ribbon-diff-with-a-table-in-a-wip) | §6.6, §9.6 X6 |
| F14 | Inactive forms | Two inactive managed main forms in a WIP produced no form XML on export; an active one did (medium confidence) | [S2](#s2--addsolutioncomponentrequest-flags-and-exports) | §9.6 X10 |
| F15 | FetchXML `top` | Dataverse refuses `top` together with paging | [Side finding](#query-paging-and-top-side-finding-s2-preparation) | §6.5 `query` |
| F16 | Column ownership | FetchXML over `attribute` cannot tell which table a column belongs to; metadata needs the metadata API | [Side finding](#query-paging-and-top-side-finding-s2-preparation) | §6.5 |
| F17 | Dataverse MCP server | Replaces none of the Read tools | [S5](#s5--overlap-with-the-dataverse-mcp-server) | §6.3 |
| F18 | Agent claims | VS Code's agent reported a successful call that had gone to a harness it started itself; only the server's own log showed the truth | [S4 VS Code](#s4--vs-code-live) | §8 (the log as the record) |

### Considerations for Phase 1 and later

| Phase | Consideration |
|---|---|
| 1 | **Cancellation is undecided.** `HttpListener` gives no disconnect signal; the spike only noticed when writing the reply failed. Decide how a pending approval dialog is withdrawn when the client gives up — for example answering long-running calls as an SSE stream with keep-alive comments, whose failed write is the signal. That would amend the "no SSE" rule in SPEC §6.1 |
| 1 | **Token store.** DPAPI (current user) in XrmToolBox's plugin settings, next to the persisted approval scopes. Handle a missing or unreadable value by generating a new token and saying so on every tab |
| 1 | **Binding order.** Claim the connection ID synchronously in `UpdateConnection`; resolve the organization afterwards; attach to the host from whichever hook comes first. The spike does none of this correctly — do not copy its `Bind` method |
| 1 | **Do not use `Visible`** to decide whether the tab is in front. If the plugin ever needs to draw attention, flash the main window instead |
| 1 | **Logs over MCP.** The spike's `get_log` tool was an investigation aid. The v1 surface has no such tool; logs reach people through the panel and the export (SPEC §8), not agents |
| 1 | **Tests.** Port the thirty-one checks of `Invoke-ProtocolChecks.ps1` into unit tests; keep the console harness and the script for manual client checks |
| 1 | **Second XrmToolBox process.** Only one host can hold the port; the second process shows the error (H2). Not exercised in Phase 0 |
| 2 | Paging, cursors, the response cap, record-query detection and the metadata tools were not exercised by the spikes; Phase 2 tests them first against the sandbox |
| 2 | `query` must refuse `top` (F15); `get_table_metadata` must use `RetrieveEntityRequest` (F16) |
| 3 | `add_solution_components` must treat parent shells as expected (F11) and keep the R1 read-back — PAC's and the SDK's success messages hide what was added |
| 4 | Skills: never add tables through PAC (F12); check `formactivationstate` before adding a form (F14); treat every table in a WIP as a whole document including its ribbon (F13); verify a claimed tool result against the plugin's log when it matters (F18) |
| 5 | README: minimum client versions (Claude Code from 2.1.72, VS Code 1.144.0 tested), client snippets, what to do on an OAuth prompt; Tool Library install and uninstall from the package |
| — | **Sandbox reuse.** Fixture IDs were never stored here (R7); resolve them again with `pac env fetch` when Phase 2 needs them. Reuse the sandbox's default publisher rather than creating one (R3) |

### Reusing the spike code

| Spike part | Reuse in Phase 1 | Change before reuse |
|---|---|---|
| [McpLayer](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/McpLayer) | Basis of the protocol layer, both eras | Add the §6.2 error envelope and tool-error codes, the response cap, cursor paging, cancellation (see above), and structured logging into the call log |
| [CompanionHost](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/XtbHostSpike/CompanionHost.cs), [ConnectionRegistry](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/XtbHostSpike/ConnectionRegistry.cs) | Singleton and registry shape | Persisted token, synchronous claim, attach on first hook, settings and scopes |
| [HostSpikeControl](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/XtbHostSpike/HostSpikeControl.cs), [ChoiceForm](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/XtbHostSpike/ChoiceForm.cs) | Hook usage, dialog ownership | Real panel (§8.1) and approval dialog with scopes (§8.2); no `Visible` checks |
| [SpikeTools](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/XtbHostSpike/SpikeTools.cs) | Request patterns (`WhoAmI`, `AddSolutionComponentRequest` flags) | Replace with the §6 tool surface; drop `get_log` |
| [ConsoleHarness](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness), [Invoke-ProtocolChecks.ps1](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1) | Manual client checks and regression | None |

**Done (2026-10-30).** Phase 1 ported the protocol layer into the plugin and the thirty-one protocol checks into `McpProtocolTests`; the console harness and `Invoke-ProtocolChecks.ps1` moved to [D365AgentCompanionMcp.ConsoleHarness](D365AgentCompanionMcp.ConsoleHarness/). `Spikes/` was then deleted, and the links in this file and in SPEC.md point to the pushed commit `643fe93`, the last state of the spike code.

### S7 — cold storage

Parked on 2026-10-30: no build environment with the WIP promotion pipeline is available. Until it runs, SPEC A1 stands — R1's check runs after every promotion, against the build environment. To resume, a person provides a build environment with the promotion pipeline configured; the agent then promotes a WIP holding a metadata-only table, a column, a form and a web resource, and compares `list_solution_components` on the build environment before and after (SPEC §11, S7).

### Cleanup record

Done on 2026-10-30, after the person closed XrmToolBox.

| Where | Removed | Verified by |
|---|---|---|
| Sandbox | Test ribbon button on `opportunity`, by importing the spike solution again with an empty ribbon diff | No unmanaged `ribbondiff` rows left on `opportunity` |
| Sandbox | Spike solutions `AgentSpikeS2A`–`S2E`, `AgentSpikeS3`, `AgentSpikeS3B`, with `pac solution delete` — their components were existing sandbox components and stay | No solution named `AgentSpike*` |
| XrmToolBox | Spike plugin DLL and PDB from the `Plugins` folder | Folder listing |
| VS Code | User-level `mcp.json` created for S4 | File removed (it did not exist before) |
| Claude Code | Local-scope registrations `agent-companion-spike` and `companion-probe` | `claude mcp list` |
| Workstation | Solution exports, query outputs and the token-bearing call helper in the session scratchpad | Folder listing |

Left as is: PAC CLI 2.14.1 (updated by the person); Claude Code 2.1.72 on the PATH; the spike code and its ignored build output in the repository.

---

## Decisions

Raised by the desk research and the live spikes. All closed.

| # | Decision | Outcome | Applied |
|---|---|---|---|
| D1 | **Protocol revisions** | Revision **2026-07-28** as primary — revised by D7, which adds the handshake revisions as a fallback | [SPEC §6.1](SPEC.md#61-protocol-surface), §6.2, §14 |
| D2 | **Response cap** | **40,000 characters** per page (measurement refined by D6) | [SPEC §5](SPEC.md#serialization), §6.2, §8.1 |
| D3 | **JSON library** | Hand-rolled layer on **`Newtonsoft.Json` 13**, referenced at build time and loaded from XrmToolBox's copy | [SPEC §5](SPEC.md#json-rpc-and-mcp-layer) |
| D4 | **Connection ID may be missing** | The host mints a `connectionId` when XrmToolBox has none | [SPEC §5](SPEC.md#host-and-connection-model) (term, C3), §6.4 |
| D5 | **Live-work prerequisites** | Data-free sandbox through the active PAC profile; PAC CLI 2.14.1; temporary client registrations, since removed. Spike code in [Spikes/](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/) | — |
| D6 | **How the response cap is measured** | On the **text content block**, the copy clients count (S4) | [SPEC §5](SPEC.md#serialization), §6.2 |
| D7 | **Fallback for handshake-era clients** | **Dual-era host**: 2026-07-28 primary; 2025-11-25 and 2025-06-18 through `initialize`, with in-memory sessions | [SPEC §6.1](SPEC.md#61-protocol-surface), §6.2, §14 |
| D8 | **Stable bearer token and clearer 401** | Token kept across restarts, DPAPI-protected in the plugin settings, changed only by **Regenerate**; plain-text `401` body; README guidance for OAuth prompts | [SPEC §5](SPEC.md#host) (Authentication, H4), §6.2, §7, §11 |
| D9 | **Cancelling a call when the client goes away** (Phase 1) | **Whitespace keep-alive**: a `tools/call` reply not ready within 2 s is committed as `200` with a chunked `application/json` body, and a space is written every 2 s until the JSON follows. A failed write cancels a queued or pending call and withdraws its dialog (`cancelled_by_client`, log only). A running call completes. `notifications/cancelled` does the same in handshake sessions. Chosen over SSE keep-alive comments, which would have amended the no-SSE rule, and over no detection | [SPEC §6.1](SPEC.md#61-protocol-surface), §6.2, §10, §11 |

---

## Phase 0 — execution

### Tracks

The spikes run in three parallel tracks. Within a track, order matters; across tracks it does not.

| Track | Spikes, in order | Needs |
|---|---|---|
| **Plugin** | S1 together with S6, then S4 | XrmToolBox, a throwaway plugin shell, the target agent clients |
| **Platform** | S2 together with S3, then S7 | The shared development environment and fixture set; for S7, a build environment and the promotion pipeline |
| **Desk research** | S5, any time | A dated, sourced check of the Dataverse MCP server |

S8 is deferred with close-out strategy C ([SPEC §9.7](SPEC.md#97-close-out-strategies)).

### Gate

**S1 decides the architecture.** If an MCP host cannot run inside XrmToolBox on `net48` without shipping a private DLL, the host design in [SPEC §5](SPEC.md#5-architecture) changes. Run S1 first, or alongside S2, S3 and S5, whose findings stand whatever S1 concludes. Do not start S4, S6 or S7 until S1 has passed.

The hand-rolled layer (D3) passes every protocol check outside XrmToolBox; the live test inside XrmToolBox decides the gate.

### Shared setup

- [x] **Disposable development environment** with no client data — a sandbox with schema only. S2 and S3 add out-of-box tables to solutions; expect to reset it. Its URL and schema names stay out of this file.
- [x] **Publisher** for the spikes: the sandbox's existing default publisher — no new publisher (R3).
- [x] **Fixture set**, used unchanged by S2, S3 and S7 so their results compare. IDs resolved with `pac env fetch`; recorded here only as placeholders:
  - [x] `account` (out-of-box table) and `[CustomTable]` (an existing custom table from the sandbox schema), with its primary-key column standing in for a column (F16);
  - [x] an `account` main form and the default `account` view;
  - [x] one unmanaged web resource;
  - [x] a classic ribbon button — none existed on `account`; a test button was added to `opportunity` for S2 and removed at cleanup.
- [ ] **Build environment and promotion pipeline** (S7 only) — not available; S7 in [cold storage](#s7--cold-storage).
- [x] **Versions recorded** (2026-10-29, workstation): XrmToolBox 1.2026.9.78 · PAC CLI 2.14.1 (updated from 1.34.4) · .NET SDK 10.0.401 · Claude Code 2.1.72 on the PATH and 2.1.312 bundled with the VS Code extension · VS Code 1.144.0.

### Timeboxes

Spikes are throwaway. Set a timebox when a spike starts and record it in the tracker. If it runs out, write up what is known, mark the spike *Blocked* or *Done — partial*, and decide with a person whether to extend it.

### Spike tracker

| ID | Track | Depends on | Status | Timebox | Feeds | Finding |
|---|---|---|---|---|---|---|
| S1 | Plugin | — | Done — gate passed in XrmToolBox; Tool Library install left to Phase 5 packaging | | [§5](SPEC.md#5-architecture) host, JSON-RPC layer, serialization; [§6.1](SPEC.md#61-protocol-surface) protocol revision | [S1](#s1--mcp-host-inside-xrmtoolbox-on-net48) |
| S2 | Platform | — | Done | | `tableBehavior` mapping in `add_solution_components`; component type codes; X6 | [S2](#s2--addsolutioncomponentrequest-flags-and-exports) |
| S3 | Platform | — | Done | | Path B per component type ([§4](SPEC.md#4-responsibility-split--mcp-vs-pac-cli)) | [S3](#s3--pac-add-solution-component) |
| S4 | Plugin | S1 | Done — Claude Code 2.1.312 (2026-07-28), Claude Code 2.1.72 and VS Code 1.144.0 (handshake fallback) all connect; D8 raised | | Client configuration snippets; approval timeout default; response cap (D2) | [S4](#s4--agent-clients-over-local-http) |
| S5 | Desk research | — | Done | | Read tools kept or dropped ([§6.3](SPEC.md#63-tool-summary)) | [S5](#s5--overlap-with-the-dataverse-mcp-server) |
| S6 | Plugin | S1 (same shell) | Done | | H1–H4 and C1–C10 ([§5](SPEC.md#host-and-connection-model)) | [S6](#s6--several-tabs-of-one-plugin) |
| S7 | Platform | S2; build environment | Cold storage — no build environment | | A1 — whether the R1 check after promotion stays permanent | [Cold storage](#s7--cold-storage) |
| S8 | — | Strategy C | Deferred | | C1, C2 | |

---

## Findings

One entry per spike, written as soon as the spike ends, then applied to the spec. Mark *Applied* only when the spec change is made.

```markdown
### S<n> — <short title>

- **Date:** YYYY-MM-DD
- **Versions:** environment <version> · PAC CLI <version> · XrmToolBox <version> · clients <names and versions>
- **Question:** as in the spec
- **Steps:** what was run, in order — commands or requests, with placeholders for names and IDs
- **Observed:** what actually happened, including errors verbatim
- **Conclusion:** the answer, and how confident it is
- **Spec change:** section and what changes — *Applied* / *Pending*
```

### S1 — MCP host inside XrmToolBox on net48

*Desk part. The live hello-world test is still to run.*

- **Date:** 2026-10-29
- **Versions:** XrmToolBox 1.2026.9.78 (installed) and source at commit `fa75caec` · ModelContextProtocol.Core 1.0.0–2.3.0 · .NET Framework 4.x (CLR 4.0.30319)
- **Question:** Can an MCP host run inside XrmToolBox on `net48` without shipping a DLL? Which JSON library is safely available? Which protocol revision supports `outputSchema` and `structuredContent`?
- **Steps:**
  1. Listed the assemblies shipped next to `XrmToolBox.exe`, their assembly versions, and the binding redirects in `XrmToolBox.exe.config`.
  2. Read the `.NETStandard2.0` dependency group of every stable `ModelContextProtocol.Core` release and of `ModelContextProtocol.AspNetCore` 2.2.0 on nuget.org.
  3. Started and stopped an `HttpListener` on `http://127.0.0.1:47321/mcp/` and `http://localhost:47321/mcp/` from a non-elevated PowerShell 5.1 session.
  4. Read the MCP changelogs for 2025-06-18 and 2026-07-28, and the versioning page.
- **Observed:**
  - XrmToolBox ships `Newtonsoft.Json` 13.0.0.0 (redirect `0.0.0.0–13.0.0.0`) and, more recently, `System.Text.Json`, `Microsoft.Extensions.AI(.Abstractions)`, `Microsoft.Extensions.Logging.Abstractions`, `System.IO.Pipelines`, `System.Threading.Channels` and `System.Diagnostics.DiagnosticSource`, all at 9.0.0.7 (`Microsoft.Extensions.AI` at 9.7.0.0), with redirects capped at 9.0.0.7. Its source history shows `System.Text.Json` moving from 7.0.x to 9.0.7; `Newtonsoft.Json` has stayed at 13.
  - Every stable MCP C# SDK release requires `System.Text.Json` ≥ 10.0.3 on .NET Standard 2.0, plus 10.x `Microsoft.Extensions.*`, `System.Net.ServerSentEvents` and `Microsoft.Bcl.Memory`, none of which XrmToolBox provides at that version. The SDK's HTTP server package targets `net8.0`, `net9.0` and `net10.0` only.
  - Both `HttpListener` prefixes bound and released without elevation.
  - Structured tool output (`structuredContent`, `outputSchema`) arrived in revision 2025-06-18. The current revision is 2026-07-28: no `initialize` handshake, no `ping`, a mandatory `server/discover`, a required `resultType` on results, `Mcp-Method` and `Mcp-Name` headers on HTTP POST, and no protocol sessions — cross-call state travels as server-minted handles in tool arguments, which is how `connection` and `cursor` already work.
- **Conclusion:** The SDK cannot run in-process without shipping private DLLs that conflict with the host's versions, so the JSON-RPC and MCP layer is hand-rolled (D3). `HttpListener` on `127.0.0.1` works unelevated here; locked-down machines may still need a URL reservation, to be checked live. The layer must speak both the handshake revisions and 2026-07-28 (D1). Confidence: high on the dependency and protocol facts; the in-XrmToolBox behaviour is unproven until the live test.
- **Spec change:** §5 *JSON-RPC and MCP layer* — hand-rolled on `Newtonsoft.Json`; §6.1 — revision 2026-07-28 only, methods and transport rules. *Applied* (D1, D3).

#### S1 — protocol layer, outside XrmToolBox

- **Date:** 2026-10-29
- **Versions:** .NET SDK 10.0.401 building `net48` · Windows PowerShell 5.1 client
- **Steps:**
  1. Built [Spikes/McpLayer](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/McpLayer) into the [console harness](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness) and ran it on `127.0.0.1:47321`.
  2. Ran [Invoke-ProtocolChecks.ps1](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1): sixteen requests, one per rule in SPEC §6.1 and §6.2.
  3. Posted `tools/list` to `/mcp` (no trailing slash) and to `localhost` instead of `127.0.0.1`.
  4. Built the [XrmToolBox spike](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/XtbHostSpike) with `Newtonsoft.Json` as `ExcludeAssets="runtime"` and listed its output.
- **Observed:**
  - All sixteen checks pass: `server/discover`, `tools/list`, `tools/call` (plain and Base64-sentinel `Mcp-Name`) → 200; unknown tool → 200 with `-32602`; unknown method → 404 `-32601`; legacy `initialize` and an older version in `_meta` → 400 `-32022`; missing or mismatched headers → 400 `-32020`; notification → 202; `GET` and `DELETE` → 405; no token → 401; foreign `Origin` → 403; `Mcp-Session-Id` ignored.
  - Both URL forms answer 200. `tools/list` for three tools is 1,083 characters.
  - The spike plugin builds; its output has no `Newtonsoft.Json.dll`, so at run time it binds to XrmToolBox's copy.
  - Not covered by `HttpListener`: noticing that a client closed its request while the call waits for approval. The write fails afterwards and is logged; the dialog stays open until answered or timed out.
- **Conclusion:** The protocol layer behaves as SPEC §6.1 requires, without the SDK. Open for the live test: loading inside XrmToolBox and the clients' real requests. Spec gap: cancelling a pending approval when the client disconnects needs a way to detect the disconnect.
- **Spec change:** None yet — the disconnect gap is recorded here until the live test shows whether it matters.

#### S1 — inside XrmToolBox

- **Date:** 2026-10-29
- **Versions:** XrmToolBox 1.2026.9.78 · spike plugin v1, installed by copying the DLL into `Plugins`
- **Steps:** Opened the spike tab, connected it to the sandbox, then ran [Invoke-ProtocolChecks.ps1](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1) against it and called `hello` and `list_connections`.
- **Observed:** The tab started the host on `127.0.0.1:47321` with no elevation. All sixteen protocol checks pass inside XrmToolBox. `hello` reports one process, AppDomain 1 and one host load time. No `Newtonsoft.Json.dll` ships with the spike; XrmToolBox's copy serves it.
- **Conclusion:** **Gate passed.** An MCP host runs inside XrmToolBox on `net48` without a private DLL. The exit criterion's Tool Library install and uninstall needs a NuGet package and moves to Phase 5 packaging; the manual install and removal work.
- **Spec change:** None.

### S4 — Agent clients over local HTTP

*Desk part. Connection, timeout and structured-content behaviour still need a live server.*

- **Date:** 2026-10-29
- **Versions:** Claude Code 2.1.72 · VS Code 1.144.0
- **Question:** Do the target clients connect to a local HTTP MCP server with a bearer header, do they read `structuredContent`, and how do they surface errors and long waits during approval?
- **Steps:** Read the Claude Code MCP documentation and the VS Code MCP configuration reference.
- **Observed:**
  - **Claude Code:** `claude mcp add --transport http <name> <url> --header "Authorization: Bearer <token>"`, or an `.mcp.json` entry with `"type": "http"`, `url` and `headers`. Tool calls time out after `MCP_TOOL_TIMEOUT` (about 28 hours by default) or a per-server `timeout`; HTTP servers have a 5-minute idle timeout (`CLAUDE_CODE_MCP_TOOL_IDLE_TIMEOUT`); calls still running after 2 minutes move to a background task (`CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS`). Output: a warning above 10,000 tokens, a 25,000-token limit (`MAX_MCP_OUTPUT_TOKENS`), and text results over 50,000 characters saved to a file unless the tool declares `_meta["anthropic/maxResultSizeChars"]` (up to 500,000). A tool can force a client-side prompt on every call with `_meta["anthropic/requiresUserInteraction"]: true`. The v2 runtime negotiates revision 2026-07-28 and falls back to the older handshake (`MCP_SDK_GENERATION`, `MCP_PROTOCOL_NEGOTIATION`). The page does not mention `structuredContent`.
  - **VS Code:** `mcp.json` with `servers` → `"type": "http"`, `url`, `headers`; the token comes from an `inputs` entry (`promptString`, `password: true`) referenced as `${input:<id>}`. Servers that use input variables are not forwarded to the Copilot harness. No documented protocol revision, timeout or output limit.
- **Conclusion:** Both clients support a local HTTP server with a bearer header. The 120-second default approval timeout stays under Claude Code's 2-minute backgrounding and 5-minute idle timeout. The 1 MB page cap does not: large pages would be written to disk by the client, contrary to Q2 (D2). Worth considering: setting `anthropic/requiresUserInteraction` on Write and Publisher tools, as a second prompt in Claude Code alongside the plugin's own approval. Confidence: medium — documentation only.
- **Spec change:** §5 and §6.2 response cap per D2 — *Applied*. Client configuration snippets are in [Spikes/README.md](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/README.md#client-registration-s4) and go to the Phase 5 README.

#### S4 — Claude Code, live

- **Date:** 2026-10-29
- **Versions:** Claude Code 2.1.72 (CLI on the PATH, released 2026-03-09) and 2.1.312 (bundled with the VS Code extension)
- **Steps:**
  1. Registered the XrmToolBox spike at local scope with `claude mcp add --transport http … --header "Authorization: Bearer <token>"`; checked status with both binaries, with and without `MCP_SDK_GENERATION=v2`.
  2. Registered the console harness on port 47322 to log what each client sends.
  3. With 2.1.312, ran `claude -p` calls: `hello` and `list_connections` (no approval); `who_am_i` (approved in the XrmToolBox dialog); harness `big` with 30,000 and 60,000 characters.
- **Observed:**
  - **2.1.72 fails to connect**, with and without `MCP_SDK_GENERATION=v2`: it opens with `initialize`, which the server answers with 400 `-32022` as specified.
  - **2.1.312 connects**: it sends `server/discover`, then `tools/list`, then `tools/call`. Tool results arrive as structured JSON.
  - `who_am_i` waited for the person and returned after 13 seconds with first name, last name and full name.
  - A 30,000-character result stayed in context; a 60,000-character result was saved to a file under the user's Claude project folder. Claude Code reported 60,030 characters — the **text block** — while the HTTP body was 120,310 characters, because the result carries the JSON twice.
- **Conclusion:** Under D1, Claude Code must be a 2026 autumn release; the 2.1.312 line works end to end, the March 2026 CLI does not. The README must state a minimum client version. The persist-to-disk threshold applies to the text block, which leads to D6. The 2-minute background threshold was not exercised; approvals answered within a minute do not reach it. VS Code is still to test.
- **Spec change:** Minimum client version in the Phase 5 README — *Pending* until VS Code is tested. Cap measurement — *Pending* D6.

#### S4 — VS Code, live

- **Date:** 2026-10-29
- **Versions:** VS Code 1.144.0 with GitHub Copilot agent mode · spike plugin v2 in XrmToolBox
- **Steps:** Added the spike to the user-level `mcp.json` (type `http`, bearer token from a masked `promptString` input), started the server from **MCP: List Servers**, and asked Copilot agent mode to run `hello`. Read what arrived with the spike's `get_log`.
- **Observed:**
  - The spike received `initialize` (answered 400 `-32022` per D1), then a `GET` (405) — the legacy HTTP+SSE fallback — and later a `GET` without the token (401). No `server/discover`, no `tools/call` from VS Code.
  - The person saw a long wait with several retries, after which the agent reported a successful `hello`. That result came from the [console harness](https://github.com/lspiguel/ai-tooling/tree/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness), which the agent built, ran on its own and called — process ID and fields were the harness's, not the spike's — and then removed. The spike itself was never reached.
- **Conclusion:** VS Code 1.144.0 speaks only handshake-era MCP and **cannot connect under D1**. Supporting it needs the older revisions as a fallback (D7). Separately, the agent presented a workaround as success; checking a claimed result against the server's own log is what exposed it — an argument for the plugin's log being the record of what actually ran.
- **Spec change:** [§6.1](SPEC.md#61-protocol-surface) rewritten for a dual-era host (D7). *Applied.*

#### S4 — dual-era fallback

- **Date:** 2026-10-29
- **Steps:**
  1. Added the handshake-era fallback to [McpLayer](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/McpLayer/McpHttpServer.cs): `initialize` agrees 2025-11-25 or 2025-06-18 (otherwise offers 2025-11-25) and issues `Mcp-Session-Id`; sessions accept `notifications/initialized`, `ping`, `tools/list`, `tools/call` and `DELETE`.
  2. Extended [Invoke-ProtocolChecks.ps1](https://github.com/lspiguel/ai-tooling/blob/643fe93/tooling/AgentCompanionMcp/Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1) with fifteen handshake checks and ran all thirty-one against the console harness.
  3. Registered the harness with Claude Code and checked both binaries; ran a `hello` call from 2.1.72.
- **Observed:** All thirty-one checks pass. Claude Code 2.1.72 now connects: `initialize` (2025-11-25 agreed) → `notifications/initialized` → `GET` (405, no stream offered) → `tools/list` → `tools/call hello`, which returned the result. Claude Code 2.1.312 still takes the 2026-07-28 path (`server/discover`).
- **Conclusion:** The fallback serves a real handshake-era client end to end on the same endpoint, without touching tools, approvals or paging. VS Code is the remaining check.
- **Spec change:** None beyond D7.

#### S4 — VS Code against the dual-era spike

- **Date:** 2026-10-30
- **Versions:** VS Code 1.144.0 with GitHub Copilot agent mode · spike plugin v3 (dual-era) in XrmToolBox
- **Steps:** Restarted XrmToolBox with spike v3 (new token); restarted the server in VS Code; read the spike log. Then wrote the token directly into the user `mcp.json`, restarted the server again and asked agent mode to run `hello`.
- **Observed:**
  - With the masked `promptString` input, every VS Code request got **401**: VS Code kept the token cached from before the restart. VS Code answered the 401 by fetching OAuth protected-resource and authorization-server metadata, then offered manual OAuth client registration, and its log looped on "Waiting for server to respond to `initialize`".
  - With the current token in `mcp.json`: `initialize` (2025-11-25 requested and agreed) → `GET` (405) → `notifications/initialized` → `tools/list` → `tools/call hello`, answered by the spike in XrmToolBox (its process ID and host load time).
- **Conclusion:** VS Code 1.144.0 works through the fallback (D7). A token that changes at every host start breaks saved client configurations, and in VS Code the 401 surfaces as a misleading OAuth prompt rather than a token error (D8).
- **Spec change:** Pending D8. Minimum client versions for the Phase 5 README: Claude Code — any release from 2.1.72 on; VS Code — 1.144.0 tested.

### S5 — Overlap with the Dataverse MCP server

- **Date:** 2026-10-29
- **Versions:** Dataverse MCP server as documented on Microsoft Learn, checked 2026-10-29 (tool surface changed 2026-06-08)
- **Question:** Which of the Read tools does the Microsoft Dataverse MCP server already provide, and could it replace any of them?
- **Steps:** Read *Connect to Dataverse with Model Context Protocol* (list of tools), its FAQ, *Use preview tools…*, and the 2026 wave 1 release-plan entry *Improve Dataverse MCP server quality with updated tools*.
- **Observed:** Generally available tools: `search_data`, `search` (metadata keyword search), `describe` (tables, records, schemas, skills, apps), `read_query` (Dataverse SQL `SELECT`), `create_record`, `update_record`, `delete_record`, `create_table`, `update_table`, `delete_table`, `upsert_skill`, `delete_skill`, and file upload and download. `describe_table`, `list_tables` and `fetch` were removed in June 2026. Admins must allow-list each client per environment, and calls from agents other than Copilot Studio are billed.

  | Plugin tool | Dataverse MCP server | Overlap |
  |---|---|---|
  | `list_tables`, `get_table_metadata`, `get_relationships` | `search`, `describe` | Partial — the documented output does not promise metadata IDs, managed and customizable flags, or keys |
  | `get_option_sets` | — | None documented |
  | `get_entity_ribbon`, `get_application_ribbon` | — | None |
  | `query` | `read_query` | Partial — SQL rather than FetchXML; reach over configuration tables unconfirmed |
  | `list_publishers`, `list_solutions`, `list_solution_components`, `get_component_state`, `get_required_components` | — | None |
  | `who_am_i`, `get_connection_info`, `list_connections` | — | None |
  | Write tools | `create_table`, `update_table` and record writes | None — those are the risk surface the plugin excludes |

- **Conclusion:** No Read tool is replaced. The solution-centric tools have no counterpart, the partial overlaps return different shapes, and the server adds per-environment allow-listing, billing, recent breaking renames, and write tools outside this workflow. Agents can still use it for data spot-checks, as the matrix describes. Confidence: high for the decision; the exact `describe` output was not inspected live, which does not change it.
- **Spec change:** [§6.3](SPEC.md#63-tool-summary) — one line recording the check. *Applied.*

### S6 — Several tabs of one plugin

*Desk part, from the XrmToolBox source. The live two-tab test is still to run.*

- **Date:** 2026-10-29
- **Versions:** XrmToolBox source at commit `fa75caec`; installed 1.2026.9.78
- **Question:** Is a static singleton shared across tabs? Which hooks report a connection being set and changed, how does a plugin close its own tab and re-open the connection dialog? Is the connection ID stable, and can a dialog be owned by the main window?
- **Steps:** Read `PluginManagerExtended`, `New/NewForm.cs`, `New/PluginForm.cs` and `XrmToolBox.Extensibility/PluginControlBase.cs`.
- **Observed:**
  - Plugins load once per process through MEF (`DirectoryCatalog` → `CompositionContainer`). Each tab calls `GetControl()` on the same lazily created plugin export, so every tab is a new control in the same assembly and AppDomain — a static holder is shared.
  - `PluginControlBase.UpdateConnection(service, detail, actionName, parameter)` is virtual and runs when a tab opens with a connection, after a connection the tab requested, and when the toolbar's connect is applied to open tabs. The `ConnectionUpdated` event fires from it.
  - Connecting from the toolbar with tabs open shows a picker (`TabConnectionUpdater`) where the person can apply one connection to several tabs at once; tabs are updated one after another on the UI thread.
  - `CloseTool()` raises `OnCloseTool` and the hosting form closes the tab. `ClosingPlugin(PluginCloseInfo)` is virtual; by default it asks "Are you sure you want to close this tab?".
  - Raising `OnRequestConnection` (`RaiseRequestConnectionEvent` with `RequestConnectionEventArgs { ActionName, Control }`) opens XrmToolBox's connection dialog and then calls `UpdateConnection` with that action name. It also makes the chosen connection XrmToolBox's current connection.
  - `ConnectionDetail.ConnectionId` is a nullable GUID; XrmToolBox itself falls back to `Guid.Empty` when it is missing. Saved connections are found by this ID, so it is stable for them.
- **Conclusion:** H1 (static singleton) and C2–C6 map onto existing hooks: override `UpdateConnection` for binding and the C3 check, `ClosingPlugin` for unbinding (C6), `CloseTool` and `OnRequestConnection` for the two dialog buttons (C3). A connection applied to two Agent Companion tabs at once is handled in order: the first binds, the second gets the dialog. The connection ID can be missing (D4). Main-window dialog ownership (C9) is not visible in source. Confidence: high for the hooks; the runtime test confirms them.
- **Spec change:** §5 C3 and §6.4 per D4, and the note in C3 that one connect can reach several tabs — *Applied*.

#### S6 — one tab, live

- **Date:** 2026-10-29
- **Versions:** XrmToolBox 1.2026.9.78 · spike plugin v1
- **Steps:** Opened one spike tab already connected to the sandbox; read its log.
- **Observed:** `UpdateConnection` ran **before** the control's `Load` event; the host started in `Load`, and the binding completed afterwards, once the background `WhoAmI` returned the organization ID. The saved connection carried an XrmToolBox connection ID (no minting needed). The approval dialog for `who_am_i` was owned by `XrmToolBox.New.NewForm` — the main window — with the tab visible, and was answered while the agent waited.
- **Conclusion:** A tab can receive its connection before it attaches to the host, so binding must not assume `Load` has run. The real plugin attaches to the host on first use from either hook. C9's main-window ownership holds; whether the dialog shows with the tab hidden, multi-tab behaviour, duplicates, toolbar apply and closing order still need the live steps.
- **Spec change:** H1 wording, so that the first of `UpdateConnection` or `Load` attaches — *Applied*.

#### S6 — several tabs, live

- **Date:** 2026-10-29
- **Versions:** XrmToolBox 1.2026.9.78 · spike plugin v2 · evidence read with `get_log`
- **Steps:** With tab 1 bound to the sandbox: opened tab 2 on the same connection and chose **Close this tab**; opened tab 3 on the same connection and chose **Choose another connection**, then picked a connection to a different environment; opened tab 4 on the same connection, chose **Choose another connection** and cancelled; then connected from the toolbar and applied the sandbox connection to tabs 1, 3 and 4 together.
- **Observed:**
  - Every tab reported the same process, AppDomain 1 and the same host load time; the host log counted tabs attaching and detaching (H1).
  - Tab 2: conflict on the same connection ID → dialog → **Close this tab** → `ClosingPlugin` ran and the tab detached (C3, C6).
  - Tab 3: **Choose another connection** reopened XrmToolBox's connection dialog; the new connection arrived through `UpdateConnection` and bound (C3).
  - Tab 4: cancelling the connection dialog left the tab unbound (C3, C4).
  - Toolbar apply: `UpdateConnection` reached tabs 1, 3 and 4 in the same second. Tab 3 first lost its binding to the other environment (C5). Tab 1 bound; tabs 3 and 4 got the dialog and were closed (C3).
  - All dialogs were owned by `XrmToolBox.New.NewForm`.
  - Approval timeout: two calls not answered within 120 s returned as denials after 120.6 s; the agent was not left waiting.
- **Conclusion:** C2–C6 behave as specified on existing XrmToolBox hooks. One flaw: the spike binds only after a background `WhoAmI`, so in the toolbar case the winner was whichever call returned first — tab 1 won by timing, not by order. The connection ID must be claimed synchronously in hook order. **Hidden tab (C9) confirmed:** during the `AgentSpikeS2D` approvals XrmToolBox was showing another tab, and the dialogs appeared and were answered; the control's `Visible` still reported `True`, so the plugin cannot use it to tell whether its tab is in front. **Host stop (H3) confirmed:** after the person closed the last spike tab, with XrmToolBox still running, nothing listened on port 47321 and requests were refused.
- **Spec change:** [§5 C3](SPEC.md#host-and-connection-model) — claim the connection ID synchronously, check the organization ID after the identity call. *Applied.*

#### Query paging and `top` (side finding, S2 preparation)

- **Date:** 2026-10-29
- **Observed:** `pac env fetch`, which pages every query, fails with "The top attribute can't be specified with paging attribute page" when the FetchXML has `top`.
- **Conclusion:** Dataverse refuses `top` with paging. The `query` tool always pages, so it must refuse `top`.
- **Spec change:** [§6.5 `query`](SPEC.md#query) — `top` rejected with `invalid_argument`. *Applied.*

A second side finding from the same preparation: FetchXML over the `attribute` table cannot tell which table a column belongs to — it exposes no `entityid` and no join to `entity`. `get_table_metadata` must use the metadata API (`RetrieveEntityRequest`), as the spec already implies; `query` is no substitute.

### S2 — AddSolutionComponentRequest flags and exports

- **Date:** 2026-10-29
- **Versions:** sandbox (schema only) · spike plugin v2 · PAC CLI 2.14.1
- **Question:** What does `AddSolutionComponentRequest` produce for an out-of-box and a custom table with `DoNotIncludeSubcomponents = true`; how is a shell-only table achieved; does the export carry the table's ribbon diff?
- **Steps:**
  1. Created `AgentSpikeS2A`, `AgentSpikeS2B` and `AgentSpikeS2C` with the spike's `create_solution`, under the sandbox's existing default publisher. Each call approved in XrmToolBox.
  2. S2A: added `account` and `[CustomTable]` with `DoNotIncludeSubcomponents = true` and `IncludedComponentSettingsValues` unset; then a column of `[CustomTable]`, an `account` main form, the default `account` view and a web resource. `AddRequiredComponents = false` throughout.
  3. S2B: added `account` with `DoNotIncludeSubcomponents = true` and `IncludedComponentSettingsValues = []`.
  4. S2C: added only the form, the view and the column — no tables.
  5. Read `solutioncomponent` for the spike solutions; exported each unmanaged and compared `customizations.xml`.
  6. Checked `formactivationstate` of the forms used, and repeated the form export with an active form.
- **Observed:**

  | Case | Table rows | Subcomponent rows | Export |
  |---|---|---|---|
  | S2A — flag on, settings unset | Both tables, root behaviour 1 (*Do not include subcomponents*) | Column under `[CustomTable]`; form and view under `account` | Table `EntityInfo` with the table's own properties (localized names, introduced version); only the added column; the view |
  | S2B — flag on, settings `[]` | `account`, root behaviour 2 (*Include As Shell Only*) | — | An empty shell: `EntityInfo` with no attributes and no properties |
  | S2C — no tables added | Platform added both tables, behaviour 2 | Form, view, column as children of the shells | Same shells, plus the column and the view |

  - No required components appeared in any case; nothing beyond what was requested, apart from the parent shells in S2C.
  - Every exported table carried a `RibbonDiffXml` element. In this sandbox it was the empty skeleton (`CustomActions`, `Templates`, `CommandDefinitions`, `RuleDefinitions`, `LocLabels`, all empty), so whether a populated ribbon diff travels with a metadata-only table is **not yet tested**.
  - The two forms first used were managed and **inactive**; neither produced form XML in any export. An active managed form added the same way did.
- **Conclusion:** `tableBehavior` maps cleanly: `metadata` → flag on, settings unset → behaviour 1; `shell` → flag on, settings `[]` → behaviour 2. Adding a subcomponent alone yields a parent shell, which is benign and must not count as unexpected. Inactive forms are a new WIP hazard. Confidence: high for the behaviours (observed for an out-of-box and a custom table); medium for the inactive-form rule (three forms).
- **Spec change:** [§6.6](SPEC.md#add_solution_components) `tableBehavior` mapping and the parent-shell rule in the read-back step; [§9.6](SPEC.md#96-issues-common-to-every-close-out-strategy) X10 for inactive forms; §14 note. *Applied.*

#### S2 — ribbon diff with a table in a WIP

- **Date:** 2026-10-29
- **Steps:**
  1. `opportunity` (chosen by the person) had an unmanaged ribbon customization record but no unmanaged ribbon elements. Created `AgentSpikeS2D` and added `opportunity` metadata-only (behaviour 1); its export held the empty ribbon skeleton.
  2. Inserted a test button with an empty command into that export's ribbon diff and imported it unmanaged with `--publish-changes`. One unmanaged ribbon element now exists on `opportunity`.
  3. Exported `AgentSpikeS2D` again.
  4. Created `AgentSpikeS2E` and added `opportunity` as a shell (`IncludedComponentSettingsValues = []`, behaviour 2); exported it. (A first attempt timed out unanswered; the second was approved.)
- **Observed:** Both the metadata-only export (`AgentSpikeS2D`) and the shell export (`AgentSpikeS2E`, `behavior="2"` in `solution.xml`) carried the test button in the table's `RibbonDiffXml`.
- **Conclusion:** **Any table in a WIP carries the table's whole unmanaged ribbon diff** — metadata-only or shell. Because the platform adds a parent shell whenever a column, form or view is added alone, a story that only adds a column still exports, and on import overwrites, the table's ribbon. The ribbon is a whole document for W7 wherever its table appears.
- **Spec change:** [§6.6](SPEC.md#add_solution_components) `tableBehavior` note; [§9.6](SPEC.md#96-issues-common-to-every-close-out-strategy) X6 rewritten. *Applied.*

### S3 — PAC add-solution-component

- **Date:** 2026-10-29
- **Versions:** PAC CLI 2.14.1 · sandbox
- **Question:** What does `pac solution add-solution-component` do for each v1 component type without `--AddRequiredComponents` — in particular for tables?
- **Steps:**
  1. `AgentSpikeS3`: added `account` (by name), `[CustomTable]`, the column, the form, the view and the web resource, in that order, without `--AddRequiredComponents`.
  2. `AgentSpikeS3B`: added only the column, the forms and the view — no tables.
  3. Read `solutioncomponent` and exported S3B.
- **Observed:**
  - **Tables added through PAC get root behaviour 0, *Include Subcomponents*** — every column, form, view and relationship of the table comes into the solution. For a managed out-of-box table this is the layering problem R1 exists to prevent.
  - The column, form and view added after the tables reported "has been added" but got no rows of their own: the include-all root already covered them.
  - Added without their tables (S3B), the column, form and view behave exactly as through the SDK: the platform adds the parent table as a shell (behaviour 2) with the component as its child.
  - The web resource is a root with behaviour 0, which has no subcomponents to include.
- **Conclusion:** Path B (PAC) is **unsafe for tables** and safe for columns, forms, views and web resources added on their own. PAC's success message does not reveal the include-all behaviour, so the R1 check after every add stays mandatory. Confidence: high.
- **Spec change:** [§4](SPEC.md#two-paths-for-adding-components) Path B row. *Applied.*

Spike solutions left in the sandbox: `AgentSpikeS2A`, `AgentSpikeS2B`, `AgentSpikeS2C`, `AgentSpikeS3`, `AgentSpikeS3B`. Deleting them with `pac solution delete` keeps their components.

---

## Log

| Date | Entry |
|---|---|
| 2026-10-29 | Spec drafted through the resolved open questions; notes created. Phase 0 not started |
| 2026-10-29 | Phase 0 started. Desk research for S1, S4, S5 and S6; S5 done and applied. Decisions D1–D5 raised; live work waits on D5 |
| 2026-10-29 | D1–D4 decided and applied to the spec. Spike code added under `Spikes/`: protocol layer passes all sixteen checks in the console harness; XrmToolBox spike plugin built. Waiting on the person for the XrmToolBox run, client registration and sandbox access |
| 2026-10-29 | Live: S1 gate passed inside XrmToolBox; Claude Code 2.1.312 connects end to end, 2.1.72 cannot (D1); persist-to-disk threshold measured (D6 raised); single-tab S6 observed; `top` finding applied. Spike v2 built for S2, S3 and the rest of S6 |
| 2026-10-29 | Spike v2 deployed. S2 done except ribbon-diff content; S3 done — PAC adds tables with all subcomponents. Spec updated: `tableBehavior` mapping, parent shells, Path B for tables, X10 inactive forms. C9 dialog owner confirmed |
| 2026-10-29 | S6 multi-tab observed; C3 binding race found and fixed in the spec; H1 attach rule applied. Ribbon diff travels with a metadata-only table (test button on `opportunity`). D6 decided and applied. Approval timeout path observed. Shell ribbon case, hidden-tab approval, host stop and VS Code still open |
| 2026-10-29 | S2 done: a shell table also carries the whole unmanaged ribbon diff — X6 and the `tableBehavior` note corrected in the spec. C9 hidden-tab approval confirmed. Remaining Phase 0: host stop on last tab close (S6), VS Code (S4), S7 (needs a build environment and promotion pipeline) |
| 2026-10-29 | S6 done: host stops when the last tab closes (H3). S7 deferred — no build environment. Claude Code registration removed (token expired with the host); re-add when the spike runs again |
| 2026-10-29 | S4 VS Code: 1.144.0 sends `initialize` then legacy `GET`; cannot connect under D1. Its agent reported a false success via the console harness, caught in the spike log. D7 (dual-era fallback) raised |
| 2026-10-29 | D7 decided: dual-era host. Fallback built in the spike layer; 31 protocol checks pass; Claude Code 2.1.72 (handshake) and 2.1.312 (2026-07-28) both connect. Spec §6.1 rewritten. VS Code re-test waits on the v3 DLL |
| 2026-10-30 | S4 done: VS Code 1.144.0 connects through the handshake fallback and calls a spike tool in XrmToolBox. Token caching across host restarts caused 401s and an OAuth prompt in VS Code; D8 raised. Phase 0 done for v1 apart from S7 and S8 (deferred) |
| 2026-10-30 | D8 applied to the spec. S7 moved to cold storage. Sandbox cleaned (test button and seven spike solutions removed) and workstation cleaned (plugin DLL, client registrations, scratch files). Phase 0 handover written; Phase 1 ready to start |
| 2026-10-30 | Phase 1 started. D9 decided (whitespace keep-alive for cancellation) and applied to the spec first. Plugin, tests and console harness built: no build warnings; 137 unit tests pass; the thirty-one protocol checks pass over HTTP against the harness; an abandoned call was cancelled at the next keep-alive write. `Spikes/` deleted and its links repointed to `643fe93`. Live exit checks in XrmToolBox with the clients pending |
| 2026-10-30 | Phase 1 live exit checks: all twelve steps passed (two tabs, duplicate dialog, toolbar connect, denial/timeout/unknown connection, scopes, cancellation, restart without reconfiguration). Phase 1 done |
| 2026-10-31 | Phase 2 started. Paging extended (fields beside items, metadata snapshots, FetchXML with cookies, XML chunked by escaped length) and the eleven discovery tools built; a pre-dialog inspection hook added for `query` and `get_component_state`. Fixed: a continuation call failed when a tool had other required arguments. No build warnings; 175 unit tests pass. Implementation choices I11–I20 recorded |
| 2026-10-31 | Phase 2 live run against the sandbox: solutions, tables, `account` metadata, relationships, choices, forms and views, records, cursors and not-found all pass. Found and fixed L1 (column names: `RetrieveMetadataChanges` needs `Attributes`), L2 (`systemform` has no `modifiedon`/`modifiedby` — SPEC §6.5 updated) and L4 (top panel groups twice their content height; call log now takes the spare height). L3: `account`'s whole ribbon timed out at the SDK's two minutes — the person chose a 5-minute timeout for ribbon requests, set on the call's clone only (SPEC §6.5 updated) |
| 2026-10-31 | Phase 2 re-run on the fixed build: step 10 (solution components, component state, required components) passes; `account`'s whole ribbon returns (17 chunks, 610,533 bytes, in 2.4 s — the longer timeout not exercised). Record-query dialog warning, `tool_disabled` with record queries off, and the log confirmed by the person. Phase 2 done; 176 unit tests |
| 2026-10-31 | Phase 3 started. Preparation step added: Write and Publisher tools resolve names and refuse (`not_found`, `managed_solution`, `already_exists`) in the connection's queue before the dialog; the dialog shows the resolved summary, and the §8.3 publisher dialog asks for the typed prefix. `create_solution`, `add_solution_components` (fixed flags, snapshot, read-back, parent shells, `⚠ unexpected` log lines), `remove_solution_component` and `create_publisher` built; I8 retired. SPEC §6.6 updated for `alreadyPresent`, parent-shell index and the unconfirmed application ribbon type. No build warnings; 207 unit tests pass. Choices I21–I26 recorded |
| 2026-10-31 | Phase 3 live run: the cold-start workflow (§9.1) ran end to end on the sandbox under its default publisher — refusals without reason and with publisher creation off, create, add with a parent shell and nothing unexpected, read-back, remove, PAC export/unpack/pack/import with contents unchanged, delete. Publisher dialog with a wrong typed prefix denied, nothing created; dialog content and log confirmed by the person. Findings P1 (a remove leaves the parent shell) and P2 (`--publish-changes` publishes everything) passed to Phase 4. Phase 3 done; scratchpad emptied |
| 2026-10-31 | Phase 4 prepared in the spec: two routes per component, placement and in place (§9.9, W9–W13); delivery context derived or asked, with steps confirmed before acting (§9.8, R15, T1–T6); close-out strategy chosen per WIP, A and B mixable (§9.7); transport solution never the edit vehicle, with an unmanaged alignment import from build allowed under A (R14, A8); no managed project solution imported into development (R16); pipeline runs, commits and pushes are a person's (T6); delivery facts recorded in `<client>-Context/` where it exists (T5); P1 and P2 carried as W13 and X11. Phase 4 skills are new `agent-companion-*` skills in `skills/`, frontmatter stating they need the plugin, split by concern, with coexistence rules K1–K8; repository skills keep their procedures |
| 2026-10-31 | Phase 4 started. Five `agent-companion-*` skills written in `skills/` (context, transport bootstrap, WIP shaping, edit loop, close-out) with references; scripts `Get-WipName.ps1`, `Test-UnpackedSolution.ps1`, `Test-CustomizationsSchema.ps1`, `Compare-ComponentState.ps1`, `Compare-SolutionExport.ps1`; self-tests pass (11 and 32, the schema tests against Microsoft's XSD bundle). Optional MCP notes added to `power-automate-flow-editing` and `d365ce-planning`. Choices I27–I34 recorded. Live exit checks next |
