# D365 CE Context Exporter

An [XrmToolBox](https://www.xrmtoolbox.com/) plugin that exports Microsoft Dynamics 365 Customer Engagement (Dataverse) metadata and configuration as Markdown grounding files for use with non-agentic AI assistants (GitHub Copilot, Claude, ChatGPT, etc.).

## What it does

The plugin connects to your Dataverse environment, runs a set of FetchXML and Web API queries defined in a spec configuration file, serialises the results to an intermediate JSON file, and invokes a Python/Jinja2 post-processor to render a structured Markdown document. The resulting `.context.md` file is ready to upload to your AI assistant as grounding context.

<!-- TODO: screenshot — plugin UI overview -->

## Prerequisites

| Requirement | Version |
|---|---|
| XrmToolBox | 1.2025.x or later |
| .NET Framework | 4.8 |
| Python | 3.11 or later |
| pip packages | see `requirements.txt` |

Install required pip packages once, inside your virtual environment:

```
pip install -r <baseDir>\config\transformations\requirements.txt
```

## Quickstart

1. **Install the plugin** — open XrmToolBox, go to _Tool Library_, search for _D365 CE Context Exporter_, and install.
2. **Connect** — connect XrmToolBox to your Dataverse environment.
3. **Pick a base directory** — click the folder picker and choose (or create) an empty folder. On first use the plugin will offer to deploy the reference configuration there.
4. **Run first-time setup** — accept the prompt to deploy sample specs, FetchXML queries, Jinja2 templates, and Python scripts to the folder.
5. **Install pip packages** — open a terminal in the base directory and run: `pip install -r config\transformations\requirements.txt`
6. **Select a spec** — choose one of the available specs from the dropdown (`EntityDictionary`, `SecurityModel` and `SolutionsReference` provided as samples).
7. **Click Run** — the plugin executes all queries, renders the template, and places the output in `output\<SpecName>.context.md`.
8. **Upload** — attach or paste the `.context.md` file into your AI assistant conversation as grounding context.

<!-- TODO: screenshot — spec picker and Run button -->

## Folder structure

After first-time setup your base directory will look like this:

```
Context-Exporter/
├── config/
│   ├── queries/                  FetchXML query files
│   │   ├── solutions-detail.fetch.xml
│   │   ├── security-roles.fetch.xml
│   │   └── ...
│   ├── transformations/          Jinja2 templates and Python scripts
│   │   ├── entity-dictionary.j2
│   │   ├── security-model.j2
│   │   ├── solutions-reference.j2
│   │   ├── transform.py          Python orchestrator (editable)
│   │   ├── filters.py            Custom Jinja2 filters (editable)
│   │   └── requirements.txt
│   ├── EntityDictionary.context-exporter-config.json
│   ├── SecurityModel.context-exporter-config.json
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

## Configuration reference

Each spec is a `*.context-exporter-config.json` file in `config/`.

| Field | Type | Required | Description |
|---|---|---|---|
| `spec` | string | Yes | Spec name; becomes the output filename stem. |
| `version` | string | No | Config schema version (default `"1.0.0"`). |
| `transformation` | string | Yes | Filename of the Jinja2 template in `config/transformations/`. |
| `legal` | string | No | Path to a LEGAL.md file (relative to base dir) prepended to the output. |
| `python.interpreter` | string | No | `"auto"` (default) or absolute path to `python.exe`. |
| `python.venv` | string | No | Path to a virtual environment; supports `%VAR%` tokens. |
| `output.attributeDenyList` | string[] | No | Attribute name substrings to exclude from output. |
| `frontMatter` | object | No | Key/value pairs injected as YAML front-matter. |
| `queries` | array | Yes | Ordered list of query definitions (see below). |

### Query definition fields

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | Yes | Unique identifier; used as the per-query intermediate filename. |
| `type` | string | Yes | `"fetchxml"`, `"webapi"`, or `"metadata"`. |
| `resultKey` | string | Yes | Key name in `intermediate.json`; referenced in templates. |
| `source` | string | fetchxml | Filename of the FetchXML file in `config/queries/`. |
| `path` | string | webapi | OData path appended to the environment URL. |
| `metadataTarget` | string | metadata | One of `entities`, `attributes`, `optionsets`, `relationships`. |
| `maxRecords` | integer | No | Maximum records to retrieve. |

## Authoring transformations

1. Create a new `.j2` file in `config/transformations/` (or copy an existing one as a starting point).
2. Write your Jinja2 template using the `resultKey` values from your spec as top-level variables.
3. Reference any of the custom filters defined in `filters.py` (e.g. `| markdown_table`, `| schemaname_to_title`).
4. Add a new spec config in `config/` pointing to your new template.

## Editing Python scripts

`transform.py` and `filters.py` live in `config\transformations\` and can be edited in place. They are user-owned once deployed: plugin upgrades will never overwrite them. If you want the latest version of these scripts from a plugin upgrade you must manually delete (or rename) your copies before upgrading.

## Building from source

```bash
dotnet restore tooling/D365ContextExporter/D365ContextExporter.sln
dotnet build   tooling/D365ContextExporter/D365ContextExporter.sln --configuration Release
dotnet test    tooling/D365ContextExporter/D365ContextExporter.Tests/D365ContextExporter.Tests.csproj
```

To pack a `.nupkg`:

```bash
nuget pack tooling/D365ContextExporter/D365ContextExporter/D365ContextExporter.nuspec \
  -Version 1.0.0 \
  -BasePath tooling/D365ContextExporter/D365ContextExporter \
  -OutputDirectory nupkg
```

For local development, copy the DLL to your XrmToolBox Plugins folder. See `Directory.Build.targets.example` for a template that automates this on build.

## License

MIT — see [LICENSE](../../LICENSE) for details.
