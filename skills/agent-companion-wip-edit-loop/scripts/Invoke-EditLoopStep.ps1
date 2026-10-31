<#
.SYNOPSIS
    Runs one step of the WIP edit loop with the work-folder layout the skill uses.

.DESCRIPTION
    Builds every path from -WorkFolder and -Wip, so no step depends on hand-quoted
    paths - a quoting slip once sent two exports to the same literal folder. Layout:

      <WorkFolder>\<Wip>\export.zip   baseline\   src\   updated.zip
                         recheck.zip  recheck\    verify.zip  verify\
                         state-baseline.json  state-now.json

    Steps:

      Export        pac export and unpack to baseline\, then a fresh copy to src\
      Validate      Test-UnpackedSolution.ps1 on src\, then the blast radius against baseline\
      Pack          pac pack src\ to updated.zip
      Schema        Test-CustomizationsSchema.ps1 on updated.zip, export.zip as baseline,
                    plus any -ReferencePackagePath
      CompareState  Compare-ComponentState.ps1, state-baseline.json against state-now.json
      Recheck       re-export and unpack to recheck\, compare with baseline\ (overwrite check)
      Import        pac import updated.zip with -ImportArguments
      Verify        export and unpack to verify\, compare with src\ (what landed)

    The state files are the get_component_state results the agent saves; the plugin
    call is the agent's, not this script's.

    The work folder must be outside every Git working tree (R7); the script refuses
    one inside. Every PAC command first checks that a profile is active. Matching the
    profile to the plugin connection (pac org who against get_connection_info) stays
    the agent's check.

.PARAMETER Step
    Export, Validate, Pack, Schema, CompareState, Recheck, Import or Verify.

.PARAMETER Wip
    The WIP solution's unique name.

.PARAMETER WorkFolder
    The folder holding one subfolder per WIP.

.PARAMETER KnownColumnsPath
    Validate: passed to Test-UnpackedSolution.ps1.

.PARAMETER KnownWebResources
    Validate: passed to Test-UnpackedSolution.ps1.

.PARAMETER BaseRibbonPath
    Validate: passed to Test-UnpackedSolution.ps1.

.PARAMETER SchemaPath
    Schema: the folder holding CustomizationsSolution.xsd.

.PARAMETER ReferencePackagePath
    Schema: extra baselines holding the kinds of element the edit adds.

.PARAMETER ImportArguments
    Import: PAC flags. Default --publish-changes --async --max-async-wait-time 20. A
    cloud-flow WIP edited with a flow-editing skill uses that skill's flags.

.EXAMPLE
    .\Invoke-EditLoopStep.ps1 -Step Export -Wip XY_Agent_WIP_1234_Base -WorkFolder $env:TEMP\agent-companion

.EXAMPLE
    .\Invoke-EditLoopStep.ps1 -Step Import -Wip XY_Agent_WIP_1234_Processes -WorkFolder $env:TEMP\agent-companion `
        -ImportArguments '--activate-plugins','--publish-changes','--force-overwrite','--async','--max-async-wait-time','20'

.OUTPUTS
    The step's own output. Exit code 0 when the step passed, 1 when it found
    differences or failed, 2 when the input is invalid.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Export', 'Validate', 'Pack', 'Schema', 'CompareState', 'Recheck', 'Import', 'Verify')]
    [string]$Step,

    [Parameter(Mandatory = $true)][string]$Wip,
    [Parameter(Mandatory = $true)][string]$WorkFolder,

    [string]$KnownColumnsPath,
    [string[]]$KnownWebResources = @(),
    [string[]]$BaseRibbonPath = @(),
    [string]$SchemaPath,
    [string[]]$ReferencePackagePath = @(),
    [string[]]$ImportArguments = @('--publish-changes', '--async', '--max-async-wait-time', '20')
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Stop-Invalid {
    param([string]$Message)
    Write-Error -ErrorAction Continue $Message
    exit 2
}

if ($Wip -notmatch '^[A-Za-z][A-Za-z0-9_]*$') { Stop-Invalid "'$Wip' is not a solution unique name." }

$WorkFolder = [System.IO.Path]::GetFullPath($WorkFolder)
# Check the nearest existing folder before creating anything.
$probe = $WorkFolder
while (-not (Test-Path -LiteralPath $probe -PathType Container)) { $probe = Split-Path -Parent $probe }
if (Get-Command git -ErrorAction SilentlyContinue) {
    # Windows PowerShell turns a native command's stderr into a terminating error under 'Stop'.
    $ErrorActionPreference = 'Continue'
    $inside = & git -C $probe rev-parse --is-inside-work-tree 2>$null
    $gitExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($gitExit -eq 0 -and "$inside".Trim() -eq 'true') {
        Stop-Invalid "$WorkFolder is inside a Git working tree. Use a folder outside every repository (R7)."
    }
}
else {
    Write-Warning 'git not found; could not check that the work folder is outside every repository.'
}
$null = New-Item -ItemType Directory -Force -Path $WorkFolder

$folder = Join-Path $WorkFolder $Wip
$null = New-Item -ItemType Directory -Force -Path $folder
$paths = @{
    Export   = Join-Path $folder 'export.zip'
    Baseline = Join-Path $folder 'baseline'
    Src      = Join-Path $folder 'src'
    Updated  = Join-Path $folder 'updated.zip'
    Recheck  = Join-Path $folder 'recheck'
    Verify   = Join-Path $folder 'verify'
}

function Invoke-Pac {
    param([string[]]$PacArguments)
    Write-Host ("pac " + ($PacArguments -join ' '))
    $ErrorActionPreference = 'Continue'
    & pac @PacArguments 2>&1 | ForEach-Object { Write-Host $_ }
    $pacExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    return $pacExit
}

function Export-Unpack {
    param([string]$Zip, [string]$Target)
    $rc = Invoke-Pac @('solution', 'export', '--name', $Wip, '--path', $Zip, '--overwrite')
    if ($rc -ne 0) { return $rc }
    if (Test-Path -LiteralPath $Target) { Remove-Item -LiteralPath $Target -Recurse -Force }
    return (Invoke-Pac @('solution', 'unpack', '--zipfile', $Zip, '--folder', $Target, '--packagetype', 'Unmanaged', '--allowWrite', '--allowDelete', '--clobber'))
}

function Invoke-Sibling {
    param([string]$Script, [hashtable]$ScriptArguments)
    # Findings go to the host, so the caller sees them and only the exit code is returned.
    & (Join-Path $PSScriptRoot $Script) @ScriptArguments | Format-List | Out-Host
    return $LASTEXITCODE
}

function Assert-Exists {
    param([string]$Path, [string]$Hint)
    if (-not (Test-Path -LiteralPath $Path)) { Stop-Invalid "$Path not found. $Hint" }
}

if ($Step -in 'Export', 'Pack', 'Recheck', 'Import', 'Verify') {
    if (-not (Get-Command pac -ErrorAction SilentlyContinue)) { Stop-Invalid 'PAC CLI (pac) is not on the path.' }
}

switch ($Step) {
    'Export' {
        $rc = Export-Unpack -Zip $paths.Export -Target $paths.Baseline
        if ($rc -eq 0) {
            if (Test-Path -LiteralPath $paths.Src) { Remove-Item -LiteralPath $paths.Src -Recurse -Force }
            Copy-Item -LiteralPath $paths.Baseline -Destination $paths.Src -Recurse
            Write-Host "Baseline and working copy ready in $folder. Save the get_component_state baseline now."
        }
    }
    'Validate' {
        Assert-Exists $paths.Src 'Run the Export step first.'
        $arguments = @{ Path = $paths.Src }
        if ($KnownColumnsPath) { $arguments.KnownColumnsPath = $KnownColumnsPath }
        if ($KnownWebResources.Count -gt 0) { $arguments.KnownWebResources = $KnownWebResources }
        if ($BaseRibbonPath.Count -gt 0) { $arguments.BaseRibbonPath = $BaseRibbonPath }
        $rc = Invoke-Sibling 'Test-UnpackedSolution.ps1' $arguments
        Write-Host 'Blast radius against the baseline:'
        $null = Invoke-Sibling 'Compare-SolutionExport.ps1' @{ Baseline = $paths.Baseline; Current = $paths.Src }
    }
    'Pack' {
        Assert-Exists $paths.Src 'Run the Export step first.'
        $rc = Invoke-Pac @('solution', 'pack', '--zipfile', $paths.Updated, '--folder', $paths.Src, '--packagetype', 'Unmanaged')
    }
    'Schema' {
        Assert-Exists $paths.Updated 'Run the Pack step first.'
        if (-not $SchemaPath) { Stop-Invalid 'Pass -SchemaPath, or say the schema check was skipped.' }
        $rc = Invoke-Sibling 'Test-CustomizationsSchema.ps1' @{
            PackagePath         = $paths.Updated
            SchemaPath          = $SchemaPath
            BaselinePackagePath = @($paths.Export) + $ReferencePackagePath
        }
    }
    'CompareState' {
        $rc = Invoke-Sibling 'Compare-ComponentState.ps1' @{
            Baseline = Join-Path $folder 'state-baseline.json'
            Current  = Join-Path $folder 'state-now.json'
        }
    }
    'Recheck' {
        $rc = Export-Unpack -Zip (Join-Path $folder 'recheck.zip') -Target $paths.Recheck
        if ($rc -eq 0) { $rc = Invoke-Sibling 'Compare-SolutionExport.ps1' @{ Baseline = $paths.Baseline; Current = $paths.Recheck } }
        if ($rc -ne 0) { Write-Host 'Overwrite check: something changed since export. Stop and tell the person.' }
    }
    'Import' {
        Assert-Exists $paths.Updated 'Run the Pack step first.'
        $rc = Invoke-Pac (@('solution', 'import', '--path', $paths.Updated) + $ImportArguments)
        if ($rc -ne 0) { Write-Host 'Import failed; the environment is unchanged. Stop: fix src and validate again. Do not import the next WIP.' }
    }
    'Verify' {
        $rc = Export-Unpack -Zip (Join-Path $folder 'verify.zip') -Target $paths.Verify
        if ($rc -eq 0) { $rc = Invoke-Sibling 'Compare-SolutionExport.ps1' @{ Baseline = $paths.Src; Current = $paths.Verify } }
    }
}

Write-Host "[$Step $Wip] exit $rc"
if ($rc -ne 0) { exit 1 }
exit 0
