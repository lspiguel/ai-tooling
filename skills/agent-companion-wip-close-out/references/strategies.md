# Close-out strategies in detail

## Comparison

|  | A — Promote to a build environment | B — Add to transport solutions in development |
|---|---|---|
| What happens to the WIP | Exported unmanaged by the promotion pipeline, imported into the build environment, copied there into one transport solution, then deleted there; the development WIP is deleted at close-out | Its components are added to the transport solution in development; the WIP is deleted |
| Source of downstream artifacts | Transport solutions extracted from the build environment | Transport solutions extracted from development |
| Isolation from in-flight work | Yes, apart from shared whole documents (X1) | No — edits to components already in transport ship with the next extract (X2) |
| Transport order enforced by | The person promoting; multi-solution deployment pipelines | Multi-solution deployment pipelines |
| R1 check after close-out | `list_solution_components` through the read-only build tab | `list_solution_components` on development |

The strategy belongs to each WIP, from where its destination is extracted from — not to the project. A project can use both.

## When the WIP's lifetime matters

- **Under B**, an in-place edit ships at the next extract whatever happens to its WIP. Ending the WIP early only removes clutter (X9).
- **Under A**, the lifetime is the release: an edit ships when its WIP is promoted, so promoting early ships early.

## Mixing strategies

| Situation | Effect | Do |
|---|---|---|
| The destination is extracted from build, but someone wants to add the components to the transport solution in development (B) | Nothing reaches build; development's copy of the transport solution drifts from build's | Do not offer it; promote (A). If the person asks for B anyway, say the work will not ship until it is promoted |
| A build environment is added to a project that used B | Components placed in development's transport solutions before the change are missing from build | A person seeds build once, before the first promotion. List what development's transport solutions hold that build's do not — `list_solution_components` on each, both tabs |
| A separate development environment for production fixes | Its transport source and pipelines can differ; its fixes must also reach the main development environment, or the next release from there overwrites them | Bind it in its own tab; establish its delivery context separately. Ask how a fix reaches the main development environment — re-applied there, or carried by the next deployment — and put it in the steps |
| An in-place edit where transport is extracted from build | The edit sits in development until promoted | The in-place WIP ends after promotion, not after import (W10) |

## Aligning development from build

Under A, a person may bring development in line with build (A8):

1. **Before:** list the open WIPs in development (`list_solutions`, `nameContains: "_Agent_WIP_"`, and any other unmanaged non-transport solution) and, for each, the whole documents it shares with the transport solution being aligned — the transport-home query from shaping, on development. Each is replaced by the build copy; the person decides whether to wait, or to re-apply those edits afterwards.
2. **Export unmanaged from build** — the person, or a pipeline they run:

   ```powershell
   pac solution export --name <transport> --path <work>\align\<transport>.zip --overwrite
   ```

   with the PAC profile on the **build** environment. No `--managed`: a managed project solution is never imported into development (R16).
3. **Import into development** with the PAC profile back on development — checked with `pac org who` — and no local edits to the zip (R14):

   ```powershell
   pac solution import --path <work>\align\<transport>.zip --publish-changes
   ```

   It publishes every pending customization in development (X11).
4. **After:** `list_solution_components` on the transport solution in development matches the same call on the build tab. Re-run the overwrite baseline for any open WIP that shares a document with it.

## Issues to check at close-out

| # | Issue | At close-out |
|---|---|---|
| X1 | In-flight changes leak through whole documents | A person reviews shared documents; under B, another story's edit to a shared form ships too |
| X3 | Deletions are not carried | Listed in the steps as person tasks |
| X4 | Moving a component between transport solutions | Not a close-out; a planned release |
| X5 | Required components are excluded | `get_required_components` before each close-out |
| X6 | Tables carry their ribbon diff | A table in the WIP — even a shell — overwrites the transport copy's ribbon diff on promotion; check overlap was handled |
| X7 | Connection references and environment variable values | Values come from deployment settings, not the WIP |
| X8 | Partial close-out | All of a story's placement WIPs together, in order, with no automated deployment in between |
| X9 | Leftover WIPs | Delete this task's WIPs; report others |
