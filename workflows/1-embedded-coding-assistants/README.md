# Embedded Coding Assistants — D365 CE Workflows

AI living inside the development environment (VS Code, Visual Studio). Primarily GitHub Copilot Chat with inline suggestions and optional agentic mode.

**Primary tools:** GitHub Copilot Chat (VS Code), GitHub Copilot (Visual Studio)

---

## Configuration & Customization

### Documenting Customizations

**AI:** GitHub Copilot Chat
**Context:** Base or specific solution folder open in VS Code

```
Document customizations done on the [TableName] table, explaining purpose,
relationships to other tables, and any notable business logic in forms or views.
```

**Extended — Generate a full solution summary:**
```
Summarize all tables in this solution, their purpose, and which other tables
they relate to. Format the output as a markdown table.
```

---

### Analyzing Cloud Flows

**AI:** GitHub Copilot Chat
**Context:** Processes solution folder

**List flows triggered from a table:**
```
Get me a list of all flows that are triggered from [TableName].
I need the list only, not code.
```

**Find flows with missing error handling:**
```
Identify cloud flows in this folder that do not have a "Configure run after"
or scope-based error handling. List them with their trigger.
```

**Detect potential performance issues:**
```
Which flows in this folder iterate over collections using Apply to Each?
List them and note if parallel branching is enabled.
```

**Optimization review:**
**Context:** Open a specific cloud flow JSON file
```
What optimizations could be applied to this flow?
Focus on reducing API calls, using batch operations, and improving error handling.
```

---

### Security Analysis

**AI:** GitHub Copilot Chat
**Context:** Security solution folder

**Generate a PowerShell audit script:**
```
Write a PowerShell script using the Dataverse Web API to list all security roles
that have Read access to [TableName] but do not have Append or AppendTo access.
```

**Identify over-privileged roles:**
```
Analyze the security role definitions in this folder.
Flag any roles that have organization-level access on sensitive tables
such as SystemUser, Team, or BusinessUnit.
```

**Explain a role:**
```
Explain what this security role allows a user to do in plain English,
grouping privileges by functional area.
```

---

### Solution Dependency and Impact Analysis

**AI:** GitHub Copilot Chat
**Context:** Full solution repo

**Find all references to a field:**
```
Find every place the field [table_fieldname] is used across this solution:
forms, views, flows, JavaScript, plugins, and FetchXML queries.
```

**Assess impact before deleting a table or field:**
```
I'm planning to remove the field [table_fieldname].
What components in this repository reference or depend on it?
```

**Validate naming conventions:**
```
Review the customizations in this solution folder.
Flag any table, field, form, view, or flow names that do not follow
the prefix convention "[prefix]_" or that use default system-generated names.
```

---

### Power Pages Analysis

**AI:** GitHub Copilot Chat
**Context:** Portal solution folder

**Audit page naming:**
```
List all web pages in this portal solution and flag any that do not follow
the naming convention "[ClientName] - [PageName]".
```

**Review Liquid templates for security issues:**
```
Review these Liquid template files and flag any places where user input
is rendered without escaping, or where table permissions are bypassed.
```

---

## JavaScript and Web Resources

### Understanding and Documenting Existing Code

**AI:** GitHub Copilot Chat
**Context:** Open code file (e.g. JavaScript)

```
What does this code do? Summarize by function, noting the purpose and
any Dataverse-specific API calls made.
```

**Add JSDoc comments without changing logic:**
```
Add a JSDoc comment header to each function explaining what it does,
its parameters, and return value. Do not change any existing code.
```

---

### Code Quality

**StyleCop-equivalent review for JS:**
```
What JS styling and quality rules from SonarQube or ESLint
could be applied to this code? List each issue with the line reference.
```

**Find dead or redundant code:**
**Context:** Open Web Resources folder
```
Is there any dead code, commented-out code, or duplicate functions
across these files? List findings with file and line number.
```

**Modernize older code patterns:**
```
This code uses XMLHttpRequest and var declarations.
Rewrite it using fetch, const/let, and async/await,
without changing the business logic.
```

---

### Generating New Event Handler Code

**Generate a form event handler:**
```
Write a Dynamics 365 CE JavaScript form OnLoad function for the [TableName] table
that hides the field [fieldname] when [otherfield] equals [value].
Use form or global context (not Xrm.Page) and follow strict mode and namespace conventions.
```

**Generate a ribbon command function:**
```
Write a JavaScript function to be used as a ribbon command on the Account form.
It should check if the record is saved, then open a custom dialog
using Xrm.Navigation.openAlertDialog confirming the user wants to proceed.
```

---

## PCF Controls

### Structure and Refactoring

**AI:** GitHub Copilot Chat
**Context:** Open PCF index file

**Decompose a monolithic control:**
```
Divide this code into components following React best practices.
Identify logical UI sections and extract each into its own functional component
with typed props.
```

**Migrate class components to hooks:**
```
Refactor this React class component to a functional component using hooks.
Preserve all existing behavior.
```

**Find dead code:**
```
Is there any dead code, unused imports, or unused state variables in this file?
```

---

### Generating a New PCF from a Description

```
Create a PCF field control for Dynamics 365 CE that renders a read-only
color-coded badge based on the value of a OptionSet field.
Use React and TypeScript. Follow the standard PCF manifest and index structure.
```

---

### Testing and Diagnostics

**Write a mock harness:**
```
Generate a test harness in index.html for this PCF control
that initializes the ComponentFramework mock and renders the component
with sample data for [fieldname].
```

---

## Plugin and Custom API Development

### Generating Plugins from a Description

**AI:** GitHub Copilot (Visual Studio)
**Context:** Open existing plugin solution with sample plugins

**Generate from existing pattern:**
```
Add a plugin similar to AccountUpdatePostOpPlugin but that triggers on
[TableName] Delete, Pre-Validation stage. It should query for active
related [RelatedTable] records and throw an InvalidPluginExecutionException
if any exist, preventing the deletion.
```

**Name and scaffold correctly:**
```
Rename the class to follow the convention [EntitySchemaName][Message][Stage]Plugin
and add the registration comment block to the class summary.
```

**Move queries to the Queries layer:**
```
Move the RetrieveMultiple calls into a static class in the Queries namespace
and folder. Each method should return an EntityCollection and accept an
IOrganizationService parameter.
```

---

### StyleCop and Code Quality

**Enforce StyleCop:**
```
Correct this code to conform to StyleCop style,
add missing documentation comments.
```

**Review for best practices:**
```
Review this plugin class for the following: unnecessary catch blocks that
rethrow without adding information, missing null checks,
any hardcoded GUIDs or magic strings that should be constants,
and any N+1 query patterns.
```

---

### Unit Tests

**Generate a full test class:**
```
Create a unit test class for plugin [PluginClassName]
inheriting from PluginTestHelperBase and following the style in [ExistingTestClass].
Include tests for: successful execution, each validation failure path,
and any branching logic.
```

**Fix mock setup to match query intent:**
```
For the RetrieveMultiple mocks, make each setup filter using a lambda
on the QueryExpression parameter rather than returning unconditionally.
Use EntityFactory.Initializer with the ColumnSet from the query.
```

**Add a targeted mock:**
```
Add a method to [MockHelperClass] that mocks the RetrieveMultiple result
for [QueryMethodName], returning [describe expected data].
```

---

### Debugging Assistance

**Interpret a plugin trace log:**
```
Here is a plugin trace log. Identify the root cause of the failure,
the method and line where it occurred, and suggest a fix.
[paste trace log]
```

**Explain a Dataverse error code:**
```
What does Dataverse error code [0x80040265] mean,
and what are the most common plugin scenarios that trigger it?
```

---

## Azure Functions

### Scaffolding and Setup

**AI:** GitHub Copilot (Visual Studio)
**Context:** New Azure Function project

**Generate Echo and WhoAmI functions:**
```
Generate an Echo function and a WhoAmI function for a .NET 8 Isolated
Azure Function project that connects to Dataverse.
WhoAmI should use ServiceClient with credentials from environment variables:
InstanceUri, ClientId, ClientSecret, TenantId.
```

**Generate a strongly-typed configuration class:**
```
Create a strongly-typed configuration class that reads the following
environment variables and validates that none are null on startup:
InstanceUri, ClientId, ClientSecret, TenantId, Environment.
```

---

### Pipeline and Deployment

**Generate an ADO YAML pipeline:**
```
Generate an Azure DevOps YAML pipeline to build and deploy
a .NET 8 Isolated Azure Function to a specific deployment slot
using a service connection. The pipeline should be manual-trigger only,
restore NuGet packages, build in Release mode, and deploy using zipDeploy.
```

---

### Observability and Resilience

**Add Application Insights structured logging:**
```
Add structured logging using ILogger to each function in this project.
Log request start, key parameters (without secrets), execution time,
and any exceptions with full inner exception detail.
```

**Add retry logic:**
```
Wrap the Dataverse ServiceClient calls in this function with a retry policy
using Polly: 3 retries with exponential backoff starting at 2 seconds,
retrying on HttpRequestException and ServiceException.
```

---

### Testing Azure Function Output as a Web Service

**AI:** GitHub Copilot (Visual Studio)
**Context:** Azure Function project with xUnit tests

**Goal:** Validate the serialized JSON output of a function — not internal model objects — so tests reflect what a Logic App or workflow.json actually consumes.

**Rewrite tests to assert on serialized JSON:**
```
Rewrite these integration tests so they assert on the serialized JSON string
returned by the function, not on model class instances.
Serialize the OkObjectResult value using JsonSerializer with camelCase naming policy.
All assertions should use string.Contains() or Regex.IsMatch() —
no model class references in the assertion code.
```

**Validate fields required by a Logic App workflow:**
```
The Logic App workflow.json accesses these fields on each item in the response:
[list field names].
Add a test that serializes the function output and asserts that each of those
field names appears in the JSON using a regex pattern like: "fieldName"\s*:
```

**Validate non-empty values via regex:**
```
Add a test that uses Regex.Matches to extract all values of the "[fieldName]"
property from the JSON output and asserts that none are null or whitespace.
```

**Cross-validate counts against actual array content:**
```
The response includes a field "sfdcAccountsToCreate" (integer) and
a "customers" array where some items have a null or empty sfdcAccountId.
Add a test that extracts both via regex and asserts the count matches
the number of items with an empty/null sfdcAccountId.
```

---

### Dynamic Field Handling Without Model Changes

**AI:** GitHub Copilot (Visual Studio)
**Context:** Azure Function that reads structured files (Excel, CSV, etc.) and returns JSON

**Goal:** Allow the function to adapt to new or renamed fields in source files without requiring changes to C# model classes or downstream JSON schemas.

**Replace fixed models with dynamic dictionaries:**
```
Refactor this Azure Function to stop using fixed C# model classes for [SourceFormat] data.
Instead, read headers dynamically and convert each row to Dictionary<string, object?>.
Normalize all keys to lowercase.
Keep only the following fields hardcoded as constants: [list join/key fields].
```

**Create a FieldNames constants class:**
```
Extract all hardcoded field name strings in this project into a static class
named FieldNames. Each constant should be the lowercase version of the field name.
Replace all string literals in the function logic with references to these constants.
```

**Update tests for lowercase dynamic keys:**
```
Update the unit tests for this function to use lowercase field names
matching the new dynamic dictionary output.
Replace any assertions that used model class properties with
string or regex assertions on the serialized JSON.
```

---

## Dataverse Queries & Solution Health

### Dataverse API Query Generation

**AI:** GitHub Copilot Chat
**Context:** FetchXML or OData description

**Generate a FetchXML query:**
```
Write a FetchXML query that retrieves all active Accounts
where the primary contact has an email on the domain "contoso.com",
including account name, primary contact name, and email.
Limit to 50 results.
```

**Convert FetchXML to QueryExpression:**
```
Convert this FetchXML query into a QueryExpression
using the Dynamics 365 SDK for .NET.
Follow the practice of using ColumnSet with explicit attribute names only.
```

**Generate an OData Web API request:**
```
Write an OData query for the Dataverse Web API to retrieve
all active opportunities with estimated revenue over 50,000
and their owning user's full name.
Format as a URL and a Postman-ready request.
```

---

### Solution Health and Technical Debt Review

**AI:** GitHub Copilot Chat
**Context:** Full solution repo

**Identify technical debt:**
```
Review this solution repository. Flag: deprecated APIs,
hardcoded environment URLs, plugins running synchronously that could be async,
flows using legacy connectors, and any components using unsupported patterns
for the current Power Platform version.
```

**Identify unused components:**
```
Are there any tables, fields, flows, or web resources in this solution
that appear to have no references in forms, views, plugins, or other flows?
```

---

## Appendix: Prompt Patterns That Work Well

| Pattern | Example |
|---|---|
| **Anchor to existing code** | "...similar to [ExistingClass], following the same pattern" |
| **Specify the output format** | "Format the output as a markdown table / Gherkin / YAML" |
| **Constrain scope explicitly** | "Do not change any existing logic, only add comments" |
| **Ask for issues, not fixes** | "List what could be improved, do not make changes yet" |
| **Chain iteratively** | Start broad → refine by layer (business logic → queries → tests) |
| **Reference conventions by name** | "Follow StyleCop / INVEST / Gherkin / the naming convention in AGENTS.md" |
| **Separate concerns** | Generate class → move queries → generate tests → enforce style (separate prompts) |

---

## Appendix: Guardrails and Human Review Checkpoints

| Stage | What to Review |
|---|---|
| Plugin / AF code generation | Logic correctness, exception handling, no hardcoded secrets |
| Unit test generation | Coverage of all branches, mock fidelity to actual queries |
| Flow generation | Missing error handling, redundant API calls, correct trigger |
| Security script generation | Verify output against actual org before running in production |
| Deployment runbook | Validate steps against actual environment topology |
