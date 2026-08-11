---
name: power-automate-flow-editing
description: Edits Power Automate cloud flows as source, using the Power Platform CLI to export a work-in-progress solution, unpack it, edit the flow definition JSON with agentic help, validate it offline, then pack and import it back. Use when changing a cloud flow's actions, triggers, expressions, run-after graph, Dataverse OData filters, or FetchXML - or when a flow is too cumbersome to build in the designer because of Workflow Definition Language expression syntax, action reference naming, or OData filter syntax.
---

# Editing Power Automate Cloud Flows as Source

The designer is a poor editor for the parts of a flow that are actually hard: `@` versus `@{}`, action names that silently become underscore-encoded identifiers, `runAfter` graphs, OData `$filter` strings, and null-safe property access. Those are text problems, and they are far easier to solve in the definition JSON.

This skill is the round trip that makes that safe: **export a WIP solution, unpack, edit the JSON, validate, pack, import.** Transport is the Power Platform CLI throughout. Nothing here writes to Dataverse except `pac solution import`.

---

## Why the solution round trip and not a direct write

There is a faster-looking path — PATCH the `clientdata` column on the `workflow` row. Do not use it. The solution round trip is chosen deliberately:

| | Solution round trip (this skill) | Direct `clientdata` write |
|---|---|---|
| **Platform validation** | Solution import validates the definition and fails loudly | None; a malformed definition can be saved |
| **Connection references** | Carried in the solution, bound on import, settable via a settings file | Must be hand-assembled and hand-bound |
| **Durability** | Is the ALM channel | Overwritten by the next solution import |
| **Rollback** | The previously exported zip is the rollback artefact | Requires having saved the original string |
| **Failure mode** | Import fails, environment unchanged | Flow can be left deactivated mid-sequence |

The full option landscape, including the direct path and why it exists, is in [deployment-options.md](./references/deployment-options.md).

---

## Preconditions — check all of these before touching anything

| Check | Command or action | Why it blocks |
|---|---|---|
| An authenticated profile is active | `pac auth list` then `pac env who` | An expired refresh token surfaces as `AADSTS700082`; re-run `pac auth create --environment <url>` |
| The target is a **development** environment | `pac env who` | This loop imports unmanaged customizations. Never point it at production |
| The flow lives in an **unmanaged** solution | `pac solution list` | Flows delivered in a managed solution cannot be edited this way; change them upstream and promote |
| The solution is a **WIP solution**, not the delivery solution | Ask the user | Keeps the blast radius to the flow being edited |
| The flow is **not** in "My Flows" | Maker portal | Non-solution flows have no solution to export |

If the user has no WIP solution yet, have them create one in the maker portal, then add the flow:

```powershell
pac solution add-solution-component --solutionUniqueName <wip_unique_name> --component <flow-guid> --componentType 29 --AddRequiredComponents
```

`29` is the Workflow component type. Other component type values are in the [solutioncomponent table reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/solutioncomponent).

**Never edit the repository's own extracted solution tree.** This loop works in a scratch folder outside the repository. The extract catches up when the environment is next exported by its own pipeline.

---

## The loop

### 1. Export

```powershell
pac solution export --name <wip_unique_name> --path .\wip\solution.zip --overwrite
```

Keep that zip. It is the rollback artefact — re-importing it restores the pre-edit state.

### 2. Unpack

```powershell
pac solution unpack --zipfile .\wip\solution.zip --folder .\wip\src --packagetype Unmanaged --allowWrite --allowDelete --clobber
```

Then take a pristine copy of `.\wip\src` before editing. Diffing against it at step 5 is what proves only the flow changed.

```powershell
Copy-Item .\wip\src .\wip\src.orig -Recurse
```

### 3. Locate the flow

Each cloud flow unpacks into a pair of files under `Workflows\`:

| File | Contents | Edit? |
|---|---|---|
| `<Name>-<GUID>.json` | The flow definition — `properties.definition` and `properties.connectionReferences` | **Yes** |
| `<Name>-<GUID>.json.data.xml` | Workflow metadata — name, category, state, primary entity | Only to change the flow's name or state |

**Never rename either file or change the GUID.** The GUID is the flow's identity; a renamed file imports as a different flow and leaves the original behind.

The anatomy of the definition, and what inside it is safe to change, is in [flow-json-anatomy.md](./references/flow-json-anatomy.md).

### 4. Edit

This is the agentic step. Read [expression-and-odata-syntax.md](./references/expression-and-odata-syntax.md) before writing any expression — it covers the four failure classes that account for most broken flows: `@` versus `@{}`, underscore-encoded action references, `runAfter` scoping, and Dataverse OData filters.

Rules for the edit itself:

- **Do not touch `connectionReferences`.** The logical names bind to connections in the target environment. Rewriting them breaks the binding, and a new connection cannot be invented in JSON.
- **Do not touch environment variable schema names** for the same reason.
- **Do not reformat the file.** Change only the lines the task requires, so the diff at step 5 is readable.
- **Renaming an action is a multi-site edit.** The JSON key, every `runAfter` entry that names it, and every `body('...')` / `outputs('...')` reference must move together. Prefer not renaming.
- **Do not edit any other component** in the unpacked tree.

### 5. Validate offline

```powershell
.\scripts\Test-FlowDefinition.ps1 -Path .\wip\src
```

The script parses the definition and checks action reference resolution, the `runAfter` graph, expression delimiter balance, and connection reference declarations. It needs no authentication. Fix everything it reports as `Error` before packing.

Findings come in three severities. `Error` blocks the pack. `Warning` is worth reading. `Info` is advisory — on a real seven-flow solution it fires 22 times for the string-coercion pattern the designer emits constantly, so it is not a defect signal.

Then confirm the blast radius against the pristine copy:

```powershell
.\scripts\Compare-SolutionTree.ps1 -Reference .\wip\src.orig -Difference .\wip\src
```

Only the one flow JSON should differ. Anything else means an unintended edit.

**Do not use `robocopy /L /MIR` for this.** It compares timestamps and size, so a re-extracted tree reports every file as changed. `Compare-SolutionTree.ps1` compares SHA256 content. A pack followed by an unpack is content-stable — verified byte-for-byte across a 42-file solution — so any difference it reports is a real edit.

> The validator catches structural and reference errors. It cannot evaluate an expression, confirm a column exists, or know whether the logic is right. It reduces the number of failed imports; it does not replace the import.

### 6. Pack

```powershell
pac solution pack --zipfile .\wip\solution-updated.zip --folder .\wip\src --packagetype Unmanaged
```

Pack failures are almost always malformed JSON or a renamed file, not a logic problem.

### 7. Import

```powershell
pac solution import --path .\wip\solution-updated.zip --activate-plugins --publish-changes --force-overwrite --async --max-async-wait-time 20
```

| Flag | Why it is here |
|---|---|
| `--activate-plugins` | Activates workflows on import; without it the edited flow can land deactivated |
| `--publish-changes` | Publishes customizations after import |
| `--force-overwrite` | Required — the flow already exists as an unmanaged customization |
| `--async` + `--max-async-wait-time` | Import can outlive the default synchronous window |

> **`--activate-plugins` acts on the whole solution, not just your flow.** A WIP solution routinely contains flows that are deliberately in Draft — in a real seven-flow solution, three were `statecode 0`. Record every flow's state before importing, and check them after:
>
> ```powershell
> Get-ChildItem .\wip\src\Workflows\*.json.data.xml | ForEach-Object {
>     [xml]$x = Get-Content $_.FullName -Raw
>     [pscustomobject]@{ Flow = $x.Workflow.Name; State = $x.Workflow.StateCode }
> }
> ```
>
> `0` is Draft, `1` is Activated. This is the strongest argument for keeping the WIP solution down to the flow being edited: it makes the flag's blast radius exactly one flow.

If connection references need explicit binding, generate and fill a settings file first:

```powershell
pac solution create-settings --solution-zip .\wip\solution-updated.zip --settings-file .\wip\settings.json
pac solution import --path .\wip\solution-updated.zip --settings-file .\wip\settings.json --activate-plugins --publish-changes --force-overwrite
```

Import is slow enough to matter: a **single-flow** solution took 2 minutes 15 seconds. Expect longer for a real one, and do not run this on a two-minute command timeout.

**Import failure is a normal outcome of this loop, not an exception.** Read the error, fix the JSON, repack, re-import. The environment is unchanged on a failed import.

### 8. Verify what actually landed

Do not trust "import succeeded". Export again and diff:

```powershell
pac solution export --name <wip_unique_name> --path .\wip\verify.zip --overwrite
pac solution unpack --zipfile .\wip\verify.zip --folder .\wip\verify --packagetype Unmanaged --allowWrite --allowDelete --clobber
```

```powershell
.\scripts\Compare-SolutionTree.ps1 -Reference .\wip\src -Difference .\wip\verify
```

Expect the edited flow file to differ and nothing else. Normalization is limited to **JSON formatting**, so the diff stays readable — on a verified round trip the only change was an inline array being expanded:

```diff
-            "Compose": [ "Succeeded" ]
+            "Compose": [
+              "Succeeded"
+            ]
```

Expression strings came back byte-identical, including a whole-value `@concat(...)` and an interpolated `@{outputs('Compose')}` cross-action reference. Write arrays in the expanded style and the round trip is diff-free.

A hand-written action needs no `metadata.operationMetadataId` — one was imported without it and the platform neither rejected it nor injected one. Only the connector `host` block must be copied from a designer-created action.

Then confirm in the environment: the flow is **On**, and a test run reaches the changed action and succeeds. A flow that imports cleanly and fails at runtime is the common case for expression errors, because expressions are evaluated at run time, not at import.

### 9. Roll back if needed

```powershell
pac solution import --path .\wip\solution.zip --activate-plugins --publish-changes --force-overwrite
```

---

## Guardrails — what a human must confirm

| Before | The human confirms |
|---|---|
| Step 1 | The environment is a development environment, and the solution is the WIP solution, unmanaged |
| Step 1 | The current activation state of **every** flow in the solution is recorded, not just the one being edited |
| Step 4 | Which flow, by name and GUID — the folder may hold several |
| Step 6 | The diff touches only the intended flow file |
| Step 7 | Import into this environment is acceptable now — it publishes and activates |
| Step 8 | The test run passes. Import success is not runtime success |
| Close | The change is carried into the delivery solution; a WIP solution is not a delivery channel |

---

## Failure reference

| Symptom | Cause |
|---|---|
| `AADSTS700082` on any pac command | Auth profile's refresh token expired; `pac auth create` again |
| Pack fails on the workflow file | Malformed JSON, or the file or its GUID was renamed |
| Import fails on connection references | Referenced connection does not exist in the target environment; create it, or supply a settings file |
| Import succeeds, flow is Off | `--activate-plugins` omitted, or the flow was already deactivated before export |
| Import succeeds, run fails at an action | Expression error — evaluated at run time, not import time. Read the run history's failed action input |
| Changes vanish after a later deployment | The edit was made in the WIP solution only and never carried into the delivery solution |
| Managed solution error on import | The flow is delivered managed; it cannot be edited here |
