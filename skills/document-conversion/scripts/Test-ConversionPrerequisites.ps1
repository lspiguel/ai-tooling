<#
.SYNOPSIS
    Checks the tools the document-conversion scripts need, and optionally installs the missing ones.

.DESCRIPTION
    Reports each tool as found (with its version) or missing, which conversions need it, and the command that
    installs it. Chocolatey is preferred, winget is the fallback, and npm / pip install the Node and Python
    packages.

    Without -Install nothing is changed. With -Install the missing tools are installed one by one; Chocolatey
    needs an elevated (Run as administrator) shell, so from a normal shell winget is used instead where it has
    the package. Open a new shell afterwards so PATH changes take effect.

.PARAMETER Conversion
    Limit the check to the tools these conversions need. Default: every conversion.

.PARAMETER Install
    Install whatever is missing for the selected conversions.

.EXAMPLE
    .\Test-ConversionPrerequisites.ps1

.EXAMPLE
    .\Test-ConversionPrerequisites.ps1 -Conversion md-to-pdf, xlsx-to-csv -Install
#>
[CmdletBinding()]
param(
    [ValidateSet('import-docx', 'import-pptx', 'import-xlsx', 'import-pdf', 'bulk-import', 'xlsx-to-csv', 'csv-to-xlsx',
        'md-to-docx', 'md-to-html', 'md-to-pdf', 'md-to-pptx', 'html-to-pdf', 'html-to-png', 'svg-to-png', 'mermaid')]
    [string[]]$Conversion,

    [switch]$Install
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Required tools per conversion; a trailing ? means "only when the document uses it".
$needs = [ordered]@{
    'import-docx' = 'python', 'pandoc'
    'import-pptx' = 'python', 'pandoc'
    'import-xlsx' = 'python', 'pandoc'
    'import-pdf'  = 'python', 'pdftotext'
    'bulk-import' = 'pandoc', 'mmdc?'
    'xlsx-to-csv' = 'python', 'openpyxl'
    'csv-to-xlsx' = 'python', 'openpyxl'
    'md-to-docx'  = 'python', 'pandoc', 'git?', 'mmdc?', 'browser?', 'magick?'
    'md-to-html'  = 'python', 'pandoc', 'mmdc?'
    'md-to-pdf'   = 'python', 'pandoc', 'browser', 'mmdc?'
    'md-to-pptx'  = 'python', 'pandoc', 'mmdc?', 'browser?', 'magick?'
    'html-to-pdf' = 'python', 'browser'
    'html-to-png' = 'browser'
    'svg-to-png'  = 'python', 'browser', 'magick?'
    'mermaid'     = 'python', 'mmdc'
}

$tools = [ordered]@{
    python    = @{ Choco = 'python'; Winget = 'Python.Python.3.12'; Note = '3.10 or later' }
    pandoc    = @{ Choco = 'pandoc'; Winget = 'JohnMacFarlane.Pandoc'; Note = '3.1 or later reads PPTX and XLSX' }
    node      = @{ Choco = 'nodejs-lts'; Winget = 'OpenJS.NodeJS.LTS'; Note = 'needed by mmdc' }
    mmdc      = @{ Npm = '@mermaid-js/mermaid-cli'; Note = 'mermaid renderer; downloads its own Chromium on install' }
    magick    = @{ Choco = 'imagemagick'; Winget = 'ImageMagick.ImageMagick'; Note = 'SVG icons (64 px or smaller)' }
    pdftotext = @{ Choco = 'poppler'; Winget = 'oschwartz10612.Poppler'; Note = 'part of poppler' }
    browser   = @{ Choco = 'googlechrome'; Winget = 'Google.Chrome'; Note = 'Chrome or Edge; Edge ships with Windows' }
    git       = @{ Choco = 'git'; Winget = 'Git.Git'; Note = 'md-to-docx reuses the committed DOCX as its style base' }
    openpyxl  = @{ Pip = 'openpyxl'; Note = 'Python package' }
}

function Get-Browser {
    $candidates = @(
        $env:DOC_CONVERSION_BROWSER
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    return $null
}

function Invoke-Quiet {
    param([string]$Exe, [string[]]$Arguments)
    # Some tools print their version on stderr; merge the streams without tripping Windows PowerShell.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $Exe @Arguments 2>&1 | ForEach-Object { "$_" }
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($out | Where-Object { $_ -match '\S' } | Select-Object -First 1)
    } catch {
        return $null
    } finally {
        $ErrorActionPreference = $previous
    }
}

function Get-ToolState {
    param([string]$Name)
    switch ($Name) {
        'browser' {
            $b = Get-Browser
            if ($b) { return (Split-Path $b -Leaf) }
            return $null
        }
        'openpyxl' {
            if (-not (Get-Command python -ErrorAction SilentlyContinue)) { return $null }
            return Invoke-Quiet python @('-c', 'import openpyxl; print(openpyxl.__version__)')
        }
        default {
            $cmd = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $cmd) { return $null }
            $flag = @{ python = '--version'; pandoc = '--version'; node = '--version'; mmdc = '--version'; magick = '-version'; pdftotext = '-v'; git = '--version' }[$Name]
            $line = Invoke-Quiet $cmd.Source @($flag)
            if ($line) { return $line.Trim() }
            return 'found'
        }
    }
}

function Get-InstallCommand {
    param([string]$Name, [bool]$Elevated)
    $t = $tools[$Name]
    if ($t.ContainsKey('Npm')) { return "npm install -g $($t.Npm)" }
    if ($t.ContainsKey('Pip')) { return "python -m pip install $($t.Pip)" }
    $hasChoco = [bool](Get-Command choco -ErrorAction SilentlyContinue)
    $hasWinget = [bool](Get-Command winget -ErrorAction SilentlyContinue)
    if ($hasChoco -and ($Elevated -or -not $hasWinget)) { return "choco install $($t.Choco) -y" }
    if ($hasWinget) { return "winget install --id $($t.Winget) --exact --accept-source-agreements --accept-package-agreements" }
    return "choco install $($t.Choco) -y"
}

$selected = if ($Conversion) { $Conversion } else { @($needs.Keys) }
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

# Which conversions need each tool, and whether any of them needs it unconditionally.
$usage = [ordered]@{}
foreach ($conv in $selected) {
    foreach ($req in $needs[$conv]) {
        $name = $req.TrimEnd('?')
        if (-not $usage.Contains($name)) { $usage[$name] = @{ Required = $false; For = @() } }
        $usage[$name].For += $conv
        if (-not $req.EndsWith('?')) { $usage[$name].Required = $true }
    }
}
if ($usage.Contains('mmdc') -and -not $usage.Contains('node')) {
    $usage['node'] = @{ Required = $usage['mmdc'].Required; For = @('mmdc') }
}

$rows = foreach ($name in $tools.Keys) {
    if (-not $usage.Contains($name)) { continue }
    $state = Get-ToolState $name
    if ($state -and $state.Length -gt 36) { $state = $state.Substring(0, 36) }
    [pscustomobject]@{
        Tool    = $name
        Status  = if ($state) { $state } elseif ($usage[$name].Required) { 'MISSING' } else { 'missing (only if used)' }
        For     = ($usage[$name].For | Select-Object -Unique) -join ', '
        Install = if ($state) { '' } else { Get-InstallCommand $name $elevated }
        Note    = $tools[$name].Note
        Found   = [bool]$state
    }
}

$rows | Format-Table Tool, Status, Install, For -AutoSize -Wrap | Out-String -Width 200 | Write-Host

$missing = @($rows | Where-Object { -not $_.Found })
if (-not $missing) {
    Write-Host 'Everything needed is installed.' -ForegroundColor Green
    return
}

if (-not $Install) {
    Write-Host "$($missing.Count) tool(s) missing. Run the Install commands above, or re-run with -Install." -ForegroundColor Yellow
    if (-not $elevated -and ($missing.Install -match '^choco')) {
        Write-Host 'Chocolatey installs need an elevated shell (Run as administrator).' -ForegroundColor Yellow
    }
    exit 1
}

# node before mmdc, python before openpyxl.
$order = 'python', 'node', 'pandoc', 'magick', 'pdftotext', 'browser', 'git', 'mmdc', 'openpyxl'
foreach ($name in $order) {
    $row = $missing | Where-Object Tool -EQ $name
    if (-not $row) { continue }
    $command = Get-InstallCommand $name $elevated
    if ($command -match '^choco' -and -not $elevated) {
        Write-Host "Skipping $name - '$command' needs an elevated shell." -ForegroundColor Yellow
        continue
    }
    if ($command -match '^(npm|python) ' -and -not (Get-Command ($command -split ' ')[0] -ErrorAction SilentlyContinue)) {
        Write-Host "Skipping $name - $(($command -split ' ')[0]) is not on PATH yet; open a new shell and re-run." -ForegroundColor Yellow
        continue
    }
    Write-Host "> $command" -ForegroundColor Cyan
    $exe, $arguments = $command -split ' '
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { Write-Host "  failed (exit code $LASTEXITCODE)" -ForegroundColor Red }
}
Write-Host 'Done. Open a new shell so PATH changes take effect, then run this check again.' -ForegroundColor Green
