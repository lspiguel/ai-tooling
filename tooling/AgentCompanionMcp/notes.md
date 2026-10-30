# Agent Companion MCP — progress notes

> Working notes for building the plugin specified in [SPEC.md](SPEC.md). The spec is the source of truth: these notes track progress and hold findings until they are written back into it. **No client data** (R7) — no organization URLs, tenant IDs, environment GUIDs, user names or records from a client environment. Use the spec's placeholders.

---

## Status

| Phase | Status | Exit criterion |
|---|---|---|
| [0 — Spikes](SPEC.md#phase-0--spikes) | In progress — S5 done; protocol layer built and checked; live XrmToolBox, client and Dataverse work [waiting](#waiting-on) | Every v1 spike has a written finding, and the spec is updated |
| [1 — Host, connections, approvals and log](SPEC.md#phase-1--host-connections-approvals-and-log) | Not started | See spec |
| [2 — Discovery](SPEC.md#phase-2--discovery) | Not started | See spec |
| [3 — Solution shaping](SPEC.md#phase-3--solution-shaping) | Not started | See spec |
| [4 — Agent skills and scripts](SPEC.md#phase-4--agent-skills-and-scripts) | Not started | See spec |
| [5 — Release](SPEC.md#phase-5--release) | Not started | See spec |

Status values: *Not started* · *In progress* · *Blocked* (say on what) · *Done*.

---

## Decisions

Raised by the desk research.

| # | Decision | Outcome | Applied |
|---|---|---|---|
| D1 | **Protocol revisions** | Revision **2026-07-28 only**. Clients that speak only the handshake revisions cannot connect; S4 records which client versions qualify | [SPEC §6.1](SPEC.md#61-protocol-surface), §6.2, §14 |
| D2 | **Response cap** | **40,000 characters** per page, measured on the whole serialized result | [SPEC §5](SPEC.md#serialization), §6.2, §8.1 |
| D3 | **JSON library** | Hand-rolled layer on **`Newtonsoft.Json` 13**, referenced at build time and loaded from XrmToolBox's copy | [SPEC §5](SPEC.md#json-rpc-and-mcp-layer) |
| D4 | **Connection ID may be missing** | The host mints a `connectionId` when XrmToolBox has none | [SPEC §5](SPEC.md#host-and-connection-model) (term, C3), §6.4 |
| D5 | **Live-work prerequisites** | A data-free sandbox environment is available. Spike code lives in [Spikes/](Spikes/). Still open — see [Waiting on](#waiting-on) | — |

### Waiting on

| Item | Needed for | Who |
|---|---|---|
| Copy the spike DLL into XrmToolBox's `Plugins` folder and run the steps in [Spikes/README.md](Spikes/README.md#xrmtoolbox-spike--live-steps-s1-s6) | S1 live, S6 live | Person — XrmToolBox is a desktop app the agent cannot drive |
| Register the spike endpoint in Claude Code and VS Code | S4 live | Person, or the agent with explicit approval — it changes client configuration |
| Which PAC auth profile (or how to authenticate) reaches the sandbox; PAC CLI updated from 1.34.4 | S2, S3 | Person |

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

- [x] **Disposable development environment** with no client data — a sandbox with schema only. S2 and S3 add out-of-box tables to solutions; expect to reset it. Its URL stays out of this file.
- [ ] **Publisher** for the spikes, with a placeholder prefix.
- [ ] **Fixture set**, used unchanged by S2, S3 and S7 so their results compare:
  - `account` (out-of-box table) and one custom table with one column and one alternate key;
  - the `account` main form and one `account` view;
  - one web resource;
  - one classic ribbon button on `account`, for the ribbon-diff question in S2.
- [ ] **Build environment and promotion pipeline** (S7 only).
- [x] **Versions recorded** (2026-10-29, workstation): XrmToolBox 1.2026.9.78 · PAC CLI 1.34.4 (current release 2.14.1 — update before S3) · .NET SDK 10.0.401 · Claude Code 2.1.72 · VS Code 1.144.0. Environment version: when one is assigned.

### Timeboxes

Spikes are throwaway. Set a timebox when a spike starts and record it in the tracker. If it runs out, write up what is known, mark the spike *Blocked* or *Done — partial*, and decide with a person whether to extend it.

### Spike tracker

| ID | Track | Depends on | Status | Timebox | Feeds | Finding |
|---|---|---|---|---|---|---|
| S1 | Plugin | — | In progress — layer built, protocol checks pass; live in XrmToolBox waiting | | [§5](SPEC.md#5-architecture) host, JSON-RPC layer, serialization; [§6.1](SPEC.md#61-protocol-surface) protocol revision | [S1](#s1--mcp-host-inside-xrmtoolbox-on-net48) |
| S2 | Platform | — | Waiting on sandbox access details | | `tableBehavior` mapping in `add_solution_components`; component type codes; X6 | |
| S3 | Platform | — | Waiting on sandbox access details and PAC update | | Path B per component type ([§4](SPEC.md#4-responsibility-split--mcp-vs-pac-cli)) | |
| S4 | Plugin | S1 | In progress — desk done; live needs S1 | | Client configuration snippets; approval timeout default; response cap (D2) | [S4](#s4--agent-clients-over-local-http) |
| S5 | Desk research | — | Done | | Read tools kept or dropped ([§6.3](SPEC.md#63-tool-summary)) | [S5](#s5--overlap-with-the-dataverse-mcp-server) |
| S6 | Plugin | S1 (same shell) | In progress — desk done; spike plugin built; live waiting | | H1–H4 and C1–C10 ([§5](SPEC.md#host-and-connection-model)) | [S6](#s6--several-tabs-of-one-plugin) |
| S7 | Platform | S2; build environment | Not started | | A1 — whether the R1 check after promotion stays permanent | |
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
  1. Built [Spikes/McpLayer](Spikes/McpLayer/) into the [console harness](Spikes/ConsoleHarness/) and ran it on `127.0.0.1:47321`.
  2. Ran [Invoke-ProtocolChecks.ps1](Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1): sixteen requests, one per rule in SPEC §6.1 and §6.2.
  3. Posted `tools/list` to `/mcp` (no trailing slash) and to `localhost` instead of `127.0.0.1`.
  4. Built the [XrmToolBox spike](Spikes/XtbHostSpike/) with `Newtonsoft.Json` as `ExcludeAssets="runtime"` and listed its output.
- **Observed:**
  - All sixteen checks pass: `server/discover`, `tools/list`, `tools/call` (plain and Base64-sentinel `Mcp-Name`) → 200; unknown tool → 200 with `-32602`; unknown method → 404 `-32601`; legacy `initialize` and an older version in `_meta` → 400 `-32022`; missing or mismatched headers → 400 `-32020`; notification → 202; `GET` and `DELETE` → 405; no token → 401; foreign `Origin` → 403; `Mcp-Session-Id` ignored.
  - Both URL forms answer 200. `tools/list` for three tools is 1,083 characters.
  - The spike plugin builds; its output has no `Newtonsoft.Json.dll`, so at run time it binds to XrmToolBox's copy.
  - Not covered by `HttpListener`: noticing that a client closed its request while the call waits for approval. The write fails afterwards and is logged; the dialog stays open until answered or timed out.
- **Conclusion:** The protocol layer behaves as SPEC §6.1 requires, without the SDK. Open for the live test: loading inside XrmToolBox and the clients' real requests. Spec gap: cancelling a pending approval when the client disconnects needs a way to detect the disconnect.
- **Spec change:** None yet — the disconnect gap is recorded here until the live test shows whether it matters.

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
- **Spec change:** §5 and §6.2 response cap per D2 — *Applied*. Client configuration snippets are in [Spikes/README.md](Spikes/README.md#client-registration-s4) and go to the Phase 5 README.

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

---

## Log

| Date | Entry |
|---|---|
| 2026-10-29 | Spec drafted through the resolved open questions; notes created. Phase 0 not started |
| 2026-10-29 | Phase 0 started. Desk research for S1, S4, S5 and S6; S5 done and applied. Decisions D1–D5 raised; live work waits on D5 |
| 2026-10-29 | D1–D4 decided and applied to the spec. Spike code added under `Spikes/`: protocol layer passes all sixteen checks in the console harness; XrmToolBox spike plugin built. Waiting on the person for the XrmToolBox run, client registration and sandbox access |
