---
name: agent-companion-wip-close-out
description: Used together with the Agent Companion MCP - the XrmToolBox plugin that gives a coding agent person-approved access to Dynamics 365 CE / Dataverse environments - and needs a plugin tab bound to the development environment (and, for promotion to a build environment, a second tab bound read-only to it). Ends WIP solutions by their route and their destination's close-out strategy - an in-place WIP once its edit has reached the transport source, a placement WIP by adding its components to the transport solution in development or by promotion to a build environment - checking dependencies against the transport order, verifying the transport solution afterwards, preparing (not running) pipeline runs for a person, and deleting the WIP. Also covers aligning development from a build environment and finding leftover WIPs. Use when a WIP solution has been imported and validated, when the person asks to close out a story or a WIP, or when leftover WIP solutions need clearing.
---

# Ending WIP solutions with the Agent Companion MCP

A WIP is never a delivery channel and is never deployed downstream (R11). It ends in one of two ways, decided by its route and by where its destination transport solution is extracted from. This skill carries that out; it does not decide the strategy — `agent-companion-context` derived it — and it does not run pipelines, commits or pushes: it prepares them for the person (T6).

**Preconditions.** The ledger names the WIP, its route, its destination and how it ends; the steps for close-out were stated and confirmed (T3). Without the plugin's tools, stop and say the Agent Companion MCP is needed.

---

## How each WIP ends

|  | Destination extracted from **development** (B) | Destination extracted from a **build environment** (A) |
|---|---|---|
| **In place** — every component already has a transport home | Ends at import and validation: re-check (W10), then delete. Nothing to add; the edit ships with the next extract (X2) | Promoted first, then deleted. The edit reaches transport only through the promotion |
| **Placement** — at least one component has no transport home | Close-out B: add the components to the transport solution in development, verify, delete | Close-out A: promotion, verify on the build connection, delete |

Placement WIPs of one story close out **together, in transport order** (X8). In-place WIPs end as soon as they are ready, independently.

Strategies C (Git-linked solutions) and D (Power Platform pipelines) are not supported by these skills. When the destination uses one, say so and ask the person how to proceed.

When strategies meet in one project — a transport solution extracted from build while someone wants to add to it in development, a build environment added after go-live, a separate development environment for production fixes — check the combination first: [strategies.md](./references/strategies.md#mixing-strategies).

---

## In place

1. **Validated.** The import succeeded, its read-back showed nothing unexpected, and the person confirmed the behaviour (`agent-companion-wip-edit-loop`, step 8).
2. **Re-check (W10).** `list_solution_components` on the WIP, every page. Every component must already have a transport home — the transport-home query in `agent-companion-wip-shaping`'s references, on the transport source (T4). A component without one — authored during the edit — makes the WIP a **placement** WIP: do not delete it; update the ledger, tell the person, and close it out as placement.
3. **Under A**, the WIP is promoted first — the steps under [Close-out A](#close-out-a--promotion). Under B, go on.
4. **Delete** (below).

## Close-out B — add to transport in development

For each placement WIP of the story, in transport order (W5, X8):

1. **Dependencies (W6, X5).** `get_required_components` for the WIP's components. Every requirement must be managed from an installed app, in a transport solution of the same or an earlier group, or in this story's WIP for an earlier group that closes out first. A requirement held only by a later group, or by no solution, stops the close-out.
2. **Add to the transport solution** — the same rules as shaping: MCP for tables (same `tableBehavior` as in the WIP), columns, choices, relationships, forms, views, web resources, sitemaps and apps; PAC for other types, one at a time, never `--AddRequiredComponents`, never a table. A `reason` names the WIP and the work item.

   ```powershell
   pac solution add-solution-component --solutionUniqueName <transport> --component <id or name> --componentType <code>
   ```

   Components already in the transport solution come back `alreadyPresent` — nothing to do.
3. **Verify the transport solution (R1, B3).** `list_solution_components` on it, every page, against the snapshot taken before step 2: only the components added — and parent shells as described in shaping — are new. Anything unexpected would go straight into the next extract; stop.
4. **Delete the WIP** once the whole story's WIPs are done.
5. **Release.** The next extract from development ships the work. If the extraction pipeline runs by hand, prepare the run for the person (T6). Remind them that every edit to components already in transport ships with that extract too (X2).

There is no pipeline validation under B; the map is applied only by this procedure and by the person approving each add (B1).

## Close-out A — promotion

For each WIP to promote — placement WIPs of a story together, in transport order (A3, X8):

1. **Dependencies (A4, W6, X5)** as in B step 1. A missing dependency fails the import into the build environment; the usual cause is a column or web resource created in development but never placed in a WIP.
2. **Name check.** The WIP's name does not start with a protected prefix (W4).
3. **Prepare the promotion run** — the pipeline, the WIP's name, the destination transport solution and any other parameter — and hand it to the person to run (T6). Promotions run one at a time, in order; say whether a commit from one promotion can trigger a build or a deployment before the next (X8).
4. **After each promotion, verify on the build connection** (A1, A2): `list_solution_components` on the destination transport solution — the WIP's components are there with the behaviours they had in the WIP, nothing else is new — and `list_solutions` shows the WIP gone from build. The pipeline's copy step runs outside the plugin's approvals, so this check is not optional.
5. **Delete the WIP in development** (A6).

Promotion validation is the backstop, not the plan (A7): if the pipeline rejects a WIP, fix the cause in development and re-shape — never work around the validator.

## Delete

```powershell
pac solution delete --solution-name <wip>
```

Deleting an unmanaged solution keeps its components. Then `list_solutions` with `nameContains` set to the WIP name returns nothing, and the ledger marks the WIP `deleted`.

## What close-out does not carry

| Not carried | Do |
|---|---|
| **Deletions** (X3) | Deleting a column, view, web resource or step is a separate task a person runs in each environment transport is extracted from, followed by the managed upgrade that removes it downstream. List them in the steps; the plugin never deletes components |
| **Moves between transport solutions** (X4) | Not a close-out. A person plans a dedicated release: the receiving solution must be imported before the losing one is upgraded |
| **Environment variable values, connection references** (X7) | Values are set per environment by deployment settings; reuse the project's connection references |

## Aligning development from build (A8)

Under A, development can be brought in line with build so that it holds the same transport solutions with the same placement. It is a person's operation, not an edit, and is never a managed import (R14, R16). Before the person runs it, list the open WIPs and the whole documents they share with that transport solution — the import replaces them in development with the build copy (X1). The steps are in [strategies.md](./references/strategies.md#aligning-development-from-build).

## Leftover WIPs (X9)

`list_solutions` with `nameContains: "_Agent_WIP_"`. Report WIPs the ledger does not know, or whose work item is closed. Delete only this task's WIPs, and only after their close-out. Another person's WIP is theirs to delete.

---

## Guardrails — what a person confirms

| Moment | The person confirms |
|---|---|
| Ending an in-place WIP | Every component already has a transport home; under A it has been promoted (W10) |
| Before close-out | All of the story's placement WIPs, in transport order; the strategy fits each destination; no dependency points up the order; deletions are listed for a person (W5, W6, X3, X8) |
| Each pipeline run, commit and push | The person runs it, or delegates that specific one (T6); no WIP is deployed downstream (R11) |
| After close-out | The transport solution — in development, or on the build connection — shows exactly what was intended (R1) |
| Aligning development | Unmanaged export from build, no local edits, open WIPs' shared documents reviewed (A8, R14, R16) |
