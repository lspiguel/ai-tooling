# AI Tooling

A practical playbook for AI-augmented implementation and maintenance of Microsoft Dynamics 365 Customer Engagement and the broader Power Platform: curated workflows, prompts, tooling, and skills organized around a delivery loop. The goal is to help development teams achieve greater productivity and higher quality during implementation of software projects.

---

## The core idea: context is the grounding

In consulting, project and development work the **main issue for AI tooling is how to provide it with context**. AI is not useful if it is not aware of things like:

- Phase within a plan (are we planning and no work has been done yet? are we in maintenance mode and we have a multi-year user base? somewhere in the middle?)
- What's already built (are there components already in place? what columns does this table have? artifacts already built)
- Overall design (which can be at the Enterprise level, module level, and specific design patterns to be used)
- Plans in place (epics, features, user stories, documentation that describes from a functional perspective what has been done, what's planned to be built and when)
- Existing constraints (which tools can be used, security restrictions, licensing, budget, capacity; is there a code freeze?)
- Tools (including development, design, project management and other tools)

This **context** is the grounding that an AI needs to provide answers and build artifacts that work. Building or standardizing the grounding once is most of the leverage — everything below depends on it.

---

## How work is organized

Project work moves through a **grounding layer**, two **one-time setups** (initial per-person/per-team, then per-client/per-engagement), and an iterative **delivery loop** of six activities. Each activity is executed through two tool surfaces — **general-purpose AI assistants** (Claude.ai, ChatGPT, M365 Copilot) and **coding assistants & agentic environments** (GitHub Copilot, Claude Code, Cursor) — because the surface, not the model, determines what the AI can see and act on.

The grounding layer resides in **git repositories**: a context repository holding markdown snapshots of the Dataverse environment (entities, security model, solutions — produced with the [D365 Context Exporter](tooling/D365ContextExporter/README.md)), and for the coding-assistant surface also the unpacked solutions & source code and the wiki. General-purpose assistants receive these as uploaded context packs; coding assistants read the repositories directly, complemented by live skill, MCP and tool access.

The full playbook is the **[Activity × Tooling Matrix](workflows/ai-augmented-d365ce-activity-matrix.md)** (also as a [visual infographic](workflows/matrix-infographic.html)). Each cell links to a per-activity guide:

| Activity | General-Purpose Assistants | Coding Assistants & Agents |
|---|---|---|
| **[A] Grounding Layer** | Context git repository + [D365 Context Exporter XrmToolBox](tooling/D365ContextExporter/README.md) | Context, Solutions/Code, Wiki git repositories |
| **[B] Initial one-time setup** | [B.1](workflows/1-general-purpose-assistants/b.1-initial-setup.md) | [B.3](workflows/3-coding-assistants/b.3-initial-setup.md) |
| **[C] Project setup** | [C.1](workflows/1-general-purpose-assistants/c.1-project-setup.md) | [C.3](workflows/3-coding-assistants/c.3-project-setup.md) |
| **[1] Specification / Intent** | [1.1](workflows/1-general-purpose-assistants/1.1-specification.md) | [1.3](workflows/3-coding-assistants/1.3-specification.md) |
| **[2] Planning / Design** | [2.1](workflows/1-general-purpose-assistants/2.1-planning.md) | [2.3](workflows/3-coding-assistants/2.3-planning.md) |
| **[3] Tasking** | [3.1](workflows/1-general-purpose-assistants/3.1-tasking.md) | [3.3](workflows/3-coding-assistants/3.3-tasking.md) |
| **[4] Implementation / Build** | [4.1](workflows/1-general-purpose-assistants/4.1-implementation.md) | [4.3](workflows/3-coding-assistants/4.3-implementation.md) |
| **[5] Validation / Peer review** | [5.1](workflows/1-general-purpose-assistants/5.1-validation-peer-review.md) | [5.3](workflows/3-coding-assistants/5.3-validation-peer-review.md) |
| **[6] Documentation / Information sharing** | [6.1](workflows/1-general-purpose-assistants/6.1-documentation.md) | [6.3](workflows/3-coding-assistants/6.3-documentation.md) |

**Ground rule — human-in-the-loop at every decision point.** AI drafts; a person decides. Every workflow above assumes review gates, not autopilot. Client data handling, assistant-tier selection, and licensing considerations are covered in the matrix's [Considerations](workflows/ai-augmented-d365ce-activity-matrix.md#considerations) section.

---

## Where the AI runs: the tool taxonomy

The workflows are organized by a six-category taxonomy of **deployment context and execution model** — where the AI runs, not which model:

1. **General-Purpose AI Assistants** — Claude.ai, ChatGPT, M365 Copilot (Premium)
2. **Local / Desktop Automation Agents** — Claude Cowork, Computer Use, Operator
3. **Coding Assistants & Agentic Environments** — GitHub Copilot, Claude Code, Cursor
4. **Platform-Embedded Copilots** — M365 Copilot in apps, Copilot for Dynamics 365
5. **Agent Builder Frameworks / Orchestration Platforms** — Copilot Studio, Azure AI Foundry
6. **AI-Augmented Specialty Tools** — XrmToolbox AI plugins, ADO/GitHub PR Copilot

Categories 1 and 3 carry the developed workflows today; the rest are still in flux and do not integrate easily with other tools, so taxonomies 2, 4, 5 and 6 are staged in the matrix's [Not covered](workflows/ai-augmented-d365ce-activity-matrix.md#not-covered-taxonomies-other-than-1-and-3) section. Full definitions, characteristics, D365 relevance, and references: **[AI Tool Taxonomy](docs/ai-tool-taxonomy.md)**.

---

## Repository map

| Path | Description |
|------|-------------|
| [workflows/](workflows/) | The Activity × Tooling Matrix, infographic, and per-activity workflow guides |
| [workflows/1-general-purpose-assistants](workflows/1-general-purpose-assistants/README.md) | Workflows using web/desktop chat AI (e.g., Claude.ai, ChatGPT) |
| [workflows/3-coding-assistants](workflows/3-coding-assistants/README.md) | Workflows using IDE-embedded and CLI/agentic coding AI (e.g., GitHub Copilot, Claude Code, Cursor) |
| workflows/[2](workflows/2-desktop-automation-agents/) · [4](workflows/4-platform-embedded-copilots/) · [5](workflows/5-agent-builder-frameworks/) · [6](workflows/6-ai-augmented-specialty-tools/) | Pending: desktop automation agents, platform-embedded copilots, agent builder frameworks, AI-augmented specialty tools |
| [docs/](docs/) | Reference documentation, including the [AI Tool Taxonomy](docs/ai-tool-taxonomy.md), architecture notes, and decision records |
| [tooling/](tooling/) | Custom tooling, including the D365 Context Exporter |
| [skills/](skills/) | Reusable skills and reference guides for AI assistants |
| [scripts/](scripts/) | Utility scripts supporting AI-assisted tasks and automation |

---

## Sections

- [AI Tool Taxonomy](docs/ai-tool-taxonomy.md)
- [Instructions for AI Tools](/INSTRUCTIONS.md)
