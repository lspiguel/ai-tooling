# Platform behaviour in the edit loop

What Dataverse and PAC CLI actually did in the plugin's live checks (spikes S2–S3, Phase 3, Phase 4; PAC CLI 2.14.1, platform 9.2), and what to do about it. None of it is in the documentation in this form.

## Export and unpack

| Behaviour | Do |
|---|---|
| A table added as **metadata** exports its properties and an empty `<attributes />`; a table added as a **shell** exports no table properties | Author a new column into `<attributes>` of a metadata-only table. To edit an existing column, the WIP must hold that column |
| **Every table in a WIP exports its whole unmanaged ribbon diff** — metadata, shell, or a shell the platform added as a parent (X6) | Treat the table's `RibbonDiff.xml` as a whole document. Leave a diff you are not changing exactly as exported |
| `Entity.xml` holds an empty `<RibbonDiffXml />` placeholder; the packager fills it from `RibbonDiff.xml` | Leave both; edit only `RibbonDiff.xml` |
| Form control bindings can be mixed case — `datafieldname="new_Name"` | Compare column names without case (the validator does) |
| Inactive forms export no form XML (X10) | Check `formactivationstate` before shaping |
| A model-driven app can export an empty `AppModuleComponents` although it has components | Compare with `appmodulecomponent` rows before importing; stop if they differ |
| An out-of-box N:N relationship whose intersect table is not customizable is held but not exported | Nothing to edit locally; tell the person |
| A cloud flow unpacks as `Workflows\<Name>-<GUID>.json` plus `.json.data.xml` | Edit only the `.json`, with a flow-editing skill if one is installed; never rename either file |

## Authoring new components

| Behaviour | Do |
|---|---|
| The packager merges a component folder into `Other\Customizations.xml` only where its node exists. A type the export held none of has no node, so a new web resource is zipped as a file but left out of the definition | Add the empty node in schema order — `<WebResources />` after `<optionsets />`, before `<CustomControls />` — plus a `RootComponent` in `Other\Solution.xml`. `Test-UnpackedSolution.ps1` reports `PackagerAnchorMissing` |
| The customization schema bundle predates the platform: an untouched export fails it on `AutoNumberFormat` (columns), `CascadeArchive` (relationships), `EnableCollapsibleGroups` (sitemaps), `OptimizedFor` (apps) and the root's version attributes | Count only finding kinds absent from every baseline; pass a reference export holding the kinds the edit adds |

## Import

| Behaviour | Do |
|---|---|
| `--publish-changes` publishes **every** pending customization in the environment (X11) | Warn the person first in a shared environment |
| Publishing did not change other components' row versions | A baseline taken for one WIP stays valid across another WIP's import |
| A small WIP took 2½ to 4½ minutes to import with publishing; a flow WIP about 2 minutes | Use `--async --max-async-wait-time 20`; never a short command timeout; run one import at a time |
| A failed import leaves the environment unchanged | Stop, fix, validate again — never import the next WIP in the order after a failure |

| Import message | Cause |
|---|---|
| *Cannot add a Root Component … of type 61 because it is not in the target system* | The component is in `Solution.xml` but not in the package's definition — the packager anchor is missing |
| *The dependent component … does not exist. Failure trying to associate it with …* | A requirement is not in the environment yet: an earlier WIP in the order was not imported, or failed (W5) |
| *This solution package cannot be imported because it contains invalid XML* | Schema failure; run the schema check against the packed zip |
