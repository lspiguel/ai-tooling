# General-Purpose AI Assistants — D365 CE Workflows

Web and desktop chat AI for requirements analysis, documentation, planning, and tasks that require broad reasoning rather than code execution in a specific environment.

**Primary tools:** Claude.ai, ChatGPT, Microsoft 365 Copilot (chat mode)

---

## Requirements and Work Item Quality

### Drafting and Evaluating User Stories

**AI:** Claude / Copilot Chat
**Context:** Requirement description or meeting notes

**Generate a user story:**
```
Write a user story for the following requirement: [describe requirement].
Format it with: Title, Description (As a / I want / So that),
Acceptance Criteria in Gherkin format (Given/When/Then),
and Implementation Notes relevant to Dynamics 365 CE.
```

**INVEST evaluation:**
```
Evaluate this user story against the INVEST criteria.
Report only criteria that are NOT met, with a short reason for each
and a final summary.
[paste user story]
```

**Break an epic into features and stories:**
```
Break this epic into features and user stories for a Dynamics 365 CE implementation.
Maintain the hierarchy: epic → features → user stories.
For each user story include acceptance criteria in Gherkin format.
[paste epic description]
```

---

### Test Case Generation from Acceptance Criteria

```
Convert these Gherkin acceptance criteria into manual test cases
formatted for Azure DevOps Test Plans.
For each scenario, include: Test Case title, preconditions,
step-by-step actions, and expected result.
[paste acceptance criteria]
```

---

### Impact Assessment Before a Change

```
I need to change the behavior of [process/field/table] in Dynamics 365 CE.
Based on the following solution components, identify which user stories,
flows, plugins, and forms may be affected and need retesting.
[describe the change]
```

---

## ALM, Data Migration, and Accessibility

### ALM and DevOps Assistance

**Generate a solution comparison summary:**
```
Compare the solution manifest files from these two solution exports
and list: components added, removed, or modified between versions.
Format as a changelog.
```

**Draft a deployment runbook:**
```
Based on the following solution components, write a deployment runbook
for deploying to UAT and then Production.
Include pre-deployment checks, the deployment steps,
post-deployment validation steps, and a rollback procedure.
[list solution components]
```

---

### Data Migration Support

**Generate a SSIS / Dataflow mapping:**
```
Given this source schema and target Dataverse table definition,
generate a field mapping table showing: source column,
target logical name, transformation needed (if any), and data type.
[paste schemas]
```

**Write a data validation script:**
```
Write a PowerShell script that queries the Dataverse Web API
and validates that all records migrated from the source have:
- A non-null [required field]
- A valid lookup to an existing [related table] record
- No duplicate [unique field] values
Output a summary report.
```

---

### Accessibility and UX Review for Power Pages

```
Review this Power Pages web template Liquid code for accessibility issues:
missing alt text, poor heading hierarchy, form labels not associated with inputs,
and interactive elements not keyboard accessible.
```
