# General-Purpose AI Assistants — D365 CE Workflows

Web and desktop chat AI for requirements analysis, documentation, planning, and tasks that require broad reasoning rather than code execution in a specific environment.

**Primary tools:** Claude.ai, ChatGPT, Microsoft 365 Copilot (chat mode)

---

## Workflow guides

The prompts formerly collected in this README have been organized into per-activity guides following the [Activity × Tooling Matrix](../ai-augmented-d365ce-activity-matrix.md). Start with the setup guides, then pick the guide for the phase you're in:

### Setup (one-time)

| Guide | What it covers |
|---|---|
| [B.1 — Initial setup](./b.1-initial-setup.md) | Machine baseline: package managers and CLI tools for AI-augmented functional work |
| [C.1 — Project setup](./c.1-project-setup.md) | Per-client grounding: Context Exporter packs, legal boundary notice, the grounded Project |

### The delivery loop

| Guide | What it covers |
|---|---|
| [1.1 — Specification](./1.1-specification.md) | User stories, INVEST evaluation, reconciling asks against the existing model, requirement mining |
| [2.1 — Planning](./2.1-planning.md) | Epic decomposition, sequencing, risk register, estimation rationale, ADO import scripting |
| [3.1 — Tasking](./3.1-tasking.md) | Feature → task checklists with a clear Definition of Done |
| [4.1 — Implementation](./4.1-implementation.md) | Power Fx / FetchXML / OData snippets, design rubber-ducking, data migration support, maker-portal copilots |
| [5.1 — Validation / peer review](./5.1-validation-peer-review.md) | Test cases from ACs, UAT scripts, design-doc review, accessibility review |
| [6.1 — Documentation](./6.1-documentation.md) | Runbooks, changelogs, release notes, user guides, training material, living schema packs |

---

## Ground rules for every prompt

- **Data boundary:** client material goes only into an enterprise/professional-tier assistant covered by the engagement's data agreement — see [Considerations](../ai-augmented-d365ce-activity-matrix.md#considerations).
- **Human-in-the-loop:** AI drafts; a person decides. Every output above is a draft for its human owner.
