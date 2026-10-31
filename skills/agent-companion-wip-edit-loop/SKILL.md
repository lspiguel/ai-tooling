---
name: agent-companion-wip-edit-loop
description: Used together with the Agent Companion MCP - the XrmToolBox plugin that gives a coding agent person-approved access to Dynamics 365 CE / Dataverse environments - and needs a plugin tab bound to the development environment. Runs the local edit loop for a shaped WIP solution - PAC export and unpack (XML layout) into a work folder outside every repository, a baseline of component versions, local edits to forms, views, ribbon diffs, sitemaps, web resources and metadata, offline validation of forms, ribbon references and the customization schema, the pre-import overwrite check against the baseline, then pack and import one WIP at a time in transport order. Use when a WIP solution created through the Agent Companion MCP holds its components and they need editing, or when an edited WIP is ready to validate and import.
---

# The WIP edit loop with the Agent Companion MCP

**Export → unpack → baseline → edit → validate → pack → overwrite check → import.** PAC CLI moves the solution; the plugin supplies the component versions that make the import safe; the scripts in [scripts/](./scripts/) check the edit offline. The platform's import is the final validation.

**Preconditions.**

- The WIP is shaped and read back clean (`agent-companion-wip-shaping`), and is in the ledger.
- The PAC profile targets the plugin connection's organization (`pac org who` against `get_connection_info`). Check again before the first PAC command of each session.
- Without the plugin's tools, stop and say the Agent Companion MCP is needed.

**Never edit a transport solution.** Edits reach development only through a WIP holding just the components being changed. Never pack and import an unpacked transport solution — from the repository or anywhere else (R14).

---

## The decisions this skill owns

| Decision | Rule |
|---|---|
| Import order | One WIP at a time, in transport order (W5) |
| Import flags | `--publish-changes`; add `--async --max-async-wait-time 20` for a large WIP. A WIP whose edit is a cloud flow handled by a flow-editing skill uses that skill's import step and flags (K7) |
| The overwrite check's outcome | Any difference stops the import until the person decides |
| What counts as validated | The import succeeded, the read-back shows nothing unexpected, and the person confirmed the behaviour |

---

## Work folder

Keep everything for a WIP in one folder **outside every Git working tree** — the session's scratch or temp folder, or one the person names (R7). Check before writing:

```powershell
git -C <folder> rev-parse --is-inside-work-tree   # must fail
```

```
<work>\<wip>\
  export.zip          the WIP as exported — the rollback artefact
  baseline\           unpack of export.zip, never edited
  src\                the working copy
  state-baseline.json get_component_state right after export
  columns.json        evidence for validation
  ribbon-<table>.xml  base ribbon, reassembled
  updated.zip         the packed edit
  recheck\            unpack of a re-export, for the overwrite check
  state-now.json      get_component_state just before import
```

---

## The loop

Run it per WIP. With several WIPs, edit them in any order but **import them in transport order** (W5).

### 1. Export and unpack

```powershell
pac solution export --name <wip> --path <work>\<wip>\export.zip --overwrite
pac solution unpack --zipfile <work>\<wip>\export.zip --folder <work>\<wip>\baseline --packagetype Unmanaged --allowWrite --allowDelete --clobber
Copy-Item <work>\<wip>\baseline <work>\<wip>\src -Recurse
```

The XML layout of `pac solution unpack` is the only format these skills edit (R5).

### 2. Baseline

Right after export, `list_solution_components` on the WIP gives every row-backed component — views `26`, forms `60`, web resources `61`, sitemaps `62`, app modules `80`. Call `get_component_state` for them (up to 200 per call) and save each result's structured content, unchanged, as `state-baseline.json` (or several files). Table and column metadata have no row version; the `baseline\` unpack is their baseline. Mark the WIP `baseline taken`.

### 3. Edit

Edit in `src\` only, and only the components the change needs.

| Component | Where it is | Notes |
|---|---|---|
| Form | `Entities\<Table>\FormXml\<type>\{formid}.xml` | Keep `formid`, and every existing tab, section, cell and control ID. New IDs are new GUIDs |
| View | `Entities\<Table>\SavedQueries\{savedqueryid}.xml` | FetchXML and layout XML must agree on columns |
| Ribbon (classic only, R4) | `Entities\<Table>\RibbonDiff.xml`; the application ribbon in `Other\Customizations.xml` | The export holds only the diff. Read the base with `get_entity_ribbon` or `get_application_ribbon`, concatenate every page in order, save it as `ribbon-<table>.xml`. Ask for `form`, `homepageGrid` and `subGrid` one at a time when the whole ribbon times out |
| Sitemap, app module | `AppModuleSiteMaps\`, `AppModules\` | |
| Web resource | `WebResources\…` with its `.data.xml` | Edit the source copy if the project keeps one, then copy it in |
| Table and column metadata | `Entities\<Table>\Entity.xml` | Only for a table added as `metadata`, or a column in the WIP |

**Anything new authored here** — a column added to `Entity.xml`, a new web resource — is a new component with no transport home. Add it to the ledger as **placement**: the WIP can no longer end as in place (W10), and the next read-back after import will show it. Tell the person.

A table in the WIP — even a shell — carries its whole unmanaged ribbon diff and imports it over development's (X6). Leave a diff you are not changing exactly as exported.

### 4. Validate offline

Build the evidence first. It turns *not verified* warnings into real checks:

- `columns.json` — `{ "<table>": ["<column>", …] }` from `get_table_metadata` for each table whose forms are edited;
- known web resources — names from a `query` over `webresource` for the libraries the forms and ribbons reference;
- the base ribbon file(s) from step 3.

```powershell
.\scripts\Test-UnpackedSolution.ps1 -Path <work>\<wip>\src -KnownColumnsPath <work>\<wip>\columns.json `
    -KnownWebResources 'new_/account/form.js' -BaseRibbonPath <work>\<wip>\ribbon-account.xml
.\scripts\Compare-SolutionExport.ps1 -Baseline <work>\<wip>\baseline -Current <work>\<wip>\src
```

`Test-UnpackedSolution.ps1` checks that every file parses; that form IDs are GUIDs, unique and matched to file names; that controls bind to columns of the form's table and handlers to the form's libraries; and that every ribbon command, rule, web resource and label reference resolves. Fix every `Error` before packing. `Compare-SolutionExport.ps1` against `baseline\` shows the blast radius: only the files the change needed may differ.

### 5. Pack and check the schema

```powershell
pac solution pack --zipfile <work>\<wip>\updated.zip --folder <work>\<wip>\src --packagetype Unmanaged
.\scripts\Test-CustomizationsSchema.ps1 -PackagePath <work>\<wip>\updated.zip -SchemaPath <schemas> -BaselinePackagePath <work>\<wip>\export.zip
```

The schema check needs Microsoft's customization schema bundle (CustomizationsSolution.xsd and the files it includes)[^1] — not shipped here. Ask the person where it is kept; if there is none, say the schema check was skipped. The bundle is older than the platform, so only findings the edit **introduced** — absent from the untouched `export.zip` — fail the check.

### 6. Overwrite check — just before import

An unmanaged import replaces each whole document with the one in the WIP. Anything changed in development since the export would be lost.

1. `get_component_state` again for the same components; save as `state-now.json`.

   ```powershell
   .\scripts\Compare-ComponentState.ps1 -Baseline <work>\<wip>\state-baseline.json -Current <work>\<wip>\state-now.json
   ```

   `Changed`, `Deleted` or `NotChecked` stops the import.
2. If the WIP holds anything without a row version — any table (its properties and ribbon diff), columns, choices, relationships, or types `get_component_state` does not accept, such as cloud flows and plug-in steps — re-export and compare the whole tree:

   ```powershell
   pac solution export --name <wip> --path <work>\<wip>\recheck.zip --overwrite
   pac solution unpack --zipfile <work>\<wip>\recheck.zip --folder <work>\<wip>\recheck --packagetype Unmanaged --allowWrite --allowDelete --clobber
   .\scripts\Compare-SolutionExport.ps1 -Baseline <work>\<wip>\baseline -Current <work>\<wip>\recheck
   ```

   Since nearly every WIP holds at least a table shell, expect to run this every time. `-Include` narrows the comparison when only some files matter, for example `'Entities\*\Entity.xml','Entities\*\RibbonDiff.xml'`.

On any difference: stop, tell the person what changed and who changed it (`modifiedBy`, where there is one), and merge — export again, re-apply the edit to the new copy, and repeat from step 2. Never import over it without the person's decision.

### 7. Import

- **Shared development environment:** tell the person first. `--publish-changes` publishes every pending customization in the environment, not only this WIP's (X11).
- One WIP at a time, in transport order (W5).

```powershell
pac solution import --path <work>\<wip>\updated.zip --publish-changes
```

Add `--async --max-async-wait-time 20` for a large WIP, and do not run it on a short command timeout. An import failure leaves the environment unchanged: read the error, fix `src\`, and go back to step 4.

### 8. Read back and verify

1. `list_solution_components` on the WIP (R1). New components authored in step 3 are expected now; anything else is not.
2. Optionally export again and compare with `src\` to see what landed and what the platform normalized.
3. The person checks the behaviour in the app — the import's success is not the change's success.

Mark the WIP `validated` only after the person confirms, then hand to `agent-companion-wip-close-out`.

### Roll back

Re-import `export.zip`. It is an unmanaged import of whole documents too: run the overwrite check against the current state before rolling back, so a rollback does not discard someone else's later change.

---

## Guardrails — what a person confirms

| Moment | The person confirms |
|---|---|
| Before editing | The work folder is outside every repository; the WIP is the one in the ledger |
| After validation | The blast radius shows only the intended files |
| Overwrite check | Passed — or the difference was merged on their decision |
| Before each import | Import now is acceptable; in a shared environment, others' pending changes will be published (X11) |
| After import | The read-back is as expected and the change behaves as intended |

[^1]: Microsoft Learn — *Edit the customizations XML file with schema validation*, <https://learn.microsoft.com/power-apps/developer/model-driven-apps/edit-customizations-xml-file-schema-validation> (checked 2026-10-31). Supports: the schema bundle and its download, and that CustomizationsSolution.xsd references the other files in the same folder.
