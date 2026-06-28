# AI-Augmented D365 CE Delivery — Activity × Tooling Matrix

> Scope of this draft: columns **[1] General-Purpose AI Assistants** and **[3] Coding Assistants & Agentic Environments** from the [AI Tool Taxonomy](README.md). Columns 2, 4, 5, 6 are stubbed at the end for a later pass.
> Status: v0.1 — starting point for iteration.

## How to read this

- **Rows** are the activities a D365 CE practitioner moves through: two one-time setups, then the iterative loop of spec → plan → task → implement → validate → document.
- **Columns** are *where the AI runs*, per the taxonomy — not which model. The same model (Claude, a GPT) shows up in both columns through different surfaces, and the surface is what determines what it can see and act on.
- **Cells** name the concrete tool/workflow to reach for, assuming you have that column's tooling available. Recurring building blocks (Context Exporter, instruction files, unpacked solutions, Dataverse MCP) are described once in [Backbone](#the-backbone-shared-across-cells) so the cells stay short.

Two constraints shape every cell:

1. **Client data must not leave the client tenant.** This is the dividing line between the two columns more than capability is. Pasting environment detail into Claude.ai / ChatGPT crosses a boundary; doing the same work through M365 Copilot (in-tenant) or the **Dataverse MCP server** (data stays in the tenant, the agent only calls governed tools) does not. The matrix flags where this matters.
2. **Human-in-the-loop at every decision point.** AI drafts; a person decides. The cells assume review gates, not autopilot.

---

## The matrix

| Activity | [1] General-Purpose AI Assistants <br><sub>Claude.ai · ChatGPT · M365 Copilot (chat) · Gemini</sub> | [3] Coding Assistants & Agentic Environments <br><sub>Copilot agent mode / coding agent · Cursor · Claude Code · VS + Copilot</sub> |
|---|---|---|
| **Initial one-time setup** <br><sub>per person / per team</sub> | Stand up a reusable **"D365 CE house style" Project / Custom GPT** seeded with conventions (publisher prefix rules, StyleCop summary, user-story + AC templates, the tool taxonomy). Enable M365 Copilot. Curate a personal prompt library. | Baseline the dev box: VS Code + Power Platform Tools + Copilot/Cursor, **pac CLI**, XrmToolBox + plugins (**D365 Context Exporter**, FetchXML Builder). Build a **template repo** carrying canonical `.github/copilot-instructions.md`, `AGENTS.md`, `.github/agents/*.agent.md`, `.github/skills/*`. Register Dataverse MCP + ADO/GitHub MCP. Fix the solution-unpack git layout. |
| **Project setup** <br><sub>per client / per engagement</sub> | Per-client **grounded Project** seeded with Context Exporter `.context.md` packs + SoW + architecture; pin client data-handling rules. Use **M365 Copilot scoped to the client tenant** for anything touching live data. | Clone repo; `pac solution unpack` into git; drop Context Exporter packs into `/context/`; fork the template instruction/agent files and fill client specifics (prefix, env URLs, naming); enable this environment's **Dataverse MCP** + ADO board MCP; run Copilot `/init` to bootstrap instructions from the actual repo. |
| **Specification** | Draft requirements, user stories, ACs from stakeholder notes. **M365 Copilot** mines Teams/email/SharePoint for source material. Reconcile asks against the *existing* model (Context Exporter grounding) to catch "we already have a field for that." | Coding assistant + repo + Context Exporter + **ADO MCP** + **Dataverse MCP** (`describe`, `search`) → grounded gap analysis and a technical spec that names *real* entities/attributes/plugins. Feasibility checked against the unpacked solution, not from memory. |
| **Planning** | Decompose epics → features, sequence, risk register, estimation rationale; produce the planning doc/deck. | Generate the **ADO work-item hierarchy via ADO MCP**; dependency + sequencing analysis from solution structure (and from **Plugin B — Import Dependency Pre-Flight**); draft ADRs (architecture skill). |
| **Tasking** | Turn features into task checklists with crisp descriptions and a clear Definition of Done. | Create ADO tasks via MCP; scaffold branches; **Plugin 9 — PCF Scaffold Orchestrator** for PCF tasks; emit task-level technical notes grounded in the repo. |
| **Implementation** | Generate/explain Power Fx, JS web resources, FetchXML, OData. Rubber-duck design. Use **maker-portal copilots** (Copilot in Power Apps / Power Automate, generative pages) for low-code build with HITL review. *Cannot touch the repo.* | The core build: plugins, PCF, web resources, Azure Functions, pipeline YAML, and **unit-test fixtures from the FetchXML Test Fixture Manager**. Agent mode for synchronous multi-file edits; coding agent for async PRs. pac CLI / **Dataverse coding-agent plugin** for environment gestures. |
| **Validation / peer review** | Review design docs; generate test scenarios + UAT scripts; sanity-check an approach; cross-check work against the requirements doc (M365 Copilot). | Code review via a **`d365-code-reviewer.agent.md`** persona enforcing StyleCop + naming; PR descriptions + self-review; run tests; **Plugin A — Component Snapshot & Drift Tracker** for config drift; security/perf review; ADO/GitHub PR Copilot. |
| **Documentation** | User docs, runbooks, release notes, training material; publish to SharePoint via M365 Copilot. | Generate technical docs + ADRs from code; **re-run Context Exporter** so the schema packs stay current as living docs; keep `copilot-instructions.md` / `AGENTS.md` honest (`/init` refresh). |

---

## The backbone (shared across cells)

These five things recur in most cells. Building or standardizing them once is most of the leverage.

### 1. D365 Context Exporter — the grounding layer for Column 1

The exporter solves the column-1 problem directly: general-purpose assistants can't browse your environment, so you hand them a structured `.context.md` snapshot (entities, attributes, security model, solution inventory). It is the right tool precisely when the person *doesn't* have agentic tooling or MCP access — which is most of the team on M365-Copilot-only licensing.

Position it against the Dataverse MCP server deliberately:

- **Context Exporter** = a *metadata snapshot* you upload. Cheap, offline, reviewable, works with any assistant including ones with no connectors. Carries the `LEGAL.md` boundary notice. Best for column 1 and for seeding a Project.
- **Dataverse MCP** = *live, governed tool access* where data stays in the tenant. Best for column 3 and for anything needing current rows, not just schema.

They're complementary, not competing — and the exporter remains the only option when the assistant can't reach the tenant at all.

### 2. The instruction / agent / skill file stack — the grounding layer for Column 3

Per the "context injection over custom agents" principle, this is now well-supported natively and stays file-based (no bespoke agent system to maintain):

- `.github/copilot-instructions.md` — always-on, project-wide. Keep it under ~1,000 words or the signal gets buried. Publisher prefix, no early binding, StyleCop, Repository pattern, "tables change fast → describe via MCP, don't assume."
- `AGENTS.md` — workflow rules: how to build, which test command, what "done" means. Nestable per subdirectory for a monorepo (plugins vs PCF vs web resources).
- `.github/agents/*.agent.md` — lightweight named personas (plugin-dev, code-reviewer, PCF) with a constrained tool list and an optional pinned model. These *are* "context injection," not a custom agent platform — exactly the line you want.
- `.github/skills/<name>/SKILL.md` — reusable task instructions auto-discovered by the agent.

Ship these from the template repo so every engagement starts consistent.

### 3. Solutions unpacked into git

`pac solution unpack` turns managed/unmanaged solutions into a diffable file tree. This is what makes column 3 *grounded* — the agent reads what actually exists (entities, forms, plugin steps, web resources) rather than guessing. It's also what makes drift detection (Plugin A) and import pre-flight (Plugin B) meaningful.

### 4. Dataverse MCP server

GA as of the Ignite-era release; reachable from Copilot Studio, GitHub Copilot (VS Code + CLI), Cursor, and Claude Code (via the `@microsoft/dataverse` local proxy or the remote `/api/mcp` endpoint with an Entra app). Exposes a defined tool surface — `describe`, `search` (metadata), `read_query`, `create_record`, `update_record`, file and prompt tools. Two caveats to put in the runbook: **admin must allow-list the client** per environment, and **tool calls from non-Copilot-Studio agents are billable** (covered if the user holds a D365 Premium or M365 Copilot license). The **Dataverse plugin for coding agents** (preview, open-source, on the Claude marketplace) bundles MCP + Dataverse CLI + Python SDK + PAC CLI behind one install — worth piloting for Claude Code / Copilot.

### 5. ADO / GitHub integration

Work items, boards, and repos reached through their MCP servers (or the coding agent's native GitHub access). This is what lets the spec/plan/task rows read and write the backlog instead of being a disconnected chat.

---

## Per-activity detail

### Initial one-time setup

**[1]** The deliverable is a *reusable* grounded surface, not a one-off chat. A Claude Project (or Custom GPT) carrying your conventions means every later spec/plan conversation inherits the house style without re-explaining it. Seed it with: naming + prefix rules, a StyleCop summary, your user-story/AC templates, and a one-paragraph statement of the data-boundary rule so the assistant itself reminds you when you're about to over-share.

**[3]** The deliverable is a **template repo**. Everything reusable across clients lives here: the instruction stack (§2), starter `.agent.md` personas, a `/context/` convention, and a documented MCP registration procedure. New projects fork this instead of reinventing it. Decide the solution-unpack layout now — it's expensive to change later.

> **Build candidate:** a small "house starter pack" — `copilot-instructions.md`, `AGENTS.md`, and 3 `.agent.md` personas — versioned and distributable, the column-3 analogue of the Context Exporter's reference config.

### Project setup

**[1]** One grounded Project per client, seeded with that client's Context Exporter packs + SoW + architecture. For anything involving *live* client data, route through **M365 Copilot in the client tenant** rather than a consumer assistant — same intelligence, no boundary crossing.

**[3]** Mechanical and scriptable: clone, `pac solution unpack`, drop in the Context Exporter packs, fork the instruction files, fill in client specifics, enable the environment's Dataverse MCP + ADO board, run `/init`. This is a strong candidate for a checklist skill so it's identical every time.

### Specification

**[1]** Two jobs: turn raw stakeholder input into structured stories/ACs, and *reconcile against what exists*. The second is where the Context Exporter pack earns its keep — the assistant can flag "you're asking for a field the OOTB contact already has" only if it can see the model. M365 Copilot adds requirement-mining from Teams/email/SharePoint.

**[3]** Same reconciliation, but live and deeper: `describe`/`search` over Dataverse MCP plus the unpacked solution means the gap analysis cites real schema, and the spec can name the actual plugins/steps a change will touch. ADO MCP pulls the related existing work items so the spec doesn't duplicate the backlog.

### Planning

**[1]** Decomposition, sequencing, risk, estimation rationale — the assistant is a structured-thinking partner, output is a planning artifact a human owns.

**[3]** Turn the plan into a real ADO work-item tree via MCP. The differentiator is *dependency-aware* sequencing grounded in solution structure — and **Plugin B (Import Dependency Pre-Flight)** is the purpose-built input here: an order-sensitive, read-only check of the import sequence feeds directly into how features get ordered.

### Tasking

**[1]** Feature → task checklists with explicit DoD. Low-tech, high-value.

**[3]** Create the tasks, scaffold the branches, and for specific task types reach for the purpose-built tool: **Plugin 9 (PCF Scaffold Orchestrator)** for PCF tasks (field-bound vs dataset-bound are separate code paths — the orchestrator picks correctly). Task notes are generated grounded in the repo so they reference real files.

### Implementation

**[1]** Snippet-level help (Power Fx, JS, FetchXML, OData) and design rubber-ducking, plus the **maker-portal copilots** — Copilot in Power Apps / Power Automate, generative pages, row summaries — for genuine low-code build. These produce config inside the platform with HITL review; they can't see or change your repo, so they sit firmly in column-1 territory.

**[3]** The heavy lifting. Note the split that landed in 2026 Copilot:
- **Agent mode** — synchronous, in-IDE, multi-file edits with you watching. Use for plugins, PCF, refactors.
- **Coding agent** — asynchronous, cloud, returns a PR. Use for well-specified, self-contained tasks you can review later. Reads `copilot-instructions.md` + `AGENTS.md` before starting; budget premium requests (a complex task can burn 10–30).

The **FetchXML Test Fixture Manager** plugs in exactly here: it generates SDK-compatible JSON fixtures from live FetchXML so the agent can write unit tests that mock `IOrganizationService` without a live connection — closing the loop between "agent wrote a plugin" and "agent wrote a test that runs offline in CI."

### Validation / peer review

**[1]** Design-doc review, UAT-script and test-scenario generation, and cross-checking an implementation narrative against the requirements doc. Good for catching "this doesn't actually satisfy AC #3."

**[3]** A **`d365-code-reviewer.agent.md`** persona with a tight tool list and explicit rules (StyleCop, prefixes, no early binding, fixture coverage) gives consistent, conventions-aware review — better than ad-hoc prompting. Pair it with **Plugin A (Component Snapshot & Drift Tracker)** to catch the config-level drift that code review can't see (someone changed a form in the sandbox). PR descriptions and self-review come from the coding agent natively.

### Documentation

**[1]** User-facing docs, runbooks, release notes, training. M365 Copilot publishes straight to SharePoint.

**[3]** Technical docs and ADRs generated from code, and — the D365-specific move — **re-running the Context Exporter** so the schema packs are regenerated as living documentation. The same artifact that grounds column 1 *is* your data-model doc. Keep the instruction files current in the same pass.

---

## Build backlog (gaps worth filling)

Ordered roughly by leverage. Several already exist in your plugin roadmap; this maps them to the matrix.

| Build | Feeds rows | Notes |
|---|---|---|
| **House starter pack** (`copilot-instructions.md` + `AGENTS.md` + `.agent.md` personas) | Setup, all of col 3 | Column-3 analogue of the exporter's reference config. Distributable, versioned. |
| **`d365-project-setup` skill** | Project setup | Codifies the clone → unpack → ground → wire-MCP checklist so it's identical every time. |
| **`d365-gap-analysis` skill** | Specification | Chains a Context Exporter pack + ADO MCP + Dataverse `describe` into a structured "new vs existing" report. |
| **Plugin A — Component Snapshot & Drift Tracker** | Validation, Documentation | Already specced (MVP, no git in MVP). The review row needs it. |
| **Plugin B — Import Dependency Pre-Flight** | Planning, Validation | Already specced (read-only, order-sensitive). Sequencing input. |
| **Plugin 9 — PCF Scaffold Orchestrator** | Tasking, Implementation | Already specced. PAC CLI orchestration with field/dataset split. |
| **FetchXML Test Fixture Manager** | Implementation, Validation | Phase 1 done. The offline-test enabler for the coding agent. |
| **Context Exporter CI refresh** | Documentation | Headless run that commits regenerated packs — your "update grounding" future enhancement, now load-bearing. |

---

## Roadmap watch (will move cells; dated and sourced)

The platform side is shifting fast enough that several cells should be re-checked each release wave.

- **Dataverse MCP server — GA, evolving tool surface.** Reframes how *both* columns reach Dataverse, and materially helps the data-boundary constraint (data stays in-tenant, accessed via allow-listed tools). Tool names changed recently (`describe`, `search_data` vs metadata `search`) — keep allow/deny lists in sync. Billing applies to non-Copilot-Studio agents. [Learn: Connect to Dataverse with MCP](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/data-platform-mcp) · [Tool-shape update, Jun 2026](https://www.microsoft.com/en-us/power-platform/blog/2026/06/08/dataverse-mcp-server-understanding-the-new-tool-shape/)
- **Dataverse plugin for coding agents (preview) + Business skills (preview).** One-install Dataverse fluency for Claude Code / Copilot (MCP + Dataverse CLI + Python SDK + PAC CLI); business skills are NL process instructions any MCP agent discovers automatically. Watch for GA. [Power Platform blog, May 2026](https://www.microsoft.com/en-us/power-platform/blog/2026/05/05/dataverse-agent-data-platform/)
- **GitHub Copilot agent model (2026).** Agent mode vs coding agent vs `.github/agents/*.agent.md` custom agents; Agent Skills (`SKILL.md`) standardized; org-level MCP allow-listing. This is the column-3 substrate — its conventions are what the backbone §2 stack targets. [VS Code custom instructions](https://code.visualstudio.com/docs/agent-customization/custom-instructions) · [GitHub: custom agents](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents)
- **Power Platform 2026 wave 1 (Apr–Sep 2026).** Native **GitHub integration + "deploy from Git"** maturing ALM with audit trails (column-3 relevant); generative pages and row summaries (column-1/maker relevant); **Copilot credit PAYG caps** (cost governance); Copilot Studio multi-agent + MCP-compliant tools. [Wave 1 overview](https://learn.microsoft.com/en-us/power-platform/release-plan/2026wave1/)

---

## Not yet covered (next columns)

Stubs for the pass that adds the rest of the taxonomy:

- **[2] Local / Desktop Automation Agents** — make.powerapps.com / PPAC navigation, XrmToolBox output extraction, repetitive maker-portal config.
- **[4] Platform-Embedded Copilots** — already partly surfaced under col-1 implementation; deserves its own column (Copilot in apps, row summaries, Power Fx generation).
- **[5] Agent Builder Frameworks** — Copilot Studio agents over Dataverse, multi-agent orchestration, Dataverse-event-triggered automation.
- **[6] AI-Augmented Specialty Tools** — XrmToolBox AI plugins (incl. your own), ADO/GitHub PR Copilot, FetchXML authoring assists.

## Open questions

1. Where exactly is the line on uploading Context Exporter packs to consumer assistants for a given client's contract? This should be a per-engagement decision recorded in project setup, not a default.
2. Does the team's M365-Copilot-only licensing cover Dataverse MCP tool billing for the cases where col-3 work needs it? (It may, via the M365 Copilot license — worth confirming before relying on it.)
3. Which `.agent.md` personas are worth standardizing first — plugin-dev and code-reviewer seem highest-value.
