# Platform behaviour when shaping

What Dataverse and PAC CLI did in the plugin's live checks (spikes S2–S3, Phase 3, Phase 4; PAC CLI 2.14.1, platform 9.2), and what to do about it.

## Adding and removing

| Behaviour | Do |
|---|---|
| A column, form or view added without its table brings the table as a shell (behaviour 2) | Expected; the tool reports it as the child's parent shell |
| A relationship brings parents too — the referencing table as a shell plus the lookup column (1:N), the first table as a shell (N:N). This plugin version reports them as `unexpected` | Expected (SPEC Q12). Unless the change customizes them, remove them and read back; ask when in doubt |
| Removing a column, form or view leaves the parent shell in the WIP — and with it the table's ribbon diff | Remove the orphaned shell too (W13) |
| PAC adds a **table** with all its subcomponents (behaviour 0) | Never add a table through PAC |
| PAC reports *"has been added"* even when the component was already covered by an include-all table, or already present | Its message proves nothing; the read-back does (R1) |
| PAC 2.14.1 cannot add a model-driven app (type 80), and refuses type codes of 10000 and above as numbers | Add apps through MCP; pass the table's logical name for a code of 10000 and above (`--componentType connectionreference`) |
| A table already in the solution as a shell cannot become metadata by adding it again — the tool reports `alreadyPresent` and does not change it | Decide `tableBehavior` before the first add |

## Reading what is there

| Behaviour | Do |
|---|---|
| `list_solution_components` gives no name for relationships, workflows and types of 10000 and above, which show as `ComponentType<code>` | Identify them by ID with `query` on their table (`workflow`, `connectionreference`, …) |
| Codes of 10000 and above are not in the `componenttype` choice | Look the object up by ID in candidate tables |
| `get_required_components` lists a requirement's direct solution rows only: a column held through an include-all table shows as in no solution | Check the table's row too before calling a requirement unplaced |
| A new custom table's system columns (`<table>id`, `ownerid`, `statecode`, `createdon`, …) show as in no solution | They are created with the table; its **primary name column** is not — place it explicitly |
| Everything made in the maker portal also lands in the *Common Data Services Default Solution* (unmanaged, `Cr…`, behaviour 0) | Not a WIP, not a transport solution: ignore it in overlap and home checks |
| A component created inside a hand-made solution is held only there | That solution is another open WIP (W7): report it; the person decides whose WIP keeps it |
| `systemform.objecttypecode` and `savedquery.returnedtypecode` filter on the type code, not the logical name | Use `objectTypeCode` from `list_tables` |
| Splitting one group's work into two WIPs puts the table in both — metadata in one, the platform's shell in the other | Expected (W12); ribbon and table-property edits go in one of them only |
