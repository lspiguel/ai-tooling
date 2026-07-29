# [C.3] Project Setup — ALM and/or repository wiring: git repo initialization and solution export automation

> **Matrix cell:** [[C.3]](../ai-augmented-d365ce-activity-matrix.md#c3-project-setup) · Column **[3] Coding Assistants & Agentic Environments** · Grounding model: [A.3](./a.3-grounding.md) · Builds on [B.3](./b.3-initial-setup.md)

> **Prefer ALM if it exists.** If the engagement already has a Power Platform Pipelines / Azure DevOps "export & unpack" ALM process, use it — this local script is the fallback for when there is no pipeline yet, or for a developer's own working copy. Either way the *folder layout* below is what the assistant reads.

This guide stands up the **three repositories** described in [A.3 — Grounding](./a.3-grounding.md): `<client>-Context/` (§1), `<client>-d365/` (§2–§6), and `<client>-wiki/` (§2). Clone them as siblings under a single `<client>/` parent so one agent session can read across all three.

---

## What you need in place

| Requirement | Notes |
|---|---|
| Git | `choco install git -y` (machine baseline, [B.3](./b.3-initial-setup.md)). |
| PAC CLI (`pac`) | `dotnet tool install --global Microsoft.PowerApps.CLI.Tool`. Provides `solution export`/`unpack` and `auth`. |
| VS Code + Copilot / Cursor / Claude Code | The coding assistant that will read the repo. |
| A connection to the client's Dataverse environment | Export from the environment that is the source of truth for configuration — usually **Dev**. |
| The engagement's **template repo** | Fork the [B.3] template so the repo layout and automation scripts start consistent; this guide fills in the client specifics. |

---

## 1. Prepare the local, personal Context repository

Your own working copy of the engagement's intent and environment state — context packs, per-story folders, and the engagement documents. It stays personal and local; it does not merge into the shared repos below.

```
<client>-Context/
├── .gitignore
├── context-exporter/        D365 Context Exporter folder
│   ├── config/              Configuration files
│   ├── output/              Context Exporter .context.md packs
│   └── runs/                Ignored by .gitignore
├── XXX-story-1
│   ├── XXX-story-1.md       Replicated user stories content in markdown
│   ├── ...
│   └── ...
├── YYY-story-2
├── ZZZ-story-3
├── offline-access/
│   ├── deployment/
│   ├── guides/
│   ├── sow/
│   ├── ...
│   └── runbooks/
└── ...
```

---

## 2. Project and wiki repository layout

Two **shared** repositories per engagement, alongside the personal context repository from §1. They are split by audience and lifecycle rather than by convenience — [A.3](./a.3-grounding.md) explains why, and what each one answers.

### 2.1 `<client>-d365/` — the project repository

Fixed top-level layout so the assistant — and every teammate — always finds things in the same place:

```
<client>-d365/
├── src/                            PRO-CODE
│   ├── Plugins/                    Plugin & Custom API assemblies
│   ├── PCF/                        PCF controls
│   └── Functions/                  Azure Functions
├── solutions/                      UNPACKED SOLUTIONS (pac solution unpack output)
│   └── <SolutionName>/
│       ├── src/                    unpacked component tree
│       └── <SolutionName>.zip      exported managed/unmanaged zips (git-ignored)
├── webresources/                   WEB RESOURCES (JS, HTML, CSS) — editable source
├── context/                        Context Exporter .context.md packs (committed copy)
├── docs/                           ADRs, runbooks, architecture (docs-as-code)
├── scripts/                        Automation, incl. the export script below
├── .gitignore
└── README.md
```

Rationale for the split: **`src/`** is code that compiles, **`solutions/`** is Dataverse configuration unpacked to a diffable tree, **`webresources/`** is hand-authored client-script kept editable (not just the base64 blob inside the solution). Keeping these separate is what makes drift detection and grounded gap analysis meaningful later.

> Decide whether `webresources/` is authored here and deployed *into* the solution, or unpacked *out of* it, and stick to one direction — mixing the two causes merge pain.

### 2.2 `<client>-wiki/` — live documentation

The engagement's published documentation. Azure DevOps wikis are backed by a git repository (`<project>.wiki`, or whichever repo is designated as "published code as wiki"), so the assistant reads and writes it with the same clone/edit/commit/push flow as any other repo — which is what makes doc publishing scriptable and reviewable instead of a browser-only edit:

```
<client>-wiki/                      Local clone of <project>.wiki
├── .order                          Page order for the wiki tree
├── .attachments/                   Images and files referenced by pages
├── Home.md
├── Architecture/                   Solution architecture, integrations, ADR summaries
├── Functional/                     Module and process documentation, user guides
├── Technical/                      Component docs generated from the repo ([6.3])
├── Runbooks/                       Operational and deployment procedures
└── Release-Notes/                  What shipped, per increment
```

Clone it next to the project repo:

```PowerShell
git clone https://dev.azure.com/<org>/<project>/_git/<project>.wiki <client>-wiki
```

Keep it separate from `<client>-d365/docs/` deliberately. `docs/` holds artifacts reviewed *with the code* — ADRs, developer runbooks, architecture notes for the build. The wiki holds what the wider engagement reads: functional consultants, testers, support, the client. Different audience, different review gate, different update trigger. [6.3](./6.3-documentation.md) is where content is generated from the repo and published here.

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

> Clone the [B.3] template repo instead of `git init` if you want the repo layout and automation scripts to arrive pre-populated — then delete the template's git history (`Remove-Item -Recurse -Force .git; git init -b main`) and lay the client folders on top.

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

## 5. The solution export/unpack script

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

## 6. First run and commit

```PowerShell
# From the repo root
./scripts/Export-Solutions.ps1 -EnvironmentUrl https://<client>-dev.crm.dynamics.com `
    -Solutions <SolutionName>

git add .
git commit -m "Initial solution export + repo scaffold for <ClientName>"
```

Review the unpacked tree under `solutions/<Name>/src/` — you should see the entities, forms, views, web resources, and plugin-step definitions as files. That readable tree is what grounds every column-3 activity from here.

---

## 7. Wire up MCP and finish grounding

With the repo grounded, complete the [C.3] setup from the matrix:

- **Drop the Context Exporter packs** into `context/` — the same `.context.md` snapshots, now sitting next to the code for the assistant to read.
- **Make all three repositories visible to the assistant at once** — a multi-root workspace (`<client>.code-workspace`) in VS Code/Cursor, or additional working directories in Claude Code — so a single prompt can span the story, the unpacked solution and the wiki pages it affects ([A.3](./a.3-grounding.md)).
- **Register this environment's Dataverse MCP** so the assistant can `describe`/`search` live schema and read current rows, not just the point-in-time unpack. Remember: the admin must **allow-list the client per environment**, and tool calls from non-Copilot-Studio agents are **billable**.
- **Authenticate the Azure DevOps CLI** so the spec/plan/task rows can read and write the backlog: `az extension add --name azure-devops`, then `az devops configure --defaults organization=https://dev.azure.com/<org> project=<project>` so every `az boards` call defaults to this engagement without repeating the org/project flags.

This whole sequence — init → unpack → ground → wire-MCP — is a strong candidate for a **`d365-project-setup` skill** so it runs identically on every engagement.
