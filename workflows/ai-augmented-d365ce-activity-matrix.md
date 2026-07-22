# AI-Augmented D365 CE Delivery — Activity × Tooling Matrix

## How to read this

- **Rows** are the activities a D365 CE practitioner moves through: a grounding layer, two one-time setups, then the iterative loop of specification/intent → planning/design → tasking → implementation/build → validation/peer review → documentation/information sharing.
- **Columns** are *where the AI runs*, per the taxonomy — not which model. The same activity leverages AI differently depending on the type/taxonomy of assistant: the same model (Claude, a GPT) shows up in both columns through different surfaces, and the surface is what determines what it can see and act on.
- **Cells** name the concrete tool/workflow to reach for, assuming you have that column's tooling available. Each column grounds its assistants through different methods (instructions, boards, context packs, solutions unpacked & code repositories, Dataverse MCP), described once in [The grounding layer](#a-the-grounding-layer) so the cells stay short.

---

## The matrix

| Activity | [1] General-Purpose AI Assistants <br><sub>Claude.ai · ChatGPT · M365 Copilot (Premium) · Gemini</sub> | [3] Coding Assistants & Agentic Environments <br><sub>Copilot agent mode / coding agent · Cursor · Claude Code · VS + Copilot</sub> |
|---|---|---|
| **[A] Grounding layer** <br><sub>every cell below depends on these</sub> | **Instructions** — conventions, tooling and templates carried in Claude Projects / Custom GPTs / M365 Copilot Notebooks. **Context packs** — markdown snapshots of entities, security model, solutions: the current extracted state of Dataverse, uploaded to the assistant. | **Boards** — ADO, Jira and other work-item tools, reached via CLIs and MCPs. **Solutions unpacked & code repositories** — thorough solution state and source code (ALM, `pac solution unpack`, git). **Dataverse MCP** — live, governed tool access: describe, search, query. |
| **[B] Initial one-time setup** <br><sub>per person / per team</sub> | **User-driven tooling.** General tools and XrmToolBox; wikis and SharePoint sites for conventions & templates; a personal prompt library. Stand up a reusable **"D365 CE house style" Project / Custom GPT** seeded with conventions (publisher prefix rules, StyleCop summary, user-story + AC templates, the tool taxonomy). Enable an AI Assistant. [[B.1]](./1-general-purpose-assistants/b.1-initial-setup.md) | **Agent-driven tooling.** Coding tools, CLIs, IDEs, XrmToolBox: VS Code + Power Platform Tools + Copilot/Cursor, **pac CLI**, XrmToolBox plugins (**D365 Context Exporter**, FetchXML Builder). Build a **template repo** carrying the canonical repo layout and automation scripts. Register Dataverse MCP; connect the **Azure DevOps CLI** (`az boards`) for ADO. Fix the solution-unpack git layout. [[B.3]](./3-coding-assistants/b.3-initial-setup.md) |
| **[C] Project setup** <br><sub>per client / per engagement</sub> | **Engagement reference.** XrmToolBox connection setup; per-client **grounded Project** seeded with Context Exporter `.context.md` packs + SoW + architecture; pin client data-handling rules. Use an **AI Assistant scoped to the client tenant** for anything touching live data. [[C.1]](./1-general-purpose-assistants/c.1-project-setup.md) | **ALM and/or repository wiring.** Setup ALM solution if available, if not setup `pac solution export`/`pac solution unpack` script to automate solution registration into git; clone repos; drop Context Exporter packs into `/context/`; connect CLIs (Azure DevOps CLI for ADO) and enable this environment's **Dataverse MCP**. [[C.3]](./3-coding-assistants/c.3-project-setup.md) |
| **[1] Specification / Intent** | **Write user stories & ACs.** Epics → features → user stories, drafted from stakeholder notes; the **AI Assistant** mines Teams/email/SharePoint for source material. Grounding context (Context Exporter packs) aligns the draft to the current solution state — catches "we already have a field for that." [[1.1]](./1-general-purpose-assistants/1.1-specification.md) | **Write user stories & ACs.** Same epic → feature → user story breakdown, concentrating on providing **intent and acceptance criteria**; use **Ask Mode** to explore options. Coding assistant + repo + Context Exporter + **Azure DevOps CLI** + **Dataverse MCP** (`describe`, `search`) → grounded gap analysis and a spec that names *real* entities/attributes/plugins. [[1.3]](./3-coding-assistants/1.3-specification.md) |
| **[2] Planning / Design** | **Design collaboration sessions.** Architect & design an implementation fulfilling the requirements — sequencing, dependencies, risk register — with the assistant as a structured-thinking partner; the design artifact is owned by a human. [[2.1]](./1-general-purpose-assistants/2.1-planning.md) | **Plan Mode.** Use Plan Mode to flesh out a detailed implementation plan grounded in the repo, then peer review and collaborate as needed; dependency + sequencing analysis from solution structure; repo-grounded estimation rationale; draft ADRs. [[2.3]](./3-coding-assistants/2.3-planning.md) |
| **[3] Tasking** | **Sprint planning.** Estimate effort, assign tasks, generate test plans; turn features into task checklists with crisp descriptions and a clear Definition of Done. [[3.1]](./1-general-purpose-assistants/3.1-tasking.md) | **Expand the plan.** Task out & prepare checklists, define unit test conditions; create ADO tasks via the **Azure DevOps CLI** (`az boards`); scaffold branches; emit task-level technical notes grounded in the repo. [[3.3]](./3-coding-assistants/3.3-tasking.md) |
| **[4] Implementation / Build** | **General coding help.** Prompt for code snippets, samples and step-by-step configuration instructions — Power Fx, JS web resources, FetchXML, OData. Rubber-duck design. Use **maker-portal copilots** (Copilot in Power Apps / Power Automate, generative pages) for low-code. Pro-Code direct implementation is out of current capabilities for **AI Assistants**. [[4.1]](./1-general-purpose-assistants/4.1-implementation.md) | **Agent Mode.** Automated implementation with a human in the loop — the core build: plugins, PCF, web resources, Azure Functions, pipeline YAML; step-by-step configuration instructions where skills and tooling are unavailable. pac CLI / **Dataverse coding-agent plugin** for environment gestures. [[4.3]](./3-coding-assistants/4.3-implementation.md) |
| **[5] Validation / Peer review** | **Test scenarios.** Test plans + UAT scripts; review design docs; peer review on pull requests; cross-check work against the ACs (AI Assistant). [[5.1]](./1-general-purpose-assistants/5.1-validation-peer-review.md) | **Review.** Use **Ask Mode with an adversary reviewer prompt** for automated review (after the human pass) — enforcing StyleCop + naming; check unit-test coverage; peer review on pull requests; test plans + UAT scripts; **Plugin A — Component Snapshot & Drift Tracker** for config drift; security/perf review; ADO/GitHub PR Copilot. [[5.3]](./3-coding-assistants/5.3-validation-peer-review.md) |
| **[6] Documentation / Information sharing** | **User-driven docs.** Runbooks, documentation & release notes written with AI assistance; training material; publish to ADO wikis; **re-run Context Exporter** so grounding stays true — the schema packs are the living data-model docs. [[6.1]](./1-general-purpose-assistants/6.1-documentation.md) | **Automated docs.** Automated documentation & wiki updates generated from code (technical docs, ADRs); runbook review. [[6.3]](./3-coding-assistants/6.3-documentation.md) |

---

## [A] The grounding layer

Every cell in the matrix depends on these. They are what turns a plausible answer into a correct one. The methods differ per column — building or standardizing them once is most of the leverage.

### [A.1] Instructions — Column 1

Conventions and tooling to use; templates for code, work items, and deliverables. Carried in the assistant's persistent surface: **Claude Projects / Custom GPT instructions and M365 Copilot Notebooks**, seeded once per person ([B.1]) and per client ([C.1]). This is a per-user surface, not project-committed — it lives in the assistant's own project/notebook configuration, not in a client's repository.

### [A.2] Boards

ADO, Jira, and other tools and sites that record work items, effort, progress, and overall work organization. Column 3 reaches them through their MCP servers and CLIs (or the coding agent's native GitHub access) — this is what lets the spec/plan/task rows read and write the backlog instead of being a disconnected chat. This playbook standardizes on the **Azure DevOps CLI** (`az boards`, via the `azure-devops` extension) for ADO specifically, rather than an ADO MCP server — no server to register or allow-list, and it reuses the `az login` session already set up for Dataverse/Azure work. Other board tools (Jira, GitHub Issues) may still be reached through their own MCP where available.

### [A.3] Context packs — the grounding method for Column 1

Markdown snapshots of entities, attributes, security model, and solutions — the current extracted state of Dataverse. The **D365 Context Exporter** solves the column-1 problem directly: general-purpose assistants can't browse your environment, so you hand them a structured `.context.md` snapshot. It is the right tool precisely when the person *doesn't* have agentic tooling or MCP access — which is most of the team on M365-Copilot-only licensing.

### [A.4] Solutions unpacked & code repositories — the grounding method for Column 3

Thorough solution state and source code: ALM (or `pac solution unpack`) turns managed/unmanaged solutions into a diffable file tree inside a git repository that also holds source code and Documentation as Code. This is what makes column 3 *grounded* — the agent reads what actually exists (entities, forms, plugin steps, web resources) rather than guessing.

### [A.5] Dataverse MCP server

*Live, governed tool access* where data stays in the tenant — directly used by coding assistants, and best for anything needing current rows, not just schema. Complementary to [A.3]/[A.4], not competing — and the exporter remains the only option when the assistant can't reach the tenant at all.

GA as of the Ignite-era release; reachable from Copilot Studio, GitHub Copilot (VS Code + CLI), Cursor, and Claude Code (via the `@microsoft/dataverse` local proxy or the remote `/api/mcp` endpoint with an Entra app). Exposes a defined tool surface — `describe`, `search` (metadata), `read_query`, `create_record`, `update_record`, file and prompt tools. Two caveats to put in the runbook: **admin must allow-list the client** per environment, and **tool calls from non-Copilot-Studio agents are billable** (covered if the user holds a D365 Premium or M365 Copilot license). The **Dataverse plugin for coding agents** (preview, open-source, on the Claude marketplace) bundles MCP + Dataverse CLI + Python SDK + PAC CLI behind one install — worth piloting for Claude Code / Copilot.

---

## Per-activity detail for General-Purpose AI Assistants (column #1)

### [B.1] Initial one-time setup

The deliverable is **user-driven tooling**: general tools and XrmToolBox on the machine, wikis and SharePoint sites for conventions & templates, a personal prompt library — and a *reusable* grounded surface, not a one-off chat. A Claude Project (or Custom GPT) carrying your conventions means every later spec/plan conversation inherits the house style without re-explaining it. Seed it with: naming + prefix rules, a StyleCop summary, your user-story/AC templates, and a one-paragraph statement of the data-boundary rule so the assistant itself reminds you when you're about to over-share.

### [C.1] Project setup

The deliverable is the **engagement reference**: XrmToolBox connections set up per client, and one grounded Project per client seeded with that client's Context Exporter packs + SoW + architecture. For anything involving *live* client data, route through an **AI Assistant scoped to the client tenant** rather than a consumer assistant — same intelligence, no boundary crossing.

### [1.1] Specification / Intent

Write user stories & ACs: turn raw stakeholder input into the epic → feature → user story hierarchy with testable acceptance criteria, and *reconcile against what exists*. The second job is where the Context Exporter pack earns its keep — the assistant can flag "you're asking for a field the OOTB contact already has" only if it can see the model; grounding context aligns the draft to the current solution state. The AI Assistant adds requirement-mining from Teams/email/SharePoint.

### [2.1] Planning / Design

**Design collaboration sessions**: architect & design an implementation fulfilling the requirements. The assistant is a structured-thinking partner for sequencing, dependencies, and risk; the output is a design artifact a human owns. (Decomposition happens in [1.1]; estimation and board writes happen in [3.1].)

### [3.1] Tasking

**Sprint planning**: estimate effort, assign tasks, generate test plans. Feature → task checklists with explicit DoD. Getting tasks into ADO happens by scripting (no native work-item integration in this column). Low-tech, high-value.

### [4.1] Implementation / Build

**General coding help**: snippet-level generation (Power Fx, JS, FetchXML, OData), samples and step-by-step configuration instructions, and design rubber-ducking, plus the **maker-portal copilots** — Copilot in Power Apps / Power Automate, generative pages, row summaries — for genuine low-code build. These produce config inside the platform with HITL review; they can't see or change your repo, so they sit firmly in column-1 territory.

### [5.1] Validation / Peer review

**Test scenarios**: test plans, UAT-script and test-scenario generation, design-doc review, peer review on pull requests, and cross-checking an implementation narrative against the requirements doc. Good for catching "this doesn't actually satisfy AC #3."

### [6.1] Documentation / Information sharing

**User-driven docs**: user-facing docs, runbooks, release notes, training, written with AI assistance; publish straight to ADO wikis. Re-export the context packs so grounding stays true — regenerating them is itself a documentation act.

---

## Per-activity detail for Coding Assistants & Agentic Environments (column #3)

### [B.3] Initial one-time setup

The deliverable is **agent-driven tooling** — coding tools, CLIs, IDEs, XrmToolBox — anchored by a **template repo**. Everything reusable across clients lives there: the standard repo layout, a `/context/` convention, and a documented MCP registration procedure. New projects fork this instead of reinventing it. Decide the solution-unpack layout now — it's expensive to change later. Per-user assistant configuration (instructions, personas, skills) is set up separately, in the developer's own tooling — not shipped as part of the client's repository.

### [C.3] Project setup

**ALM and/or repository wiring** — mechanical and scriptable: set up ALM-based solution sync if it's available, otherwise script `pac solution export`/`pac solution unpack`; clone the repo(s); drop in the Context Exporter packs; fill in client specifics; connect the CLIs and enable the environment's Dataverse MCP + ADO board. This is a strong candidate for a checklist skill so it's identical every time.

### [1.3] Specification / Intent

Write user stories & ACs — the same epic → feature → user story breakdown as [1.1], concentrating on providing **intent and acceptance criteria**; use **Ask Mode** to explore options. The reconciliation is live and deeper: `describe`/`search` over Dataverse MCP plus the unpacked solution means the gap analysis cites real schema, and the spec can name the actual plugins/steps a change will touch. The Azure DevOps CLI (`az boards`) pulls the related existing work items so the spec doesn't duplicate the backlog.

### [2.3] Planning / Design

Use **Plan Mode** to flesh out a detailed implementation plan grounded in the repo, then peer review and collaborate on it as needed. Dependency + sequencing analysis is derived from solution structure; estimation rationale cites the same real components and dependencies rather than category-only cost drivers; ADRs are drafted for the decisions the plan implies.

### [3.3] Tasking

**Expand the plan**: task out & prepare checklists, define unit test conditions. Create the tasks in ADO via MCP, scaffold the branches. Task notes are generated grounded in the repo so they reference real files.

### [4.3] Implementation / Build

**Agent Mode** — automated implementation with a human in the loop; step-by-step configuration instructions where skills and tooling are unavailable. Note the split that landed in 2026 Copilot:
- **Agent mode** — synchronous, in-IDE, multi-file edits with you watching. Use for plugins, PCF, refactors.
- **Coding agent** — asynchronous, cloud, returns a PR. Use for well-specified, self-contained tasks you can review later; budget premium requests (a complex task can burn 10–30).

### [5.3] Validation / Peer review

Use **Ask Mode with an adversary reviewer prompt** for automated review: explicit rules (StyleCop, prefixes, no early binding, fixture coverage) give consistent, conventions-aware review — better than ad-hoc prompting. Check unit-test coverage; peer review on pull requests; test plans + UAT scripts. Pair it with **Plugin A (Component Snapshot & Drift Tracker)** to catch the config-level drift that code review can't see (someone changed a form in the sandbox). PR descriptions and self-review come from the coding agent natively.

### [6.3] Documentation / Information sharing

**Automated docs**: technical docs and ADRs generated from code, automated wiki updates, and runbook review — published to ADO wikis alongside the column-1 material. The context-pack re-export is column 1's move ([6.1]).

---

## Build backlog (gaps worth filling)

Ordered roughly by leverage. Several already exist in your plugin roadmap; this maps them to the matrix.

| Build | Feeds rows | Notes |
|---|---|---|
| **`d365-project-setup` skill** | [C] Project setup | Codifies the clone → unpack → ground → wire-MCP checklist so it's identical every time. |
| **`d365-gap-analysis` skill** | [1] Specification | Chains a Context Exporter pack + an Azure DevOps CLI query + Dataverse `describe` into a structured "new vs existing" report. |
| **Plugin A — Component Snapshot & Drift Tracker** | [5] Validation, [6] Documentation | Already specced (MVP, no git in MVP). The review row needs it. |
| **FetchXML Test Fixture Manager** | [4] Implementation, [5] Validation | Phase 1 done. The offline-test enabler for the coding agent. |
| **Context Exporter CI refresh** | [6] Documentation | Headless run that commits regenerated packs — your "update grounding" future enhancement, now load-bearing. |

---

## Not covered (taxonomies other than 1 and 3)

The following AI taxonomies/tools are not yet integrated. May be used as productivity tools to enhance tasks, or to handle specific automation tasks.

- **[2] Local / Desktop Automation Agents** — make.powerapps.com / PPAC navigation, XrmToolBox output extraction, repetitive maker-portal config.
- **[4] Platform-Embedded Copilots** — already partly surfaced under col-1 implementation; deserves its own column (Copilot in apps, row summaries, Power Fx generation).
- **[5] Agent Builder Frameworks** — Copilot Studio agents over Dataverse, multi-agent orchestration, Dataverse-event-triggered automation.
- **[6] AI-Augmented Specialty Tools** — XrmToolBox AI plugins, ADO/GitHub PR Copilot, FetchXML authoring assists.

## Considerations

Two constraints shape every cell:

1. **Client data must stay within a boundary cleared to hold it.** This — not raw capability — is the dividing line between the two columns, but the boundary is *contractual*, not merely the tenant wall: a destination qualifies if it's covered by the engagement's data agreement (DPA, zero data retention, no training on input). Pasting environment detail into a *consumer* Claude.ai / ChatGPT account crosses it; doing the same work through an **enterprise-tier assistant under that agreement**, an **AI Assistant scoped to the tenant**, or the **Dataverse MCP server** (data stays in-tenant, only governed tool calls) does not. The bar scales with sensitivity — schema/metadata is lower-stakes than live PII or regulated rows — and some contracts forbid any subprocessor outright, so the acceptable destinations are a per-engagement determination. The matrix flags where this matters.
2. **Human-in-the-loop at every decision point.** AI drafts; a person decides. The cells assume review gates, not autopilot.

**Data handling and AI assistant selection for client engagements.** Free or consumer-tier AI Assistants (column 1) are not suitable for client work. Engagements require an enterprise or professional tier that provides configurable privacy controls and a zero-data-retention policy, so that context material never trains the model. Any context extract shared with an assistant — Context Exporter packs, source code, documentation — must carry a legal boundary notice (e.g. `LEGAL.md` appended to the pack) that makes the proprietary nature of the material explicit. This is not a per-engagement judgment call; it is a baseline requirement.

**Licensing.** Premium AI Assistant (column 1) licenses are likely needed for optimal functioning — larger context windows, higher rate limits, and tool-use support. Column 3 tooling may incur additional charges for MCP server calls and agent-skill usage. Because the ecosystem is evolving rapidly, the specific licenses required should be identified and budgeted at the start of each engagement rather than assumed from a previous one.

**Human-in-the-loop discipline and the role of AI personas.** The "AI drafts; a person decides" principle is the load-bearing constraint. No AI personas or automated review agents should be configured by default. If personas or skills are introduced in a later stage to assist review, they must be positioned as a *final check after the human has already reviewed* — never as the first pass. This matters because people (and models) anchor on initial feedback: AI providing the first review would bias the human reviewer rather than complement them. Any review persona is therefore a last-stage detail-catcher, not a gatekeeper.

---

## Roadmap watch (will move cells; dated and sourced)

The platform side is shifting fast enough that several cells should be re-checked each release wave.

- **Dataverse MCP server — GA, evolving tool surface.** Reframes how *both* columns reach Dataverse, and materially helps the data-boundary constraint (data stays in-tenant, accessed via allow-listed tools). Tool names changed recently (`describe`, `search_data` vs metadata `search`) — keep allow/deny lists in sync. Billing applies to non-Copilot-Studio agents. [Learn: Connect to Dataverse with MCP](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/data-platform-mcp) · [Tool-shape update, Jun 2026](https://www.microsoft.com/en-us/power-platform/blog/2026/06/08/dataverse-mcp-server-understanding-the-new-tool-shape/)
- **Dataverse plugin for coding agents (preview) + Business skills (preview).** One-install Dataverse fluency for Claude Code / Copilot (MCP + Dataverse CLI + Python SDK + PAC CLI); business skills are NL process instructions any MCP agent discovers automatically. Watch for GA. [Power Platform blog, May 2026](https://www.microsoft.com/en-us/power-platform/blog/2026/05/05/dataverse-agent-data-platform/)
- **GitHub Copilot agent model (2026).** Agent mode vs coding agent vs `.github/agents/*.agent.md` custom agents; Agent Skills (`SKILL.md`) standardized; org-level MCP allow-listing. This is the column-3 substrate — worth tracking even though this playbook keeps per-user assistant configuration (instructions, personas, skills) out of client repos. [VS Code custom instructions](https://code.visualstudio.com/docs/agent-customization/custom-instructions) · [GitHub: custom agents](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents)
- **Power Platform 2026 wave 1 (Apr–Sep 2026).** Native **GitHub integration + "deploy from Git"** maturing ALM with audit trails (column-3 relevant); generative pages and row summaries (column-1/maker relevant); **Copilot credit PAYG caps** (cost governance); Copilot Studio multi-agent + MCP-compliant tools. [Wave 1 overview](https://learn.microsoft.com/en-us/power-platform/release-plan/2026wave1/)
