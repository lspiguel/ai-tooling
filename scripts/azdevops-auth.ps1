#Requires -Version 5.1
<#
.SYNOPSIS
    Authenticates the Azure CLI + azure-devops extension using a PAT and
    persists the org / project defaults so every subsequent `az devops` command
    just works — including when called from other tools.

.DESCRIPTION
    This script sets up authentication for Azure DevOps using Azure CLI.
    It prompts for organization URL, project name, and Personal Access Token (PAT).
    It configures az devops defaults and sets environment variables.

    Usage:
        .\azdevops-auth.ps1                          # prompts for everything
        .\azdevops-auth.ps1 -OrgUrl <org-url> -Project <project>  # prompts for PAT only

    Or set env vars to make it fully non-interactive:
        $env:AZURE_DEVOPS_EXT_PAT = "<your-pat>"
        $env:AZDEVOPS_ORG = "https://dev.azure.com/myorg"
        $env:AZDEVOPS_PROJECT = "MyProject"
        .\azdevops-auth.ps1

.PARAMETER OrgUrl
    The Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)

.PARAMETER Project
    The Azure DevOps project name

.EXAMPLE
    .\azdevops-auth.ps1

.EXAMPLE
    .\azdevops-auth.ps1 -OrgUrl "https://dev.azure.com/myorg" -Project "MyProject"
#>

param (
    [string]$OrgUrl,
    [string]$Project
)

# ── Colour helpers ─────────────────────────────────────────────────────────
$Green = [ConsoleColor]::Green
$Yellow = [ConsoleColor]::Yellow
$Red = [ConsoleColor]::Red
$NC = [ConsoleColor]::White

function Write-Info {
    param([string]$Message)
    Write-Host "[✔] $Message" -ForegroundColor $Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor $Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[✘] $Message" -ForegroundColor $Red
    exit 1
}

function Write-Ask {
    param([string]$Message)
    Write-Host "[?] $Message" -ForegroundColor $Yellow
}

# ── 1. Check az CLI is installed ────────────────────────────────────────────
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI not found. Install it from https://aka.ms/installazurecli"
}
$azVersion = az version --query '"azure-cli"' -o tsv 2>$null
Write-Info "Azure CLI found: $azVersion"

# ── 2. Ensure azure-devops extension is present ─────────────────────────────
$extensionCheck = az extension show --name azure-devops 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Warn "azure-devops extension not installed — installing now…"
    az extension add --name azure-devops --yes
}
Write-Info "azure-devops extension ready"

# ── 3. Collect org URL ──────────────────────────────────────────────────────
$org = if ($OrgUrl) { $OrgUrl } elseif ($env:AZDEVOPS_ORG) { $env:AZDEVOPS_ORG } else { $null }
if (-not $org) {
    Write-Ask "Enter your Azure DevOps organisation URL"
    Write-Ask "  Example: https://dev.azure.com/myorg  or  https://myorg.visualstudio.com"
    $org = Read-Host "  Org URL"
}
# Normalise: strip trailing slash
$org = $org.TrimEnd('/')
if (-not $org) {
    Write-Error "Organisation URL cannot be empty."
}
Write-Info "Organisation: $org"

# ── 4. Collect project name ─────────────────────────────────────────────────
$project = if ($Project) { $Project } elseif ($env:AZDEVOPS_PROJECT) { $env:AZDEVOPS_PROJECT } else { $null }
if (-not $project) {
    Write-Ask "Enter your Azure DevOps project name (leave blank to skip setting a default):"
    $project = Read-Host "  Project"
}
if ($project) {
    Write-Info "Project: $project"
}

# ── 5. Collect PAT ──────────────────────────────────────────────────────────
$pat = $env:AZURE_DEVOPS_EXT_PAT
if (-not $pat) {
    Write-Ask "Enter your Personal Access Token (input hidden):"
    $pat = Read-Host "  PAT" -AsSecureString
    $pat = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pat))
}
if (-not $pat) {
    Write-Error "PAT cannot be empty."
}
$env:AZURE_DEVOPS_EXT_PAT = $pat
Write-Info "PAT accepted (not echoed)"

# ── 6. Persist defaults in az CLI config ────────────────────────────────────
$defaults = "organization=$org"
if ($project) {
    $defaults += " project=$project"
}
az devops configure --defaults $defaults
Write-Info "az devops defaults saved: $defaults"

# ── 7. Set environment variables in current session ─────────────────────────
$env:AZDEVOPS_ORG = $org
if ($project) {
    $env:AZDEVOPS_PROJECT = $project
}
Write-Info "Environment variables set for current session"

# ── 8. Optionally add to PowerShell profile ────────────────────────────────
$profilePath = $PROFILE
$envContent = @"
# Azure DevOps environment — added by azdevops-auth.ps1
# Regenerate by running azdevops-auth.ps1 again.
`$env:AZURE_DEVOPS_EXT_PAT = "$pat"
`$env:AZDEVOPS_ORG = "$org"
"@
if ($project) {
    $envContent += "`n`$env:AZDEVOPS_PROJECT = `"$project`""
}

if (-not (Test-Path $profilePath)) {
    Write-Warn "PowerShell profile not found at $profilePath. Creating it."
    New-Item -Path $profilePath -ItemType File -Force | Out-Null
}

$existingContent = Get-Content $profilePath -Raw
if ($existingContent -notmatch "# Azure DevOps environment") {
    Write-Ask "Add Azure DevOps environment variables to $profilePath? (y/N):"
    $reply = Read-Host "  "
    if ($reply -match "^[Yy]$") {
        Add-Content -Path $profilePath -Value "`n$envContent"
        Write-Info "Added to $profilePath — will take effect in new PowerShell sessions"
        Write-Warn "WARNING: PAT is stored in plain text in your profile. Consider using Windows Credential Manager for better security."
    }
} else {
    Write-Info "Azure DevOps environment already in profile. Skipping."
}