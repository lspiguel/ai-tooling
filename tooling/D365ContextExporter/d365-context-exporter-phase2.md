# Phase 2 — Query Execution: Detailed Implementation Plan

## What Phase 2 Delivers

Phase 2 wires up the data-acquisition half of the pipeline. The exit criterion is: given a loaded `ExportJob`, the plugin connects to Dataverse, executes every query defined in the config, writes one `output.<queryId>.fetch.json` per query, assembles `intermediate.json`, and places all of these under a timestamped `runs/<timestamp>/` folder. No Python invocation yet — that is Phase 3.

The stub in `D365ContextExporter/Orchestration/ExportJobRunner.cs` will become real.

---

## New Source Files

### `Queries/FetchXmlQueryRunner.cs`

**Responsibility:** Execute a single `QueryDefinition` of type `"fetchxml"` against Dataverse using `IOrganizationService.RetrieveMultiple`, with transparent automatic paging.

**What it does:**
1. Reads the `.fetch.xml` file at the path resolved by `PathResolver.Resolve(query.Source, baseDir)`.
2. Injects `count` and `page` attributes into the FetchXML root element to enable server-side paging.
3. Loops until `EntityCollection.MoreRecords` is false, accumulating all pages into a single `List<Entity>`.
4. Stops early if `query.MaxRecords` is set and the accumulated count reaches it.
5. Returns an `IReadOnlyList<Entity>`.

**How configuration drives it:**
- `query.Source` → the `.fetch.xml` filename; resolved relative to `config/queries/` via `PathResolver`.
- `query.MaxRecords` → if non-null, stops paging when this many records have been accumulated.
- `query.Id` → used only for logging ("Executing fetchxml query '{id}'...").

**Dependencies (incoming):** Called from `ExportJobRunner` for each `QueryDefinition` where `Type == "fetchxml"`.
**Dependencies (outgoing):** `IOrganizationService`, `PathResolver`, `Microsoft.Xrm.Sdk.Query.FetchExpression`.

---

### `Queries/WebApiQueryRunner.cs`

**Responsibility:** Execute a single `QueryDefinition` of type `"webapi"` via the Dataverse Web API using `HttpClient`, reusing the bearer token already held by the current `ServiceClient`.

**What it does:**
1. Extracts the bearer token from the `ServiceClient` (via `AccessToken` property or re-acquire through the client's auth manager).
2. Builds the OData request URL: `<environmentUrl>/api/data/v9.2/<query.Path>`. Appends `$select` if `query.Select` is non-empty.
3. Sends a `GET` request with `Accept: application/json; odata.metadata=minimal` and `OData-MaxVersion: 4.0`.
4. Handles OData `@odata.nextLink` pagination, accumulating all pages into a single `JArray`.
5. Does **not** strip OData metadata annotations (`@odata.*` keys) from each record.
6. Respects `query.MaxRecords` as a page-accumulation cap.
7. Returns a `JArray` (raw JSON, already well-typed by the Web API).

**How configuration drives it:**
- `query.Path` → the OData resource segment (e.g. `"EntityDefinitions"`, `"GlobalOptionSetDefinitions"`).
- `query.Select` → optional list of fields for `$select` (keeps payloads small; avoids pulling every metadata column).
- `query.MaxRecords` → caps total accumulated records.

**Dependencies (incoming):** Called from `ExportJobRunner` for each `QueryDefinition` where `Type == "webapi"`.
**Dependencies (outgoing):** `System.Net.Http.HttpClient`, `Newtonsoft.Json.Linq.JArray`, `Microsoft.PowerPlatform.Dataverse.Client.ServiceClient` (for token extraction).

---

### `Queries/MetadataQueryRunner.cs`

**Responsibility:** Execute a single `QueryDefinition` of type `"metadata"` using the typed Dataverse metadata API (`RetrieveAllEntitiesRequest`, `RetrieveOptionSetRequest`, etc.).

**What it does:**
1. Dispatches based on `query.MetadataTarget` (a new sub-field on `QueryDefinition`): one of `"entities"`, `"attributes"`, `"optionsets"`, `"relationships"`.
2. For `"entities"`: calls `RetrieveAllEntitiesRequest` with `EntityFilters.Entity`, returns basic entity metadata (logical name, display name, primary attribute, ownership type, custom flag).
3. For `"attributes"`: calls `RetrieveAllEntitiesRequest` with `EntityFilters.Attributes`, returns attribute metadata per entity.
4. For `"optionsets"`: calls `RetrieveAllOptionSetsRequest`, returns global option set definitions.
5. For `"relationships"`: calls `RetrieveAllEntitiesRequest` with `EntityFilters.Relationships`, returns 1:N, N:1, and N:N relationships.
6. Converts each typed metadata object into a plain `Dictionary<string, object?>` so it serializes cleanly without Dataverse SDK types leaking into JSON.
7. Returns an `IReadOnlyList<Dictionary<string, object?>>`.

**How configuration drives it:**
- `query.MetadataTarget` → the dispatch key (`"entities"` / `"attributes"` / `"optionsets"` / `"relationships"`).
- `query.Id` → used for logging.

**Note:** The current `Sample.context-exporter-config.json` uses `"webapi"` for entity and option set queries rather than `"metadata"` — `MetadataQueryRunner` is available for cases where strongly-typed metadata SDK access is preferable (richer .NET types, no HTTP overhead), but it is not required by the sample config. The plan lists it because the design document calls for it.

**Dependencies (incoming):** Called from `ExportJobRunner` for each `QueryDefinition` where `Type == "metadata"`.
**Dependencies (outgoing):** `Microsoft.Xrm.Sdk.Messages.RetrieveAllEntitiesRequest`, `Microsoft.Xrm.Sdk.Metadata`.

---

### `Helpers/EntityJsonSerializer.cs`

**Responsibility:** Convert a Dataverse `Entity` or `IEnumerable<Entity>` into a `List<Dictionary<string, object?>>` that is safe to pass to `Newtonsoft.Json` without any SDK types leaking through.

**What it does:**
1. Iterates over every `Attribute` in the entity.
2. Converts each attribute value to a plain CLR type:
   - `EntityReference` → `{ "id": "<guid>", "logicalName": "...", "name": "..." }` dictionary
   - `OptionSetValue` → the integer value (label resolution is left to templates)
   - `OptionSetValueCollection` (multi-select) → `List<int>`
   - `Money` → the decimal value
   - `AliasedValue` → unwrap and recurse on the inner value
   - `EntityCollection` → recurse through `EntityJsonSerializer.SerializeEntities()`
   - `DateTime` → ISO-8601 string (`yyyy-MM-ddTHH:mm:ssZ`)
   - `bool`, `int`, `long`, `double`, `decimal`, `Guid`, `string` → pass through unchanged
   - `null` → `null`
3. Adds a virtual `"_id"` key set to `entity.Id.ToString()` so templates always have a reliable row identifier.
4. Returns a `List<Dictionary<string, object?>>`.

**Dependencies (incoming):** Called from `ExportJobRunner` after `FetchXmlQueryRunner` returns raw entities.
**Dependencies (outgoing):** `Microsoft.Xrm.Sdk` (Entity, EntityReference, OptionSetValue, Money, AliasedValue, EntityCollection).

**Unit test coverage required:** Every Dataverse attribute type listed above, plus null handling, plus nested `AliasedValue` wrapping `EntityReference`.

---

### `Orchestration/IntermediateJsonBuilder.cs`

**Responsibility:** Assemble the `_meta` block plus all per-query result sets into a single `intermediate.json` document and write it (and the per-query raw files) to the run folder.

**What it does:**
1. Accepts the run directory path, the loaded `ExportJob`, the environment URL and org name as plain strings, and a dictionary mapping each `query.ResultKey` to its already-serialized result (`JArray` or `List<Dictionary<string, object?>>`).
2. Writes each per-query result to `runs/<timestamp>/output.<queryId>.fetch.json` immediately after it is produced (called once per query, not all at once at the end).
3. After all queries complete, assembles the combined document:
   ```json
   {
     "_meta": {
       "exportedAtUtc": "<ISO-8601>",
       "environment": { "url": "...", "orgName": "..." },
       "spec": "<name>",
       "frontMatter": { ... }
     },
     "<resultKey1>": [...],
     "<resultKey2>": [...]
   }
   ```
4. Writes `runs/<timestamp>/intermediate.json` using `JsonTextWriter` (streaming, not holding the full document in memory) to handle large orgs without OOM.
5. Returns the full path to `intermediate.json`.

**How configuration drives it:**
- `job.Spec` → placed in `_meta.project`.
- `job.FrontMatter` → placed in `_meta.frontMatter`.
- `query.ResultKey` per query → top-level key name in `intermediate.json`.
- `query.Id` per query → used to name per-query files (`output.<id>.fetch.json`).

**Dependencies (incoming):** Called from `ExportJobRunner` once per query (to write per-query raw files) and once at the end (to write `intermediate.json`).
**Dependencies (outgoing):** `Newtonsoft.Json` (`JsonTextWriter`, `JArray`), `System.IO`.

---

### `Helpers/ProcessRunner.cs`

**Note:** This file is listed in the master plan but belongs to Phase 3 (Python invocation). It is **not needed** in Phase 2, add as a stub.

---

## Modified Files

### `Orchestration/ExportJobRunner.cs`

This is the primary site of Phase 2 work. The Phase 1 stub is replaced with a real orchestration loop.

**New responsibilities:**
1. Creates the timestamped run folder: `Path.Combine(baseDir, "runs", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))`.
2. Instantiates the three query runners, passing `IOrganizationService` and the `log` delegate.
3. Loops over `job.Queries`, dispatching each to the appropriate runner based on `query.Type`:
   - `"fetchxml"` → `FetchXmlQueryRunner.Run(query, baseDir, cancellationToken)` → passes result entities through `EntityJsonSerializer` → produces a `List<Dictionary<string, object?>>` → hands to `IntermediateJsonBuilder.WriteQueryResult(...)`.
   - `"webapi"` → `WebApiQueryRunner.Run(query, cancellationToken)` → produces a `JArray` → hands to `IntermediateJsonBuilder.WriteQueryResult(...)`.
   - `"metadata"` → `MetadataQueryRunner.Run(query, cancellationToken)` → produces a `List<Dictionary<string, object?>>` → hands to `IntermediateJsonBuilder.WriteQueryResult(...)`.
   - Unrecognized type → throws `NotSupportedException`, which is caught as a per-query failure (logged as error, added to the failures list).
4. After all queries, calls `IntermediateJsonBuilder.WriteIntermediate(runDir, job, environmentUrl, orgName, results)`.
5. Logs per-query timings and final output paths.
6. On `OperationCanceledException`: logs cancellation, does not re-throw (caller checks token).
7. On any other exception per query: logs the error, marks the query as failed, and continues with remaining queries. At the end, if any queries failed, throws an `AggregateException` so the UI can surface the partial-failure state.

**How configuration drives it:**
- `job.Queries` → the ordered list of queries to execute; dispatched by `query.Type`.
- `job.Python` / `job.Transformation` → read by Phase 3; ignored here.

---

### `Models/QueryDefinition.cs`

**Addition:** One new optional property:
- `MetadataTarget` (string?, `[JsonProperty("metadataTarget")]`) — required when `Type == "metadata"`. Values: `"entities"`, `"attributes"`, `"optionsets"`, `"relationships"`.

No other changes to models.

---

### `Models/ExportJob.cs`

**Addition:** One new optional property:
- `Output` (OutputSettings, `[JsonProperty("output")]`) — already defined as a class (`OutputSettings.cs`) but not yet wired into `ExportJob`. Add it here with a default of `new OutputSettings()`.

---

## Unchanged Files

| File | Reason untouched |
|---|---|
| `ContextExporterPluginControl.cs` | UI wiring complete; only `ExportJobRunner` output changes |
| `UI/BaseDirectoryPickerControl.cs` | No change |
| `UI/SpecPickerControl.cs` | No change |
| `UI/ExportProgressControl.cs` | Log append already works; progress bars deferred to Phase 3 |
| `Helpers/PathResolver.cs` | Complete and tested |
| `Models/PythonSettings.cs` | Used only in Phase 3 |
| All `.j2` templates and `.fetch.xml` files | Consumed by Phase 3 (templates) and query runners (FetchXML); files themselves are not modified |
| `config/transformations/filters.py` | Phase 3 |

---

## Configuration Contract

The `Sample.context-exporter-config.json` defines five queries. Here is exactly how each flows through Phase 2 code:

| Query `id` | `type` | Runner | Serializer step | `resultKey` in `intermediate.json` |
|---|---|---|---|---|
| `entity-attributes` | `webapi` | `WebApiQueryRunner` | OData metadata stripped; `JArray` passed directly | `entityAttributes` |
| `optionsets-global` | `webapi` | `WebApiQueryRunner` | Same | `globalOptionSets` |
| `security-roles` | `fetchxml` | `FetchXmlQueryRunner` → `EntityJsonSerializer` | Deny list applied; `EntityReference`, `OptionSetValue` flattened | `securityRoles` |
| `forms-and-views` | `webapi` | `WebApiQueryRunner` | OData metadata stripped | `formsAndViews` |
| `solutions` | `fetchxml` | `FetchXmlQueryRunner` → `EntityJsonSerializer` | Deny list applied | `solutions` |

---

## Unit Test Coverage Required

All new tests go in `D365ContextExporter.Tests/`:

| Test class | Location | Covers |
|---|---|---|
| `FetchXmlQueryRunnerTests` | `QueriesTests/` | Happy path, paging (3 pages), empty result, `MaxRecords` cap, file-not-found |
| `WebApiQueryRunnerTests` | `QueriesTests/` | Happy path, nextLink pagination, `MaxRecords` cap, 4xx/5xx error surfacing, token extraction |
| `MetadataQueryRunnerTests` | `QueriesTests/` | Each `MetadataTarget` value, unknown target warning |
| `EntityJsonSerializerTests` | `HelpersTests/` | All Dataverse attribute types, deny list filtering, null handling, nested `AliasedValue` |
| `IntermediateJsonBuilderTests` | `OrchestrationTests/` | `_meta` content, per-query file naming, `resultKey` placement, streaming correctness |

Mocking: `IOrganizationService` via Moq for query runner tests; `IntermediateJsonBuilder` uses a temp directory created by the test fixture (no mocking needed for file I/O).

---

## Exit Criteria

1. Given `Sample.context-exporter-config.json` and a live sandbox org, the plugin produces:
   - `runs/<timestamp>/output.entity-attributes.fetch.json`
   - `runs/<timestamp>/output.optionsets-global.fetch.json`
   - `runs/<timestamp>/output.security-roles.fetch.json`
   - `runs/<timestamp>/output.forms-and-views.fetch.json`
   - `runs/<timestamp>/output.solutions.fetch.json`
   - `runs/<timestamp>/intermediate.json` (well-formed, all five `resultKey` keys present, `_meta` populated)
2. `intermediate.json` can be opened and inspected without Dataverse connectivity.
3. All Phase 2 unit tests pass.
4. The `ExportProgressControl` log shows per-query timing and the final output path.
