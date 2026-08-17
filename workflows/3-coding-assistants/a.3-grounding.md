# [A.3] Grounding — the three-repository organization: context, project, and wiki

> **Matrix cell:** [[A.3]](../ai-augmented-d365ce-activity-matrix.md#a3-solutions-unpacked--code-repositories--the-grounding-method-for-column-3) · Column **[3] Coding Assistants & Agentic Environments** · Stood up per client in [C.3](./c.3-project-setup.md)

## The problem this solves

A coding assistant grounds itself by **reading**. You point it at repositories and it goes and looks.

Some context components recur throughout the delivery loop. They have different dynamics, different owners and behave differently — so they get different repositories:

1. **What did the client ask for?** → `<client>-Context/`
2. **What is actually built?** → `<client>-d365/`
3. **What does everyone need to know about it?** → `<client>-wiki/`

Three repositories, laid out as siblings so a single agentic session can read all of them.

---

## The three repositories at a glance

| | `<client>-Context/` | `<client>-d365/` | `<client>-wiki/` |
|---|---|---|---|
| **What it is** | Personal context: story folders, notes, exported packs, engagement documents | The project repository: unpacked solutions and source code | Live documentation, published to the team and the client |
| **Audience** | You (and your assistant) | The delivery team | Everyone — including people outside the delivery team |
| **Ownership** | Personal, local | Team, shared, PR-reviewed | Team, shared; the wiki's own review flow |
| **Lifecycle** | Added to and refreshed when work moves | Every commit of every story | Updated when increments have passed QA |
| **The assistant** | Reads for intent, specification and current environment state; may help write | Reads *and writes* — this is where it builds | Writes drafts; a human edits, validates and publishes |
| **Related activities** | [1.3] [2.3] [3.3] [4.3] [5.3] [6.3] | [1.3] [2.3] [4.3] [5.3] [6.3] | [1.3] [2.3] [6.3] |

---

## 1. `<client>-Context/` — intent and delivery direction

This repository holds the story folders, working notes, context packs and engagement documents — but here, a coding assistant reads it **off disk** instead of you attaching files to a prompt.

```
<client>-Context/
├── .gitignore
├── context-exporter/        D365 Context Exporter folder
│   ├── config/              Configuration files
│   ├── output/              Context Exporter .context.md packs
│   ├── LEGAL.md             Boundary notice prepended to every pack
│   └── runs/                Ignored by .gitignore
├── XXX-story-1/
│   ├── XXX-story-1.md       The story, mirrored from the work item
│   ├── XXX-notes.md         Personal notes, raw stakeholder input
│   ├── XXX-plan.md          Plan and tasks
│   ├── additional/          Related files: spreadsheets, CSV inputs/outputs
│   └── screenshots/         Form/view captures, error dialogs
├── YYY-story-2/
├── ZZZ-story-3/
└── offline-access/
    ├── sow/                 Scope
    ├── deployment/          Standard checklists
    ├── instructions/        Instructions
    └── guides/
```

The story folder is where the agent's own output lands too — the design options it drafted in [2.3](./2.3-planning.md), the task breakdown from [3.3](./3.3-tasking.md), the test scenarios for [5.3](./5.3-validation-peer-review.md). Once a human has reviewed them they belong next to the story, so the next session starts from where the last one ended rather than from zero.

What the coding assistant gets from it that the project repo can't give:

- **The story folders carry intent.** The repo tells the agent what exists; the story folder tells it what someone asked for and why. Point the agent at `<client>-Context/1234-account-merge-rules/` when you start work and the plan is written against the actual acceptance criteria.
- **`offline-access/` carries the constraints** — SoW scope, architecture decisions, naming and publisher-prefix conventions, deployment checklists. These are the boundaries the agent would otherwise invent.
- **The packs remain useful even with MCP available.** They are the cheap, offline, diffable read; the [Dataverse MCP](../ai-augmented-d365ce-activity-matrix.md#aiii-dataverse-mcp-server--column-3) is the live, billable, allow-listed one. Use the pack to orient, MCP to confirm.

This repository stays **personal**. It holds your working notes and mirrored story text; don't merge it into the team repo.

---

## 2. `<client>-d365/` — the project repository

The engagement's shared repository, with a fixed top-level layout so the assistant — and every teammate — always finds things in the same place:

```
<client>-d365/
├── src/                            PRO-CODE
│   ├── Plugins/                    Plugin & Custom API assemblies
│   ├── PCF/                        PCF controls
│   └── Functions/                  Azure Functions
├── solutions/                      UNPACKED SOLUTIONS (pac solution unpack output)
│   └── <SolutionName>/
│       ├── src/                    unpacked component tree
│       └── <SolutionName>.zip      exported managed/unmanaged zips (git-ignored)
├── webresources/                   WEB RESOURCES (JS, HTML, CSS) — editable source
├── context/                        Context Exporter .context.md packs (committed copy)
├── scripts/                        Automation, incl. the solution export script
├── .gitignore
└── README.md
```

This is the repository that makes column 3 *grounded*: ALM (or `pac solution export` / `pac solution unpack`) turns managed and unmanaged solutions into a **diffable file tree**, so the agent reads the entities, forms, views, plugin steps and web resources that actually exist rather than guessing at them. Grounded gap analysis ([1.3]), sequencing from real dependencies ([2.3]) and drift detection ([5.3]) all depend on this tree existing.

Two placement decisions worth settling early, because they are expensive to change:

- **The zips stay out of git.** The unpacked `solutions/<Name>/src/` tree is the reviewable source of truth; the `.zip` is a rebuildable artifact.
- **`webresources/` flows one way** — authored here and deployed *into* the solution. Treat it as source code, not as something unpacked back out of the solution; mixing the two directions causes merge pain.

`context/` is a **copy** of the packs from `<client>-Context/`, committed so the whole team is grounded on the same snapshot rather than on whatever each person last exported.

---

## 3. `<client>-wiki/` — live documentation

Azure DevOps wikis are backed by a git repository (`<project>.wiki`, or whichever repo is designated as "published code as wiki"), which is what makes documentation a **repository the assistant can read and write** rather than a browser-only surface:

```
<client>-wiki/                      Local clone of <project>.wiki
├── .order                          Page order for the wiki tree
├── .attachments/                   Images and files referenced by pages
├── Home.md
├── Architecture/                   Solution architecture, integrations, ADRs
├── Functional/                     Module and process documentation, user guides
├── Technical/                      Component docs generated from the repo ([6.3])
├── Runbooks/                       Operational and deployment procedures
└── Release-Notes/                  What shipped, per increment
```

Why it is a repository of its own and not a folder in `<client>-d365/`:

- **Different audience and different review gate.** Wiki pages are read by functional consultants, testers, support and the client. Holding them to a code-review flow slows publication; holding code to a wiki flow loses rigour.
- **Different lifecycle.** The project repo changes on every commit; the wiki changes when *behaviour* changes. Keeping them separate means the wiki's history is a readable record of what the solution does over time, not noise from refactors.
- **It's the published surface.** ADO renders it directly, so a push is a publication. That's a good reason for it to have its own clone, its own diff, and its own "is this ready to be seen?" moment.

Written documentation lives here rather than in the project repo — including the ADRs, which sit under `Architecture/`. The project repo holds solutions and code; keeping the prose in one published place avoids a second, half-maintained documentation tree next to it. [6.3](./6.3-documentation.md) is where content is generated from the repo and pushed here.

---

## Laying them out on disk

Clone all three as **siblings under one parent folder** so a single agentic session can read across them:

```
<client>/
├── <client>-Context/
├── <client>-d365/
└── <client>-wiki/
```

Then make all three visible to the assistant at once:

- **VS Code / Cursor** — a multi-root workspace (`<client>.code-workspace`) with the three folders as roots.
- **Claude Code** — add the other two as additional working directories so it can read them without leaving the project repo.
- **Either way**, open the *parent* folder if the tooling supports it, so relative paths between the repos stay stable in prompts and scripts.

One prompt can then span all three: *"Read the story in `<client>-Context/1234-*`, check it against the solutions in `<client>-d365/solutions/` and the source code, and tell me which wiki pages under `Functional/` will need updating."* The assistant works across repositories, reading what it needs.

---

## Which repository answers which question

| Question | Repository | Read via |
|---|---|---|
| What was asked for, and why? | `<client>-Context/` (story folder) | File read |
| What are the conventions, scope and constraints? | `<client>-Context/offline-access/` | File read |
| What does the environment's model look like? | `<client>-Context/context-exporter/output/` or `<client>-d365/solutions/` | File read |
| …and what does it look like *right now*? | The environment itself | [Dataverse MCP](../ai-augmented-d365ce-activity-matrix.md#aiii-dataverse-mcp-server--column-3) |
| What is actually built and configured? | `<client>-d365/solutions/`, `src/`, `webresources/` | File read, `git diff` |
| Why was it built that way? | `<client>-wiki/Architecture/` (ADRs) | File read |
| What is the state of the work? | The board | [Azure DevOps CLI](../ai-augmented-d365ce-activity-matrix.md#aii-boards--column-3) (`az boards`) |
| What do people need to know about it? | `<client>-wiki/` | File read, then commit + push |

---

## Boundaries between them

- **Secrets live in none of them.** Connection strings, client secrets and `.env` files stay out of all three; the `.gitignore` in [C.3](./c.3-project-setup.md#4-the-gitignore) excludes them explicitly.
- **The context repo does not become a dumping ground for client data.** Schema and metadata are a lower-stakes extract than live rows or PII — see [Considerations](../ai-augmented-d365ce-activity-matrix.md#considerations) — and the `LEGAL.md` notice travels with every pack.
- **Personal working material does not migrate into the shared repos.** Story mirrors, drafts and notes stay in `<client>-Context/` — kept local, or somewhere the engagement's data agreement already covers; what graduates into `<client>-wiki/` does so deliberately, reviewed.
- **Each repo refreshes on its own trigger** — the packs when the model moves (a re-export), the solution tree on every export/unpack ([C.3 §5](./c.3-project-setup.md#5-the-solution-exportunpack-script)), the wiki when behaviour changes ([6.3]).

---

## Where this gets set up

[C.3 — Project setup](./c.3-project-setup.md) is the step-by-step: prepare the context repository, scaffold the project repository, clone the wiki, script the solution export/unpack, and wire up Dataverse MCP and the Azure DevOps CLI. [B.3 — Initial setup](./b.3-initial-setup.md) covers the machine tooling and the template repo that the project repository is forked from.
