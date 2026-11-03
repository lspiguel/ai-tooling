# Plug-in registration through a WIP

Registering plug-in code — a new build of an assembly, new or changed steps and images, custom APIs with their request parameters and response properties — is an edit like any other: authored in `src\` of the unpacked WIP and imported with PAC CLI (R14, spec §9.10). Neither the plugin nor PAC CLI registers a step directly, and `pac plugin push` would change development outside the WIP and the overwrite check, so it is not used.

**Not yet verified live.** Until it is, the person confirms the first use on a project against the read-back (step 6).

---

## 1. What the WIP must hold before export

| Change | Add to the WIP first (shaping, PAC path) |
|---|---|
| New build of an existing assembly; new plug-in type in it | The assembly (91) |
| New step or image on an existing assembly | The assembly |
| Changed step or image | The step (92) |
| Changed custom API | The custom API (`--componentType customapi`) |
| New assembly, or new custom API | Nothing of its own — it does not exist yet. The WIP needs a component of the Assemblies group to export, usually the project's existing assembly |

## 2. Find a model for every new file (G1)

Copy shapes; never write them from memory. For each kind of file you will add, find an export that already holds one of that type:

1. this WIP's `baseline\`;
2. the project's extract of the Assemblies transport solution in `<client>-d365/` (read-only — copy from it, never edit it);
3. an unmanaged export of the Assemblies transport solution to the work folder (`pac solution export` is read-only).

Take from the model: the folder and file layout, the XML elements and their order, the `RootComponent` entry in `Other\Solution.xml`, and the anchor node in `Other\Customizations.xml`. When the WIP's export held no component of a type, its anchor node is missing and the packager silently leaves the new files out — the same failure as for web resources ([platform-behaviour.md](./platform-behaviour.md)). `Test-UnpackedSolution.ps1` does not check these anchors yet; compare `Other\Customizations.xml` with the model's.

No model anywhere — the project has never registered a custom API, say: tell the person. They choose between providing an example and registering that component by hand in the Plugin Registration Tool, after which it is added to the WIP like any existing component.

## 3. Author

| Change | In `src\` |
|---|---|
| New build | Build the assembly from the repository (Release). Replace the `.dll` under `PluginAssemblies\<name>-<id>\`. Keep the assembly's name, strong-name key and version unless the plan changes them — steps reference the version |
| New plug-in type | A `PluginType` entry in the assembly's `.dll.data.xml`, modelled on the existing ones, with a new ID; the class must be in the new build |
| New step | A file under `SdkMessageProcessingSteps\` named for a new ID, modelled on a step of the same message; set message, table, stage, mode, rank, filtering attributes and the plug-in type from the plan. A `RootComponent` of type 92 |
| New or changed image | Inside its step's file, modelled on an existing image: alias, image type, attributes |
| New assembly | Its `PluginAssemblies\` folder with the `.dll` and `.dll.data.xml`, and a `RootComponent` of type 91 |
| Custom API, request parameters, response properties | Its folder in the model's layout, with the plug-in type that implements it; a `RootComponent` for a new one |

Record every new component in the ledger as **placement** — it has no transport home (W10).

## 4. Before pack — the build keeps every registered type (G2)

1. Run the [plug-in types query](../../agent-companion-wip-shaping/references/queries.md#research--what-already-runs-on-a-table) for the assembly: every plug-in type registered in development, and the steps on each.
2. List the types in the new build (the plug-in classes in the source, or the `PluginType` entries you kept and added).
3. Every registered type must be in the build. A missing one stops the import: a step uses it — possibly another story's, registered from a branch you do not have. Tell the person.
4. If the assembly is in another open WIP (overlap check, G3), that WIP will carry a different build; the person sequences them.

Then validate, pack, run the schema check and the overwrite check as for any WIP (SKILL.md steps 4–6). Plug-in components have no row version, so the overwrite check is the re-export and compare.

## 5. Import

As in SKILL.md step 7. Import messages about plug-in types or missing dependencies mean the build and the `.dll.data.xml` disagree, or a referenced component is not in development; fix `src\` and go back to step 4.

## 6. Read back (G4)

1. `list_solution_components` on the WIP: the assembly, and each new step and custom API, are there (R1).
2. The steps query on the table: each step has the message, stage, mode, rank, filtering attributes and plug-in type the plan gives; the images query: each image has its alias and attributes.
3. The custom API query: binding, bound table and plug-in type as planned.
4. The person confirms the behaviour in the app.
