# [A.1] Grounding — the `<client>-Context/` repository: context packs, story folders, and engagement documents

> **Matrix cell:** [[A.1]](../ai-augmented-d365ce-activity-matrix.md#a1-context-packs--the-grounding-method-for-column-1) · Column **[1] General-Purpose AI Assistants** · Stood up per client in [C.1](./c.1-project-setup.md)

## The problem this solves

A general-purpose assistant cannot browse the client's Dataverse environment, open the solution files, or read the backlog. **Everything it knows about the engagement, you handed it.** The quality of every [1.1] specification, [2.1] design and [5.1] review conversation is therefore decided *before* the conversation starts — by what you uploaded and what you attached.

That material needs somewhere to live. Spread across Downloads, Teams chats and a couple of OneDrive folders, it goes stale silently: you re-upload a six-month-old entity dictionary, the assistant confidently proposes a field that already exists, and the grounding has quietly become a liability.

`<client>-Context/` is that place — **one local git repository per client**, holding everything you would ever seed into a Claude Project / Custom GPT / M365 Copilot Notebook or attach to a single prompt.

It is **personal and local**: your working copy of the engagement's context, not a team deliverable and not the client's repository.

---

## What it holds

```
<client>-Context/
├── .gitignore
├── context-exporter/        D365 Context Exporter folder
│   ├── config/              Query, template and spec configuration
│   ├── output/              Context packs to be supplied to the General AI Assistants
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
    └── guides/
```

| Folder | What it is | Where it comes from | How it reaches the assistant |
|---|---|---|---|
| `context-exporter/output/` | The `.context.md` packs — EntityDictionary, SolutionInventory, SecurityModel, FormsAndViews, Optionsets, SolutionsReference | [D365 Context Exporter](../../tooling/D365ContextExporter/README.md) run against the client environment | **Project knowledge** — uploaded once per refresh |
| `context-exporter/config/` | The specs, FetchXML and Scriban templates that produced those packs | Deployed by the plugin; edited per client | Not uploaded — it's the recipe, not the result |
| `<ID>-story-*/` | One folder per work item: the story markdown, plus notes, the plan, screenshots, extracts, and the drafts the assistant produced | You, mirroring ADO/Jira | **Attached per conversation** — only the story you're working on |
| `offline-access/` | The engagement documents the packs don't cover: SoW, architecture and design docs, deployment checklists, guides, naming and publisher-prefix conventions | The engagement's SharePoint/Teams, copied down as markdown or PDF | **Project knowledge** — the stable ones; attached ad hoc for the rest |

The reason this is a *git* repository and not a folder: the packs are point-in-time snapshots, and the commit history is what tells you how much the model moved between exports. `git diff` on a regenerated `EntityDictionary.context.md` is a free change report on the client's environment.

---

## The two ways context reaches the assistant

Everything in the repo arrives at the assistant through one of two doors, and putting a file through the wrong one is the most common failure:

| | **Project knowledge** (seeded once) | **Per-conversation attachment** |
|---|---|---|
| Surface | Claude Project files · Custom GPT knowledge · M365 Copilot Notebook | Files attached to the individual chat |
| Holds | Context packs, SoW, architecture, conventions, data-handling rules | The story folder for the thing you are working on right now |
| Changes | Per refresh (a solution import, a wave of new tables, a security-role change) | Every conversation |
| If you get it wrong | Story-specific detail in project knowledge bleeds into every unrelated conversation | Re-pasting the entity model into each chat burns context and drifts between chats |

**Rule of thumb:** if it is true for the whole engagement, it belongs in project knowledge. If it is true for one story, attach it.

---

## Per-story folders: the assistant's missing memory

A general-purpose assistant has no repository and no work-item access, so the story folder is its memory: a self-contained bundle you can drag into a fresh conversation and be immediately grounded:

```
1234-account-merge-rules/
├── 1234-account-merge-rules.md      The story: title, intent, ACs — mirrored from the work item
├── 1234-notes.md                    Raw stakeholder input the story was drafted from
├── 1234-plan.md                     Plan and tasks
├── design-options.md                [2.1] output, once a human has reviewed it
├── test-scenarios.md                [5.1] output
├── additional/                      Related files: spreadsheets, CSV inputs/outputs
└── screenshots/                     Form/view captures, error dialogs
```

Conventions worth holding to:

- **Name the folder with the work-item ID and a slug** (`1234-account-merge-rules`). The ID makes the round trip back to ADO/Jira unambiguous; the slug makes the folder findable six weeks later.
- **The story markdown is a mirror, not the master.** ADO/Jira remains the system of record — this copy exists because you can't grant the assistant access to the board.
- **Write the assistant's reviewed output back into the folder.** The draft ACs, the design options, the test scenarios: once a human has owned them, they belong here. The next conversation about the same story starts from where the last one ended instead of from zero.
- **Keep raw input alongside the polished artifact.** Requirement mining works better when the assistant can see the workshop notes the story was distilled from, not just the distillation.

---

## Conventions that keep the grounding honest

- **Date every export.** Record the source environment URL and the export date in the Project instructions, and re-state them when you re-upload. A pack whose age nobody knows is worse than no pack.
- **One environment per pack set.** Export from the environment that is the source of truth for configuration — usually **Dev**. Mixing Dev and Prod packs in one Project produces answers that are true of neither.
- **Keep `LEGAL.md` attached.** The exporter prepends the boundary notice to every pack; don't strip it. Any context extract shared with an assistant must carry it — see [Considerations](../ai-augmented-d365ce-activity-matrix.md#considerations).
- **`runs/` stays git-ignored.** The per-run working directories are intermediate JSON and drafts; the committed artifact is `output/`.
- **Client material only goes to an enterprise/professional-tier assistant** under the engagement's data agreement. The repository being local doesn't make the upload safe — the destination does.
- **Keep the repo private and local.** If it is ever pushed, push it somewhere the engagement's data agreement already covers.

---

## Keeping it current

The packs are a **point-in-time snapshot**. Re-run the exporter and re-upload when the model has moved materially — after a solution import to Dev, a wave of new tables or fields, or a security-role change. This is not maintenance overhead bolted onto the workflow: regenerating the packs *is* the [6.1] Documentation step, because the same artifact that grounds the assistant is the living data-model doc.

Refresh the Project instructions' export date each time, and commit the regenerated packs so the diff records what changed.

---

## Where this gets set up

[C.1 — Project setup](./c.1-project-setup.md) is the step-by-step: prepare the repository, install and run the Context Exporter, assemble the grounded Project, and set the refresh cadence. [B.1 — Initial setup](./b.1-initial-setup.md) covers the machine tooling and the reusable "house style" Project that sits behind every client.
