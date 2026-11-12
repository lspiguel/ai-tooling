---
name: d365ce-planning
description: Produces implementation plans for Microsoft Dynamics 365 Customer Engagement and Dataverse work. Covers researching the live environment and the solution extract before planning - through the Agent Companion MCP when it is connected, otherwise through whatever environment tooling is available - separating artefacts authored in source from changes made in the environment, deciding which environment changes the agent can carry out (WIP solutions) and which need a person, settling the close-out strategy for WIP solutions before writing tasks, recording settled delivery facts in the project's delivery-facts.md, splitting delivery into manual and agentic tasks, scaling plan depth to the size of the change, and tracking open points to resolution. Use when planning, scoping, or breaking down D365CE or Dataverse changes - plugins, custom APIs, web resources, forms, views, solutions, schema - or when turning a user story into an implementation plan.
---

# Planning D365CE Work

D365CE plans fail for reasons that have nothing to do with the code being wrong. They fail because the plan targeted a generated file, or a form nobody uses, or assumed a column was writable when it is a rollup, or changed a plugin step's trigger in a way that broke code the plan never looked at.

This skill is about the research and framing that prevents those failures. Implementation conventions - naming, folder structure, tracing, StyleCop - come from the project rules, not from here.

---

## Scale the plan to the change

The ground rules below hold at any size - a one-line fix can still target a generated file. Everything else scales: research covers what the plan touches, risks appear when the change can trigger them, and the document grows only as the design does. Size the plan before writing it, and when the expected complexity or the level of detail wanted is ambiguous - a quick fix versus the first slice of something larger - ask the user rather than defaulting to the full treatment.

| Size | Typical shape | The plan carries |
|---|---|---|
| **Small** | One component, no schema writes, no new registrations - a label change, a guard in an existing handler, a column added to a view | The component's channel (ground rule 1), the approach in a sentence or two, and the task list. Half a page. |
| **Medium** | A few components, possibly both channels, but the design is not in question - a new function in an existing web resource plus its form event wiring | Approach, component inventory, and task list in full; other sections only where they have content. |
| **Large** | New schema, new plugin steps or components, behaviour spanning components, ordering constraints, rollups, recursion | The full skeleton. |

- **The task list is never thinned.** Performer labels, prerequisites, and status lines are what make the plan a progress record; a one-task plan still carries all of them.
- **Sections with nothing to say are omitted, not filled.** An open point is a genuine unknown that survived research; a risk belongs in the plan only when the change can trigger it. Inventing either to complete the skeleton spends reviewer attention exactly where the plan is meant to save it. "No open points" is one line, and it is a finding.
- **The size call is provisional until research is done.** A label change that turns out to live on three forms has outgrown Small. Grow the plan and tell the user the scope moved, rather than growing it silently.

---

## Know your tooling before you plan

What the plan can rely on, and what the agent can carry out itself, depends on the tools connected to the session. Check before research. Some clients list MCP tools as deferred - only their names are visible until they are searched for or loaded - so search for them before concluding they are not there.

| Available | Research from | Environment changes the agent can carry out |
|---|---|---|
| **Agent Companion MCP**, with a tab bound to the development environment | The live environment through the plugin's read tools - first, and as much as possible. The extract for what the plugin does not show | Whatever a WIP solution can carry with the plugin and PAC CLI: schema and metadata, forms, views, ribbons, sitemaps, web resources, solution membership. Apply `agent-companion-context` before writing those tasks (see "Delivery context for environment changes") |
| Another MCP server or tool with access to the environment, or PAC CLI alone | That tooling, read-only, and the extract | Only what that tooling can carry out end to end and verify afterwards. Name the tool in the task |
| Neither | The extract and the repository. Anything that needs the environment becomes a technical confirmation | None - environment changes are manual |

**Use it now.** A question the tools can answer is answered while planning, not written into the plan as a check for later. When the user says to verify something with a tool, that means now. Say what each check showed, and which tool showed it.

---

## Ground rules

### 1. Unpacked solution folders are generated. Treat them as read-only

Most D365CE repositories contain an unpacked solution tree produced by an export/unpack pipeline.

**Never plan to create, edit, or delete a file in that tree.** A hand-edit is overwritten by the next extract at best, and conflicts with the pipeline at worst.

This splits every deliverable into one of three channels, and a plan is not finished until each component is assigned to one:

- **Authored in source and committed by us** - plugin C# and unit tests, JavaScript and other web resource source, project files, PCF controls, build scripts.
- **Changed in the environment by the agent, through a WIP solution** - when the tooling can carry the change out and verify it (see "Know your tooling before you plan"): schema and metadata, form and view XML, published web resources, solution membership. It reaches the repo via the extract, after close-out.
- **Changed in the environment by a person** - what the tooling cannot do, and what the user keeps manual: among them the compiled plugin assembly, plugin step registrations and images, and custom APIs. It reaches the repo only via the extract.

Put that split in the plan explicitly - a routing table when several components are in play, a single line when there is one. It determines the whole task list: channels one and two are agentic work, channel three is manual work, and both environment channels end with close-out and an extract.

An unpacked WIP solution in a work folder outside the repository is not the generated tree. Editing it is how the second channel works.

A useful corollary: when JavaScript exists both in a source folder and inside the extract, the source copy is the one to edit. The extract copy appears after the web resource is published. Plan a diff of the two as a verification step, since a divergence means the wrong file was uploaded.

### 2. Absence from the extract does not prove absence in the environment

A component registered in the environment but never added to a solution has no XML in the extract. Searching and finding nothing therefore means one of two things - it does not exist, or it exists and is unsolutioned - and those have opposite consequences.

**Never state that a component is missing on the extract's evidence alone.** Check the environment with the tooling you have (see "Know your tooling before you plan") and report both results - "not in the extract; not in the development environment, per the Agent Companion MCP". Only when nothing can check the environment, state that it is not in the extract and ask the user to confirm. An unsolutioned component is also a finding worth reporting: it means the behaviour has never promoted past the environment it was registered in, which is usually news to the business.

### 3. Confirm which component is actually in use

Extracts accumulate clutter. A single table can carry dozens of main forms, most of them legacy or managed, and views and dashboards behave the same way.

Before planning against a form, view, or dashboard, establish **which one is live** and record its name and id in the plan. Then say plainly that the others are out of scope, and add a verification step that no other file under that folder appears in the extract diff. A diff touching a second form means the wrong one was opened.

Check whether the component you need to change appears more than once on the same form. Two controls bound to the same column on two different tabs is one form change, not two - but only if you noticed both.

### 4. Schema names are inline string literals

Write Dataverse table, column, relationship, form control, and tab names as inline literals in C# and JavaScript. Do not declare constants for them.

The literal is itself the dependency on the schema. Wrapping it in a constant does not remove the coupling, does not make a rename detectable any earlier, and adds indirection a reader must follow to learn which column is being touched.

This applies to schema names only. Named members remain right for values that are not schema names - option set values, GUIDs, and identifiers we invent ourselves such as a form notification `uniqueId`, where a mismatch between two call sites is a real defect a constant prevents.

Leave pre-existing constants in files you touch exactly as they are. Do not remove them, retrofit them, or reference them from new code. Mixed style within one file is preferable to an unrequested refactor of members that drive live behaviour.

### 5. Extend before creating

Prefer, in order: widen an existing plugin step's filtering attributes; add members to an existing class or JavaScript namespace; add a new class to an existing assembly; create something new.

Extending an already-registered web resource or step often means **no form change and no new registration at all**, which removes an entire manual task from the plan. Say so explicitly when it applies, because it is the difference between a one-step deployment and a form publish.

Resist new columns for values that can be computed at runtime. A total that JavaScript can sum through the Web API on form load does not need a column, a rollup, or a recalculation job.

---

## Research before writing the plan

Do this first. Every item below has changed a plan's design at least once, and each applies only when the plan touches what it checks - a plan that writes no columns has no writability to confirm, and a change with no client-side surface needs no form research.

Start from the project's `delivery-facts.md` (see "Record what you settle"). Then, wherever the environment is reachable, check it live: the extract lags the environment, misses whatever was never put in a solution, and may not be in the workspace at all - say so when it is not. With the Agent Companion MCP, the queries for steps, images, custom APIs and flows on a table are in `agent-companion-wip-shaping`'s [queries.md](../agent-companion-wip-shaping/references/queries.md#research--what-already-runs-on-a-table).

**Column writability.** Read each column the plan writes: its type and required level from the environment where it is reachable, and its `SourceType` from the entity XML. A column with `SourceType 2` is a rollup and a plan that writes it is invalid; `SourceType 1` is calculated; only `SourceType 0` with `ValidForUpdateApi 1` is a plain writable column. Note existing rollups on the same table and whether they filter by status - an unfiltered rollup next to a status-aware plan is a contradiction to resolve.

**Option values.** When code maps or branches on choice values, read the values and labels from the environment and record them in the plan with their source.

**Existing plugin steps on the tables involved.** For each: message, stage, mode, rank, filtering attributes, and registered images - from the environment where it is reachable, since an unsolutioned step has no XML in the extract. Filtering attributes decide what the Target carries, and images decide what else is available without a retrieve. A redundant `Retrieve` in existing code is usually evidence that no image is registered.

**Pre-validation and validation plugins that could block the change.** A step that rejects updates to a column under some status will reject the very edit your test script asks a tester to make. Find these before writing test steps, and state the precondition in the step rather than letting a tester log a defect against correct behaviour.

**Every write path to the tables involved.** Model-driven forms, ribbon and command bar handlers, custom APIs, cloud flows, portals, integrations, bulk operations. A cloud flow names its table in its definition, not in `primaryentity`, so a query on `primaryentity` alone finds none - never report "no flows" from it. Plugins registered on the message cover all of them automatically, which is an argument for server-side logic - but each path still belongs in the test pass.

**The client-side surface.** Which web resource libraries are registered on the form, which handlers are wired to which events, and the existing namespace structure and comment-block layout of the file you plan to extend. New members go where the file's existing organisation says they go.

**Form structure.** Tab and section names, subgrid names, default visibility and expansion. A tab that defaults to hidden or collapsed means controls inside it do not resolve on first load, which shapes any event registration plan.

---

## Delivery context for environment changes

When the plan changes the environment through WIP solutions, settle how those changes leave development before writing the task list - the close-out strategy decides the tasks. With the Agent Companion MCP connected, apply `agent-companion-context`: it derives the facts from what the user has said, the project's delivery facts, the repositories and the environments, and asks only for what is left. Without it, settle the same facts yourself.

| Strategy | Agentic tasks end at | Then |
|---|---|---|
| **A - Promote to build** | The WIP imported and validated in development, left for promotion | A person runs the promotion; the extract is taken from build; the agent verifies the build environment and deletes the development WIP |
| **B - Consolidate in development** | New components added to the transport solution in development and verified; the WIP deleted | The extract from development ships them |

- **What the user says settles it.** "We promote to build", "close-out with build environment" and "transport solutions are only consolidated through promotion" all mean A. Never rewrite a task the user wrote in those terms into the other strategy; if something contradicts it, say what.
- **Never assume it.** When nothing settles it, ask once, with a recommendation and the evidence - earlier WIPs in development named `... - PROMOTED` are evidence of A. It is not an unnecessary question: a plan built on the wrong strategy puts work where it never ships.
- **Promotion is not a deployment of the WIP.** A WIP is never deployed to test, UAT or production. Promoting it to a build environment is how it reaches its transport solution under A.
- **Use the WIP names the user gives**, or the project's convention from earlier WIPs.

Write the delivery context into section 0 of the plan.

---

## Risks to analyse explicitly

These recur across engagements and belong in the plan when relevant. When the change cannot trigger one, leave it out - do not record it as not applicable.

**Widening filtering attributes exposes latent defects.** Existing code written when a step had one filtering attribute may dereference that attribute unguarded, because the Target always carried it. Add a second filtering attribute and the Target often will not. Read the existing handler line by line before widening any filter, and when you find such a defect, note that it is **dormant today and becomes reachable on the change** - then make the sequencing explicit: the guarded assembly deploys *before* the filter widens, never after. Ordering inside a single manual task is easy to get wrong and expensive to debug.

**Rollup logic must cover every message that changes the sum.** Logic on Update alone leaves a total overstated when a child record is deleted, and understated when one is created outside the plan's assumptions. Decide the coverage and record what is deliberately out of scope.

**Recursion and depth.** A synchronous plugin that writes to a related record can re-enter itself. Prefer writing the parent from a child's Post-Operation over chains that write back down, and skip the update when the recalculated value is unchanged - it both avoids a pointless write and dampens loops.

**Synchronous versus asynchronous.** Synchronous means a grid refresh reads already-recalculated values, so no client-side arithmetic is needed. That is often the deciding argument, and it is worth stating rather than leaving implicit.

**Currency.** Money columns carry a `_base` companion the platform maintains. Write the transaction-currency column and say that multi-currency conversion is out of scope, if it is.

**Backfill.** New calculations do not touch existing rows until they are next saved. Either plan a one-off data fix or state that historical rows keep stale values.

---

## Plan document structure

Write the plan as markdown, stored with the work item it implements and named for its id. Use this skeleton, sized per the table in "Scale the plan to the change" - a Large change uses all of it, a smaller one keeps the numbering and drops the sections it does not need:

```markdown
# <id> - <title>: Implementation Plan

## 0. Ground rules
The read-only generated tree, the artefact routing table, and any convention
this plan overrides. When the plan changes the environment, the delivery
context: connection, close-out strategy per destination, WIP names.

## 1. Solution approach
A capability-to-component table: capability, layer, component, new or existing.
A mermaid data-flow diagram. Then design decisions taken, each with its reason.

## 2. Existing schema (no changes required)
Only what the plan touches, with the extract path as read-only reference and
what the environment showed, naming the tool. Note writability and existing
rollups. Identify the live form by name and id.

## 3. New functionality
Per component: file path, registration block, logic as numbered steps,
and the test cases it must satisfy.

## 4. Components to modify
Per component: what changes and why. Call out defects found in passing.

## 5. Full component inventory
Files we author and commit. Changes made in the environment, each with its
WIP, its landing solution and who makes it. Schema names touched, with read or write.
Close with what is explicitly not created.

## 6. Assumptions and open points
Numbered. Each states the assumption, the consequence if wrong, and the cost
of changing it.

## 7. Task list
A single list in precedence order, each task labelled with its performer and
its prerequisites and closed with a status line.
```

Sections 5 and 6 carry most of the review value. An inventory that ends with "no new columns, tables, relationships, option sets, web resources, or form libraries are created" tells a reviewer more about blast radius than any prose.

---

## Task list conventions

Note the performer by **who or what can perform the task**, not by layer.

- **Agentic (`A1`, `A2`, ...)** - file edits in the repository, including unit tests and running them, and environment changes the agent carries out with the tooling it has: shaping, editing and importing WIP solutions through the Agent Companion MCP and PAC CLI.
- **Manual (`M1`, `M2`, ...)** - what needs a person: maker portal or desktop-tool work the tooling cannot do - plugin assembly, step, image and custom API registration among them - every pipeline run - promotion, extract and commit, deployment - and functional testing.

The same environment change is agentic when the tooling is there and manual when it is not; a manual one says which tool would make it agentic. Cross-reference the two, note prerequisites on each, and sequence the tasks in a unified, ordered way.

**Agentic environment tasks name their means.** The connection, the WIP, the tools, the read-back that verifies the change, and where the close-out strategy hands over to a person.

**Manual tasks are written for someone who is not you.** Number every step. Name the tool, the solution, the component and its id, and the exact control or checkbox. State how to confirm the right component is open before editing it. End with a verification step phrased as an observable outcome - what the user should see on screen, not "confirm it worked".

**Agentic tasks reference plan sections rather than restating them.** Say "per section 3.1" and keep the detail in one place. Where an edit is additive to shared code, say **additive only** and name what depends on it. Include the build and test gate as a step.

**Test steps carry their preconditions.** If a scenario needs a specific status or a record shape, say so in the step. Include regression steps for behaviour the change could break, and a step for every write path found during research.

**Every task ends with a status line.** Close each task with:

```markdown
> **Status**: Pending.
```

Update it in place as implementation progresses, so the plan itself is the progress record and no separate tracker is needed. A task that is blocked should say what it is waiting on.

---

## Open points

Number them and keep them in the plan as it evolves. A question the connected tools can answer is not an open point - answer it. Zero is a valid count - when research left no genuine unknowns, one line saying so closes the section, and an open point invented to fill it obliges someone to resolve it. Each real one needs the assumption, the consequence if it is wrong, and the cost of changing course. Quantify that cost concretely - "one extra condition in the query method" tells a reader how much is at stake in a way that "may require rework" does not.

When one is settled, mark it resolved in place with the date and what was decided, rather than deleting it. The trail of what was considered and rejected is worth more than a clean list.

Distinguish what needs a **business decision** from what needs a **technical confirmation**, and address each to whoever can answer it. Anything that stays open at the end belongs in a documentation task.

---

## Record what you settle

The project's `delivery-facts.md` - in `<client>-Context/`, or next to the segmentation map in `<client>-d365/` when there is no context repository - is its memory of how it delivers. Read it before research, and check what it says against the environment before relying on it.

Whenever planning settles a fact a later plan would need again and it is not recorded, add it - or update the row that changed - and tell the user in one line. Typical facts: the close-out strategy and transport source of each transport solution, the WIP naming convention, who runs which pipeline, the plugin assembly and its transport home, the live main form of a table, a convention the user stated. The shape is in `agent-companion-context`'s [delivery-facts.md](../agent-companion-context/references/delivery-facts.md#recording); without it, a table of fact, value, date confirmed and source. You write the file; the user commits it. No secrets, tokens or record data.

---

## Quality bar

Before presenting a plan:

- [ ] Every component is assigned to a channel - authored in source, changed in the environment by the agent, or by a person
- [ ] Facts the connected tools could check were checked while planning, not left as open points or later tasks
- [ ] Environment changes the tooling can carry out are agentic; the rest are manual and say which tool would change that
- [ ] Every WIP task follows a settled close-out strategy, and no task the user wrote was rewritten to another
- [ ] No file in the generated solution tree is created, edited, or deleted
- [ ] Every column the plan writes is confirmed writable - type and required level from the environment where reachable, `SourceType` from the entity XML
- [ ] Existing plugin steps on the affected tables are enumerated with their filters and images
- [ ] Any filter or image change has been checked against the existing handler code
- [ ] The live form is identified by name and id, and other forms are declared out of scope
- [ ] Schema names are literals; no new constants for them
- [ ] Deployment ordering constraints are stated where they exist
- [ ] Manual tasks are numbered, tool-specific, and end in an observable verification
- [ ] Every task carries a status line, set to `Pending.` in a new plan
- [ ] Open points state the consequence and the cost of changing course
- [ ] Plan depth matches the size of the change - no section, open point, or risk exists only to complete the skeleton
- [ ] Findings that are defects or gaps, rather than planned work, are called out as such
- [ ] Facts settled while planning are added to `delivery-facts.md`
