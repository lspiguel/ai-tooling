# Agent Companion MCP — progress notes

> Working notes for building the plugin specified in [SPEC.md](SPEC.md). The spec is the source of truth: these notes track progress and hold findings until they are written back into it. **No client data** (R7) — no organization URLs, tenant IDs, environment GUIDs, user names or records from a client environment. Use the spec's placeholders.

---

## Status

| Phase | Status | Exit criterion |
|---|---|---|
| [0 — Spikes](SPEC.md#phase-0--spikes) | **Done for v1** (2026-10-30). S7 in cold storage (no build environment); S8 deferred with strategy C — both now tracked in [Phase 6a](#phase-6a--refinements). Sandbox and workstation cleaned up | Every v1 spike has a written finding, and the spec is updated |
| [1 — Host, connections, approvals and log](SPEC.md#phase-1--host-connections-approvals-and-log) | **Done** (2026-10-30) — built, unit-tested, and all twelve live exit checks passed in XrmToolBox. See [Phase 1 — execution](#phase-1--execution) | See spec |
| [2 — Discovery](SPEC.md#phase-2--discovery) | **Done** (2026-10-31) — built, unit-tested (176 tests), and the live exit checks passed against the sandbox; four findings (L1–L4) fixed on the way. See [Phase 2 — execution](#phase-2--execution) | See spec |
| [3 — Solution shaping](SPEC.md#phase-3--solution-shaping) | **Done** (2026-10-31) — built, unit-tested (207 tests), and the cold-start workflow (§9.1) ran end to end on the sandbox; two findings (P1, P2) for the Phase 4 skills. See [Phase 3 — execution](#phase-3--execution) | See spec |
| [4 — Agent skills and scripts](SPEC.md#phase-4--agent-skills-and-scripts) | **In progress** (2026-10-31) — five companion skills and their scripts built; script self-tests pass (35 + 11). Live exit checks: I31 verified; scenarios 1–4 pass; scenario 5 desk-checked; scenario 6 partial — the imported flow did not fire (E13). Findings E1–E13. See [Phase 4 — execution](#phase-4--execution) | See spec |
| [5 — Release](SPEC.md#phase-5--release) | **Done** (2026-11-03) — published in the XrmToolBox Tool Library. Still to follow the release: the column-3 guide changes. See [Phase 5 — execution](#phase-5--execution) | See spec |
| [6 — Planned enhancements](SPEC.md#phase-6--planned-enhancements) | **Built; live exit checks pending** (2026-11-05) — scoped approvals and the Active Approvals window, `update_web_resources`, plug-in components in the selectors, the skill changes and the docs, as version 1.2026.11.2. Built and unit-tested (383 tests); nothing of it has run in XrmToolBox or against a sandbox yet. See [Phase 6 — execution](#phase-6--execution) | See spec |
| [6a — Refinements to 1.2026.11.2](#phase-6a--refinements) | **In progress** (2026-11-08) — follow-up tasks on the unreleased 1.2026.11.2 build, ahead of its release. 2 of 3 tasks built: the Tool Library icons, and the disk call log (2026-11-09; unit-tested, 416 tests, live check pending); spikes S7, S8 and S12 moved here. Same version; no bump. See [Phase 6a — refinements](#phase-6a--refinements) | See [spec](SPEC.md#phase-6a--refinements) |
| [7 — Plug-in authoring](SPEC.md#phase-7--plug-in-authoring) | **Not started** (2026-11-03) — split from Phase 6 after field use (U10): registration through a WIP, `pac plugin push`, step mirroring and switching, custom API rebinding. Spike S9 done; S12 moved to [Phase 6a](#phase-6a--refinements) (2026-11-08) and gates this phase. Deferred at the person's choice | See spec |

Status values: *Not started* · *In progress* · *Blocked* (say on what) · *Done*.

---

## Phase 6 — preparation

### Direct updates in development — analysis, 2026-11-03

Raised in field use: solution export and import are the slowest part of the edit loop. The person asked whether four kinds of component could be updated in development directly, still held by WIPs but without the import round trip. Each was weighed on its own. Decisions D10–D14; spec [§6.8](SPEC.md#68-direct-update-tools-phase-6) and [§9.11](SPEC.md#911-direct-updates-in-development).

| Component | Platform facts | Complexity | Decision |
|---|---|---|---|
| Web resources | One `webresource` row each: content, then `PublishXmlRequest` for just those IDs. `get_component_state` already gives the version for the overwrite check. No PAC command updates a web resource | Low | **Build** `update_web_resources` (D10) |
| Plug-in assemblies | `pac plugin push` already updates a registered assembly in seconds. A per-developer side-loaded build — the same source compiled under `<Assembly>.<initials>` in Debug, the shared name in Release — cannot be pushed over the shared assembly, because Dataverse matches assemblies by name. The slow, error-prone part is the steps: disabling the shared steps, re-creating them on the side-loaded assembly with their images, and undoing it | Push: low. Step work: medium–high | **Push with PAC after G5; build tools for the steps** (D11) |
| Custom APIs | The main operation is `customapi.plugintypeid`, not a step, and it can be changed after creation. Steps on a custom API's message are ordinary steps | Low | **Build** `rebind_custom_apis` (D12) |
| Power Pages | Standard data model: site configuration is `adx_*` records, not solution components. `pac pages upload` already uploads only what changed, from the manifest of the last download. The gap is concurrency with design-studio edits | Low as a skill; high as a write tool | **Not pursued** (D13) |
| PCF | `pac pcf push --incremental` sends changed files as entity updates; `--solution-unique-name` puts the control in the WIP instead of `PowerAppsTools_<prefix>` | None for the plugin | **Guidance only** (D14) |

Cross-cutting points that shaped the tools:

- **The overwrite check moves into the tool.** Each item carries the baselined version; the tool refuses a mismatch before the dialog and again just before writing.
- **Files by path, not content.** The host is on the person's machine. A DLL or bundle as base64 through MCP would cost tokens for nothing. Paths are resolved against the organization's **File root** (D15), and the dialog shows each file's size and SHA-256.
- **A targeted publish avoids X11.** `update_web_resources` publishes only what it wrote, unlike `--publish-changes` on import (P2).
- **Approval volume.** An edit-and-push loop raises many Write dialogs; Phase 6's scoped Write approvals make these tools practical.

### Findings — 2026-11-03

| # | Finding | Change |
|---|---|---|
| U7 | **Imports re-enable disabled steps.** Solution import with plug-in activation enables every step in the imported solution, so a disabled step does not stay disabled — reported by the person from field use, where it has already caused problems. The edit-loop helper passes `--activate-plugins` (`Invoke-EditLoopStep.ps1`), as pipelines commonly do | Rules M1–M7 in SPEC §9.11: mirrors created disabled, switching only by explicit calls, a side-load WIP never imported, open switches re-checked after every import and closed before extract or close-out, retire by deleting |
| U8 | **Environment manifest tracked in git.** On a client project, the Power Pages environment manifest (`<org-url>-manifest.yml`) is committed. Microsoft says to git-ignore it; tracked, each developer's delta upload is computed against another's download | Recorded in SPEC §9.11 (Power Pages, not pursued); raise with the project, not a plugin change |
| U9 | **Step mirroring also serves version changes.** A major or minor version change registers a new assembly beside the old one; mirroring the steps to it is the same operation as for a side-load | `mirror_steps` takes any target assembly; version-change flow in SPEC §9.11 (§9.10 since U10) |
| U10 | **Plug-in authoring does not work as specified.** The person tested another agent (not Claude) adding plug-ins to an existing assembly with `pac plugin push`. The push did not register the new plug-in types; steps were missing from the solutions exported; after import, the steps were not created. The quirks need a spike before anything is built or put in a skill | Plug-in authoring moved to SPEC Phase 7, starting with spike S12: §9.10 (now with side-loading, version change and M1–M7), the step tools in §6.9, G1–G5. The plug-in edits on this branch's skills removed — `plugin-registration.md`, the G rules, the plug-in rows in edit loop and shaping; `d365ce-planning` plans registration as manual again. The research queries and the allow-list (U6) stay |

### Spikes S9–S11 — 2026-11-03

On a data-free trial environment the person provided for spikes. Its URL stays out of this file (R7).

**Method.** S9 and S10 ran through a throwaway SDK console in the session scratchpad, with its own interactive sign-in. A minimal plug-in assembly was registered twice: as `Spike.S9.Plugins` and, compiled under a suffixed name, as `Spike.S9.Plugins.XY` — the side-load pattern. Its step plug-in throws a message naming the assembly that ran and the configuration and image it received; its custom API plug-in returns the assembly name. The person stopped the console's sign-in prompts as outside the design: environment access is through the plugin and PAC profiles only. S11 ran with PAC on the person's profile. The results below are platform behaviour; the tools are still to be verified through the plugin.

**S9 — steps and custom APIs**

| Check | Result |
|---|---|
| Same `typename` in two assemblies | Allowed |
| Create a step with `statecode` 1 and `statuscode` 2 | Refused: *2 is not a valid status code for state code Enabled* — the create treats the step as enabled |
| Create with `statecode` 1 only | Accepted, but the step is **enabled** — `statecode` is ignored on create |
| Create then disable | Works; both copies can run for the moment in between |
| Secure configuration | Readable by a System Administrator; copied to a new `sdkmessageprocessingstepsecureconfig` row |
| Mirror inputs | After the switch the mirror ran with the same unsecure configuration, secure configuration and pre-image as the original |
| Step created with `SolutionUniqueName` | Only the step (92, behaviour 0) is added; images and secure configuration are not separate components. Assemblies created the same way are added (91); the custom API was added with an environment-specific type code (10038 there) |
| Export | The step XML has no state element. It carries configuration, images and the plug-in type; the secure configuration is never exported |
| Import, step disabled, no activation (T1) | Disabled |
| Import, activation (T2) | Original enabled; the mirror, not in the solution, untouched |
| Import, step enabled in the environment, no activation (T3, T5, T6) | **Disabled** — also with a zip exported while the step was enabled |
| Import with activation while a switch is open (T4) | Original enabled, mirror still enabled: **both run** |
| Custom API rebinding | `plugintypeid` changed by update; the next call ran the other assembly within 0.3–0.7 s, and back the same. `description` is required to create a custom API and its response properties |
| Import times (one-step solution) | 6.5–15.6 s |

**S10 — web resources**

| Check | Result |
|---|---|
| `versionNumber` | Rises on every update — identical content included — and again on publish |
| Published vs unpublished content | `Retrieve` returns the published content until the publish; `RetrieveUnpublishedRequest` returns the new content at once |
| Direct update | Update 0.3–0.5 s; targeted `PublishXmlRequest` 2.9–3.8 s |
| Round trip, one-resource WIP | Export 6.6–8.2 s; import 7.2–12.8 s; publish-all 11.5–31.2 s. Unpack and pack not included |

**S11 — PCF with PAC 2.14.1**

| Check | Result |
|---|---|
| `--solution-unique-name` with `--publisher-prefix` | Refused: only one of the two |
| `--solution-unique-name` for a solution that does not exist | Refused; push does not create it |
| First push into the named solution | Wrapper solution built and imported, control added to the solution, publish-all; no `PowerAppsTools_new` left. 48 s |
| `--incremental` after a code change | Only `cc_S11Spike.Probe/bundle.js` updated, then publish-all. 24 s |

**Changes:** SPEC §6.8 (version handling, unpublished comparison, create-then-disable, what the platform adds), §9.11 (import sets every carried step's state; M1–M5 reworded; PCF push guidance), Phase 6 spike table.

**Left in the trial environment.** Solutions `S9Spike`, `S9Import`, `S10Spike` and `S11Spike`. Under them: the two spike assemblies with their types, the original and mirror steps on *Update of account* with images and secure configurations, the custom API `new_S9Api`, the web resource `new_/s10spike/probe.js` and the control `S11Spike.Probe`. Outside any solution: an account named *S9 spike account*. **The original step is enabled and throws on every account name change.** Removing these needs the Plugin Registration Tool or XrmToolBox, not PAC — the person's to do.

### Open for Phase 6

- **Shaping** closed with the build (2026-11-05); see [Phase 6 — execution](#phase-6--execution).
- **Plug-in components in `add_solution_components`** (person's request, 2026-11-03): assemblies, steps, custom APIs, request parameters and response properties, already registered — not authoring, which stays in Phase 7. Custom API codes are per environment, resolved through `solutioncomponentdefinition`; built, and still to confirm live, with what the platform adds with a custom API. SPEC §6.6.
- **Q14** — decided: no delete tool; removal stays with the person.
- **Skill changes** listed in the Phase 6 table. Done with the build (2026-11-05): `agent-companion-wip-edit-loop` (direct web resource updates, PCF push) and `agent-companion-wip-shaping` (plug-in components, the record-read note in `queries.md`).

### Open for Phase 7

- **Spike S12** first ([SPEC Phase 7](SPEC.md#phase-7--plug-in-authoring)), starting from U10: what registers new plug-in types, what a WIP must hold for its export to carry steps, and what makes an import create them. Through the plugin and the person's PAC profile only.
- **Skill changes** listed in the Phase 7 table, including `Get-WipName.ps1`'s `_Sideload` suffix.

---

## Phase 6a — refinements

Started 2026-11-08. Follow-up work on the Phase 6 build before 1.2026.11.2 is released. The version stays **1.2026.11.2** in the `.csproj` and the `.nuspec` (the two-file rule is not triggered, because nothing is released yet). Tasks are added here as they come up.

| # | Task | Status |
|---|---|---|
| 6a.1 | [Tool Library icon resolution](#6a1--tool-library-icon-resolution) | **Done** (2026-11-08); needs a build, and the 150px file pushed to `main`, to be seen in the Tool Library |
| 6a.2 | [Automatic call log on disk](#6a2--automatic-call-log-on-disk) — one JSON file per connection and day, full arguments and results ([SPEC](SPEC.md#6a2--automatic-call-log-on-disk)) | **Built** (2026-11-09); unit-tested (416 tests, 0 warnings), not yet run in XrmToolBox. Specified 2026-11-08. Never reported to the agent in any tool response. One global checkbox in Active Approvals asks for the folder when ticked and is saved with the plugin settings. Decided: host calls go to `AgentCompanion-host-yyyyMMdd.json`; two connections with one name get an 8-character connection ID in the file name |
| 6a.3 | Pending spikes S7, S8 and S12, moved from Phase 0 and Phase 7 ([SPEC](SPEC.md#6a3--pending-spikes)) | Moved (2026-11-08); none run. S12 gates Phase 7 |

### 6a.1 — Tool Library icon resolution

**Problem.** The plugin's icons looked much lower resolution in XrmToolBox than FetchXML Builder's.

**Finding.** XrmToolBox reads tool icons from the `BigImageBase64` and `SmallImageBase64` metadata on the plugin class, and the Tool Library list also uses the package's `iconUrl`. Decoded from the code of both plugins:

| Source | Agent Companion (before) | FetchXML Builder | Agent Companion (now) |
|---|---|---|---|
| `BigImageBase64` | 32×32 | 80×80 | 80×80 |
| `SmallImageBase64` | 16×16 | 32×32 | 32×32 |
| nuspec `iconUrl` | 128px | 150px | 150px |

The XrmToolBox convention is 80×80 for Big and 32×32 for Small. The plugin embedded half of that, so XrmToolBox stretched the images. The `iconUrl` difference (128px against 150px) was minor.

**Change.**

- `AgentCompanionPlugin.cs`: `BigImageBase64` now holds `d365-agent-companion-80px.png`; `SmallImageBase64` now holds `d365-agent-companion-32px.png`. Both values were produced by Base64-encoding the PNG files, and their decoded dimensions were checked.
- `D365AgentCompanionMcp.nuspec`: `iconUrl` now points to `d365-agent-companion-150px.png`.
- The source PNGs (16, 32, 64, 80, 128, 150 and 256px) stay in the project folder. Only the 32px and 80px files are embedded, so the other files are for packaging and reference.

**Still to check.** Not built or run in this session. After a build, the tool tile and the tool list in XrmToolBox should show the sharper icons. The Tool Library shows the 150px icon only once `d365-agent-companion-150px.png` is on `main`, because the `iconUrl` is a raw.githubusercontent link.

### 6a.2 — Automatic call log on disk

Built on 2026-11-09 from [SPEC 6a.2](SPEC.md#6a2--automatic-call-log-on-disk). Same version, 1.2026.11.2. Unit tests went from 383 to 416, with 0 build warnings in Release.

**What is built.**

| Area | Where |
|---|---|
| Settings | `Hosting/HostSettings`: `CallLogToDisk` (default off) and `CallLogFolder` (default not set). Both have defaults, so `SchemaVersion` stays 1 and there is no upgrade mapping |
| The setting and the writer | `Logging/DiskCallLog`, owned by `CompanionHost` as `DiskLog`: the startup check, `TurnOn` (refuses a missing folder or one inside a Git working tree), `TurnOff` (keeps the folder for the next prompt), the background write queue, file-name resolution, warn-once |
| The file | `Logging/CallLogFile`: file name, header, entry, the `\n]}\n` trailer, bidi and separator escaping, `Check` (trailer, parse, schema, connection) |
| The entry | `Logging/DiskLogRecord`: built from the panel's `CallLogEntry`, the call and the `ToolResult` |
| Hook | `Tools/ToolDispatcher.Log` → `WriteToDisk`, after the panel entry. The active folder is captured when the call is received, so a call running when the log is turned off is still written |
| Active Approvals | `UI/ActiveApprovalsForm`: the *Call log* row under the token — checkbox, folder or *Off — last folder*, and warnings. Not touched by any Reset |
| Folder prompt | `UI/CallLogFolderForm`: the rules, the client-data warning, and an inline refusal |
| Log panel | `UI/LogView.AppendNotice`; each tab's host log shows the startup warning on attach and every warning the writer raises |
| Tests | `LoggingTests/CallLogFileTests` (6), `LoggingTests/DiskCallLogTests` (22), five call-log tests in `UITests/ActiveApprovalsFormTests`, and a `TempFolder` fake |

**Implementation choices.**

| # | Choice | Why |
|---|---|---|
| I60 | Writes are queued on one background task chain and run one at a time | A write must never delay the reply to the agent, and entries must stay in the order calls ended |
| I61 | A file is fully parsed only the first time this process uses it; later writes check only the trailer | Parsing on every write grows with the file. The trailer check catches truncation and most hand edits; a file changed in the middle with its trailer intact is not caught until XrmToolBox restarts |
| I62 | A plain-named file that belongs to another connection ID is never appended to; this connection takes its `[id8]` name | The spec's same-name rule looks only at connections bound now. A connection with the same name bound later the same day, after the first tab closed, would otherwise append to the other connection's file |
| I63 | `result` is `null` on every error, and the error's details are in `outcome.errorDetails` | A failed call has no `structuredContent`. The details (for example from `version_conflict`) are in the call detail, so the file keeps them to stay a superset |
| I64 | `approval` is `null` where the panel shows `—` | JSON readers should not have to know the dash. `outcomeLine` keeps it |
| I65 | Warnings go to the host log of every tab, as notice lines that open no detail | The setting is host-wide, so its warnings are too. The host log is the one panel every tab shows |
| I66 | The checkbox is ticked only after `TurnOn` accepts the folder; the prompt also checks inline and stays open on a refusal | The box never shows ticked for a folder that was refused |

**Observation, not changed.** Arguments are parsed with Newtonsoft's defaults, which turn an ISO-date-shaped string into a date. Such a value is written back in Newtonsoft's ISO form, which can differ in offset or precision from what the agent sent. This already applies to the panel and the call detail, and to what tools receive. No current tool takes a bare date string.

**Still to check — the live check in the spec.** Not run in XrmToolBox. In particular: the folder prompt's layout, the warning line under the checkbox, a startup warning in the host log after the saved folder is renamed, and the files written during an ordinary session in two tabs.

---

## Phase 6 — execution

Built on 2026-11-05 from [SPEC Phase 6](SPEC.md#phase-6--planned-enhancements), in this order, with the build and the unit tests run after each step: grants and settings, dispatcher authorization, File root and `update_web_resources`, plug-in components, the windows, then skills and documents. Version **1.2026.11.2** in the `.csproj` and the `.nuspec`. Unit tests went from 251 to 383, with 0 build warnings.

### What is built

| Area | Where | Spec |
|---|---|---|
| Scopes, kinds, identity | `Approvals/ApprovalScope` (adds `Tenant`; `Always` stays the name and is labelled *global*), `ApprovalKind` (adds `RecordRead`, `Write`), `ConnectionIdentity` (organization, environment and tenant IDs), `Grant` (immutable, with `Matches`, `IsStale`, `Covers`, `WithIdentity`) | §7 Phase 6 |
| The grant store | `Approvals/ApprovalStore` — `Allow`, `Find` (broadest wins), `FillIdentity`, `Stale`, `RevokeEnvironment`, `Remove`, `ClearGlobal`, `ClearTenant`, `ClearEnvironment`, `ClearAll`, `EndSession` | §7, §8.8 |
| Settings | `Hosting/HostSettings` (`SchemaVersion`, `FileRoots`), `PersistedGrant` (+ `EnvironmentId`, `TenantId`), `PersistedFileRoot`, `HostSettingsStore.Upgrade` (version 0 → 1: drops what the new rules do not allow, never the token, port, limits or setup version) | §7 Saved settings |
| Identity at bind | `BoundConnection.EnvironmentId`/`TenantId`/`Identity`, `ConnectionRegistry.Complete` (new overload), `CompanionHost.CompleteBinding` (fills the IDs into matching grants); the control reads `CrmServiceClient.EnvironmentId` and `TenantId` | §7 Identity |
| Authorization | `Tools/ToolDispatcher` — the grant kind follows the tool and the inspection (record query → `RecordRead`); the offered decisions follow the tier; a decision the dialog did not offer is a denial; a publisher is never a grant | §7, R2 |
| Active Approvals | `Approvals/ApprovalTree` (the tree in one pure function), `UI/ActiveApprovalsForm` (one window, in-window confirmations, live), `UI/TokenControl` moved into it | §8.8 |
| File root | `Files/FileRoot`, `FileRootStore`, `FilePathResolver`, `IFileRootPrompt` with `FileRootRequest`/`FileRootChoice`, `UI/FileRootForm`; `ToolExecution.RequireFileRoot` | §6.8 |
| `update_web_resources` | `Tools/WebResourceTools`; new error codes `version_conflict`, `managed_component`, `not_in_solution` | §6.8 |
| Plug-in components | `Tools/ComponentSelector` (selectors by name and by `componentTable`; codes from `solutioncomponentdefinition`), `ComponentNames.PluginRows`, `SolutionShapingTools` (accepted types, labels, per-environment type names in the read-back) | §6.6 |
| Panel and dialogs | `AgentCompanionControl` (File root, Reads, Records and Writes rows; **Active Approvals…**; no Record queries box, no Always settings), `UI/ApprovalForm` (every scope, disabled Per tenant with its reason, warnings) | §8.1, §8.2 |
| Skills and documents | `agent-companion-wip-edit-loop`, `agent-companion-wip-shaping`, the tooling README, `.nuspec` release notes, INSTRUCTIONS.md (R2 and the fixed safety behaviour reworded), CLAUDE.md, the root README | Phase 6 table |

### Implementation choices

| # | Choice | Why |
|---|---|---|
| I46 | `Grant` is immutable and compares by value; learning an ID replaces the grant in the store | The store's change detection stays a sequence comparison, and a filled-in grant is saved once |
| I47 | The labels in the log and in `get_connection_info` say `global` for the enum's `Always`; `devEnvironmentScope` no longer has `always` | The enum name stays so saved grants load; what people read says Global |
| I48 | Two grants of one kind and reach replace each other; a narrower grant is kept beside a broader one | A tab's **Revoke** is then exactly "this tab's grants for its environment", and the window shows what exists |
| I49 | `ApprovalKind.Allows(scope)` is the one rule for what a kind may reach; `Allow` throws on a violation and a saved grant that breaks it is dropped on load | A hand-edited or downgraded file cannot widen a Write grant or the dev-environment confirmation |
| I50 | An environment's key in the tree is its environment ID, else `org:<organization ID>`; a grant from a v1 file lists under **No tenant** until a connection fills its IDs in, then moves under its tenant | §8.8 asks for exactly this, and it needs no migration step |
| I51 | The tree puts a tenant's environments in alphabetical order and orders tenants by their first environment's name; a bound environment is listed when no File root covers it | §8.8 |
| I52 | The File root is resolved in preparation through a delegate the dispatcher gives `ToolExecution`; the person is asked on the background thread that runs preparation, with the approval timeout and the call's cancellation | The prompt precedes the approval dialog and still appears when a Write grant would skip that dialog |
| I53 | `FilePathResolver` checks, in order: valid path, inside the root (a separator-terminated prefix, so a sibling folder `repo2` is outside `repo`), no reparse point on any segment, the file exists | A link can lead outside while the path text stays inside |
| I54 | `update_web_resources` also returns `publishError` (null when none) and the read-back's `unexpected`; a failed publish does not fail the call, because the writes already happened | Throwing would hide that the content was written |
| I55 | The web resource type comes from the file extension; an existing resource of another type, an unknown extension, a file over `maxuploadfilesize` and a duplicate name in one call are `invalid_argument` before the dialog | §6.8 step 1 |
| I56 | `ComponentNames.PluginRows` names assemblies and steps beside `RowBacked`, which stays the list `get_component_state` accepts | Adding 91 and 92 to `RowBacked` would have made `get_component_state` accept types it has no version for |
| I57 | The Active Approvals window re-renders on any store change and rebuilds its rows; a confirmation is one string in the form (`global`, `tenant:<id>`, `env:<key>`, `all`, `clear:…`) | Nothing else in the window holds state, so a change from another tab cannot leave it half-updated |
| I58 | `AlwaysSettingsForm` and the panel's **Always settings…** are deleted; a Global read or record-read grant shows a warning on every tab | Global is chosen in the dialog and managed in Active Approvals |
| I59 | Test seams: `ActiveApprovalsForm`'s constructor is internal; `FakeInstance` answers File root prompts; `WebResourceData` is a synthetic environment where `versionnumber` rises on every update and publish (S10); `ShapingData` gained plug-in rows | UI tests build forms without showing them; `Button.PerformClick` does nothing on a hidden control, so the tests call `OnClick` by reflection |

### What the build found

| # | Finding | Change |
|---|---|---|
| B1 | **The publish request was malformed.** The first version of `PublishXmlRequest`'s XML used `$"{{{id:D}}}"`, which .NET reads as a format string `D}`: the unit test of a failed publish showed `Format String can be only "D"…` where the publish should have been. Fixed by building the braces by concatenation | `WebResourceTools.PublishXml`; covered by `CreatesANewResourceInTheWip_AndPublishesJustIt`, which reads the XML back |
| B2 | **A test fixture hid a stowaway.** The first read-back test put the extra component in the WIP from the start, so it was in the snapshot and never reported. The fixture now adds it only after something is written | `WebResourceData`; the read-back test passes for the right reason |

### Exit checks

**Done as unit tests** (all synthetic, R7): every scope and kind and who each reaches, tenant grants across two environments of one tenant and not another, the Per tenant option unavailable without a tenant ID, Write grants that skip the dialog but not the reason, the dev-environment confirmation or the log pair, a decision the dialog did not offer denied, revoke and reset, Stale grants, ID fill-in at bind, the settings upgrade (grants kept, the *always* dev-environment confirmation dropped, token, port and limits untouched), the tree order, the File root levels and precedence, the path checks including a junction, the File root prompt before the dialog and with a Write grant, cancel and timeout, `update_web_resources` end to end (create, update, unchanged, publish true and false, a failed publish, version conflicts at preparation and while the dialog is open, managed, not in solution, prefix, extension, size, duplicate names, read-back), and plug-in components by name, version, ID and `componentTable`, with the numeric 10000 refusal.

**Still to run** — the spec's Phase 6 exit, live, through the plugin and the person's PAC profile only (the endpoint was not reachable in this session, and the windows have not been shown inside XrmToolBox):

1. In XrmToolBox, with grants of every scope for two environments in one tenant and one in another, the Active Approvals window lists them in §8.8's order; **Revoke** and **Reset** take effect on the next call; the main panel no longer shows the token. The windows rendered correctly to images in a test harness, but not in XrmToolBox.
2. A Write call approved *Per environment* runs the next Write to that environment without a dialog and with its log line pair; Write and the dev-environment confirmation offer nothing broader; a publisher creation still prompts every time.
3. A Read granted *Per tenant* covers a second environment of that tenant bound afterwards, and not the environment in the other tenant. **Check that `CrmServiceClient.EnvironmentId` and `TenantId` are filled for the connection types in use** (OAuth, certificate, client secret): where they are not, the connection reports no tenant and Per tenant is unavailable, which is the designed fallback but should be known.
4. A record query prompts on its own even with a Read grant; a `RecordRead` grant covers no other read.
5. A v1 settings file loads: Read grants kept, IDs filled at the next binding, an *always* dev-environment confirmation dropped, token unchanged. A grant whose environment ID no longer matches shows *Stale* and is not applied.
6. On the sandbox: a web resource edit is written, published and visible in the app without an import; a stale `expectedVersion` is refused; with no File root the first call prompts, and Cancel fails it with `denied_by_user`; a tenant File root serves a second environment without a prompt, and an environment File root takes precedence; `..`, an absolute path outside the root and a path through a junction are refused before the dialog; the File root is shown, changed and cleared in Active Approvals, and a change applies from the next call.
7. On the sandbox, with an assembly, two of its steps (one with an image) and a custom API with a request parameter and a response property registered beforehand: all added by name, unique name or ID in one call; the read-back shows what the platform added with each — **record it here**; an export of the WIP carries the steps with their image and the custom API with its parameter and property. This also answers part of spike S12.

---

## Phase 5 — execution

### What is built

| Area | Where | Spec |
|---|---|---|
| Version gate | `Setup/SetupGate` — first run, update or nothing, from `HostSettings.SetupCompletedVersion`; **Finish**, **Done** and **Skip** record the version, nothing else | §8.7 |
| Prompts | `Setup/Prompts/*.txt`, embedded as `Setup.Prompts.<name>.txt`; `Setup/SetupPrompts` fills `{{NAME}}` placeholders — version, previous version, endpoint, client configuration, the tab's organization URL, the export folder, the skill names | §8.7 |
| Skills | `skills/agent-companion-*/**` embedded as `Skills\<skill>\<path>` (csproj); `Setup/SkillExporter` writes one zip per skill, holding the skill's folder | §8.7, §10 |
| Wizard | `UI/SetupWizardControl` — the three pages and the **Plugin updated** page; `AgentCompanionControl` overlays it on the main panel, records the version on completion and closes the tab on **Cancel** | §8.7 |
| Client configuration | `Hosting/ClientConfig` — shared by **Copy client config** and the two MCP prompts | §8.1, §8.7 |
| CI | [build-and-pack-d365-agent-companion-mcp.yml](../../.github/workflows/build-and-pack-d365-agent-companion-mcp.yml) — the exporter's jobs, triggered also by `skills/agent-companion-*/**`, plus the skills' script self-tests in Windows PowerShell | Phase 5 |
| Repository pages | INSTRUCTIONS.md tooling section (and a rule on the embedded skills), CLAUDE.md map, root README map, matrix build-backlog row: built, release in progress | Phase 5 |
| Tests | 38 new: `SetupGateTests`, `SetupPromptsTests`, `SkillExporterTests` (the zips against `skills/`, byte for byte), `SetupWizardControlTests`; 245 in total | §10 |

Not done here: a local `nuget pack` with the wizard build — no `nuget.exe` on this workstation; the nuspec is unchanged since the last test pack, and CI packs on every run. The INSTRUCTIONS.md change must be re-pasted into the chat-surface project configuration.

### Implementation choices to review

| # | Choice | Why |
|---|---|---|
| I37 | The wizard is a control laid over the main panel in the same tab, not a dialog. The tab binds, the host runs, and the dev-confirmation and approval dialogs appear while it is open | §8.7: the host starts as usual so the test can reach it; the test's `list_solutions` dialog must be answerable from the wizard |
| I38 | **Copy client config** now starts with `Endpoint:` and `Bearer token:` lines | VS Code's `mcp.json` holds an input prompt, not the token; the person and the agent both need the token itself. One composer for the panel and the prompts |
| I39 | A zip holds the skill's whole folder, `scripts/tests/` included. Existing zips of the same names are replaced only after the person confirms; a folder inside a Git working tree warns first, as **Save as…** does | The zips match `skills/` exactly, which the tests check; nothing is overwritten unasked |
| I40 | A placeholder with no value is filled with an instruction to the agent — the tab is not connected, ask for the URLs; the zips were not exported from this page, ask for the folder — rather than left blank | The prompt still works when copied before connecting or exporting. The skills prompts name the folder exported from the same page |
| I41 | The version recorded is the assembly version; any difference, a downgrade included, shows the update page | §8.7 says "differs" |
| I42 | The MCP prompts ask for registration at user level (`--scope user` for Claude Code), never in a file inside a Git repository, and tell the agent not to repeat the token | The token is a secret (R7); project-scope MCP files are often committed |
| I43 | The setup prompts ask the agent to state an install command before running it, to ask before replacing an existing skill folder, and to touch no skill outside `agent-companion-*` | HITL; the update page's "leave every other skill alone" (§8.7) |
| I44 | No **Back** button | §8.7 names the buttons; a change in behaviour goes to the spec first |
| I45 | The column-3 guide changes (C.3 §7, 4.3) are not made yet | Phase 5: "add the column-3 guide changes then, not before" — at release, when the plugin installs from the Tool Library |

### Live exit checks — steps

Exit criterion (SPEC §11 Phase 5). Use the data-free sandbox and a fresh agent profile or client where the companion skills and the endpoint are not yet installed.

1. **Fresh install.** Build Release; copy only `D365AgentCompanionMcp.dll` (and `.pdb`) into XrmToolBox's `Plugins` folder. With no `SetupCompletedVersion` in the plugin's settings, open the plugin: page 1 shows.
2. **Cancel.** **Cancel** closes the tab; open the plugin again: the wizard shows again.
3. **Copy buttons.** On page 2 each **Copy setup prompt** puts its prompt on the clipboard, and the status line says so — for (e), with the token warning. Before connecting the tab, prompt (c) asks for the URLs; after connecting, it names the tab's organization URL.
4. **Export skills.** **Export skills…** to a folder outside any repository writes five zips, each holding `agent-companion-*/SKILL.md`; a second export asks before replacing them; a folder inside a Git working tree warns first. Prompt (d) then names the folder.
5. **Agent set up from the prompts alone.** Paste (b) to (e) into the fresh agent in order; then page 3's **Copy test prompt**. The agent reports all five steps as passed, and the `list_solutions` dialog appears in this tab.
6. **Finish.** **Finish** shows the main panel with the test call in the call log; open another tab: no wizard.
7. **Update.** Bump the version in the `.csproj` and `.nuspec` (two-file rule), rebuild and redeploy: the **Plugin updated** page names both versions, once. **Skip** records the version; set it back and repeat with **Done**. An agent following the page's prompts ends with the new skills and the endpoint registered, and no other skill changed.
8. **Tool Library.** A person adds the nuget.org trusted-publishing policy for `Lspiguel.Xrm.D365AgentCompanionMcp`; the merge to `main` publishes; install from the Tool Library, open, uninstall — no locked file; install again.
9. **README.** The tool table and settings match `tools/list` and the panel.

### Field use — 2026-11-02

A planning session on a client project, with another vendor's agent in a code editor, the plugin connected and the companion skills installed. The plan was for two plug-in steps and one column's required level, delivered through WIPs promoted to a build environment. Findings, each written back into the spec and the skills:

| # | Finding | Change |
|---|---|---|
| U1 | The agent researched from repository files only and wrote "confirm in the environment" as open points, though the read tools were available. The client listed them as deferred tools — names only — and no skill tied research to the plugin. "Use the MCP to verify" became a plan step until the person said *now* | K7: `d365ce-planning` reads the environment through the plugin when it is connected; `agent-companion-context` gains a research section and planning triggers |
| U2 | The agent read every companion skill and still wrote close-out B into the plan, and put "establish the delivery context" into a later task. T3 was tied to the first Write call, which a planning session never reaches | §9.8: the context is established while planning; T3: in a plan the steps are the task list |
| U3 | The agent argued that promotion contradicts R11 and rewrote the person's promotion task into an extract from development. It ignored earlier WIPs in development named `… - PROMOTED`, and the person's own statement of the strategy | R11 says what *downstream* means; T1: the person's statement is a source; §9.8: earlier WIPs are evidence |
| U4 | `d365ce-planning` defined agentic tasks as repository file edits, so a column change and plug-in registration were planned as manual | K7 and §9.10: registration through a WIP is agentic when the tooling can carry it out. **Reversed by U10:** registration is manual until Phase 7; a column change through a WIP stays agentic |
| U5 | The project names WIPs `<initials> <work item> <Group>`, so the leftover and reconcile searches for `_Agent_WIP_` would miss them | §9.5: a project's own naming convention wins and is searched for |
| U6 | The query for steps on a table joined `sdkmessagefilter`, `sdkmessage` and `plugintype`, outside the configuration allow-list, so it was flagged as touching records and would fail with record queries off | §6.5: the plug-in and custom API registration tables joined the allow-list; `FetchXmlQuery`, the `query` description and tests (251 pass); version 1.2026.11.1 |

**Not verified live:** §9.10 — a new build of an existing assembly, a new step with an image, and a new custom API authored in an unpacked WIP and imported with PAC CLI. `Test-UnpackedSolution.ps1` checks no packager anchors for these types yet. Field use then contradicted it (U10), and it moved to Phase 7.

The skills and the allow-list changed; the version is bumped to 1.2026.11.1 (two-file rule).

---

## Phase 4 — execution

### What is built

Five skills in [skills/](../../skills/), each a folder with `SKILL.md`, `references/` and, where needed, `scripts/`. Every `name` starts with `agent-companion-`, and every `description` opens by saying the skill is used together with the Agent Companion MCP and needs a bound plugin tab (SPEC §11 Phase 4).

| Skill | Owns | References and scripts |
|---|---|---|
| [agent-companion-context](../../skills/agent-companion-context/SKILL.md) | Delivery context, steps (T3), ledger shape; R15, R16 detection, T1–T6 | `rules.md` (rule index by ID), `delivery-facts.md` (where each fact is found; recording format), `wip-ledger.md` (context, steps, components, WIPs, states, reconcile); `Invoke-CompanionTool.ps1` |
| [agent-companion-transport-bootstrap](../../skills/agent-companion-transport-bootstrap/SKILL.md) | Missing transport solutions, publisher choice (R3), first-party apps (§9.3) | — |
| [agent-companion-wip-shaping](../../skills/agent-companion-wip-shaping/SKILL.md) | Classification, transport home, routes, naming, create, add (Path A/B), read-back, removal with shell cleanup, overlap and dependency checks | `component-types.md`, `queries.md`, `platform-behaviour.md`; `Get-WipName.ps1` |
| [agent-companion-wip-edit-loop](../../skills/agent-companion-wip-edit-loop/SKILL.md) | Export, baseline, edit, validate, pack, overwrite check, import order and flags | `platform-behaviour.md`; `Invoke-EditLoopStep.ps1`, `Test-UnpackedSolution.ps1`, `Test-CustomizationsSchema.ps1`, `Compare-ComponentState.ps1`, `Compare-SolutionExport.ps1` |
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
| I31 | ~~Global choices, relationships, sitemaps and apps (9, 10, 62, 80) are added through MCP only~~ **Verified live (E1–E4):** 9, 10 and 62 through MCP or PAC; 80 through MCP only | S3 verified PAC for columns, forms, views and web resources only; the live check below covers the rest |
| I32 | The rule index lives in `agent-companion-context/references/rules.md`; the other skills cite IDs and name the context skill as their precondition | One place to keep in step with the spec (K3) |
| I33 | Under strategy A, the bootstrap prepares the transport solutions for a person to create in build, and development receives them by alignment (A8) | The build tab is read-only (A2) |
| I35 | The scratchpad helpers used in the live run (`mcp.py`, `loop.py`) ship as PowerShell: `Invoke-CompanionTool.ps1` in the context skill, `Invoke-EditLoopStep.ps1` in the edit-loop skill | Matches the skills' other scripts and Windows PowerShell 5.1, which XrmToolBox already requires; no Python dependency. The token comes from `-Token` or `AGENT_COMPANION_TOKEN`, never from a client's configuration file |
| I36 | `Invoke-CompanionTool.ps1` live-tested: `list_connections`, `-All` paging (9 tables from pages of 2), ribbon chunks joined to `sizeBytes`, a tool error (exit 1), a wrong token (401, exit 2), JSON-string arguments. `Invoke-EditLoopStep.ps1` live-tested for Export, Validate, Pack, Schema and Recheck, and its refusal of a work folder inside a Git working tree; **Import and Verify not run** through it — they wrap the same `pac` commands the live run used | No second WIP was imported for the test, to avoid an unconfirmed import in a client environment |
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

### Live run — I31 add paths, 2026-10-31

Against a client R&D environment (not the data-free sandbox; nothing from it recorded here, R7), one tab bound and confirmed as development, reads approved per environment, each Write approved by the person. Calls over HTTP (2026-07-28) from a helper in the session scratchpad; PAC CLI 2.14.1. Publisher: the environment's default publisher (prefix `new`), reused (R3). Steps stated and confirmed before the first Write (T3).

Two empty unmanaged test solutions; the same managed out-of-box components added to each — one global choice, a 1:N and an N:N relationship of `competitor`, a sitemap and a model-driven app — through `add_solution_components` into one and `pac solution add-solution-component` (no `--AddRequiredComponents`) into the other. Both read back, exported and unpacked, compared, then deleted with `pac solution delete`. Nothing was imported.

| Type | MCP | PAC | Export |
|---|---|---|---|
| 9 global choice | Root, behaviour 0, nothing else | Same row | `OptionSets\<name>.xml` |
| 10 relationship | Added; the platform also added the referencing table as a shell with the lookup column under it (1:N), and the first table as a shell (N:N). The tool reported all three as `unexpected` | Same rows | The 1:N in `Other\Relationships.xml`, and the lookup column's whole definition in the referencing table's `Entity.xml`. The N:N: **not exported** |
| 62 sitemap | Root, behaviour 0 | Same row | `AppModuleSiteMaps\<name>\AppModuleSiteMap.xml` |
| 80 model-driven app | Root, behaviour 0 | **Fails**: type `80` "not known"; the name `AppModule` resolves to an invalid code, `Model-driven App` to a table | `AppModules\<name>\AppModule.xml` with an **empty** `AppModuleComponents`, while `appmodulecomponent` held 8 rows for the app (7 tables, 1 sitemap) |

Apart from the app, the two solutions' rows and unpacked files were identical. No required component appeared on either path (R1).

Findings:

| # | Finding | Action |
|---|---|---|
| E1 | Global choices and sitemaps behave the same through MCP and PAC | Skills: MCP or PAC for 9 and 62 — applied |
| E2 | A relationship brings parent table shells, and for a 1:N the referencing lookup column, on both paths. `add_solution_components` counts them as `unexpected`, because its parent-shell rule covers only columns, forms and views. The 1:N export carries the lookup column's full definition — on import, a whole document like X6 | Skills: MCP or PAC for 10; the read-back names these to the person — applied. **Decided (SPEC Q12):** they are expected platform behaviour; the tool is to report them as the relationship's parents (plugin change, not yet built), and unless the change customizes them the agent removes them afterwards, asking when in doubt — shaping and close-out skills, `platform-behaviour.md` |
| E3 | An out-of-box N:N whose intersect table is not customizable is held by the solution but not exported. Adding the intersect table as a shell fails (not customizable) | Skills note it. A custom N:N is untested |
| E4 | PAC 2.14.1 cannot add a model-driven app. An app's export lists no app components even when it has them; an import could replace the app's component list | Skills: MCP only for 80; compare `AppModuleComponents` with `appmodulecomponent` before importing a WIP holding an app — applied. **Open (SPEC Q13):** confirm on a test app what an import does |

### Live run — exit scenarios, 2026-10-31

Same environment, connection and approvals as the I31 run. The environment's out-of-box tables sit in a client's managed solutions, so editing them would leave unmanaged layers over managed components; at the person's choice the scenarios use a **disposable custom table** the person created in the maker portal (`new_agentp4test`, publisher prefix `new`), and test transport solutions `AgentP4Resources`, `AgentP4Base`, `AgentP4Apps`. Delivery context recorded in a scratchpad ledger in the `agent-companion-context` shape: strategy B, no build environment, no pipelines, not shared (person), segmentation map in the scratchpad standing in for `<client>-d365/`. Work items 9001–9004 are invented.

**Setup.** `AgentP4Base` and `AgentP4Apps` created; the table (metadata), its main form and its Active view added to `AgentP4Base` as their transport home. `AgentP4Resources` deliberately missing.

**Scenario 1 — placement story (9001): pass.** Steps stated and confirmed before the first Write (T3), and restated once (E8).

| Step | Result |
|---|---|
| Classify | Transport-home query: table, form, view at home in `AgentP4Base` → in place; new column and web resource → placement. Overlap check flagged the environment's *Common Data Services Default Solution* (E5) |
| Bootstrap | Only `AgentP4Resources` created; read back empty |
| Shape | `Get-WipName.ps1` → `LS_Agent_WIP_9001_Resources`, `LS_Agent_WIP_9001_Base`; names free; Base WIP: table (metadata), form, view — read back exact |
| Edit | Column authored into `Entity.xml`; form: field, library, onload handler; view: column; ribbon: a form button, command, enable rule, label; web resource authored into the Resources WIP |
| Validate | `Test-UnpackedSolution.ps1` with evidence (33 columns from `get_table_metadata`, the 218 KB base form ribbon reassembled from `get_entity_ribbon`, the web resource name): 0 errors, 0 warnings. Blast radius: exactly the four edited files, plus the web resource |
| Schema | One new error in Base — `AutoNumberFormat` — which Microsoft's own untouched export also fails (E6). Resolved by matching kinds against a reference export; Resources 0 new |
| Overwrite check | Versions unchanged; whole-tree re-export identical |
| Import | Resources **failed**: the web resource was not in the package (E7). The Base import was then run as well and failed on the missing dependency — the agent should have stopped (skill already says so; now stressed). Both failures rolled back. After the fix: Resources 3 min 22 s, Base 4 min 22 s, each publishing all customizations |
| Read back | Resources WIP: the web resource. Base WIP: table, form, view and the new column — expected |
| Validate in app | The person confirmed the view column, the field, the onload notification and the ribbon button |
| Close-out B | `get_required_components`: form and view need the new column (this story's Base WIP) and the web resource (this story's Resources WIP, earlier group); five columns in no solution (E8). Web resource → `AgentP4Resources`; new column and primary name column → `AgentP4Base`; each transport solution gained exactly those components against its snapshot (R1, B3). Both WIPs deleted; no WIP left |

**Scenario 2 — in-place change (9002): pass.** `LS_Agent_WIP_9002_Base` with the main form alone; the platform added the table as a shell, which the tool reported as the form's parent shell, not as unexpected. The shell's export carried the table's ribbon diff, including 9001's button (X6). Label edited; validation, schema, version and tree checks clean; import 3 min 57 s. W10: table and form both at home in `AgentP4Base`. The person confirmed the new label; the WIP was deleted with no close-out add.

**Scenario 3 — both routes, split (9003): pass.** The person chose to end the in-place part first (W12): `LS_Agent_WIP_9003_Base` (table as metadata, new column `new_agentp4score` authored into it) and `LS_Agent_WIP_9003_Base_2` (the Active view; the platform added the table as its shell). The overlap check showed the table in both WIPs (E9). Both validated, packed, schema-checked and overwrite-checked; imported `_Base` then `_Base_2`. The person confirmed the widened view column; `_Base_2` (W10: table and view at home) was deleted at once. `_Base` closed out by B: no dependencies, the column added to `AgentP4Base` — the only new row against the snapshot — then deleted.

**Scenario 4 — route change (9004): pass.** `LS_Agent_WIP_9004_Base` started in place — table as metadata (description edit) and the main form. During the edit a new column `new_agentp4flag` was authored and put on the form; the edit-loop skill marks it placement at that point. After import (2 min 33 s) the W10 read-back found it with no transport home and stopped the in-place deletion. The person confirmed the field; close-out B added it to `AgentP4Base` (the only new row); the WIP was deleted. The form's dependencies now resolve to `AgentP4Base` and `AgentP4Resources`, including the primary name column placed under E8.

**Scenario 6 — flow coexistence (9005): partial.** The person created a cloud flow (Dataverse *row added* on the test table, one Compose action) in a hand-made solution `LSManualP4Processes`, with a new Dataverse connection reference. Processes group added to the test map.

| Step | Result |
|---|---|
| Classify | Flow: workflow category 5 → Processes. The second component showed as `ComponentType10049`; not in the `componenttype` choice (E10); found by ID in `connectionreference`. Both placement. Overlap check: both held only by `LSManualP4Processes` — another open WIP (W7), reported; the person chose to delete it after the agent's WIP held both |
| Bootstrap | `AgentP4Processes` created — the bootstrap's second run, for a group first needed mid-project |
| Shape | `LS_Agent_WIP_9005_Processes`; PAC added the flow (type 29) at once; the connection reference failed as `10049` and as `Connection Reference`, and was added as `connectionreference` (E11). Read back: exactly the two |
| Coexistence | Loading `power-automate-flow-editing` showed the **installed** copy differs from the repository's: it still passes `--AddRequiredComponents` (E12). The add step had already run under the companion skill without the flag, so it did not bite; reported (K8) |
| Edit | The flow skill owned the edit (K7): one line, the Compose input to a whole-value `@concat`. Its `Test-FlowDefinition.ps1` (repository copy): 0 findings. Blast radius: the flow JSON only. Schema: 0 new |
| Overwrite check | Whole-tree re-export identical — flows have no row version in `get_component_state` |
| Import | The flow skill's flags (`--activate-plugins --publish-changes --force-overwrite`), 2 min 3 s. Flow still Activated; a re-export matched the edit byte for byte |
| Runtime | **The flow did not run** on a new record — nor after the person turned it off and on. Its connection reference is bound and Active; `flowrun` holds no rows. The flow was never run before the import, so the cause — environment or import — is not established |
| Close-out | **Not done**: not validated at runtime, so the WIP stays open (`LS_Agent_WIP_9005_Processes`); the person deferred cleanup (the sandbox will be overwritten) |

| # | Finding | Action |
|---|---|---|
| E10 | Component type codes of 10000 and above are not in the `componenttype` choice (90 values here); `list_solution_components` names them `ComponentType<code>` | `component-types.md`: identify by querying the candidate table by ID — applied |
| E11 | PAC 2.14.1 refuses such a code as a number and resolves a display name to the wrong type; the table's logical name works | Shaping skill and `component-types.md` — applied |
| E12 | The user-level installed `power-automate-flow-editing` is an older copy than the repository's and passes `--AddRequiredComponents`, against R1 | **Resolved** (2026-10-31): the person reinstalled the user-level skill from the repository. No repository change: the repository copy was right |
| E13 | A Dataverse-triggered flow imported through the loop did not fire in this environment; cause not isolated | Judged by the person to be an issue of this environment. **Not carried into the skills.** Re-test in another environment, with a run before the import as the control |

**Scenario 5 — gaps in the delivery context: desk check, pass.** T3 was exercised live in every scenario above — steps confirmed before the first Write call, and restated when E8 changed them. With the map and the transport source withheld, `agent-companion-context` produces, per `delivery-facts.md`:

```
1. Segmentation map. None found next to the solutions. Three transport solutions exist —
   AgentP4Resources (1 web resource), AgentP4Base (1 table, 3 columns, 1 form, 1 view) and
   AgentP4Apps (empty). Recommendation: write their current placement down as the map —
   web resources to Resources; tables, columns, forms and views to Base; apps and sitemaps to
   Apps. Until you confirm, I classify only components that already have a home (W2).
2. Transport source. No extraction pipeline in the repository and no build environment bound.
   Recommendation: treat all three as extracted from development (strategy B): close-out adds
   new components there, and edits to components already in them ship at the next extract.
   If they are extracted from a build environment instead (A), close-out is a promotion per
   WIP and the build environment should be bound in a second tab, left unconfirmed.
```

Given a described build environment (`[ClientName] BUILD`, bound read-only) and a promotion pipeline taking the WIP name and the destination, the steps for a story with a placement WIP and an in-place WIP:

```
1. get_required_components for both WIPs (A4, W6, X5); names checked against protected prefixes (W4).
2. You run the promotion pipeline for the placement WIP — WIP <name>, destination AgentP4Base — and
   then for the in-place WIP; one at a time, in transport order (A3). Its commit triggers no
   deployment between the two runs (X8) — please confirm.
3. After each run, on the BUILD tab: list_solution_components on AgentP4Base — the WIP's
   components with the behaviours they had, nothing else new (A1); list_solutions — the WIP is
   gone from build.
4. Then pac solution delete of each WIP in development (A6). The in-place WIP ends only now,
   after its promotion — not at import (W10).
I run no pipeline, commit or push (T6).
```


Findings:

| # | Finding | Action |
|---|---|---|
| E5 | Every maker-portal component is also in the environment's *Common Data Services Default Solution* (unmanaged, unique name `Cr…`, behaviour 0). The overlap check counted it as another WIP | `queries.md`: ignore it, found once by friendly name — applied |
| E6 | The schema bundle (9.0.0.2090) fails current platform exports: `AutoNumberFormat` on columns, `CascadeArchive` on relationships, `EnableCollapsibleGroups` on sitemaps, `OptimizedFor` on apps, and the root's version attributes. A newly authored column is a *new* finding against a WIP export that held none | `Test-CustomizationsSchema.ps1`: findings matched by kind (message before the expected-elements list) against any number of baselines; skill passes a reference export holding the same kinds — applied, test added |
| E7 | A component of a type the export did not hold has no anchor node in `Other\Customizations.xml`; `pac solution pack` then silently leaves it out (the file is zipped, the definition is not), and the import fails with *"Cannot add a Root Component … not in the target system"* | Edit-loop skill documents the anchor; `Test-UnpackedSolution.ps1` reports `PackagerAnchorMissing` (Error for web resources, verified; Warning for workflows, choices, apps, sitemaps, not verified) — applied, tests added |
| E9 | Splitting a group's work (W12) puts the table in both of the story's WIPs — metadata in one, the platform's parent shell in the other — so both carry its ribbon diff (X6). W12's "a component is in at most one of a story's WIPs" cannot hold for the table | SPEC W12 amended first: the parent table shell is the exception; ribbon and table-property edits go in only one WIP. Shaping skill and rule index updated — applied |
| E8 | A metadata-only table exports no columns. A custom table placed that way would reach a new environment without its primary name column; its system columns are created with it. `get_required_components` lists only direct solution rows, so columns held through an include-all table show as in no solution | Shaping and close-out skills: place a new custom table's primary name column (and project columns) explicitly; system columns and columns under an include-all table are in place — applied. Downstream import of the result untested (no second environment) |

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

Raised by the desk research, the live spikes and field use. All closed.

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
| D10 | **Web resources updated directly** (Phase 6) | `update_web_resources`: create or update from local files in a WIP, overwrite check inside the tool, targeted publish, files by path under the organization's **File root** (D15) | [SPEC §6.8](SPEC.md#68-direct-update-tools-phase-6), §9.11, §11 |
| D11 | **Assemblies and steps** (Phase 7 since U10) | A new build of a registered assembly goes through `pac plugin push` after G5. Three separate step tools: `mirror_steps` only mirrors, and always creates disabled; `disable_steps` and `enable_steps` switch, on purpose. The person's choice over one combined switch tool, so that which copy runs is never a side effect. Side-load WIP named `…_Assemblies_Sideload`, never imported (M2) | [SPEC §6.9](SPEC.md#69-plug-in-step-tools-phase-7), §9.5, §9.10 (G5, M1–M7) |
| D12 | **Custom APIs** (Phase 7 since U10) | `rebind_custom_apis` — a custom API's main operation is its `plugintypeid`, which mirroring cannot cover; steps on its message use `mirror_steps` | [SPEC §6.9](SPEC.md#69-plug-in-step-tools-phase-7) |
| D13 | **Power Pages** | Not pursued for now, at the person's choice: standard-model configuration is records outside solutions, and `pac pages upload` already uploads changes in place. A read-only drift check is the candidate if the topic returns | [SPEC §9.11](SPEC.md#911-direct-updates-in-development) |
| D14 | **PCF** | Guidance only: build, raise the patch version, `pac pcf push --solution-unique-name <WIP>` (then `--incremental`), publish when the change does not show | [SPEC §9.11](SPEC.md#911-direct-updates-in-development) |
| D15 | **File root** (Phase 6, 2026-11-04) | The person's choice, replacing the per-tab *File roots* list: there is one File root per organization, the root of the git repository holding the source files the agent works on. A file path from the agent is either relative, read as File root + path, or absolute and checked to lie inside the root. Both forms are normalized first, and links on the path are refused. When a file-taking call prepares with no File root set, the plugin prompts the person, explaining these rules. The File root is shown and edited in the Active Approvals window, and returned by `get_connection_info`. Revised by D16: set per environment or per tenant instead of per organization | [SPEC §6.8](SPEC.md#file-root), §6.5, §7, §10, §11 |
| D16 | **Scopes and grants** (Phase 6, 2026-11-04) | The person's choice. A client project usually has one repository and many environments, and organization and environment are effectively 1:1, so grants are scoped by reach:<br>• **Scopes:** one enum — Once, Session, Environment, a new Tenant, and Global, which keeps the enum name `Always` so saved grants still load.<br>• **Kinds:** Read, a new `RecordRead` that replaces the per-connection Record queries switch, a new `Write`, and the dev-environment confirmation. Write and dev env go no further than Environment, and the *always* dev-env confirmation is withdrawn. Publisher creation stays once per call.<br>• **Identity:** grants record organization, environment and tenant IDs (`CrmServiceClient.EnvironmentId` / `TenantId`). The organization ID alone is accepted and the others are filled in when a connection reports them. Any known mismatch makes a grant *Stale*.<br>• **File root:** set at Environment or Tenant level, asked in the prompt; Tenant is pre-selected.<br>• **Settings file:** gains `SchemaVersion` and an upgrade mapping. Enum and element names are unchanged. An upgrade may drop grants, never the token or host settings. Losing grants on a downgrade is accepted.<br>• **Active Approvals window:** one tree instead of a section per scope. Global comes first, then each tenant with its grants and then its environments; each environment shows its environment grants, then its session grants. Environments without a tenant come last. Environments are in alphabetical order and tenants follow the name of their first environment. File roots sit under the tenant or environment they belong to. **Reset** applies to Global, to a tenant together with its environments, or to one environment | [SPEC §7](SPEC.md#phase-6--scopes-and-grants), §5 (C8), §6.2, §6.5, §6.8, §8.1, §8.2, §8.6, §8.8, §10, §11, §12 |

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
| 2026-10-31 | Phase 4 live run recorded (I31, scenarios 1–6, E1–E13); E13 judged an environment issue, kept out of the skills. Q12 decided: relationship parents are expected; the agent removes them unless customized (spec §6.6, shaping and close-out skills). Platform behaviour registered in `platform-behaviour.md` for the shaping and edit-loop skills. Helpers ported to PowerShell and shipped: `Invoke-CompanionTool.ps1`, `Invoke-EditLoopStep.ps1` (I35, I36). Spec: first-run setup wizard (§8.7) added to Phase 5; scoped approvals for every tier but Publisher added to Phase 6, with the R2 amendment it requires |
| 2026-10-31 | E12 resolved: the user-level flow-editing skill was reinstalled from the repository. Cleanup deferred by the person: the test environment is a sandbox for deployment tests and will be overwritten, taking with it the AgentP4 transport solutions, `LS_Agent_WIP_9005_Processes`, the test table, web resource, flow and connection reference. Phase 6 scoped Write approvals reworded as a person's decision within R2. Phase 5: `D365AgentCompanionMcp.nuspec` written after the Context Exporter's; a test `nuget pack` (placeholder readme, output in the scratchpad) produced the package with the DLL in `lib/net472/Plugins` and the exporter's two warnings |
| 2026-10-31 | Phase 5 started. Icons added by the person (`d365-agent-companion-16px.png` … `-256px.png`); nuspec `iconUrl` set to the 128 px file. Tooling README written in the Context Exporter's style — what it does, prerequisites, quickstart, then endpoint registration and the OAuth-prompt note, how it works, WIP solutions, close-out strategies, close-out, skills, tools, settings, building from source, license. It describes the setup wizard, which is not built yet. Test `nuget pack` with the real README: package holds LICENSE, readme.md and the DLL; same two warnings as before |
| 2026-10-31 | Icons wired into the plugin: the 16 px and 32 px PNGs as base64 `SmallImageBase64` / `BigImageBase64`, as the exporter does. Release build 0 warnings, 207 tests pass. Phase 5: GitHub workflow specified after the exporter's — same jobs, triggers also on `skills/agent-companion-*/**`, plus a step for the skills' script self-tests; nuget.org trusted-publishing policy for the new package ID is a person's step |
| 2026-10-31 | Spec §8.7: the wizard records the plugin version it was completed for; after an update a one-time **Plugin updated** page offers an optional reinstall of the skills and the MCP configuration through agent prompts (**Skip** or **Done** record the new version). Phase 5 bullet and exit updated; README gained a *Plugin upgrades* section |
| 2026-10-31 | Phase 5 built. Setup wizard (§8.7): version gate on `SetupCompletedVersion`, three pages and the **Plugin updated** page laid over the main panel, seven prompts as text resources, the five companion skills embedded from `skills/` and exported one zip per skill; **Copy client config** shared with the MCP prompts and now names the endpoint and token. GitHub workflow `build-and-pack-d365-agent-companion-mcp.yml`, with the skills' script self-tests (11 and 31 pass locally). INSTRUCTIONS.md, CLAUDE.md, root README and matrix describe the plugin as built, release in progress; column-3 guides wait for the release. Release build 0 warnings; 245 tests pass. Choices I37–I45 recorded; live exit checks next |
| 2026-11-03 | Phase 6 prepared in the spec from field use: direct updates in development analysed per component (notes, *Phase 6 — preparation*). D10–D14 decided: `update_web_resources`; `pac plugin push` after G5 for a registered assembly; `mirror_steps` (always disabled), `disable_steps`, `enable_steps`; `rebind_custom_apis`; PCF guidance only; Power Pages not pursued. U7 (imports re-enable disabled steps) gave rules M1–M7. SPEC §3, §6.2, §6.3, §6.8 (new), §7, §9.5 (`_Sideload`), §9.10 (G5), §9.11 (new), Phase 6 (S9–S11, exit), §12, Q14, references 5–7. Nothing built |
| 2026-11-03 | Phase 5 done: the plugin is published in the XrmToolBox Tool Library. Q14 decided: no delete tool. Spikes S9–S11 run on a trial environment (S9, S10 through a throwaway SDK console — its sign-in prompts stopped by the person as outside the design; S11 through PAC). Key results: steps cannot be created disabled; an import sets every carried step enabled with activation and disabled without, with no state in the export; custom API rebinding is immediate; web resource version rises on every update and on publish; direct update ≈ 3.5 s against 25–50 s for a round trip; `pac pcf push` publishes all and needs an existing solution. Spec §6.8, §9.11, Phase 5 status, Phase 6 note and spike table updated. Spike components left in the trial environment for the person to remove |
| 2026-11-03 | Plug-in authoring moved to a new Phase 7 after field use (U10): `pac plugin push` did not register new plug-in types, and steps were neither exported nor created by import. Spec: §3 (registration is a person's until Phase 7), §6.9 (step tools, split from §6.8), §7, §9.5 (`_Sideload` is Phase 7), §9.10 (Phase 7 notice; side-loading, version change and M1–M7 moved in from §9.11), §9.11, Phase 4 row, K7, Phase 6 (S9 and the step rows out), Phase 7 (new: field findings, enhancements, S9 and S12, exit), §12. W7 and X1 back to their wording without plug-in assemblies (G3 is Phase 7). Skills: the plug-in edits of this branch removed; research queries kept |
| 2026-11-03 | Phase 6 gains plug-in components in `add_solution_components` (person's request): plug-in assembly 91 by name and version, step 92 by ID, custom API, request parameter and response property by `componentTable` with codes resolved per environment; registration stays a person's (Phase 7). Spec §6.6 (selectors, table, behaviour), §9.10 notice, Phase 6 table, skill-change row and exit |
| 2026-11-04 | Phase 6 gains the **File root** (D15, person's choice): one per organization, the root of the git repository holding the source files; `update_web_resources` paths are relative to it or absolute inside it, normalized, links refused; prompted on first use, shown and edited in Active Approvals, returned by `get_connection_info`. Replaces the per-tab *File roots* list. Spec §6.8, §6.5, §7, §10, §11 and the Phase 6 exit |
| 2026-11-04 | Phase 6 **scopes and grants** specified (D16, person's choice): Tenant scope added and *Always* shown as Global; `RecordRead` and `Write` grants replace the **Record queries** switch; Write and dev-environment confirmation go no further than Environment and the *always* confirmation is withdrawn; grants record organization, environment and tenant IDs and go *Stale* on a mismatch; settings file gains `SchemaVersion` and an upgrade mapping; File root now set at Environment or Tenant level (revises D15). Spec §7 (new *Phase 6 — scopes and grants*), §5 (C8), §6.2, §6.5, §6.8, §8.1, §8.2, §8.6, §10, §11, §12 |
| 2026-11-04 | **Active Approvals** restructured as one tenant tree: Global first, then each tenant with its grants and environments, each environment with its environment grants then session grants, environments without a tenant last, alphabetical order; File roots shown under the tenant or environment they belong to; **Reset** for Global, a tenant with its environments, or one environment, plus **Reset all approvals**, each confirmed in the window. Spec §8.8 (layout), Phase 6 table and exit |
| 2026-11-05 | Phase 6 build version set to **1.2026.11.2**: assembly and NuGet package versions aligned (`.csproj`, `.nuspec`), noted in the Phase 6 section of the spec. Nothing built yet |
| 2026-11-05 | Phase 6 built (version 1.2026.11.2). Grants and settings first: `ApprovalScope.Tenant`, `ApprovalKind.RecordRead` and `Write`, `ConnectionIdentity`, an immutable `Grant`, the store with `Allow`, `FillIdentity`, `Stale` and the revoke and reset calls, `HostSettings.SchemaVersion` with an upgrade mapping and `FileRoots`. Then the dispatcher (grant kind by tool and inspection, offered decisions by tier, an unoffered decision is a denial), `get_connection_info` (environment and tenant IDs, `recordReadScope`, `writeScope`, `fileRoot`, `fileRootScope`; `recordQueriesEnabled` gone) and `query` (no `tool_disabled`). Then the File root (store, `FilePathResolver`, the prompt before the dialog) and `update_web_resources`; plug-in components in `ComponentSelector` and `SolutionShapingTools`. Then the windows: `ActiveApprovalsForm` with the tree, `FileRootForm`, `ApprovalForm` scopes, the panel; `AlwaysSettingsForm` deleted. The unit tests found and fixed a malformed publish request (B1). 251 → 383 tests, 0 warnings. Skills (edit loop, shaping), README, `.nuspec`, INSTRUCTIONS.md (R2 and the fixed safety behaviour reworded), CLAUDE.md, root README and SPEC updated. No live check run: see [Phase 6 — execution](#phase-6--execution) |
