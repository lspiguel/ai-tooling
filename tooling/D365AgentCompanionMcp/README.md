# D365 Agent Companion MCP

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that gives a local coding agent (GitHub Copilot, Cursor, Claude Code) a small set of person-approved Dataverse operations over the Model Context Protocol (MCP), so the agent can discover components and shape work-in-progress solutions for Microsoft Dynamics 365 Customer Engagement.

## What it does

Here's the gap I kept running into: a coding agent working on an unpacked solution can see everything that is already in a solution — it reads the export and edits it like any other source. What it cannot see is everything that isn't. An out-of-box form nobody has customized yet, the view a story needs to change, the ribbon of a table that was never exported: their IDs and current definitions live only in the environment. PAC CLI can export, pack and import, but it cannot create an empty solution, and it cannot add a table without dragging in every subcomponent.

D365 Agent Companion MCP is a free XrmToolBox plugin that closes that gap. It opens a local MCP endpoint over your XrmToolBox connections. Through it, the agent can list publishers and solutions, describe tables, relationships, choices, forms, views and ribbons, run FetchXML over configuration tables, create an empty unmanaged solution, and add existing components to it — **always without required components and without subcomponents**. From there PAC CLI exports the solution, the agent edits it locally, and PAC CLI imports it back. The platform's import is the final validation.

Nothing happens behind your back. Each plugin tab binds one connection, and every call the agent makes to it shows up in that tab for you to approve or deny, with the agent's stated reason next to the arguments. Every call is written to the tab's log. Writes refuse to run on a connection you have not confirmed as development, and publisher creation is off unless you switch it on.

It comes with a set of agent skills, the `agent-companion-*` skills, that carry the delivery method: segmented WIP solutions per story, an overwrite check before every import, and close-out into transport solutions — whether the project is just starting, in the middle of delivery, or in support.

I'd love to hear from others in the D365 / Power Platform space working with coding agents — which operations would you want an agent to have, and which ones should always stay with a person?

👉 Search for "D365AgentCompanionMcp" on the XrmToolBox Tool Library.<br>
👉 https://github.com/lspiguel/ai-tooling

#Dynamics365 #Dataverse #XrmToolBox #AIAgents #MCP #PowerPlatform #OpenSource

<!-- TODO: screenshot — a tab with an approval dialog and the call log -->

## Prerequisites

| Requirement | Version |
|---|---|
| XrmToolBox | 1.2025.x or later |
| .NET Framework | 4.8 |
| A local coding agent with MCP over HTTP | Claude Code 2.1.72 or later; VS Code 1.144.0 or later (GitHub Copilot agent mode). Cursor is expected to work through the same protocol fallback as VS Code; not yet tested |
| Power Platform CLI (PAC) | 2.14.1 or later, with an authenticated profile (`pac auth create`) for each environment you will use |
| A development environment | The plugin shapes unmanaged solutions; never point it at production |

## Quickstart

The plugin is the approval window and the endpoint; the agent does the work, using PAC CLI and the agent-companion skills. Setup is mostly one-off.

1. **Install the plugin** — open XrmToolBox, go to _Tool Library_, search for _D365 Agent Companion MCP_, and install.
2. **Open it and follow the setup wizard** — the first time, the plugin opens a short wizard. Its *Preconditions* page has a button for each step below that copies a ready-made prompt for your agent, and an **Export skills…** button that writes the agent-companion skills as zip files.
3. **Install PAC CLI and sign in** — `pac auth create --environment <url>` for each environment; check with `pac org who`.
4. **Install the skills** — unpack the exported zips into your agent's skills location (for Claude Code, `~/.claude/skills/` or the project's `.claude/skills/`).
5. **Register the endpoint** — **Copy client config** on the main panel copies the URL and bearer token. See [Registering the endpoint](#registering-the-endpoint) for each client.
6. **Connect the tab** — connect the plugin tab to your development environment, then confirm in the tab that it *is* a development environment. Write calls refuse to run until you do.
7. **Run the test** — the wizard's last page copies a test prompt: the agent checks the skills, PAC and the connection, then calls `list_solutions`. Approve the call in the tab and watch it appear in the log.
8. **Work** — ask the agent for the change you want, with its work item number. It establishes how your project delivers, states its steps for you to confirm, and asks you in the plugin before every call.

## Plugin upgrades

When the plugin opens after an update, it shows a *Plugin updated* page once. The skills and the MCP client configuration may have changed with the new version, so the page offers to reinstall them through your agent — **Export skills…** for the new skill zips, and prompts that ask the agent to replace its agent-companion skills (and only those) and to check its endpoint registration — plus the test prompt. Reinstalling is optional: **Skip** goes straight to the main panel, and the page does not come back until the next update.

An older settings file is upgraded when the plugin loads it. Read grants you gave per environment or globally are kept and gain their environment and tenant IDs the next time that environment is bound; the *always* development-environment confirmation is withdrawn, so confirm each development environment again. The token, port and limits are never touched. Downgrading drops the grants an older plugin does not know, and you approve again.

## Registering the endpoint

The endpoint is `http://127.0.0.1:47321/mcp` (the port can be changed on the main panel) and every request carries the bearer token, shown masked in the **Active Approvals** window. The token is kept across XrmToolBox restarts; **Regenerate** replaces it for every client. **Copy client config** on the main panel copies the URL and token for setup.

Claude Code:

```powershell
claude mcp add --transport http d365-agent-companion http://127.0.0.1:47321/mcp --header "Authorization: Bearer <token>"
```

VS Code (`.vscode/mcp.json` outside any client repository, or your user profile):

```json
{
  "inputs": [{ "type": "promptString", "id": "companion-token", "description": "Agent Companion MCP token", "password": true }],
  "servers": {
    "d365-agent-companion": {
      "type": "http",
      "url": "http://127.0.0.1:47321/mcp",
      "headers": { "Authorization": "Bearer ${input:companion-token}" }
    }
  }
}
```

> **If your client shows an OAuth or sign-in prompt,** the token it is sending is missing or out of date. MCP clients treat a `401` as the start of an OAuth flow; this plugin does not use OAuth. Cancel any client registration the prompt offers, copy the token from the plugin again and update the client — in VS Code, run **MCP: Reset Cached Inputs**, then restart the server.

## How it works

| Step | Who | How |
|---|---|---|
| Choose the environment | Agent, person | `list_connections`; the person says which when more than one tab is bound. The agent checks that `pac org who` targets the same organization |
| Find what the change touches | Agent, through the plugin | Metadata, ribbon and FetchXML tools |
| Create the WIP solution and add components | Agent, through the plugin | `create_solution`, `add_solution_components` — then a read-back of the solution, every time |
| Export, unpack, edit, validate | Agent, with PAC CLI and the skills' scripts | XML layout of `pac solution unpack`; form, ribbon and schema checks offline |
| Overwrite check, then import | Agent, with PAC CLI | Component versions and a re-export compared with the baseline; `pac solution import` |
| Check the result | Person | In the app — an import's success is not the change's success |
| Close out | Agent, person | Depends on the project's strategy — see below |

**One tab per connection.** Each tab binds one XrmToolBox connection, and every call names the connection it is for — there is no default. A second tab on the same environment is refused, so the approvals and the log for an environment are always in one place. One MCP endpoint serves every tab in the XrmToolBox process.

**Approvals.** A project usually has one repository and many environments, often in one tenant, so an approval reaches as far as you choose: **once**, **session only**, **per environment**, **per tenant** or **global**. Reads, record reads (a `query` that touches records) and writes are separate grants: one never covers another. Write approvals and the development-environment confirmation stop at one environment, because only development is written. Whatever a grant covers, every Write call still needs a reason, the development-environment confirmation and a log entry, and publisher creation is typed in every time. Per tenant and global carry a warning, because they also cover environments bound later. A denial or a timeout reaches the agent as a normal result, and it does not retry without new instructions. The call log keeps one line for each call and one for its outcome; **Save as…** writes it to a text file.

**Active Approvals.** One window, opened from any tab, lists every grant and File root across all environments and tenants as one tree — Global first, then each tenant with its environments, then environments that report no tenant. Each row has **Revoke**; each heading has **Reset**, which asks inside the window before it revokes. A grant whose environment no longer matches (a copied or restored environment) is marked *Stale* and not applied. The bearer token is shown here, masked, with **Copy** and **Regenerate**.

**File root.** Tools that take files — `update_web_resources` — read them by path under the File root: the root folder of the git repository that holds the source files the agent edits, set for one environment or for every environment in a tenant. The first call that needs it asks you to choose; a relative path is read from it, an absolute path must lie inside it, and links are refused. It is shown and changed in Active Approvals.

## WIP solutions

A work-in-progress (WIP) solution holds exactly the components a change touches, so they can be exported, edited and imported without carrying anything else.

**Segmented by transport solution.** Projects deliver through a fixed, ordered set of long-lived *transport solutions* — typically Resources, Base, Assemblies, Processes, Apps and Security — each holding one group of component types, and each depending only on those before it. A WIP feeds exactly one of them, so a story that touches a web resource and a form gets two WIPs, Resources and Base, created, imported and closed out in that order. The project's *segmentation map* says which group each component type belongs to; the agent applies it, the plugin does not.

**Named for the person and the work item:** `<initials>_Agent_WIP_<work item id>_<Group>` — for example `XY_Agent_WIP_1234_Base`.

**Placement and in place.** Each component is routed on its own:

| | Placement | In place |
|---|---|---|
| The component | Has no transport home yet — new, or out-of-box and touched for the first time | Is already in a transport solution — for example a form on a table already in production |
| The WIP | Carries the edit and, at close-out, places the component in its transport solution | Carries the edit only |
| Ends | At close-out | As soon as the edit has reached the place transport is extracted from |

The two mix freely in one project and one story: a fix to a production form can use a short-lived in-place WIP while a new column for the same story waits for close-out.

**What a WIP never does.** It is never deployed downstream, and never includes required components or subcomponents — that is how solution bloat and layers that cannot be removed start. A transport solution itself is never the edit vehicle.

## Close-out strategies

How a WIP's work reaches the transport solution depends on where that transport solution is extracted from — so the strategy is decided per WIP, and a project can use both:

| | A — Promote to a build environment | B — Add to transport solutions in development |
|---|---|---|
| What happens | A promotion pipeline imports the WIP into a build environment, copies its components into the transport solution there, and deletes it there | The WIP's components are added to the transport solution in development |
| Transport extracted from | The build environment | Development |
| Isolation from other in-flight work | Yes, apart from shared whole documents such as forms | No — edits to components already in a transport solution ship with the next extract |
| The agent | Prepares each promotion run for you, then checks the result through a read-only tab on the build environment | Adds the components, then reads the transport solution back |

Git-linked solutions and Power Platform pipelines are recorded in the specification but not supported by the skills yet.

## Close-out

Close-out ends a story's WIPs:

- **In-place WIPs** end once their edit has reached the transport source — at import and your check under strategy B, after promotion under A — after a last read-back confirms that every component in them already has a transport home. A component authored during the edit, such as a new column, turns the WIP into a placement WIP instead.
- **Placement WIPs** close out together, in transport order: the agent checks that nothing depends on a later group or on something not yet placed, adds the components to the transport solution (B) or prepares the promotions (A), and verifies the result.
- **Then the WIP is deleted** with `pac solution delete`, which keeps its components.

Deleting a component, and moving one between transport solutions, are never part of close-out: those are planned by a person. Pipeline runs, commits and pushes are always yours, unless you hand a specific one to the agent.

## Agent skills

| Skill | Use it for |
|---|---|
| `agent-companion-context` | How the project delivers — connection, segmentation map, transport solutions and their sources, strategy, pipelines — derived first, asked with a recommendation when it cannot be derived; the steps you confirm; the shared WIP ledger |
| `agent-companion-transport-bootstrap` | Transport solutions the work needs that do not exist yet; a first-party app newly installed |
| `agent-companion-wip-shaping` | Classifying and routing components, naming and creating WIPs, adding and reading back, overlap and dependency checks |
| `agent-companion-wip-edit-loop` | Export, baseline, local edits, validation, overwrite check, import |
| `agent-companion-wip-close-out` | Ending WIPs by route and strategy |

Any of them can be active at once; each owns its own decisions, so they never give the agent two instructions for the same step. Other skills you already use — for example one for editing cloud flows — keep their own procedures. The sources are in [skills/](../../skills/).

## Tools

| Tool | Tier | Purpose |
|---|---|---|
| `list_connections` | Host | The connections bound to plugin tabs |
| `get_connection_info`, `who_am_i` | Read | The organization and settings; the user, for WIP names |
| `list_publishers`, `list_solutions`, `list_solution_components` | Read | Publishers, solutions and what they hold |
| `list_tables`, `get_table_metadata`, `get_relationships`, `get_option_sets` | Read | Table, column, relationship and choice metadata |
| `get_entity_ribbon`, `get_application_ribbon` | Read | The full classic ribbon, not just the diff an export holds |
| `query` | Read | FetchXML, paged; flags any query that touches records |
| `get_component_state`, `get_required_components` | Read | Versions for the overwrite check; dependencies against the transport order |
| `create_solution`, `add_solution_components`, `remove_solution_component` | Write | Shape a WIP solution — including already-registered plug-in assemblies, steps, custom APIs and their parameters and properties |
| `create_publisher` | Publisher | Off unless switched on; confirmed by typing the prefix |
| `update_web_resources` | Write | Create or update web resources from local files in a WIP, with the overwrite check inside the tool and a targeted publish |

## Settings

| Setting | Scope | Default |
|---|---|---|
| Port | Host | 47321 |
| Bearer token | Host | Generated on first use; **Regenerate** to replace |
| Response cap | Host | 40,000 characters per page; larger results are paged, never truncated or written to a file |
| Maximum page size | Host | 500 items |
| Approval timeout | Host | 120 seconds; a call not approved in time is denied |
| Development environment confirmed | Connection | Off — confirm once, for the session, or for the environment; never wider |
| Approvals (reads, record reads, writes) | Environment, tenant or global | Every call asks; grants are given in the dialogs and managed in Active Approvals |
| File root | Environment or tenant | Not set — asked for on the first call that takes files |
| Publisher creation | Connection | Off |

Host settings apply to every tab; changing the port or the token affects every client configuration.

## Building from source

```bash
dotnet restore tooling/D365AgentCompanionMcp/D365AgentCompanionMcp.sln
dotnet build   tooling/D365AgentCompanionMcp/D365AgentCompanionMcp.sln --configuration Release
dotnet test    tooling/D365AgentCompanionMcp/D365AgentCompanionMcp.Tests/D365AgentCompanionMcp.Tests.csproj
```

`D365AgentCompanionMcp.ConsoleHarness` hosts the MCP layer outside XrmToolBox; [Invoke-ProtocolChecks.ps1](D365AgentCompanionMcp.ConsoleHarness/Invoke-ProtocolChecks.ps1) runs the protocol checks against it. The skills' scripts have their own self-tests under each skill's `scripts/tests/`.

For local development, copy `D365AgentCompanionMcp.dll` to your XrmToolBox Plugins folder. The plugin ships no other DLL: `Newtonsoft.Json` is XrmToolBox's own copy, so do not add package references that place a DLL beside it.

The agent-companion skills are embedded in the DLL at build time from [skills/](../../skills/), and the setup wizard's prompts are text resources in [Setup/Prompts/](D365AgentCompanionMcp/Setup/Prompts/). Rebuild after changing either. To see the wizard again, remove `SetupCompletedVersion` from the plugin's settings file in XrmToolBox's settings folder; set it to another version to see the *Plugin updated* page.

The behaviour of the plugin and the skills is specified in [SPEC.md](SPEC.md); progress and test findings are in [notes.md](notes.md).

## License

MIT — see [LICENSE](../../LICENSE) for details.
