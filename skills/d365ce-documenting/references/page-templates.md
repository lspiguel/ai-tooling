# Page templates

Structures for the default artefact set — the functional/technical pair. A combined design document merges them into one page, the functional structure first, keeping the audience separation between the two halves. Larger model sets agreed with the user still hang off these two: additional views attach to the technical page's component map and runtime sequence. Adjust heading names to match the wiki's existing pages; keep the order and the split. Link paths follow the wiki's own convention — see [wiki-mechanics.md](wiki-mechanics.md).

## Functional page structure

```markdown
# [Feature] functionality

One paragraph: what it is, who initiates it, what it produces.

For implementation details, see [[Feature] - technical](<link per wiki convention>).

<in-page table of contents, if the wiki supports one>

## Who does what
Role, where they work, what they do.

## Prerequisites / eligibility
Why the thing a user is looking for might not be there. This is the section
support will use most.

## <Doing the task>
Numbered steps in the order a user performs them.

## What happens next
System behaviour described in outcomes, not components.

## <Reference tables>
Fields and their meaning. Commands and what each one does. Statuses.

## Rules and restrictions
What is enforced, when, and what the user sees when it is.

## Things to be aware of
Known behaviours and limitations, stated plainly.

## Troubleshooting
Symptom, likely cause, what to do.

## Related documentation
```

## Technical page structure

```markdown
# [Feature] - technical

One paragraph of scope.

For end-user behaviour, see [[Feature] functionality](<link per wiki convention>).

<in-page table of contents, if the wiki supports one>

## Purpose
What the feature does, in data terms: which records, which columns, which limit.

## Component map
Diagram, one grouping per layer.

## Solution / source placement
Component | Authored source | Solution extract.
Close with a note that the extract is generated and hand-edits are overwritten.

## Data model
Per table: ownership, primary name, every custom column with type and source
type. Option set values with labels. Relationships with their schema names.

## <Form / UI surface>
The live form by name and id. Tabs, sections, controls, default visibility.
Subgrid configuration. Registered libraries and event handlers.

## Server-side components
Registration table for every step: type, message, entity, stage, mode, rank,
filtering attributes, images. Then per component: what it does and why.

## Client-side components
Per file: namespace, members table, wiring, Web API calls quoted verbatim.
Command bar table: label, button and command id, library, function, enable rule.

## Automations
Per flow or workflow: trigger, filter, conditions, what it writes.

## <Integration / portal layer>
Endpoints, environment variables, and what sits behind each name.

## <Configuration that is not a solution component>
Queues, teams, categories, routing rules — things reproduced by hand per
environment. Say that GUIDs differ and to link by name.

## Runtime sequence
Sequence diagram for the main path.

## Known gaps and things to watch
Numbered. Deployment ordering constraints go here.

## Work items
The work item URLs this page documents, as a list. Append on every
documentation pass; never replace the existing entries. This is the
cross-reference from implemented stories and tasks to the live
documentation of the current system.

## Related pages
```

## What the technical page must not omit

**Option set collisions.** Two tables reusing the same numeric values with different labels is the single most error-prone thing a reader can hit. Put the mapping in a table near the top of the data model section and say plainly that it is error-prone.

**Source type on every column.** A reader needs to know at a glance which columns are plain and written by code, and which are platform rollups. Note where a rollup's filtering differs from a plugin's — an unfiltered rollup beside a status-aware calculation is a real inconsistency worth surfacing.

**Which form is live.** Name it and give its id, and say that the other forms in the extract are out of scope. A table carrying dozens of main forms is normal.

**Where the same control appears twice.** Two controls bound to one column on two tabs, locked by different mechanisms — one declaratively, one at runtime — is exactly the sort of thing that looks like a bug to the next person. Explain why both exist.

**Deployment ordering constraints.** Where a sequence is forced — register the image, then deploy the assembly, then widen the filter — state it as a sequence and say what breaks if it is reversed.

**Defects and gaps found in passing.** A component registered in the environment but never added to a solution, a flow exporting an empty update step, registration documentation that contradicts the registered step. These belong in Known gaps, phrased as observations from the extract with a note to verify the live definition, not as accusations.
