---
name: d365ce-planning
description: Produces implementation plans for Microsoft Dynamics 365 Customer Engagement and Dataverse work. Covers researching the solution extract before planning, separating artefacts authored in source from changes made directly in the environment, splitting delivery into manual and agentic tasks, and tracking open points to resolution. Use when planning, scoping, or breaking down D365CE or Dataverse changes - plugins, custom APIs, web resources, forms, views, solutions, schema - or when turning a user story into an implementation plan.
---

# Planning D365CE Work

D365CE plans fail for reasons that have nothing to do with the code being wrong. They fail because the plan targeted a generated file, or a form nobody uses, or assumed a column was writable when it is a rollup, or changed a plugin step's trigger in a way that broke code the plan never looked at.

This skill is about the research and framing that prevents those failures. Implementation conventions - naming, folder structure, tracing, StyleCop - come from the project rules, not from here.

---

## Ground rules

### 1. Unpacked solution folders are generated. Treat them as read-only

Most D365CE repositories contain an unpacked solution tree produced by an export/unpack pipeline.

**Never plan to create, edit, or delete a file in that tree.** A hand-edit is overwritten by the next extract at best, and conflicts with the pipeline at worst.

This splits every deliverable into one of two channels, and a plan is not finished until each component is assigned to one:

- **Authored in source and committed by us** - plugin C# and unit tests, JavaScript and other web resource source, project files, PCF controls, build scripts.
- **Changed directly in the environment, and reaching the repo only via the extract** - the compiled plugin assembly, plugin step registrations and images, published web resources, form and view XML, schema, security roles, solution membership.

Put that split in the plan as an explicit table. It determines the whole task list: channel one is agentic work, channel two is manual work followed by waiting for an extract.

A useful corollary: when JavaScript exists both in a source folder and inside the extract, the source copy is the one to edit. The extract copy appears after the web resource is published. Plan a diff of the two as a verification step, since a divergence means the wrong file was uploaded.

### 2. Absence from the extract does not prove absence in the environment

A component registered in the environment but never added to a solution has no XML in the extract. Searching and finding nothing therefore means one of two things - it does not exist, or it exists and is unsolutioned - and those have opposite consequences.

**Never state that a component is missing. State that it is not in the extract and ask the user to confirm.** An unsolutioned component is also a finding worth reporting: it means the behaviour has never promoted past the environment it was registered in, which is usually news to the business.

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

Do this first. Every item below has changed a plan's design at least once.

**Column writability.** Read the entity XML for each column the plan writes. A column with `SourceType 2` is a rollup and a plan that writes it is invalid; `SourceType 1` is calculated; only `SourceType 0` with `ValidForUpdateApi 1` is a plain writable column. Note existing rollups on the same table and whether they filter by status - an unfiltered rollup next to a status-aware plan is a contradiction to resolve.

**Existing plugin steps on the tables involved.** For each: message, stage, mode, rank, filtering attributes, and registered images. Filtering attributes decide what the Target carries, and images decide what else is available without a retrieve. A redundant `Retrieve` in existing code is usually evidence that no image is registered.

**Pre-validation and validation plugins that could block the change.** A step that rejects updates to a column under some status will reject the very edit your test script asks a tester to make. Find these before writing test steps, and state the precondition in the step rather than letting a tester log a defect against correct behaviour.

**Every write path to the tables involved.** Model-driven forms, ribbon and command bar handlers, custom APIs, portals, integrations, bulk operations. Plugins registered on the message cover all of them automatically, which is an argument for server-side logic - but each path still belongs in the test pass.

**The client-side surface.** Which web resource libraries are registered on the form, which handlers are wired to which events, and the existing namespace structure and comment-block layout of the file you plan to extend. New members go where the file's existing organisation says they go.

**Form structure.** Tab and section names, subgrid names, default visibility and expansion. A tab that defaults to hidden or collapsed means controls inside it do not resolve on first load, which shapes any event registration plan.

---

## Risks to analyse explicitly

These recur across engagements and belong in the plan when relevant.

**Widening filtering attributes exposes latent defects.** Existing code written when a step had one filtering attribute may dereference that attribute unguarded, because the Target always carried it. Add a second filtering attribute and the Target often will not. Read the existing handler line by line before widening any filter, and when you find such a defect, note that it is **dormant today and becomes reachable on the change** - then make the sequencing explicit: the guarded assembly deploys *before* the filter widens, never after. Ordering inside a single manual task is easy to get wrong and expensive to debug.

**Rollup logic must cover every message that changes the sum.** Logic on Update alone leaves a total overstated when a child record is deleted, and understated when one is created outside the plan's assumptions. Decide the coverage and record what is deliberately out of scope.

**Recursion and depth.** A synchronous plugin that writes to a related record can re-enter itself. Prefer writing the parent from a child's Post-Operation over chains that write back down, and skip the update when the recalculated value is unchanged - it both avoids a pointless write and dampens loops.

**Synchronous versus asynchronous.** Synchronous means a grid refresh reads already-recalculated values, so no client-side arithmetic is needed. That is often the deciding argument, and it is worth stating rather than leaving implicit.

**Currency.** Money columns carry a `_base` companion the platform maintains. Write the transaction-currency column and say that multi-currency conversion is out of scope, if it is.

**Backfill.** New calculations do not touch existing rows until they are next saved. Either plan a one-off data fix or state that historical rows keep stale values.

---

## Plan document structure

Write the plan as markdown, stored with the work item it implements and named for its id. Use this skeleton:

```markdown
# <id> - <title>: Implementation Plan

## 0. Ground rules
The read-only generated tree, the artefact routing table, and any convention
this plan overrides.

## 1. Solution approach
A capability-to-component table: capability, layer, component, new or existing.
A mermaid data-flow diagram. Then design decisions taken, each with its reason.

## 2. Existing schema (no changes required)
Only what the plan touches, with the extract path as read-only reference.
Note writability and existing rollups. Identify the live form by name and id.

## 3. New functionality
Per component: file path, registration block, logic as numbered steps,
and the test cases it must satisfy.

## 4. Components to modify
Per component: what changes and why. Call out defects found in passing.

## 5. Full component inventory
Files we author and commit. Changes made directly in the environment, each
with its landing solution. Schema names touched, with read or write.
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

- **Manual (`M1`, `M2`, ...)** - anything in a browser or a desktop tool: maker portal edits, plugin step registration, publishing, solution membership, functional testing.
- **Agentic (`A1`, `A2`, ...)** - anything that is a file edit in the repository.

Cross-reference the two, note prerequisites on each, and sequence the tasks in a unified, ordered way. 

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

Number them and keep them in the plan as it evolves. Each needs the assumption, the consequence if it is wrong, and the cost of changing course. Quantify that cost concretely - "one extra condition in the query method" tells a reader how much is at stake in a way that "may require rework" does not.

When one is settled, mark it resolved in place with the date and what was decided, rather than deleting it. The trail of what was considered and rejected is worth more than a clean list.

Distinguish what needs a **business decision** from what needs a **technical confirmation**, and address each to whoever can answer it. Anything that stays open at the end belongs in a documentation task.

---

## Quality bar

Before presenting a plan:

- [ ] Every component is assigned to a channel - authored in source, or changed in the environment
- [ ] No file in the generated solution tree is created, edited, or deleted
- [ ] Every column the plan writes is confirmed writable from the entity XML
- [ ] Existing plugin steps on the affected tables are enumerated with their filters and images
- [ ] Any filter or image change has been checked against the existing handler code
- [ ] The live form is identified by name and id, and other forms are declared out of scope
- [ ] Schema names are literals; no new constants for them
- [ ] Deployment ordering constraints are stated where they exist
- [ ] Manual tasks are numbered, tool-specific, and end in an observable verification
- [ ] Every task carries a status line, set to `Pending.` in a new plan
- [ ] Open points state the consequence and the cost of changing course
- [ ] Findings that are defects or gaps, rather than planned work, are called out as such
