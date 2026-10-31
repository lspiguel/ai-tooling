# Delivery facts — where to find them, how to record them

How to settle each fact of the delivery context (T1) before asking the person (T2). Repositories named here are the client project's: `<client>-Context/` (grounding, stories, engagement documents) and `<client>-d365/` (unpacked solutions and source). Read them; do not commit to them — a person does (T6).

---

## Recorded facts

Search `<client>-Context/` first, then `<client>-d365/`, for a delivery-facts record (see [Recording](#recording)). A record speeds the task up but is not trusted on its own:

- every transport solution it names must appear in `list_solutions` on the environment it names;
- every pipeline it names must exist in the repository;
- a fact older than the last change to the pipeline definitions is re-checked.

A fact that fails a check is asked again, with what changed.

## Segmentation map and transport order

Look next to the solutions in `<client>-d365/`: a file named for the segmentation or the solution map, or the configuration a promotion pipeline reads to validate WIPs. The map gives, per component type, the transport group — and for some types the subtype rule (business rules and business process flows to Base; dashboards to Apps) and, for type codes of 10000 and above, the mapping by name.

| Found | Do |
|---|---|
| A map | Use it. Report types it does not cover when the work meets them |
| No map, but transport solutions exist | Derive the current placement from `list_solution_components` on each transport solution, show it, and recommend writing it down as the map. Until the person confirms, classify only what an existing component's home settles (W2) |
| No map and no transport solutions | Recommend the order below, or a single transport solution for a small project, and let the person decide. A project with one transport solution has a map with one group |

Typical order, imported downstream first to last: shared libraries (their own publisher) · Resources · Base · Assemblies · Processes · Apps · Security. Projects vary; the fixed rule is that a transport solution may depend only on solutions imported before it.

## Transport solutions present

- `list_solutions` with `managed: "unmanaged"` on development, and on the build environment's tab where there is one.
- In `<client>-d365/`, each unpacked solution's `Other/Solution.xml` gives `UniqueName`, the publisher and `Managed`.
- Compare the two lists and report any difference — a transport solution in the repository but missing from the environment, or the reverse.
- A **managed** solution whose publisher is a project transport publisher, found in development, stops the task (R16).

## Transport source of each transport solution

Read the pipeline definitions in `<client>-d365/` — `azure-pipelines*.yml`, `Pipeline/`, `pipelines/`, `.github/workflows/`:

| Look for | Tells you |
|---|---|
| An export step — `pac solution export`, or a build-tools export task — followed by an unpack and a commit | An extraction pipeline. The environment URL or service connection it exports from is the transport source |
| An import into a build environment, a copy into a transport solution, a solution delete | A promotion pipeline (strategy A) |
| An import of managed solutions into test, UAT or production | A deployment pipeline — never run by the agent (T6) |
| `trigger:`, `schedules:`, `pr:`; `trigger: none` | When it runs: on commit, on a schedule, or by hand |

Service connection and variable-group names point at an environment but rarely prove which; when the export source is not explicit, show what was found and ask. Approvals and environment checks are configured outside the YAML; ask about them.

| Source | Close-out strategy available | When an in-place WIP ends |
|---|---|---|
| Development | B | At import and validation |
| Build environment | A | After its promotion |

Strategies C (Git-linked solutions) and D (Power Platform pipelines) are not supported by these skills. If the project uses one, say so and ask how the person wants to proceed.

## Build environment

- A second bound tab whose `devEnvironmentConfirmed` is false and whose organization the pipelines name.
- Exists but is not bound: ask the person to open a second plugin tab on it and leave it unconfirmed (A2).
- None: strategy A is not available.

## Protected prefixes

From the promotion pipeline's parameters or configuration — the prefixes it refuses at the start of a WIP name. Without one, use the project publisher's prefix, partner prefixes visible in `list_publishers`, and `msdyn`, `mscrm`, `adx`, and check names against them (W4).

## Others in the development environment

Ask. The answer decides whether the person is warned before every import (X11) and how hard to look at shared whole documents (X1).

---

## Questions

Ask one fact at a time, or a short numbered list when several are open. Each carries what was found, the recommendation, and what the alternatives change (T2).

```
Two things I could not settle:

1. Protected prefixes. The promotion pipeline is not in this repository. I will check WIP
   names against the project prefix and msdyn/mscrm/adx. Recommendation: confirm, or give
   me the pipeline's list.
2. Others in the development environment. If anyone else works there, every import I run
   publishes their pending changes too. Recommendation: assume yes, and I will tell you
   before each import.
```

---

## Recording

When the person wants confirmed facts kept (T5), write `delivery-facts.md`:

- in `<client>-Context/` when the project has one, in the folder the person names;
- otherwise in `<client>-d365/`, next to the segmentation map.

```markdown
# Delivery facts — Agent Companion MCP

> Confirmed by a person; re-checked against the environments each time they are used.

| Fact | Value | Confirmed | Notes |
|---|---|---|---|
| Development environment | [ClientName] DEV — <organization URL> | YYYY-MM-DD | |
| Build environment | none / [ClientName] BUILD — <organization URL> | YYYY-MM-DD | Bound read-only |
| Segmentation map | <path in the repository> | YYYY-MM-DD | |
| Transport order | Resources, Base, Assemblies, Processes, Apps, Security | YYYY-MM-DD | |
| Transport source — <solution> | development / build | YYYY-MM-DD | <pipeline file> |
| Promotion pipeline | <name> — run by a person | YYYY-MM-DD | Parameters: <names> |
| Extraction pipeline | <name> — trigger | YYYY-MM-DD | |
| Protected prefixes | <list> | YYYY-MM-DD | |
| Shared development environment | yes / no | YYYY-MM-DD | |
```

No secrets, tokens or record data. Then hand the file to the person to commit (T6).
