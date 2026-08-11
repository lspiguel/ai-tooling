# Cloud Flow Definition Anatomy

What an unpacked cloud flow looks like on disk, and which parts of it are safe to edit.

---

## Unpacked layout

`pac solution unpack` writes each cloud flow as a pair of files:

```
src/
  Other/
    Solution.xml            Solution version and metadata
    Customizations.xml      Connection references, environment variables, component list
  Workflows/
    My_Flow_Name-{GUID}.json           The flow definition
    My_Flow_Name-{GUID}.json.data.xml  The workflow record's metadata
```

| File | Holds | Edit |
|---|---|---|
| `<Name>-<GUID>.json` | `properties.definition` and `properties.connectionReferences` | This is the file the skill edits |
| `<Name>-<GUID>.json.data.xml` | Flow display name, `category`, `statecode`, primary entity, trigger scope | Only to rename the flow or change its state |
| `Other/Customizations.xml` | The connection reference definitions the flow binds to | Do not edit |
| `Other/Solution.xml` | Solution unique name and version | Do not edit |

The filename is derived from the flow's display name with **spaces and punctuation stripped**, hyphens kept, followed by the workflow id in uppercase without braces:

| Flow display name | Unpacked filename |
|---|---|
| `Account Definition - Get account by Type` | `AccountDefinition-GetaccountbyType-<GUID>.json` |
| `Order - Every 10 min - Check if source changed` | `Order-Every10min-Checkifsourcechanged-<GUID>.json` |

The GUID is the flow's identity. Renaming the file or changing the GUID imports a **second** flow and leaves the original in place.

---

## Definition structure

```json
{
  "properties": {
    "connectionReferences": {
      "shared_commondataserviceforapps": {
        "runtimeSource": "embedded",
        "connection": { "connectionReferenceLogicalName": "pub_sharedcommondata_abc12" },
        "api": { "name": "shared_commondataserviceforapps" }
      }
    },
    "definition": {
      "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
      "contentVersion": "1.0.0.0",
      "parameters": {},
      "triggers": {},
      "actions": {},
      "outputs": {}
    }
  },
  "schemaVersion": "1.0.0.0"
}
```

| Node | What it is | Safe to edit |
|---|---|---|
| `properties.connectionReferences` | Maps each connector used to a connection reference logical name in the environment | **No.** Rewriting a logical name breaks the binding, and a connection cannot be created from JSON |
| `properties.definition.parameters` | Usually `$connections` and `$authentication`, injected by the platform | **No** |
| `properties.definition.triggers` | Exactly one trigger for a cloud flow | Yes — its inputs and conditions |
| `properties.definition.actions` | The action graph | Yes — this is the working area |
| `$schema`, `contentVersion`, `schemaVersion` | Fixed | **No** |

---

## Action shape

Every action is a named key whose value carries at minimum a `type`:

```json
"actions": {
  "Get_a_row_by_ID": {
    "type": "OpenApiConnection",
    "inputs": {
      "host": {
        "connectionName": "shared_commondataserviceforapps",
        "operationId": "GetItem",
        "apiId": "/providers/Microsoft.PowerApps/apis/shared_commondataserviceforapps"
      },
      "parameters": {
        "entityName": "accounts",
        "recordId": "@triggerOutputs()?['body/accountid']"
      },
      "authentication": "@parameters('$authentication')"
    },
    "runAfter": {}
  },
  "Condition": {
    "type": "If",
    "expression": { "and": [ { "not": { "equals": [ "@outputs('Get_a_row_by_ID')?['body/name']", "" ] } } ] },
    "actions": {},
    "else": { "actions": {} },
    "runAfter": { "Get_a_row_by_ID": [ "Succeeded" ] }
  }
}
```

### The action key is an identifier, not a label

The designer shows **Get a row by ID**. The JSON key is **`Get_a_row_by_ID`**. Every expression that references the action must use the encoded form.

**Only spaces are encoded.** Parentheses, apostrophes and other punctuation survive into the key:

| Designer name | JSON key and reference form |
|---|---|
| `Get a row by ID` | `Get_a_row_by_ID` |
| `Apply to each` | `Apply_to_each` |
| `(Systemuser) Owner (sales) Logic` | `(Systemuser)_Owner_(sales)_Logic` |
| `If child flow doesn't end successfully` | `If_child_flow_doesn't_end_successfully` |

The apostrophe case matters: referencing that action from an expression means escaping the apostrophe by doubling it, because expression string literals are single-quoted — `outputs('If_child_flow_doesn''t_end_successfully')`. Naming actions without apostrophes avoids the problem entirely.

A key that still contains a **space** is a hand-edit mistake. `Test-FlowDefinition.ps1` flags it.

Action names must be **unique across the whole definition**, not just within one level. Two actions named `Compose` in different scopes is invalid even though the JSON nesting permits it.

### `runAfter` is the execution graph

```json
"runAfter": { "Previous_Action": [ "Succeeded" ] }
```

| Rule | Consequence of breaking it |
|---|---|
| Keys must be existing action names | Import fails, or the action never runs |
| Status values are `Succeeded`, `Failed`, `Skipped`, `TimedOut` | Import fails |
| An action may only `runAfter` a **sibling** at the same nesting level | Import fails — an action inside a Scope cannot reference one outside it |
| Exactly one action per level has `"runAfter": {}` | That is the entry point of that level; two entry points means two parallel branches, which may not be intended |

Deleting an action means repairing every `runAfter` that named it — otherwise the following action is orphaned and never runs.

### Container actions

| `type` | Children live under |
|---|---|
| `If` | `actions` and `else.actions` |
| `Scope` | `actions` |
| `Foreach` | `actions` |
| `Until` | `actions` |
| `Switch` | `cases.<name>.actions` and `default.actions` |

Nested actions are still part of the global name space, and their `runAfter` keys resolve only among their immediate siblings.

---

## Adding an action by hand

The reliable way to add a connector action is not to write it from scratch. The `host` block — `connectionName`, `operationId`, `apiId` — must match the connector exactly, and there is no way to derive it.

**Add one instance of the action in the designer first, export, and copy its `host` block.** Then duplicate and edit as text. Guessing an `operationId` produces an import that succeeds and a run that fails.

---

## Renaming a flow

The display name lives in `<Name>-<GUID>.json.data.xml`, not in the definition JSON.

Because the unpacked filename is derived from the display name, renaming means changing both consistently — and the filename encoding (strip spaces and punctuation, keep hyphens) has to be reproduced exactly. **Rename in the maker portal and re-export instead.** It is one action rather than two coupled edits, and it cannot desynchronize the pair.

---

## References

- [Workflow definition language schema](https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-workflow-definition-language)
- [Trigger and action types reference](https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-workflow-actions-triggers)
- [Work with cloud flows using code](https://learn.microsoft.com/en-us/power-automate/manage-flows-with-code)
