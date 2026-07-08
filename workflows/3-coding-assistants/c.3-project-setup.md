# [C.3] Project Setup — git repo initialization and solution export automation

> **Prefer ALM if it exists.** If the engagement already has a Power Platform Pipelines / Azure DevOps "export & unpack" ALM process, use it — this local script is the fallback for when there is no pipeline yet, or for a developer's own working copy. Either way the *folder layout* below is what the assistant reads.

## Contents

1. [Prerequisites](#1-prerequisites)
2. [Standard repository layout](#2-standard-repository-layout)
3. [Initialize the repository](#3-initialize-the-repository)
4. [The `.gitignore`](#4-the-gitignore)
5. [The AI instruction stack](#5-the-ai-instruction-stack)
6. [The solution export/unpack script](#6-the-solution-exportunpack-script)
7. [First run and commit](#7-first-run-and-commit)
8. [Wire up MCP and finish grounding](#8-wire-up-mcp-and-finish-grounding)

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| Git | `choco install git -y` (machine baseline, [B.3](./b.3-initial-setup.md)). |
| PAC CLI (`pac`) | `dotnet tool install --global Microsoft.PowerApps.CLI.Tool`. Provides `solution export`/`unpack` and `auth`. |
| VS Code + Copilot / Cursor / Claude Code | The coding assistant that will read the repo. |
| A connection to the client's Dataverse environment | Export from the environment that is the source of truth for configuration — usually **Dev**. |
| The engagement's **template repo** | Fork the [B.3] template so the instruction/agent/skill files start consistent; this guide fills in the client specifics. |

---

## 2. Standard repository layout

One repo (or a small set) per engagement, with a fixed top-level layout so the assistant — and every teammate — always finds things in the same place:

```
<client>-d365/
├── .github/
│   ├── copilot-instructions.md     Always-on project instructions (Copilot)
│   ├── agents/                     *.agent.md personas (reviewer, plugin-dev, PCF)
│   └── skills/                     <name>/SKILL.md reusable task instructions
├── .claude/                        Claude Code equivalents (agents/, skills/)
├── AGENTS.md                       Build/test/"done" workflow rules (both ecosystems)
├── CLAUDE.md                       Claude Code always-on instructions
├── src/                            PRO-CODE
│   ├── Plugins/                    Plugin & Custom API assemblies
│   ├── PCF/                        PCF controls
│   └── Functions/                  Azure Functions
├── solutions/                      UNPACKED SOLUTIONS (pac solution unpack output)
│   └── <SolutionName>/
│       ├── src/                    unpacked component tree
│       └── <SolutionName>.zip      exported managed/unmanaged zips (git-ignored)
├── webresources/                   WEB RESOURCES (JS, HTML, CSS) — editable source
├── context/                        Context Exporter .context.md packs (see C.1)
├── docs/                           ADRs, runbooks, architecture (docs-as-code)
├── scripts/                        Automation, incl. the export script below
├── .gitignore
└── README.md
```

Rationale for the split: **`src/`** is code that compiles, **`solutions/`** is Dataverse configuration unpacked to a diffable tree, **`webresources/`** is hand-authored client-script kept editable (not just the base64 blob inside the solution), **AI instruction files** live where each assistant natively discovers them. Keeping these separate is what makes drift detection and grounded gap analysis meaningful later.

> Decide whether `webresources/` is authored here and deployed *into* the solution, or unpacked *out of* it, and stick to one direction — mixing the two causes merge pain.

---

## 3. Initialize the repository

```PowerShell
param(
    [Parameter(Mandatory)] [string]$ClientName,
    [string]$Root = (Get-Location).Path
)

$repo = Join-Path $Root "$ClientName-d365"
New-Item -ItemType Directory -Force -Path $repo | Out-Null

# Standard folders
$folders = @(
    ".github/agents", ".github/skills",
    ".claude/agents", ".claude/skills",
    "src/Plugins", "src/PCF", "src/Functions",
    "solutions",
    "webresources",
    "context",
    "docs",
    "scripts"
)
foreach ($f in $folders) {
    New-Item -ItemType Directory -Force -Path (Join-Path $repo $f) | Out-Null
    # Keep empty folders in git until real content lands
    New-Item -ItemType File -Force -Path (Join-Path $repo "$f/.gitkeep") | Out-Null
}

Set-Location $repo
git init -b main
"# $ClientName — Dynamics 365 CE" | Out-File -Encoding utf8 README.md
Write-Host "Initialized $repo"
```

> Clone the [B.3] template repo instead of `git init` if you want the instruction stack, agent personas, and skills to arrive pre-populated — then delete the template's git history (`Remove-Item -Recurse -Force .git; git init -b main`) and lay the client folders on top.

---

## 4. The `.gitignore`

Commit the **unpacked** solution tree, never the binary zips or build output:

```gitignore
# Dataverse solution zips (source of truth is the unpacked tree)
solutions/**/*.zip

# PAC / build artifacts
**/bin/
**/obj/
**/out/
*.dll
*.pdb

# Node / PCF
node_modules/
**/node_modules/

# Context Exporter working data (packs in /context are committed; runs are not)
context/runs/
context/output/

# Local settings / secrets
*.user
.env
appsettings.local.json
```

Keep the `.zip` **out** of git deliberately: the diffable `solutions/<Name>/src/` tree is the reviewable source; the zip is a rebuildable artifact.

---

## 5. The AI instruction stack

Fork the template files and fill in the client specifics — this is what carries conventions into every assistant session:

- **`copilot-instructions.md` / `CLAUDE.md`** — always-on, project-wide. Set the **publisher prefix**, environment URLs, "no early binding," StyleCop, Repository pattern, and the standing rule *"tables change fast → describe via Dataverse MCP, don't assume schema."* Keep it under ~1,000 words.
- **`AGENTS.md`** — how to build, which test command, what "done" means. Nestable per subdirectory (`src/Plugins` vs `src/PCF`).
- **`.github/agents/*.agent.md`** (`.claude/agents/*`) — named personas with a constrained tool list.
- **`.github/skills/<name>/SKILL.md`** (`.claude/skills/*`) — reusable task instructions, auto-discovered.

Fill in per client: **publisher prefix**, **environment URLs**, **naming conventions**, and the **data-boundary rule**. See [B.3](./b.3-initial-setup.md) for the canonical stack.

---

## 6. The solution export/unpack script

Drop this in `scripts/Export-Solutions.ps1`. It authenticates against the environment, exports each named solution (unmanaged, and optionally managed), and unpacks it into `solutions/<Name>/src/` — a clean, diffable tree ready to commit.

```PowerShell
<#
.SYNOPSIS
    Export Dynamics 365 CE solutions and unpack them into the repo for diffing.
.DESCRIPTION
    Local ALM fallback: pac solution export -> pac solution unpack.
    Run from the repo root. Commits are left to the caller so you can review the diff.
.EXAMPLE
    ./scripts/Export-Solutions.ps1 -EnvironmentUrl https://contoso-dev.crm.dynamics.com `
        -Solutions Core,Sales -Managed
#>
param(
    [Parameter(Mandatory)] [string]$EnvironmentUrl,
    [Parameter(Mandatory)] [string[]]$Solutions,
    [switch]$Managed,                       # also export a managed copy
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

# 1. Authenticate (interactive; reuses an existing matching profile if present)
$auth = pac auth list
if ($LASTEXITCODE -ne 0 -or ($auth -notmatch [regex]::Escape($EnvironmentUrl))) {
    pac auth create --environment $EnvironmentUrl
} else {
    pac auth select --environment $EnvironmentUrl
}

$solutionsDir = Join-Path $RepoRoot "solutions"

foreach ($name in $Solutions) {
    Write-Host "=== $name ===" -ForegroundColor Cyan
    $targetDir = Join-Path $solutionsDir $name
    $srcDir    = Join-Path $targetDir "src"
    New-Item -ItemType Directory -Force -Path $srcDir | Out-Null

    # 2. Export unmanaged (the editable source of truth)
    $unmanagedZip = Join-Path $targetDir "$name.zip"
    pac solution export --name $name --path $unmanagedZip --managed false --overwrite

    # 3. Unpack into a diffable tree. --processCanvasApps expands canvas app sources too.
    pac solution unpack --zipfile $unmanagedZip --folder $srcDir `
        --packagetype Unmanaged --allowDelete true --processCanvasApps

    # 4. Optionally export a managed copy (kept as an artifact, git-ignored)
    if ($Managed) {
        $managedZip = Join-Path $targetDir "$name`_managed.zip"
        pac solution export --name $name --path $managedZip --managed true --overwrite
    }
}

Write-Host "`nDone. Review the diff under solutions/ and commit when it looks right." -ForegroundColor Green
git -C $RepoRoot status --short solutions
```

Notes:

- **`--allowDelete true`** on unpack removes components that no longer exist in the solution, so the tree tracks deletions instead of accumulating stale files. Review the diff before committing.
- The **zips are git-ignored** (step 4 of §4) — only the unpacked `src/` tree is committed.
- Re-running this script is the standard **refresh** gesture: export → unpack → review diff → commit. That commit *is* your record of what changed in the environment between runs.
- For a **managed-only** downstream environment, unpack with `--packagetype Managed`; for a repo that tracks both, `Both`.

---

## 7. First run and commit

```PowerShell
# From the repo root
./scripts/Export-Solutions.ps1 -EnvironmentUrl https://<client>-dev.crm.dynamics.com `
    -Solutions <SolutionName>

git add .
git commit -m "Initial solution export + repo scaffold for <ClientName>"
```

Review the unpacked tree under `solutions/<Name>/src/` — you should see the entities, forms, views, web resources, and plugin-step definitions as files. That readable tree is what grounds every column-3 activity from here.

---

## 8. Wire up MCP and finish grounding

With the repo grounded, complete the [C.3] setup from the matrix:

- **Drop the Context Exporter packs** into `context/` (produced per [C.1](../1-general-purpose-assistants/c.1-project-setup.md)) — the same `.context.md` snapshots, now sitting next to the code for the assistant to read.
- **Register this environment's Dataverse MCP** so the assistant can `describe`/`search` live schema and read current rows, not just the point-in-time unpack. Remember: the admin must **allow-list the client per environment**, and tool calls from non-Copilot-Studio agents are **billable**.
- **Register the ADO/GitHub MCP** so the spec/plan/task rows can read and write the backlog.
- **Bootstrap the instruction files** from the actual repo (`/init` or the equivalent) so `copilot-instructions.md` / `CLAUDE.md` reflect what's really there.

This whole sequence — init → unpack → ground → wire-MCP — is a strong candidate for a **`d365-project-setup` skill** so it runs identically on every engagement.
