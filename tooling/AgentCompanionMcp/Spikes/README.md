# Agent Companion MCP — spikes

> Throwaway code for the Phase 0 spikes in [SPEC.md](../SPEC.md#phase-0--spikes). Nothing here ships, and none of it follows the plugin's release conventions (analyzers, versioning, packaging). Findings go to [notes.md](../notes.md); the code stays only as long as it helps reproduce them. **No client data** in code, logs or screenshots committed here (R7).

| Folder | Spikes | What it is |
|---|---|---|
| [McpLayer/](McpLayer/) | S1, S4 | Hand-rolled, dual-era MCP Streamable HTTP endpoint — 2026-07-28 plus the 2025-11-25 and 2025-06-18 handshake fallback — on `HttpListener` and `Newtonsoft.Json`. Shared source, compiled into both projects below |
| [ConsoleHarness/](ConsoleHarness/) | S1, S4 | Runs the MCP layer as a console app with fake tools (`hello`, `wait`, `big`), plus [Invoke-ProtocolChecks.ps1](ConsoleHarness/Invoke-ProtocolChecks.ps1) |
| [XtbHostSpike/](XtbHostSpike/) | S1, S2, S6 | XrmToolBox plugin: process-wide singleton host, one tab per connection, *Connection already active* dialog, approval dialog owned by the main window. Tools: `hello`, `get_log` and `list_connections` (no approval); `who_am_i`, `create_solution` and `add_solution_component` (approved in the tab; the last runs `AddSolutionComponentRequest` with `AddRequiredComponents = false` and explicit flags, for S2) |

## Build

```powershell
dotnet build tooling/AgentCompanionMcp/Spikes/ConsoleHarness/ConsoleHarness.csproj
dotnet build tooling/AgentCompanionMcp/Spikes/XtbHostSpike/XtbHostSpike.csproj
```

## Protocol checks (no XrmToolBox needed)

```powershell
# terminal 1 — runs until Ctrl+C
tooling/AgentCompanionMcp/Spikes/ConsoleHarness/bin/Debug/net48/AgentCompanionMcp.ConsoleHarness.exe 47321 <token>

# terminal 2
powershell -ExecutionPolicy Bypass -File tooling/AgentCompanionMcp/Spikes/ConsoleHarness/Invoke-ProtocolChecks.ps1 -Token <token>
```

The same script runs against the XrmToolBox spike: use the token from **Copy bearer token** in any spike tab.

## XrmToolBox spike — live steps (S1, S6)

The harness and the spike both use port 47321; stop one before starting the other.

1. Close XrmToolBox. Copy **only** `XtbHostSpike/bin/Debug/net48/AgentCompanionMcp.XtbHostSpike.dll` into `%APPDATA%\MscrmTools\XrmToolBox\Plugins`. No other DLL — `Newtonsoft.Json` must come from XrmToolBox.
2. Start XrmToolBox and open **Agent Companion MCP (spike)** without a connection. The log shows the host starting.
3. Connect the tab to the sandbox. The log shows `UpdateConnection`, the connection ID (or `null`) and `Bound as …`.
4. Open a second spike tab and connect it to a **different** connection, if one is available. Both appear in `list_connections`; `hello` shows one process, one AppDomain and one `hostLoadedUtc`.
5. Open a third tab on the **same** connection as the first. Expect the *Connection already active* dialog; try both buttons.
6. From the toolbar, connect while two spike tabs are open and apply the connection to both. Expect the first to bind and the second to show the dialog.
7. Switch to a different tab (another tool), then call `who_am_i` from a client. The approval dialog must still appear; the tab log records the dialog owner and whether the tab was visible.
8. Close the tabs in any order. The host stops when the last spike tab closes; `list_connections` drops each closed tab.
9. Remove the DLL from the `Plugins` folder when done.

## Client registration (S4)

Register against the harness or the spike, then remove when done:

```powershell
claude mcp add --transport http agent-companion-spike http://127.0.0.1:47321/mcp --header "Authorization: Bearer <token>"
claude mcp remove agent-companion-spike
```

VS Code (`.vscode/mcp.json` outside this repository, or the user profile):

```json
{
  "inputs": [{ "type": "promptString", "id": "companion-token", "description": "Agent Companion spike token", "password": true }],
  "servers": {
    "agent-companion-spike": {
      "type": "http",
      "url": "http://127.0.0.1:47321/mcp",
      "headers": { "Authorization": "Bearer ${input:companion-token}" }
    }
  }
}
```
