# Power Automate Cloud Flow Deployment and Update Options

This document describes Microsoft-based options for deploying and updating Power Automate Cloud Flows (modern flows) in Dynamics 365 / Dataverse. It covers two main approaches: **solution-based deployment** and **direct flow update** without solution import.

---

## Part 1: Solution-Based Deployment

Use this approach when you have an unpacked solution containing Cloud Flows (e.g., from version control) and want to deploy them to Dynamics 365.

### Overview

| Option | Pack Unpacked → ZIP | Import to D365 |
|--------|---------------------|----------------|
| **Power Platform CLI (pac)** | `pac solution pack` | `pac solution import` |
| **PowerShell** | Use `pac solution pack` | `Import-CrmSolutionAsync` |
| **.NET / C#** | Use `pac solution pack` | `ImportSolutionToCrm` / `ImportSolutionRequest` |
| **Web API** | Use `pac solution pack` | `ImportSolution` / `ImportSolutionAsync` |

---

### 1.1 Power Platform CLI (pac) — Recommended

**Installation**

- [Power Platform CLI](https://learn.microsoft.com/en-us/power-platform/developer/cli/introduction) — Install via .NET Tool, Visual Studio Code extension, or Windows MSI
- Example: `dotnet tool install -g Microsoft.PowerPlatform.CLI`

**Step 1: Pack the unpacked solution to a ZIP**

```powershell
pac solution pack --zipfile "C:\Out\Processes.zip" --folder "D:\Path\To\UnpackedSolution\solution"
```

- `--folder` must point to the root of the unpacked solution (where `Solution.xml` lives, or the folder containing the `.cdsproj`).
- Use `--packagetype Managed` if you need a managed package.
- Use `--map` to specify a mapping XML file for custom folder structure.

**Step 2: Authenticate and import**

```powershell
pac auth create --environment https://yourorg.crm.dynamics.com
pac solution import --path "C:\Out\Processes.zip" --activate-plugins --publish-changes
```

**Useful import flags**

| Flag | Description |
|------|-------------|
| `--activate-plugins` | Activate plug-ins and workflows on import |
| `--publish-changes` | Publish customizations after import |
| `--async` | Import asynchronously |
| `--force-overwrite` | Force overwrite of unmanaged customizations |
| `--skip-lower-version` | Skip import if same or higher version exists |
| `--stage-and-upgrade-up` | Import and upgrade the solution |
| `--settings-file` | JSON file for connection references and environment variables |

**Reference**

- [pac solution command reference](https://learn.microsoft.com/en-us/power-platform/developer/cli/reference/solution)
- [pac auth command reference](https://learn.microsoft.com/en-us/power-platform/developer/cli/reference/auth)

---

### 1.2 PowerShell

**Option A: Microsoft.Xrm.Data.PowerShell (community, widely used)**

Microsoft's ALM documentation references this module for solution import.

```powershell
# Install module
Install-Module -Name Microsoft.Xrm.Data.PowerShell -Scope CurrentUser

# Connect (interactive or connection string)
$conn = Get-CrmConnection -InteractiveMode

# Pack first with pac (see 1.1), then import
Import-CrmSolutionAsync -conn $conn -SolutionFilePath "C:\Out\Processes.zip" -ActivateWorkflows -OverwriteUnManagedCustomizations -MaxWaitTimeInSeconds 600
```

**References**

- [Manage solutions using PowerShell](https://learn.microsoft.com/en-us/power-platform/alm/powershell-api)
- [Microsoft.Xrm.Data.PowerShell (GitHub)](https://github.com/seanmcne/Microsoft.Xrm.Data.PowerShell)
- [Solution samples](https://github.com/seanmcne/Microsoft.Xrm.Data.PowerShell.Samples/tree/master/Solutions)

**Option B: Microsoft.Xrm.Tooling.PackageDeployment**

For full package deployment automation (not just importing a single ZIP).

- [Use Microsoft.Xrm.Tooling.PackageDeployment](https://learn.microsoft.com/en-us/powershell/powerapps/get-started-packagedeployment)

---

### 1.3 .NET / C# (Dataverse SDK)

Use when building custom tools, scripts, or CI/CD pipelines in C#.

**Pack:** Use `pac solution pack` (or SolutionPackager) to produce a ZIP from the unpacked folder. The SDK does not replace the pack step.

**Import:** Use the Dataverse SDK to import the ZIP.

**CrmServiceClient (simplified):**

```csharp
using Microsoft.Xrm.Tooling.Connector;

using var client = new CrmServiceClient(connectionString);

client.ImportSolutionToCrm(
    pathToSolutionZip: @"C:\Out\Processes.zip",
    activatePlugIns: true,
    overwriteUnManagedCustomizations: true);
```

**ImportSolutionRequest (more control):**

```csharp
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

var bytes = File.ReadAllBytes(@"C:\Out\Processes.zip");
var request = new ImportSolutionRequest
{
    CustomizationFile = bytes,
    OverwriteUnmanagedCustomizations = true,
    PublishWorkflows = true
};
service.Execute(request);
```

**References**

- [ImportSolutionRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.crm.sdk.messages.importsolutionrequest)
- [CrmServiceClient.ImportSolutionToCrm](https://learn.microsoft.com/en-us/dotnet/api/microsoft.xrm.tooling.connector.crmserviceclient.importsolutiontocrm)
- [Sample: Work with solutions](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/samples/work-solutions)

---

### 1.4 Dataverse Web API

For non-Windows environments or HTTP-based automation.

- **ImportSolution** — Synchronous import
- **ImportSolutionAsync** — Asynchronous import

The solution ZIP is sent as part of the request (e.g., base64 or multipart, depending on API usage).

**References**

- [ImportSolution action](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/importsolution)
- [ImportSolutionAsync](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/importsolutionasync)

---

## Part 2: Direct Flow Update (Without Solution Import)

Use this approach when you want to update a **single** Power Automate Cloud Flow (modern flow) directly, without importing a full solution.

### Overview

- Modern flows are stored in the **workflow** table with `category = 5`.
- The flow definition is in the `clientdata` column as a JSON string.
- You can update the flow by updating the workflow record via Dataverse SDK, Web API, or the Power Automate Management connector.

---

### 2.1 Dataverse Workflow Entity Update (SDK / Web API)

**Prerequisite:** The flow must be in **Draft** (`statecode = 0`) before you can change `clientdata`.

**Steps**

1. Deactivate the flow (`statecode = 0`).
2. Update the `clientdata` attribute with the new definition.
3. Activate the flow (`statecode = 1`).

**C# (Dataverse SDK):**

```csharp
// 1. Deactivate
service.Update(new Entity("workflow", workflowId)
{
    ["statecode"] = new OptionSetValue(0)  // Draft
});

// 2. Update flow definition
service.Update(new Entity("workflow", workflowId)
{
    ["clientdata"] = newClientDataJson  // Full JSON: definition + connectionReferences
});

// 3. Activate
service.Update(new Entity("workflow", workflowId)
{
    ["statecode"] = new OptionSetValue(1)  // Activated
});
```

**Web API (PATCH):**

```http
PATCH /api/data/v9.2/workflows(<workflowid>)
Content-Type: application/json

{ "clientdata": "<escaped JSON string>" }
```

**PowerShell (Microsoft.Xrm.Data.PowerShell):**

```powershell
$conn = Get-CrmConnection -InteractiveMode

# Deactivate
Set-CrmRecordState -conn $conn -EntityLogicalName workflow -Id $workflowId -State 0 -Status 1

# Update clientdata (retrieve flow, modify, save)
$flow = Get-CrmRecord -conn $conn -EntityLogicalName workflow -Id $workflowId -Fields clientdata
$flow.clientdata = $newClientDataJson
Set-CrmRecord -conn $conn -EntityLogicalName workflow -Id $workflowId -Fields $flow

# Activate
Set-CrmRecordState -conn $conn -EntityLogicalName workflow -Id $workflowId -State 1 -Status 2
```

---

### 2.2 Power Automate Management Connector

The **Update flow** action allows you to update a flow by environment and flow identifier, passing the new flow definition.

- Works for flows in Power Automate (including solution flows).
- Can be used from Power Automate, Logic Apps, or via the underlying REST API.
- Parameters typically include: Environment Name, Flow Name/ID, and the updated Flow definition.

**Reference**

- [Power Automate Management connector](https://learn.microsoft.com/en-us/connectors/flowmanagement/)

---

### 2.3 ClientData JSON Structure

The `clientdata` column must contain a JSON string with this structure:

```json
{
  "properties": {
    "connectionReferences": {
      "shared_commondataserviceforapps": {
        "runtimeSource": "embedded",
        "connection": { ... },
        "api": { "name": "shared_commondataserviceforapps" }
      }
    },
    "definition": {
      "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
      "contentVersion": "1.0.0.0",
      "parameters": { ... },
      "triggers": { ... },
      "actions": { ... }
    }
  },
  "schemaVersion": "1.0.0.0"
}
```

**Obtaining the JSON**

- Retrieve the existing flow via Web API or SDK and read its `clientdata`.
- Export the flow from Power Automate and use the exported definition.
- Build it from your source if you have the equivalent structure.

---

### 2.4 Important Considerations for Direct Update

| Topic | Notes |
|-------|-------|
| **Connection references** | `connectionReferences` must map to connections in the target environment (by logical name or connection reference). |
| **Solution-aware flows** | Direct update works, but future solution imports can overwrite these changes. |
| **Managed flows** | Flows from managed solutions typically cannot be updated directly; use solution import/upgrade instead. |
| **Scope** | Only flows under **Solutions** are supported for programmatic management via Dataverse; "My Flows" are not supported. |
| **Deactivate first** | The flow must be in Draft before updating `clientdata`. |

---

### 2.5 Workflow Table Reference

Key columns for modern flows:

| Logical Name | Type | Description |
|--------------|------|-------------|
| `category` | Choice | `5` = Modern Flow (Automated, instant, or scheduled) |
| `clientdata` | String | JSON of flow definition and connectionReferences |
| `statecode` | Choice | `0` = Draft, `1` = Activated, `2` = Suspended |
| `type` | Choice | `1` = Definition, `2` = Activation, `3` = Template |
| `workflowid` | Guid | Unique identifier for the flow |
| `workflowidunique` | Guid | Unique identifier for this installation |

**Reference**

- [Work with cloud flows using code](https://learn.microsoft.com/en-us/power-automate/manage-flows-with-code)
- [workflow EntityType (Web API)](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/workflow)
- [Process (Workflow) table reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/workflow)

---

## Summary

| Scenario | Recommended approach |
|----------|----------------------|
| Deploy multiple flows or full solution from unpacked source | `pac solution pack` → `pac solution import` |
| Automate solution import in CI/CD | PowerShell (`Import-CrmSolutionAsync`) or C# (`ImportSolutionToCrm`) |
| Update a single flow without solution import | Dataverse workflow entity update (`clientdata`) or Power Automate Management connector |
