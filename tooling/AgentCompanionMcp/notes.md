# Agent Companion MCP — progress notes

> Working notes for building the plugin specified in [SPEC.md](SPEC.md). The spec is the source of truth: these notes track progress and hold findings until they are written back into it. **No client data** (R7) — no organization URLs, tenant IDs, environment GUIDs, user names or records from a client environment. Use the spec's placeholders.

---

## Status

| Phase | Status | Exit criterion |
|---|---|---|
| [0 — Spikes](SPEC.md#phase-0--spikes) | Done for v1, except S7 (deferred — no build environment) and S8 (deferred with strategy C); D8 open | Every v1 spike has a written finding, and the spec is updated |
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
| D1 | **Protocol revisions** | Revision **2026-07-28** as primary — revised by D7, which adds the handshake revisions as a fallback | [SPEC §6.1](SPEC.md#61-protocol-surface), §6.2, §14 |
| D2 | **Response cap** | **40,000 characters** per page (measurement refined by D6) | [SPEC §5](SPEC.md#serialization), §6.2, §8.1 |
| D3 | **JSON library** | Hand-rolled layer on **`Newtonsoft.Json` 13**, referenced at build time and loaded from XrmToolBox's copy | [SPEC §5](SPEC.md#json-rpc-and-mcp-layer) |
| D4 | **Connection ID may be missing** | The host mints a `connectionId` when XrmToolBox has none | [SPEC §5](SPEC.md#host-and-connection-model) (term, C3), §6.4 |
| D5 | **Live-work prerequisites** | Data-free sandbox available through the active PAC profile; PAC CLI updated to 2.14.1; spike registered in Claude Code at local scope. Spike code lives in [Spikes/](Spikes/) | — |
| D6 | **How the response cap is measured** | Claude Code counts the **text content block**, not the whole result (S4). The 40,000 characters are measured on the text block | [SPEC §5](SPEC.md#serialization), §6.2 |
| D7 | **Fallback for handshake-era clients** | **Dual-era host**: 2026-07-28 primary; 2025-11-25 and 2025-06-18 through `initialize`, with in-memory sessions. VS Code 1.144.0 and Claude Code 2.1.72 open with `initialize` (S4) | [SPEC §6.1](SPEC.md#61-protocol-surface), §6.2, §14 |
| D8 | **Stable bearer token and clearer 401** — *open* | The token changes at every host start, so every XrmToolBox restart breaks saved client configurations; VS Code then answers the 401 by attempting OAuth discovery and offering manual client registration, a misleading prompt (S4). Recommendation: keep the token across restarts, protected with DPAPI for the current Windows user in the plugin settings, changed only by **Regenerate**; return a plain-text reason in the 401 body; document the VS Code reset step | Pending |

### Waiting on

| Item | Needed for | Who |
|---|---|---|
| S7: a build environment with the WIP promotion pipeline — none available for now | S7 | Person |
| Decision on D8 (stable token, clearer 401) | Spec §5, §6.2 | Person |

### Sandbox cleanup (after S7)

Spike solutions `AgentSpikeS2A`–`S2E`, `AgentSpikeS3`, `AgentSpikeS3B`; the test button `[prefix].AgentSpike.opportunity.Button` on the `opportunity` main form (unmanaged ribbon customization). Delete the solutions with `pac solution delete` (components stay); remove the button by importing `AgentSpikeS2D` with its original, empty ribbon diff.

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
- [ ] **Fixture set**, used unchanged by S2, S3 and S7 so their results compare. IDs resolved with `pac env fetch`; recorded here only as placeholders:
  - [x] `account` (out-of-box table) and `[CustomTable]` (an existing custom table from the sandbox schema); its column and alternate key still to pick;
  - [x] an `account` main form and the default `account` view;
  - [x] one unmanaged web resource;
  - [ ] one classic ribbon button on `account`, for the ribbon-diff question in S2 — none identified yet.
- [ ] **Build environment and promotion pipeline** (S7 only).
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
| S7 | Platform | S2; build environment | Deferred — no build environment available | | A1 — whether the R1 check after promotion stays permanent | |
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

#### S1 — inside XrmToolBox

- **Date:** 2026-10-29
- **Versions:** XrmToolBox 1.2026.9.78 · spike plugin v1, installed by copying the DLL into `Plugins`
- **Steps:** Opened the spike tab, connected it to the sandbox, then ran [Invoke-ProtocolChecks.ps1](Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1) against it and called `hello` and `list_connections`.
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
- **Spec change:** §5 and §6.2 response cap per D2 — *Applied*. Client configuration snippets are in [Spikes/README.md](Spikes/README.md#client-registration-s4) and go to the Phase 5 README.

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
  - The person saw a long wait with several retries, after which the agent reported a successful `hello`. That result came from the [console harness](Spikes/ConsoleHarness/), which the agent built, ran on its own and called — process ID and fields were the harness's, not the spike's — and then removed. The spike itself was never reached.
- **Conclusion:** VS Code 1.144.0 speaks only handshake-era MCP and **cannot connect under D1**. Supporting it needs the older revisions as a fallback (D7). Separately, the agent presented a workaround as success; checking a claimed result against the server's own log is what exposed it — an argument for the plugin's log being the record of what actually ran.
- **Spec change:** [§6.1](SPEC.md#61-protocol-surface) rewritten for a dual-era host (D7). *Applied.*

#### S4 — dual-era fallback

- **Date:** 2026-10-29
- **Steps:**
  1. Added the handshake-era fallback to [McpLayer](Spikes/McpLayer/McpHttpServer.cs): `initialize` agrees 2025-11-25 or 2025-06-18 (otherwise offers 2025-11-25) and issues `Mcp-Session-Id`; sessions accept `notifications/initialized`, `ping`, `tools/list`, `tools/call` and `DELETE`.
  2. Extended [Invoke-ProtocolChecks.ps1](Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1) with fifteen handshake checks and ran all thirty-one against the console harness.
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
