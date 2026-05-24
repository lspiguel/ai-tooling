# D365 CE Context Exporter

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that exports Microsoft Dynamics 365 Customer Engagement (Dataverse) metadata and configuration as Markdown grounding files for use with non-agentic AI assistants (GitHub Copilot, Claude, ChatGPT, etc.).

## What it does

The plugin connects to your Dataverse environment, runs a set of FetchXML and Web API queries defined in a spec configuration file, serialises the results to an intermediate JSON file, and renders a structured Markdown document using an in-process [Scriban](https://github.com/scriban/scriban) template. The resulting `.context.md` file is ready to upload to your AI assistant as grounding context.

<!-- TODO: screenshot — plugin UI overview -->

## Prerequisites

| Requirement | Version |
|---|---|
| XrmToolBox | 1.2025.x or later |
| .NET Framework | 4.8 |

## Quickstart

1. **Install the plugin** — open XrmToolBox, go to _Tool Library_, search for _D365 CE Context Exporter_, and install.
2. **Connect** — connect XrmToolBox to your Dataverse environment.
3. **Pick a base directory** — click the folder picker and choose (or create) an empty folder. On first use the plugin will offer to deploy the reference configuration there.
4. **Run first-time setup** — accept the prompt to deploy sample specs, FetchXML queries, and Scriban templates to the folder.
5. **Select a spec** — choose one of the available specs from the dropdown (six sample specs are provided out of the box).
6. **Click Run** — the plugin executes all queries, renders the template, and places the output in `output\<SpecName>.context.md`.
7. **Upload** — attach or paste the `.context.md` file into your AI assistant conversation as grounding context.

<!-- TODO: screenshot — spec picker and Run button -->

## Sample specs

Six specs are deployed on first-time setup:

| Spec | What it captures |
|---|---|
| `EntityDictionary` | All entity definitions with their attributes |
| `SecurityModel` | Security roles and privilege depths |
| `SolutionsReference` | Solution hierarchy and components |
| `FormsAndViews` | Entity forms and views |
| `Optionsets` | Global option sets |
| `SolutionInventory` | Solutions, plugins, flows, environment variables, custom APIs |

## Folder structure

After first-time setup your base directory will look like this:

```
Context-Exporter/
├── config/
│   ├── queries/                  FetchXML query files
│   │   ├── solutions-detail.fetch.xml
│   │   ├── security-roles.fetch.xml
│   │   └── ...
│   ├── transformations/          Scriban templates (.sbn)
│   │   ├── entity-dictionary.sbn
│   │   ├── forms-and-views.sbn
│   │   ├── optionsets.sbn
│   │   ├── security-model.sbn
│   │   ├── solution-inventory.sbn
│   │   └── solutions-reference.sbn
│   ├── EntityDictionary.context-exporter-config.json
│   ├── FormsAndViews.context-exporter-config.json
│   ├── Optionsets.context-exporter-config.json
│   ├── SecurityModel.context-exporter-config.json
│   ├── SolutionInventory.context-exporter-config.json
│   └── SolutionsReference.context-exporter-config.json
├── output/                       Rendered grounding files (git-ignored)
│   └── EntityDictionary.context.md
├── runs/                         Per-run working directories (git-ignored)
│   └── 20250501-120000/
│       ├── intermediate.json
│       └── output.md
├── LEGAL.md                      Legal notice prepended to all outputs
└── version.txt                   Plugin version that last deployed this config
```

## Plugin upgrades

When the plugin detects that its version differs from the `version.txt` in your base directory, it offers to redeploy the reference configuration (queries, templates, and sample specs). Your `LEGAL.md` and any custom files are never overwritten during an upgrade.

## Configuration reference

Each spec is a `*.context-exporter-config.json` file in `config/`.

| Field | Type | Required | Description |
|---|---|---|---|
| `spec` | string | Yes | Spec name; becomes the output filename stem. |
| `version` | string | No | Config schema version (default `"1.0.0"`). |
| `transformation` | string | Yes | Filename of the Scriban template in `config/transformations/`. |
| `legal` | string | No | Path to a `LEGAL.md` file (relative to base dir) prepended to the output. |
| `output.attributeDenyList` | string[] | No | Attribute name substrings to exclude from output. |
| `frontMatter` | object | No | Key/value pairs injected as YAML front-matter. |
| `queries` | array | Yes | Ordered list of query definitions (see below). |
| `python` | object | No | Ignored. Retained for compatibility with existing config files. |

### Query definition fields

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | Unique identifier; used as the per-query intermediate filename. |
| `type` | string | Yes | `"fetchxml"`, `"webapi"`, or `"metadata"`. |
| `resultKey` | string | Yes | Key name in `intermediate.json`; referenced in templates. |
| `source` | string | fetchxml only | Filename of the FetchXML file in `config/queries/`. |
| `path` | string | webapi only | OData path appended to the environment URL. |
| `select` | string[] | No | OData field names appended as `$select=…` to a webapi path. |
| `metadataTarget` | string | metadata only | One of `entities`, `attributes`, `optionsets`, `relationships`. |
| `maxRecords` | integer | No | Maximum records to retrieve. |

## Authoring templates

Templates use the [Scriban](https://github.com/scriban/scriban) template language (`.sbn` extension). Each `resultKey` from your spec is available as a top-level variable in the template.

1. Create a new `.sbn` file in `config/transformations/` (or copy an existing one as a starting point).
2. Write your template using Scriban syntax. The `resultKey` values from your spec are the top-level objects.
3. Use any of the built-in functions listed below.
4. Add a new spec config in `config/` pointing to your new template.

### Built-in template functions

The following functions are available in all templates:

| Function | Signature | Description |
|---|---|---|
| `schemaname_to_title` | `(name)` | Converts a schema name like `MyCustomField` to `My Custom Field`. |
| `display_label` | `(labelObj, fallback)` | Extracts the user-localised label string from a Dataverse `DisplayName` object. |
| `markdown_table` | `(rows, columns)` | Renders a Markdown table from an array of objects and a column list. |
| `csv_list` | `(items, attr)` | Joins an array into a comma-separated string, optionally extracting a named attribute. |
| `pluck` | `(items, key)` | Returns a deduplicated array of a single attribute extracted from each item. |
| `group_by_key` | `(items, key)` | Groups an array by a key; returns `[{key, items}]`. |
| `optionset_label` | `(options, value)` | Resolves an option set integer value to its localised label. |
| `attr_type_abbrev` | `(type)` | Abbreviates a Dataverse attribute type string (e.g. `Lookup` → `lkp`). |
| `req_indicator` | `(requiredLevel)` | Returns `**R**` for Required, `r` for Recommended, `-` otherwise. |
| `iso_date` | `(dateString)` | Truncates a date-time string to `yyyy-MM-dd`. |
| `component_type_name` | `(code)` | Resolves a solution component type code to a human-readable name. |
| `format_forms` | `(forms)` | Formats a list of form objects as `Name(type), …`. |
| `format_views` | `(views)` | Formats a list of view objects as a comma-separated name list. |
| `entity_forms` | `(forms, entityLogicalName)` | Filters a form array to those belonging to the given entity. |
| `entity_views` | `(views, entityLogicalName)` | Filters a view array to those belonging to the given entity. |
| `plugin_stage` | `(code)` | Resolves a plugin stage integer (10/20/40/45) to `PreVal`, `Pre`, or `Post`. |
| `plugin_mode` | `(code)` | Resolves a plugin mode integer (0/1) to `Sync` or `Async`. |
| `flow_trigger` | `(name)` | Infers the trigger type of a cloud flow from its name (`HTTP`, `Sched`, `Child`, `Manual`, `Auto`). |
| `classic_trigger` | `(workflow)` | Returns the trigger string for a classic workflow object (`Create/Update/Delete`). |
| `envvar_type` | `(code)` | Resolves an environment variable type integer to its type name. |
| `api_param_type` | `(code)` | Resolves a custom API parameter type integer to a short type string. |
| `priv_depth` | `(code)` | Resolves a privilege depth integer to a human-readable depth name. |

## Building from source

```bash
dotnet restore tooling/D365ContextExporter/D365ContextExporter.sln
dotnet build   tooling/D365ContextExporter/D365ContextExporter.sln --configuration Release
dotnet test    tooling/D365ContextExporter/D365ContextExporter.Tests/D365ContextExporter.Tests.csproj
```

For local development, copy the DLL to your XrmToolBox Plugins folder. See `Directory.Build.targets.example` for a template that automates this on build.

## License

MIT — see [LICENSE](../../LICENSE) for details.
