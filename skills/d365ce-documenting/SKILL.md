---
name: d365ce-documenting
description: Documents delivered Microsoft Dynamics 365 Customer Engagement and Dataverse work as complementary views of one system, from work items, implementation plans, the source repository and the generated solution extract. Defaults to a functional and technical design pair, collapsing to a combined design document for small changes or expanding to richer model sets when the architecture warrants it. Covers researching what was actually built rather than what was planned, updating existing pages when work modifies documented behaviour, keeping each view to one audience, cross-referencing the work items documented, publishing through a git-backed markdown wiki, and maintaining indexes and tables of contents. Use when documenting a delivered work item, story, task or feature into a wiki, or when asked to write functional and technical documentation for something already built.
---

# Documenting Delivered D365CE Work

A plan describes intent. A wiki page describes a system somebody has to operate, extend, or debug six months from now. The failure mode of this work is writing an elegant summary of the implementation plan: it reads well, it is wrong in three places, and nobody notices until someone relies on it.

The target is a wiki site edited in markdown and reachable via git. Products differ in page naming, ordering and link syntax — see [references/wiki-mechanics.md](references/wiki-mechanics.md) before creating any file.

---

## Update or create, then choose the artefact set

**The wiki is live documentation.** It describes the current system, not the history of changes to it. Before writing anything, search the table of contents, index pages and catalogue pages for existing coverage of what the work items touch. Delivered work that modifies documented behaviour updates the existing pages in place — a modification published as a new page forks the truth and leaves a reader two pages that disagree. Create new pages only for capability the wiki does not yet describe.

Documentation is a set of views of one system, and for new coverage the set should match the feature's weight. Microsoft's Dynamics 365 implementation guidance names the simplest sets — the functional/technical design document pair (FDD/TDD), or a combined design document; a complex architecture can warrant views up to the full range of UML model kinds.

| Feature being documented | Artefact set |
|---|---|
| A typical delivered work item or feature — the **default** | The FDD/TDD pair: a functional page and a technical page, cross-linked |
| A small, self-contained change where the pair would be two thin pages | A combined design document: one page, functional content first, technical detail after, the same audience separation between sections |
| An architecture the pair cannot carry — multiple integrated systems, many actors, significant asynchronous or state behaviour | **Ask the user before writing** whether additional views are warranted — state, sequence, deployment or other UML-range models — and which |

Default to the pair when nothing argues otherwise. The rest of this skill is written in terms of the pair; every rule applies unchanged to whichever set is chosen — the research behind the views is the same, and each view keeps to one audience.

---

## Ground rules

### 1. Document what is built, not what was planned

The plan is an index into the code, not a source of truth. Read it to learn which files, tables and components are in scope, then verify every load-bearing statement against the actual artefact. Built code diverges from approved designs constantly, and the divergences are rarely announced — a namespace root differing by capitalisation, a command filtering on one status where the plan said all returned records, a command acting on the whole parent record because it was wired to the primary control rather than the selected items. Each changes what a reader would do.

When source and plan disagree, **document the source, and report the divergence in your summary**. Do not silently pick one, and do not correct the code to match the plan.

### 2. One audience per view, no bleed

Whatever the artefact set, each view serves exactly one audience. In the default pair:

| Page | Reader | Contains |
|---|---|---|
| Functional | Someone using the feature | Behaviour, steps, rules and the messages the user sees. No schema names, file paths, option set integers, or class names. |
| Technical | Someone changing the feature | Actual identifiers — logical names, ids, option set values, registration blocks, queries — quoted verbatim, because approximations there are worse than useless. |

If a rule is enforced by a plugin, the functional page describes the rule and the message the user sees, not the plugin. A sentence that would suit both pages is usually too vague for the technical one.

### 3. Verify behavioural claims yourself

Research can be delegated and run in parallel across the four axes below, where the tool supports delegation; serially otherwise. Delegated research is reliable on inventory and unreliable on nuance. Anything you are about to state as a behavioural rule, a status value, or an enabled/disabled state gets verified by opening the file yourself. Two researchers disagreeing about the same component is a signal to go look, not to pick one.

### 4. Match the wiki before writing a word of it

Find an existing functional/technical pair in the same wiki and copy its shape: heading order, table style, how the companion link is phrased, whether diagrams are used, how the Related section is built. A page that looks like the others is easier to trust. Then read the surrounding furniture: the parent index page, any ordering files, the root table of contents, and catalogue pages that list components by type.

### 5. Research sources are read-only

The unpacked solution extract is produced by an export pipeline; it is a research source here and nothing else. The same applies to the source repository — documenting a feature does not change it. Tell any delegated researcher the extract is read-only and that its XML files are large enough to need targeted search rather than whole-file reads.

---

## Research the four axes

- **Server-side** — plugins, custom APIs, business logic and query classes, unit tests. Collect the registration documentation verbatim, what each class actually does, and which shared classes it calls.
- **Client-side** — web resources, namespaces, every public function with its signature, which form event or command it is wired to, Web API calls quoted, and every control, tab, section and grid name referenced.
- **Solution extract** — tables and column source types, option set values with labels, forms and views with ids, relationships, plugin step registrations, command bar definitions, and which solution each artefact lives in.
- **Automations** — cloud flows and classic workflows: trigger message, entity, filtering attributes, filter expression, conditions, and what each action writes.

---

## Publish through git

The wiki is backed by a git repository: clone it, edit pages as files, review the diff, commit, push. This is the preferred mechanic — it keeps AI-assisted documentation changes in reviewable git history instead of unreviewable in-browser edits. Generate and review the content locally first, then commit.

---

## Writing the pages

Page structures for the default pair, and the list of things the technical page must not omit, are in [references/page-templates.md](references/page-templates.md). A combined design document merges the two structures into one page, functional content first. Two content rules carry most of the quality:

- **Where a command's behaviour would surprise someone reading its label, say so in a callout.** A button on a subgrid that ignores the selection and acts on the whole parent record needs a highlighted note, not a footnote.
- **Interactions between features belong on the functional page, not only the technical one.** Where one rule silently cancels another — a status transition that removes a record from a total — the user needs to know before it costs them a reconciliation.
- **Every documentation pass records its work items.** The technical page — or the technical half of a combined document — carries the list of work item URLs it documents, the cross-reference between the implemented stories and tasks and the live documentation of the current system. A modification appends its work items to the existing list rather than replacing it, so the page accumulates the delivery trail.

---

## Link maintenance is part of the deliverable

A page nobody can reach was not written. Every documentation task closes by updating:

- **The companion views** — each view links to the others, once near the top and once in Related.
- **The parent index page** — a row in its page table with a one-line description.
- **The ordering file** for each folder touched, where the wiki uses them.
- **The root table of contents**, if the wiki maintains one.
- **Catalogue pages by component type** — pages listing plugins, web resources, automations, integrations. If the feature has a plugin, it belongs on the plugin catalogue page.
- **Any adjacent module page where a reader would plausibly look first.** A feature delivered under one module but operated from another belongs in the second module's index as a pointer.

Verify every link target resolves to a file that exists before finishing.

---

## Distinguishing evidence from inference

State what you found, and separately what it implies. "The extract shows a condition comparing an empty string" is a fact; "this flow is broken" is an inference that could be wrong if the export is lossy. Write the fact, offer the inference, and say the live definition should be confirmed.

Never write that a component does not exist. Write that it is not in the extract, which is a different claim — a registered but unsolutioned component leaves no XML behind.

Defects and gaps found in passing — a component registered but never solutioned, a flow exporting an empty step, registration documentation contradicting the registered step — belong in the technical page's known-gaps section, phrased as observations from the extract with a note to verify the live definition, not as accusations.

---

## Quality bar

Before presenting the pages:

- [ ] Every behavioural claim was verified against the artefact, not the plan
- [ ] Divergences between plan and built code are documented as built, and reported to the user
- [ ] The functional page contains no schema names, file paths, option set integers, or class names
- [ ] The technical page quotes actual identifiers, ids and values
- [ ] Option set collisions and column source types are stated
- [ ] The live form is named with its id, and other forms declared out of scope
- [ ] Deployment ordering constraints appear where they exist
- [ ] Findings that are defects or gaps are called out as such, separated from inference
- [ ] Existing coverage was searched for; work that modifies documented behaviour updates those pages in place, not as new parallel pages
- [ ] The artefact set was chosen deliberately — the default pair, or a documented reason and (for a larger set) the user's confirmation
- [ ] The technical page lists the work item URLs documented, appended to any existing list on a modification
- [ ] The views cross-link in both directions
- [ ] Parent index, ordering files, root table of contents and catalogue pages are updated
- [ ] File and folder names follow the wiki's naming mechanics, and every new folder has its parent page
- [ ] Every link target resolves to a file that exists
