# Package Managers and Tools for Work Automation

This guide lists the package managers and CLI tools used to set up a machine for AI-augmented development work. This profile targets general-purpose, functionally-oriented assistant work, where Python is used only occasionally — so tooling is installed via Chocolatey rather than pulling in the full Visual Studio IDE. Each section gives an **unattended** install suitable for scripted/repeatable provisioning. Unless noted otherwise, commands should be run from a PowerShell console opened **as Administrator**.

> After installing any of these, remember to tell your AI assistant that you have them available — see section 10 for a ready-to-use prompt.

## Contents

1. [Chocolatey](#1-chocolatey)
2. [.NET SDK](#2-net-sdk)
3. [pip (Python)](#3-pip-python)
4. [npm (Node.js)](#4-npm-nodejs)
5. [Visual Studio Code](#5-visual-studio-code)
6. [PAC CLI (Power Platform CLI)](#6-pac-cli-power-platform-cli)
7. [Azure CLI](#7-azure-cli)
8. [PandaDoc](#8-pandadoc)
9. [mmdc (Mermaid CLI)](#9-mmdc-mermaid-cli)
10. [Telling your AI assistant](#10-telling-your-ai-assistant)
11. [Appendix: Combined unattended bootstrap script](#appendix-combined-unattended-bootstrap-script)

---

## 1. Chocolatey

An open-source Windows package manager for installing and managing software from the command line. Install it first — the .NET SDK, Python, and Node.js sections below all rely on it.

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

## 2. .NET SDK

Provides the `dotnet` CLI and runtime — required by the PAC CLI (section 6, installed as a .NET global tool) and by any .NET build or scripting work.

```PowerShell
choco install dotnet-sdk -y
```

If .NET is already installed, this is safe. Chocolatey only tracks packages it installed itself, so it will run the SDK installer regardless of how .NET got onto the machine. But .NET SDKs install side by side by version and the installer is idempotent: an already-present version is left as-is (a no-op/repair) and a newer one is simply added alongside the existing ones. You won't break or downgrade an existing install.

---

## 3. pip (Python)

Python is used only occasionally in this general-purpose/functional workflow, but it's worth having on hand for scripting and tooling.

```PowerShell
choco install python -y
```

(`pip` is bundled with this package automatically.)

---

## 4. npm (Node.js)

```PowerShell
choco install nodejs-lts -y
```

(`npm` is bundled with this package automatically.)

---

## 5. Visual Studio Code

Lightweight, cross-platform code editor. Handy for quick edits, scripting, and general-purpose work.

```PowerShell
winget install --id Microsoft.VisualStudioCode -e --silent --accept-package-agreements --accept-source-agreements
```

(`winget` ships by default on Windows 10 1809+ and Windows 11 via the App Installer.)

---

## 6. PAC CLI (Power Platform CLI)

Command line for Power Platform: Dataverse, solutions, environments, and ALM automation. Installed as a .NET global tool, so it requires the .NET SDK from section 2:

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

- Chocolatey (`choco`).
- .NET SDK (`dotnet`).
- Python and pip.
- Node.js and npm.
- Visual Studio Code.
- PAC CLI (`pac`).
- Azure CLI (`az`).
- PandaDoc CLI — converts documents (PDF, DOCX) to/from Markdown.
- Mermaid CLI (`mmdc`) — renders Mermaid diagrams to PNG/SVG/PDF.

When a task would benefit from one of these tools, use it, offer to describe usage steps, or prepare a command line for the user to execute.
```

---

## Appendix: Combined unattended bootstrap script

For provisioning a fresh machine end to end, run the following as Administrator. It installs Chocolatey, then the .NET SDK, Python, Node.js, and the remaining tools with no prompts:

```PowerShell
# Chocolatey
Set-ExecutionPolicy Bypass -Scope Process -Force; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
choco feature enable -n allowGlobalConfirmation

# Chocolatey-managed tools (dotnet-sdk provides dotnet; python provides pip; nodejs-lts provides npm)
choco install dotnet-sdk python nodejs-lts notepadplusplus postman git xrmtoolbox pandadoc -y

# Refresh PATH so dotnet, python, and npm are usable later in this same session
refreshenv

# Visual Studio Code
winget install --id Microsoft.VisualStudioCode -e --silent --accept-package-agreements --accept-source-agreements

# PAC CLI (requires the .NET SDK installed above)
dotnet tool install --global Microsoft.PowerApps.CLI.Tool

# Azure CLI
winget install --exact --id Microsoft.AzureCLI --silent --accept-package-agreements --accept-source-agreements

# Mermaid CLI
npm install -g @mermaid-js/mermaid-cli
```
