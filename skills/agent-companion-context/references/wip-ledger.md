# The shared record — delivery context, steps and WIP ledger

One shape that every agent-companion skill reads and updates (K5). It lives in the conversation; where the person wants it kept, it goes into the task's implementation plan in `<client>-Context/` — written by the agent, committed by a person (T6). The environment always wins over it (K6).

---

## Delivery context

```markdown
### Delivery context
| Fact | Value | Source |
|---|---|---|
| Development connection | <connectionId> — [ClientName] DEV | list_connections; confirmed by the person |
| PAC profile | matches | pac org who |
| Build connection | none / <connectionId> (unconfirmed) | |
| Segmentation map | <path> | <client>-d365 |
| Transport order | Resources › Base › … | map |
| Transport source | Base: development · Apps: build | <pipeline file> / person |
| Pipelines | promotion <name>, extraction <name> — run by a person | <pipeline files> |
| Protected prefixes | <list> | |
| Work item | <id> | task |
| Shared development environment | yes / no | person |
```

Source says where the fact came from: a file, a tool, or "person". A fact the person answered is marked so.

## Steps

Stated before the first Write call and before close-out (T3); restated whenever they change.

```markdown
### Steps — confirmed <date> <time>
1. Create XY_Agent_WIP_1234_Resources and XY_Agent_WIP_1234_Base under <publisher> (development).
2. Add: Resources ← new_/account/form.js (placement); Base ← account (shell), account main form (in place), new_rating (placement).
3. Export, unpack, baseline; edit; validate; overwrite check.
4. Import Resources, then Base (W5). Development is shared: each import publishes others' pending changes (X11).
5. Close-out by B: Resources then Base. A person runs no pipeline; the next scheduled extract ships them.
6. Delete both WIPs after the close-out read-back.
Who runs what: every pipeline run, commit and push — <person>.
```

## Components

One row per component the task touches.

```markdown
### Components
| Component | Type | Id | Group | Transport home | Route | WIP |
|---|---|---|---|---|---|---|
| account | 1 Entity | <guid> | Base | <Base transport> | in place | XY_Agent_WIP_1234_Base (shell) |
| account main form "[FormName]" | 60 | <guid> | Base | <Base transport> (under account, include-all) | in place | XY_Agent_WIP_1234_Base |
| new_rating | 2 Attribute | <guid> | Base | — | placement | XY_Agent_WIP_1234_Base |
```

## WIPs

```markdown
### WIPs
| WIP | Connection | Group | Route | Ends by | State |
|---|---|---|---|---|---|
| XY_Agent_WIP_1234_Resources | <id> | Resources | placement | close-out B | imported |
| XY_Agent_WIP_1234_Base | <id> | Base | placement | close-out B | baseline taken |
```

- **Route** is *placement* when any component in the WIP has no transport home, otherwise *in place* (W9).
- **Ends by** is one of: *import and validation* (in place, source development) · *promotion* (in place or placement, source build) · *close-out B* (placement, source development).
- **State** moves forward only: `planned` → `created` → `shaped` (read back clean, R1) → `baseline taken` → `edited` → `imported` → `validated` → `closed out` / `promoted` → `deleted`. A state that has to go back — a failed import, a stopped overwrite check — is noted next to it.

## Reconcile

When a skill enters a task already under way (K6):

1. `list_solutions` with `nameContains: "_Agent_WIP_<work item id>"` on the development connection. Every WIP in the ledger must be there unless its state is `deleted`; any WIP there but not in the ledger is added, after asking the person whose it is.
2. `list_solution_components` on each open WIP, every page. Update the Components table; a component the ledger does not list — added in the maker portal, or authored during an edit — is classified and routed before anything else happens (it may change the WIP's route, W10).
3. For a WIP marked `imported` or later, nothing in the environment says whether the person validated it; ask.
4. Under strategy A, check the build connection: a promoted WIP's components are in the destination transport solution, and the WIP itself is gone from build.

State the differences found, then continue.
