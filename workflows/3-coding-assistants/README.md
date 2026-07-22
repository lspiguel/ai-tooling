# Coding Assistants & Agentic Environments — D365 CE Workflows

AI tools integrated into a developer's working environment — either embedded in an existing IDE/editor or operating as a dedicated AI-first IDE or CLI agent. Covers the full spectrum from inline suggestion and chat to fully autonomous multi-file, multi-step task execution with build and test feedback loops.

**Primary tools:** GitHub Copilot Chat (VS Code), GitHub Copilot (Visual Studio), Claude Code (CLI), Cursor, Windsurf, Aider

---

## Workflow guides

Phase-specific prompts have been organized into per-activity guides following the [Activity × Tooling Matrix](../ai-augmented-d365ce-activity-matrix.md). This README keeps the prompts not yet mapped to a phase, plus the shared appendices at the end.

### Setup (one-time)

| Guide | What it covers |
|---|---|
| [B.3 — Initial setup](./b.3-initial-setup.md) | Agent-driven tooling: Visual Studio, package managers, CLI tools, IDEs |
| [C.3 — Project setup](./c.3-project-setup.md) | ALM and/or repository wiring: repo layout, solution export/unpack automation, Dataverse MCP + Azure DevOps CLI wiring |

### The delivery loop

| Guide | What it covers |
|---|---|
| [1.3 — Specification / Intent](./1.3-specification.md) | User stories & ACs focused on intent, Ask Mode exploration, grounded gap analysis, field-reference/impact search |
| [2.3 — Planning / Design](./2.3-planning.md) | Plan Mode: detailed implementation plan, peer review of the plan, sequencing from solution structure, risk register, ADRs |
| [3.3 — Tasking](./3.3-tasking.md) | Expand the plan: checklists, unit test conditions, ADO tasks via the Azure DevOps CLI, branch scaffolding, coding-agent pre-flight |
| [4.3 — Implementation / Build](./4.3-implementation.md) | Agent Mode with human in the loop: plugins, web resources, PCF, Azure Functions, pipelines, queries, async coding-agent delegation |
| [5.3 — Validation / Peer review](./5.3-validation-peer-review.md) | Ask Mode with adversary personas, unit-test coverage, config/security/flow review, drift detection, PR hygiene |
| [6.3 — Documentation / Information sharing](./6.3-documentation.md) | Automated docs: technical docs from code, wiki updates, runbook review, instruction-file refresh |

---

## Configuration & Customization

### Analyzing Cloud Flows

**AI:** GitHub Copilot Chat

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

---

### Power Pages Analysis

**AI:** GitHub Copilot Chat
**Context:** Portal solution folder

**Audit page naming:**
```
List all web pages in this portal solution and flag any that do not follow
the naming convention "[ClientName] - [PageName]".
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

---

### Generating New Event Handler Code

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

### Testing and Diagnostics

**Write a mock harness:**
```
Generate a test harness in index.html for this PCF control
that initializes the ComponentFramework mock and renders the component
with sample data for [fieldname].
```

---

## Plugin and Custom API Development

### Unit Tests

**AI:** GitHub Copilot (Visual Studio)
**Context:** Open existing plugin solution with sample plugins

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

**Generate a strongly-typed configuration class:**
```
Create a strongly-typed configuration class that reads the following
environment variables and validates that none are null on startup:
InstanceUri, ClientId, ClientSecret, TenantId, Environment.
```

---

### Observability and Resilience

**Add Application Insights structured logging:**
```
Add structured logging using ILogger to each function in this project.
Log request start, key parameters (without secrets), execution time,
and any exceptions with full inner exception detail.
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

**Convert FetchXML to QueryExpression:**
```
Convert this FetchXML query into a QueryExpression
using the Dynamics 365 SDK for .NET.
Follow the practice of using ColumnSet with explicit attribute names only.
```

---

### Solution Health and Technical Debt Review

**AI:** GitHub Copilot Chat
**Context:** Full solution repo

**Identify unused components:**
```
Are there any tables, fields, flows, or web resources in this solution
that appear to have no references in forms, views, plugins, or other flows?
```

---

## CI/CD and Automation

### AI-Powered Code Review (GitHub Actions)

**AI:** Claude Code / Agentic CLI
**Concept:** Integrate a GitHub Actions workflow that runs on Pull Requests and uses an AI API call to review plugin or Azure Function changes against internal best practices before a human reviewer picks up the PR.

**Generate the action:**
```
Create a GitHub Actions workflow that triggers on pull requests targeting main.
For each changed .cs file, call the Anthropic API to review the code
against these rules: [paste rules from best-practices.md].
Post the findings as a PR comment. Use a repository secret for the API key.
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
| **Reference conventions by name** | "Follow StyleCop / INVEST / Gherkin / the project's naming convention" |
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
