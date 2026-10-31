---
name: agent-companion-transport-bootstrap
description: Used together with the Agent Companion MCP - the XrmToolBox plugin that gives a coding agent person-approved access to Dynamics 365 CE / Dataverse environments - and needs a plugin tab bound to the development environment. Creates the transport solutions a change needs that do not exist yet - only the missing ones, empty, in transport order, under the right publisher - choosing an existing publisher and creating one only with the person's explicit, individually confirmed agreement; and brings a newly installed first-party app's components into view so the person can choose which to customize. Use when a transport solution or the publisher the work needs is missing - at the start of a project or at any later point, such as the first plug-in on a project that so far had only tables - or when a first-party app such as Sales, Customer Service or Field Service has been installed in the development environment.
---

# Transport bootstrap with the Agent Companion MCP

Transport solutions are long-lived, ordered, and each holds one group of component types. This skill creates the ones the work needs and that are missing — at the start of a project, or whenever a change first needs a group the project has not used yet. It does not put components into them: new components go into WIP solutions first (`agent-companion-wip-shaping`), and close-out places them.

**Preconditions.** The delivery context is established: the segmentation map and transport order, which transport solutions exist in each environment, and where each is extracted from (`agent-companion-context`). Without the plugin's tools, stop and say the Agent Companion MCP is needed.

---

## What this skill decides

| Decision | Rule |
|---|---|
| Which transport solutions to create | Only those the work needs that are missing, in transport order |
| Where they are created | In the environment they are extracted from — development under B. Under A the build environment's tab is read-only (A2): a person creates them there, and development receives them by alignment (A8) |
| Which publisher | An existing one — the project publisher, or for a shared-library solution the library's own. A new publisher only with the person's explicit certainty (R3) |

---

## 1. Find what is missing

From the delivery context, compare the transport solutions the work needs with those that exist — `list_solutions` with `managed: "unmanaged"` on development, and on the build tab where there is one — and with the solutions folder of `<client>-d365/`. Report the difference before creating anything.

A **managed** solution of the project's publisher in development stops the task (R16). Never create a transport solution by importing a managed build of it.

## 2. Choose the publisher (R3)

1. `list_publishers`. The transport solutions that exist show the project publisher; reuse it.
2. A **shared-library** transport solution is imported from the library's own publisher, as the library ships it. The agent does not create it, or a publisher for it.
3. Only when no suitable publisher exists, and the person is certain a new one is needed, propose `create_publisher`. It is off by default; the person switches it on for the connection, and confirms the call in the plugin by typing the prefix. A second publisher — even with the same prefix — creates layering problems that cannot be solved later; say so when proposing it. Switch it off again afterwards.

## 3. Create the missing transport solutions

State the steps first (T3): each solution's unique name, friendly name, publisher, and place in the order. Names come from the segmentation map or the person.

Under **B**, for each missing transport solution, in transport order:

1. `list_solutions` with `nameContains` set to the name: it must be free.
2. `create_solution` — `publisherUniqueName` from step 2, `version` `1.0.0.0` unless the person gives another, a `reason` (R12) such as *"Create the empty Assemblies transport solution for the project; first plug-in arrives with work item 1234."*
3. `list_solution_components` on it: empty.

Under **A**, prepare the same list for the person to create in the build environment, then confirm on the build tab with `list_solutions` that each exists and is empty. Development gets them by the alignment import a person runs (`agent-companion-wip-close-out`, A8).

Add them to the delivery context. The extraction pipeline may need the new solution's name before it picks it up — say so, and prepare the change for the person (T6).

## 4. A first-party app joined

A person installs the app in the development environment — outside the plugin. Every downstream environment needs the same app installed before any transport solution that references its components is imported; add that to the steps as a person task.

1. `list_solutions` with `managed: "managed"` and `nameContains` for the app — its solutions.
2. `list_solution_components` on each, and `query` over `appmodule`, `sitemap`, `systemform` and `savedquery`, show what the app added.
3. The person decides which of those components the project customizes now. Only those are pulled in; the rest stay out until a change needs them.
4. Each chosen component then goes through `agent-companion-wip-shaping` as **placement**: it has no transport home yet. Close-out places it in the transport solution its group owns.

---

## Guardrails — what a person confirms

| Moment | The person confirms |
|---|---|
| What is missing | The list of transport solutions to create, their names and order |
| Publisher | The existing publisher is reused; a new one only with explicit certainty, confirmed in the plugin (R3) |
| Under A | They create the transport solutions in the build environment and run the alignment |
| A first-party app | It is installed in development and will be installed downstream first; which of its components the project customizes now |
