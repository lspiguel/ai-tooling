# Package Managers and Tools for Work Automation

This guide lists the package managers and CLI tools used to set up a machine for AI-augmented development work. Each section includes both an **interactive** install (for reference) and an **unattended** install (for scripted/repeatable provisioning). Unless noted otherwise, unattended commands should be run from a PowerShell console opened **as Administrator**.

> After installing any of these, remember to tell your AI assistant that you have them available — see section 10 for a ready-to-use prompt.

## Contents

1. [Visual Studio](#1-visual-studio)
2. [pip (Python)](#2-pip-python)
3. [npm (Node.js)](#3-npm-nodejs)
4. [Chocolatey](#4-chocolatey)
5. [Visual Studio Code](#5-visual-studio-code)
6. [PAC CLI (Power Platform CLI)](#6-pac-cli-power-platform-cli)
7. [Azure CLI](#7-azure-cli)
8. [PandaDoc](#8-pandadoc)
9. [mmdc (Mermaid CLI)](#9-mmdc-mermaid-cli)
10. [Telling your AI assistant](#10-telling-your-ai-assistant)
11. [Appendix: Combined unattended bootstrap script](#appendix-combined-unattended-bootstrap-script)

---

## 1. Visual Studio

If you don't have a license, the **Community** edition is free; **Professional** and **Enterprise** require a paid license. Visual Studio can be downloaded and installed entirely from the command line, unattended, using `winget` (ships by default on Windows 10 1809+ and Windows 11 via the App Installer). Pick the edition you need by swapping the `--id`:

| Edition | winget `--id` |
|---|---|
| Community | `Microsoft.VisualStudio.2022.Community` |
| Professional | `Microsoft.VisualStudio.2022.Professional` |
| Enterprise | `Microsoft.VisualStudio.2022.Enterprise` |

The `--override` string is passed straight to the Visual Studio installer, so `--add` (workload/component selection), `--quiet`/`--passive`, and `--wait` behave exactly as they would when calling the bootstrapper directly. `--wait` is important: without it, `winget` returns as soon as the bootstrapper launches, before Visual Studio has actually finished installing.

Workloads and individual components installed below:

| Workload | ID |
|---|---|
| ASP.NET and web development | `Microsoft.VisualStudio.Workload.NetWeb` |
| Azure and AI development | `Microsoft.VisualStudio.Workload.Azure` |
| Python development | `Microsoft.VisualStudio.Workload.Python` |
| Node.js development | `Microsoft.VisualStudio.Workload.Node` |
| .NET desktop development | `Microsoft.VisualStudio.Workload.ManagedDesktop` |

| Individual component | ID |
|---|---|
| .NET Framework 4.6.1 targeting pack | `Microsoft.Net.Component.4.6.1.SDK` |
| .NET Framework 4.6.2 targeting pack | `Microsoft.Net.Component.4.6.2.TargetingPack` |
| .NET Framework 4.7 targeting pack | `Microsoft.Net.Component.4.7.TargetingPack` |
| .NET Framework 4.7.1 targeting pack | `Microsoft.Net.Component.4.7.1.TargetingPack` |

```PowerShell
param(
    [ValidateSet("Community", "Professional", "Enterprise")]
    [string]$Edition = "Community"
)

$workloadsAndComponents = @(
    "Microsoft.VisualStudio.Workload.NetWeb",              # ASP.NET and web development
    "Microsoft.VisualStudio.Workload.Azure",                # Azure and AI development
    "Microsoft.VisualStudio.Workload.Python",                # Python development
    "Microsoft.VisualStudio.Workload.Node",                  # Node.js development
    "Microsoft.VisualStudio.Workload.ManagedDesktop",         # .NET desktop development
    "Microsoft.Net.Component.4.6.1.SDK",                      # .NET Framework 4.6.1 targeting pack
    "Microsoft.Net.Component.4.6.2.TargetingPack",            # .NET Framework 4.6.2 targeting pack
    "Microsoft.Net.Component.4.7.TargetingPack",              # .NET Framework 4.7 targeting pack
    "Microsoft.Net.Component.4.7.1.TargetingPack"             # .NET Framework 4.7.1 targeting pack
)
$addArgs = ($workloadsAndComponents | ForEach-Object { "--add $_" }) -join " "

winget install --id "Microsoft.VisualStudio.2022.$Edition" -e --silent `
  --accept-package-agreements --accept-source-agreements `
  --override "--quiet --wait --norestart $addArgs --includeRecommended"
```

---

## 2. pip (Python)

Included with Visual Studio's Python workload — skip this if you installed it in step 1.

Standalone unattended install:

```PowerShell
winget install --id Python.Python.3.12 -e --silent --accept-package-agreements --accept-source-agreements
```

(`pip` is bundled with this installer automatically.)

---

## 3. npm (Node.js)

Included with Visual Studio's Node.js workload — skip this if you installed it in step 1.

Standalone unattended install:

```PowerShell
winget install --id OpenJS.NodeJS.LTS -e --silent --accept-package-agreements --accept-source-agreements
```

---

## 4. Chocolatey

An open-source Windows package manager for installing and managing software from the command line.

Install (already unattended — this script is non-interactive by design):

```PowerShell
Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
```

By default, `choco install` prompts for confirmation. Either pass `-y` on every call, or enable global confirmation once so every future install is unattended:

```PowerShell
choco feature enable -n allowGlobalConfirmation
```

Example package installs:

```Shell
choco install notepadplusplus -y
choco install xrmtoolbox -y
choco install postman -y
choco install git -y
```

---

## 5. Visual Studio Code

Lightweight, cross-platform code editor — distinct from Visual Studio itself. Handy alongside Visual Studio for quick edits, scripting, and non-.NET work.

```PowerShell
winget install --id Microsoft.VisualStudioCode -e --silent --accept-package-agreements --accept-source-agreements
```

---

## 6. PAC CLI (Power Platform CLI)

Command line for Power Platform: Dataverse, solutions, environments, and ALM automation. Installed as a .NET global tool (already unattended):

```Shell
dotnet tool install --global Microsoft.PowerApps.CLI.Tool
```

---

## 7. Azure CLI

Cross-platform CLI to manage Azure resources.

```PowerShell
winget install --exact --id Microsoft.AzureCLI --silent --accept-package-agreements --accept-source-agreements
```

---

## 8. PandaDoc

Document automation tool; converts most document formats (PDF, DOCX) to Markdown and vice versa.

```Shell
choco install pandadoc -y
```

---

## 9. mmdc (Mermaid CLI)

Generates diagrams (PNG/SVG/PDF) from Mermaid definitions — useful for docs and architecture diagrams. `npm install -g` is already non-interactive:

```Shell
npm install -g @mermaid-js/mermaid-cli
```

---

## 10. Telling your AI assistant

Even once these tools are installed, an AI coding assistant won't know they're available unless you tell it — it may otherwise default to walking you through manual installs or asking permission before using a CLI tool it doesn't know it has. Give it this context once, using whatever persistent instructions/memory mechanism it supports (a project instructions file such as `CLAUDE.md`, `AGENTS.md`, `.github/copilot-instructions.md`, or a chat-level "custom instructions"/memory feature):

```text
The following developer tools are installed and available on this machine's command line. Assume they can be used directly, and commit this to your memory:

- Visual Studio (edition: Community/Professional/Enterprise — specify which), with the ASP.NET and web development, Azure and AI development, Python development, Node.js development, and .NET desktop development workloads.
- Python and pip.
- Node.js and npm.
- Chocolatey (`choco`).
- Visual Studio Code.
- PAC CLI (`pac`).
- Azure CLI (`az`).
- PandaDoc CLI — converts documents (PDF, DOCX) to/from Markdown.
- Mermaid CLI (`mmdc`) — renders Mermaid diagrams to PNG/SVG/PDF.

When a task would benefit from one of these tools, use it, offer to describe usage steps, or prepare a command line for the user to execute.
```

---

## Appendix: Combined unattended bootstrap script

For provisioning a fresh machine end to end, run the following as Administrator. It installs Chocolatey, Visual Studio (edition selectable via `-Edition`), and the remaining tools with no prompts:

```PowerShell
param(
    [ValidateSet("Community", "Professional", "Enterprise")]
    [string]$Edition = "Community"
)

# Visual Studio (includes Python + Node.js workloads, which provide pip and npm — see section 1 for the full workload/component list)
$vsWorkloadsAndComponents = @(
    "Microsoft.VisualStudio.Workload.NetWeb",
    "Microsoft.VisualStudio.Workload.Azure",
    "Microsoft.VisualStudio.Workload.Python",
    "Microsoft.VisualStudio.Workload.Node",
    "Microsoft.VisualStudio.Workload.ManagedDesktop",
    "Microsoft.Net.Component.4.6.1.SDK",
    "Microsoft.Net.Component.4.6.2.TargetingPack",
    "Microsoft.Net.Component.4.7.TargetingPack",
    "Microsoft.Net.Component.4.7.1.TargetingPack"
)
$vsAddArgs = ($vsWorkloadsAndComponents | ForEach-Object { "--add $_" }) -join " "

winget install --id "Microsoft.VisualStudio.2022.$Edition" -e --silent `
  --accept-package-agreements --accept-source-agreements `
  --override "--quiet --wait --norestart $vsAddArgs --includeRecommended"

# Chocolatey
Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
choco feature enable -n allowGlobalConfirmation

# Chocolatey-managed tools
choco install notepadplusplus postman git xrmtoolbox pandadoc -y

# Visual Studio Code
winget install --id Microsoft.VisualStudioCode -e --silent --accept-package-agreements --accept-source-agreements

# PAC CLI
dotnet tool install --global Microsoft.PowerApps.CLI.Tool

# Azure CLI
winget install --exact --id Microsoft.AzureCLI --silent --accept-package-agreements --accept-source-agreements

# Mermaid CLI
npm install -g @mermaid-js/mermaid-cli
```
