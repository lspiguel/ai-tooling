# Rule index

The rules of the Agent Companion MCP specification (`tooling/D365AgentCompanionMcp/SPEC.md` in the AI Tooling repository), as the agent-companion skills cite them. The specification is the source; this index names each rule and the skill that applies it, and does not change any of them (K3). Where a line here and the specification differ, the specification wins.

## Non-negotiable rules (R)

| ID | Rule | Applied by |
|---|---|---|
| R1 | No required components and no subcomponents when adding components to a solution. Never pass `--AddRequiredComponents`; never add a table through PAC. Every add is followed by `list_solution_components` and a comparison with what was intended; anything unexpected stops the work until a person reviews it | shaping, bootstrap, close-out |
| R2 | Every plugin call except `list_connections` is approved by a person in the plugin | all (the plugin enforces it) |
| R3 | Publisher creation is confirmed individually, never for a session, and is off by default | bootstrap |
| R4 | Classic ribbon only; modern commanding is out of scope | edit loop |
| R5 | Unpacked format is the XML layout of `pac solution unpack`, not the YAML source-control layout | edit loop |
| R7 | No client data in the repository — logs, exchange files and samples stay outside the working tree. The only files the skills write into a repository are T5's delivery facts and the ledger, and only into the client project's own repositories | all |
| R8 | Text the agent supplies is untrusted and labelled as such in the plugin | all |
| R9 | One plugin tab per connection; every call names its connection; there is no default connection | context |
| R10 | One transport group per WIP; one transport home per component | shaping |
| R11 | WIP solutions are never deployed downstream; only transport solutions are | close-out |
| R12 | Write and Publisher calls state a non-blank `reason` | shaping, bootstrap, close-out |
| R13 | Every call is logged in the plugin tab | all (the plugin enforces it) |
| R14 | A transport solution is never the edit vehicle. Local edits reach development only through a WIP holding just the components being changed. The one import of a transport solution into development is the unmanaged alignment import from build under strategy A, with no local edits (A8) | edit loop, close-out |
| R15 | Delivery facts are derived or asked, never assumed; the steps are stated and confirmed before shaping and before close-out | context |
| R16 | No managed solution of the project is ever imported into development. A managed project solution found there stops the work | context, close-out |

## WIP rules (W)

| ID | Rule | Applied by |
|---|---|---|
| W1 | One group per WIP: classify every component with the segmentation map, including subtype rules | shaping |
| W2 | A component already in a transport solution stays in that group, even if the map changed since | shaping |
| W3 | A WIP uses the publisher of the transport solution it feeds | shaping |
| W4 | WIP names never start with a protected prefix | shaping |
| W5 | Create, import and close out a story's WIPs in transport order | shaping, edit loop, close-out |
| W6 | Dependencies point down the order; a component that requires one in a later group stops the work for a placement decision | shaping, close-out |
| W7 | Before adding a whole-document component, check for the same object in other open WIPs and report any overlap | shaping |
| W8 | Never in a WIP: record creation and update rules and their generated flows; managed tables with all subcomponents; components carrying another publisher's prefix | shaping |
| W9 | Route per component (placement when it has no transport home, in place when it has one); lifetime per WIP | shaping |
| W10 | Before ending an in-place WIP, read it back: every component must already have a transport home | close-out |
| W11 | Changing route never changes a component's group | shaping |
| W12 | A second WIP for one group of a story only by the person's choice; a component is in at most one of a story's WIPs | shaping |
| W13 | After removing a column, form or view, remove the orphaned parent table shell when nothing else needs it | shaping |

## Issues common to every close-out (X)

| ID | Issue |
|---|---|
| X1 | In-flight changes leak through whole documents (forms, views, sitemaps, ribbon diffs, web resources, workflows, app modules) |
| X2 | A WIP isolates only what is not yet in a transport solution when transport is extracted from development |
| X3 | Deletions are not carried; they are separate, person-run tasks |
| X4 | Moving a component between transport solutions risks deleting it downstream |
| X5 | Required components are excluded, so everything a WIP depends on must already exist where it lands |
| X6 | Any table in a WIP exports its whole unmanaged ribbon diff, and a metadata-only table its table properties |
| X7 | Connection references and environment variable values travel with the WIP |
| X8 | Partial close-out: a build between a story's close-outs carries half a story |
| X9 | Leftover WIP solutions distort overlap checks |
| X10 | Inactive forms are not exported |
| X11 | `pac solution import --publish-changes` publishes every pending customization in the environment |

## Delivery context (T)

| ID | Rule |
|---|---|
| T1 | Derive first, then ask; never fill a gap with a default without saying so |
| T2 | Ask with a recommendation, its reason and what the alternatives change |
| T3 | Steps before action: before the first Write call and before close-out, the person confirms the concrete steps |
| T4 | Transport homes are looked up where transport is extracted from — build under A, development under B |
| T5 | Recorded facts go in `<client>-Context/` where it exists, otherwise next to the segmentation map; they are checked against the environments when used |
| T6 | Pipeline runs, commits and pushes are a person's, unless that specific one is delegated; never a downstream deployment or one that deploys a WIP |

## Close-out strategies (A, B)

| ID | Consideration |
|---|---|
| A1 | The promotion pipeline's copy step is an add path outside the plugin; R1's check runs after every promotion, against the build environment |
| A2 | The build environment is bound read-only: a second tab, never confirmed as development |
| A3 | One promotion per WIP, in transport order |
| A4 | Missing dependencies fail the import into build; check before requesting a promotion |
| A5 | Deletions in the build environment are made by a person |
| A6 | The development WIP survives promotion and is deleted at close-out |
| A7 | Promotion validation is the backstop, not the plan |
| A8 | Development may be aligned from build by an unmanaged export and import of a transport solution, run by a person, after reviewing open WIPs' shared documents |
| B1 | Under B nothing validates placement at close-out except the skill and the person approving |
| B2 | Under B a WIP gives scope and an overwrite check, not a hold on release |
| B3 | Under B the transport solution is verified with `list_solution_components` after close-out |

Strategies C (Git-linked solutions) and D (Power Platform pipelines) are not supported by these skills.

## Skill coexistence (K)

| ID | Rule |
|---|---|
| K1 | Skills apply on the state of the work, never on the project's phase |
| K2 | One owner per decision; a skill without the owner's result applies the owner skill |
| K3 | Rules come from the specification, by ID, with their meaning kept |
| K4 | No skill-local defaults for delivery facts |
| K5 | One shared record: delivery context, confirmed steps, WIP ledger |
| K6 | The environment wins over the ledger |
| K7 | Skills that do not depend on the plugin keep their procedures; the agent-companion skills defer to them for the steps they cover |
| K8 | When two instructions still differ, stop and tell the person |
