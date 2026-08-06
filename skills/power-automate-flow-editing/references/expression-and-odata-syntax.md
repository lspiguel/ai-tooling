# Expression and OData Syntax

The four syntax classes that break hand-edited cloud flows. Read this before writing any expression into a flow definition.

---

## 1. `@` versus `@{}`

A JSON string value is either **entirely an expression** or **a string with expressions interpolated into it**. The delimiters differ, and mixing them is the most common hand-edit error.

| Intent | Form | Example |
|---|---|---|
| The value *is* the expression | `"@expression"` — no braces, `@` is the first character | `"@triggerOutputs()?['body/name']"` |
| Expression inside a string | `"text @{expression} text"` | `"Hello @{triggerOutputs()?['body/name']}, welcome"` |
| A literal `@` at the start | `"@@"` escapes it | `"@@example.com"` produces `@example.com` |
| A literal `{` after `@` | Escape as `@@{` | |

| Wrong | Why |
|---|---|
| `"@{triggerOutputs()?['body/name']}"` as a whole value | Works, but coerces the result to string — breaks when the target expects a number, boolean, or object |
| `"Hello @triggerOutputs()"` | The `@` is not at position 0, so it is a literal — no evaluation happens |
| `"@concat('a','b') and more text"` | Invalid — a whole-string expression cannot have trailing text. Use `"@{concat('a','b')} and more text"` |

**Rule:** `@` only at position 0, for the whole value. Anything else uses `@{}`.

---

## 2. Referencing other actions

Every reference uses the **underscore-encoded action key**, not the designer's display name.

| Function | Returns | Note |
|---|---|---|
| `triggerOutputs()` | The trigger's full output | Includes `body`, headers, status |
| `triggerBody()` | The trigger's body | Shorthand for `triggerOutputs()['body']` |
| `outputs('Action_Name')` | The action's full output | |
| `body('Action_Name')` | The action's body | |
| `items('Apply_to_each')` | The current item of that loop | Names the loop, not the collection |
| `item()` | The current item of the innermost loop | Ambiguous when nested; prefer `items()` |
| `variables('varName')` | A variable's value | Must be initialized earlier in the flow |
| `parameters('$connections')` | Platform-injected | Do not hand-write |

### Property access and null safety

```
@triggerOutputs()?['body/accountid']
@body('Get_a_row_by_ID')?['name']
@outputs('Compose')?['body']?['nested']?['field']
```

The `?` before `[` is **null-safe access**. Without it, a missing property throws at run time and fails the run. Use `?` on every hop that can be absent.

Note the two shapes above: Dataverse actions return a flattened body where the path is a single key containing a slash — `?['body/accountid']` on `triggerOutputs()`, but `?['accountid']` on `body(...)`. When unsure, run the flow once and read the raw output in the run history.

### Lookup and choice columns

| Column type | Read as | Note |
|---|---|---|
| Text, number, date | `?['fieldname']` | |
| Lookup — the GUID | `?['_fieldname_value']` | Leading underscore, `_value` suffix |
| Lookup — the display text | `?['_fieldname_value@OData.Community.Display.V1.FormattedValue']` | |
| Choice — the number | `?['fieldname']` | |
| Choice — the label | `?['fieldname@OData.Community.Display.V1.FormattedValue']` | |

Writing a lookup uses a different form again — the `/entitysetname(guid)` navigation binding, on the plain field name, not the `_value` form.

### Useful functions

| Need | Function |
|---|---|
| First non-null | `coalesce(a, b)` |
| Test for empty | `empty(x)` — true for null, empty string, empty array |
| Conditional | `if(condition, whenTrue, whenFalse)` |
| String join | `concat(a, b, c)` |
| Parse a JSON string | `json(string)` |
| Format a date | `formatDateTime(date, 'yyyy-MM-dd')` |
| Date arithmetic | `addDays(date, -7)`, `addHours`, `addMinutes` |
| Current time | `utcNow()` |
| Type conversion | `int(x)`, `string(x)`, `float(x)`, `bool(x)` |

Full list: [Workflow definition language functions](https://learn.microsoft.com/en-us/azure/logic-apps/workflow-definition-language-functions-reference).

### Quoting inside expressions

String literals inside an expression use **single quotes**. A literal single quote is escaped by doubling it:

```
@concat('It', '''', 's here')
```

This bites on action references, because an apostrophe in a designer name survives into the action key. An action named `If child flow doesn't end successfully` has the key `If_child_flow_doesn't_end_successfully`, and referencing it requires the doubled form:

```
@outputs('If_child_flow_doesn''t_end_successfully')
```

Avoid apostrophes when naming actions.

Double quotes inside an expression are JSON string delimiters and must be escaped as `\"` — which is why single quotes are the convention throughout.

---

## 3. Dataverse OData filters

The `$filter` parameter on **List rows** is an OData query string, not an expression. It follows Dataverse Web API rules.

| Need | Syntax |
|---|---|
| Text equals | `name eq 'Contoso'` |
| Number equals | `revenue eq 5000` |
| GUID equals | `accountid eq 00000000-0000-0000-0000-000000000000` — **unquoted** |
| Lookup equals | `_primarycontactid_value eq <guid>` — the `_value` form, unquoted GUID |
| Choice equals | `statuscode eq 1` — the numeric value, not the label |
| Boolean | `donotemail eq true` |
| Null test | `_primarycontactid_value eq null` |
| Combine | `and`, `or`, `not`, parentheses |
| Comparison | `gt`, `ge`, `lt`, `le`, `ne` |
| Substring | `contains(name, 'Contoso')` |
| Prefix | `startswith(name, 'Con')` |
| Date comparison | `createdon gt 2026-01-01T00:00:00Z` — ISO 8601, quoted only if the column is a string |

### The escaping trap

A `$filter` containing a flow expression is a **string with interpolation**, so it uses `@{}`, and the single quotes belong to OData, not to the expression:

```
name eq '@{triggerOutputs()?['body/name']}'
```

Two things go wrong here routinely:

- **Missing OData quotes** around a text value — `name eq @{...}` is invalid OData.
- **A quote inside the value** breaks the filter. Values that may contain an apostrophe need `replace(value, '''', '''''')` before interpolation, or a FetchXML query instead.

### When to use FetchXML instead

OData `$filter` cannot express link-entity joins, aggregation, or `distinct`. Use the **Fetch Xml Query** parameter on List rows for those. FetchXML is verbose but far easier to get right by hand, and it can be built and tested in a query tool before being pasted in — which makes it the better target for agentic editing on anything non-trivial.

Remember that XML inside a JSON string needs `"` escaped as `\"`.

---

## 4. Run-time versus import-time errors

| Error class | Caught by |
|---|---|
| Malformed JSON | `pac solution pack` |
| Unknown action referenced in `runAfter`, invalid status value, bad schema shape | `pac solution import`, and `Test-FlowDefinition.ps1` beforehand |
| Wrong `operationId`, nonexistent column, bad OData filter, null dereference, wrong type | **Nothing until the flow runs** |

The last row is the important one. **A successful import proves nothing about an expression.** Every expression change needs a test run, and the diagnosis lives in the run history: open the failed action and read its raw **inputs**, which show the expression's evaluated result rather than its source.

---

## Checklist before packing

- [ ] Every whole-value expression starts with `@` at position 0 and has no trailing text
- [ ] Every interpolated expression is wrapped in `@{}`
- [ ] Every action reference uses the underscore-encoded key and names an action that exists
- [ ] Every property hop that can be absent uses `?['...']`
- [ ] Lookup reads use `_fieldname_value`; choice comparisons use the numeric value
- [ ] Text values in `$filter` are wrapped in single quotes; GUIDs are not
- [ ] Interpolated values that may contain an apostrophe are escaped or the query uses FetchXML
- [ ] Parentheses and braces balance in every expression
