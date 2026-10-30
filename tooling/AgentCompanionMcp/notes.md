# Agent Companion MCP — progress notes

> Working notes for building the plugin specified in [SPEC.md](SPEC.md). The spec is the source of truth: these notes track progress and hold findings until they are written back into it. **No client data** (R7) — no organization URLs, tenant IDs, environment GUIDs, user names or records from a client environment. Use the spec's placeholders.

---

## Status

| Phase | Status | Exit criterion |
|---|---|---|
| [0 — Spikes](SPEC.md#phase-0--spikes) | Not started | Every v1 spike has a written finding, and the spec is updated |
| [1 — Host, connections, approvals and log](SPEC.md#phase-1--host-connections-approvals-and-log) | Not started | See spec |
| [2 — Discovery](SPEC.md#phase-2--discovery) | Not started | See spec |
| [3 — Solution shaping](SPEC.md#phase-3--solution-shaping) | Not started | See spec |
| [4 — Agent skills and scripts](SPEC.md#phase-4--agent-skills-and-scripts) | Not started | See spec |
| [5 — Release](SPEC.md#phase-5--release) | Not started | See spec |

Status values: *Not started* · *In progress* · *Blocked* (say on what) · *Done*.

---

## Phase 0 — execution

### Tracks

The spikes run in three parallel tracks. Within a track, order matters; across tracks it does not.

| Track | Spikes, in order | Needs |
|---|---|---|
| **Plugin** | S1 together with S6, then S4 | XrmToolBox, a throwaway plugin shell, the target agent clients |
| **Platform** | S2 together with S3, then S7 | The shared development environment and fixture set; for S7, a build environment and the promotion pipeline |
| **Desk research** | S5, any time | A dated, sourced check of the Dataverse MCP server |

S8 is deferred with close-out strategy C ([SPEC §9.7](SPEC.md#97-close-out-strategies)).

### Gate

**S1 decides the architecture.** If an MCP host cannot run inside XrmToolBox on `net48` without shipping a private DLL, the host design in [SPEC §5](SPEC.md#5-architecture) changes. Run S1 first, or alongside S2, S3 and S5, whose findings stand whatever S1 concludes. Do not start S4, S6 or S7 until S1 has passed.

### Shared setup

- [ ] **Disposable development environment** with no client data. S2 and S3 add out-of-box tables to solutions; expect to reset it.
- [ ] **Publisher** for the spikes, with a placeholder prefix.
- [ ] **Fixture set**, used unchanged by S2, S3 and S7 so their results compare:
  - `account` (out-of-box table) and one custom table with one column and one alternate key;
  - the `account` main form and one `account` view;
  - one web resource;
  - one classic ribbon button on `account`, for the ribbon-diff question in S2.
- [ ] **Build environment and promotion pipeline** (S7 only).
- [ ] **Versions recorded** at the start of each spike: environment version, PAC CLI, XrmToolBox, agent clients.

### Timeboxes

Spikes are throwaway. Set a timebox when a spike starts and record it in the tracker. If it runs out, write up what is known, mark the spike *Blocked* or *Done — partial*, and decide with a person whether to extend it.

### Spike tracker

| ID | Track | Depends on | Status | Timebox | Feeds | Finding |
|---|---|---|---|---|---|---|
| S1 | Plugin | — | Not started | | [§5](SPEC.md#5-architecture) host, JSON-RPC layer, serialization; [§6.1](SPEC.md#61-protocol-surface) protocol revision | |
| S2 | Platform | — | Not started | | `tableBehavior` mapping in `add_solution_components`; component type codes; X6 | |
| S3 | Platform | — | Not started | | Path B per component type ([§4](SPEC.md#4-responsibility-split--mcp-vs-pac-cli)) | |
| S4 | Plugin | S1 | Not started | | Client configuration snippets; approval timeout default | |
| S5 | Desk research | — | Not started | | Read tools kept or dropped ([§6.3](SPEC.md#63-tool-summary)) | |
| S6 | Plugin | S1 (same shell) | Not started | | H1–H4 and C1–C10 ([§5](SPEC.md#host-and-connection-model)) | |
| S7 | Platform | S2; build environment | Not started | | A1 — whether the R1 check after promotion stays permanent | |
| S8 | — | Strategy C | Deferred | | C1, C2 | |

The *Finding* column links to the finding below once it is written.

---

## Findings

One entry per spike, written as soon as the spike ends, then applied to the spec. Mark *Applied* only when the spec change is made.

```markdown
### S<n> — <short title>

- **Date:** YYYY-MM-DD
- **Versions:** environment <version> · PAC CLI <version> · XrmToolBox <version> · clients <names and versions>
- **Question:** as in the spec
- **Steps:** what was run, in order — commands or requests, with placeholders for names and IDs
- **Observed:** what actually happened, including errors verbatim
- **Conclusion:** the answer, and how confident it is
- **Spec change:** section and what changes — *Applied* / *Pending*
```

No findings yet.

---

## Log

| Date | Entry |
|---|---|
| 2026-10-29 | Spec drafted through the resolved open questions; notes created. Phase 0 not started |
