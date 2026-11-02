<#
.SYNOPSIS
    The pre-import overwrite check: compares get_component_state results with the baseline.

.DESCRIPTION
    Forms, views, sitemaps, web resources and app modules are imported as whole
    documents, so an import discards any change made in the environment after the WIP
    was exported. Right after export, save the get_component_state result for every
    row-backed component in the WIP as the baseline. Just before import, call the tool
    again for the same components and compare.

    Each input file holds a get_component_state result as JSON - the structured
    content, { "components": [ ... ] } - or just the components array. Results of
    several calls (the tool takes 200 components per call) can be passed as several
    files.

    Table and column metadata have no row version. They are checked by re-exporting
    the WIP and comparing trees with Compare-SolutionExport.ps1.

.PARAMETER Baseline
    One or more files with the results saved right after export.

.PARAMETER Current
    One or more files with the results taken just before import.

.EXAMPLE
    .\Compare-ComponentState.ps1 -Baseline .\work\state-baseline.json -Current .\work\state-now.json

.OUTPUTS
    One object per component that is not unchanged (Status, Type, Id, Name,
    BaselineVersion, CurrentVersion, ModifiedOn, ModifiedBy). Status is Changed,
    Deleted, NotChecked (in the baseline, missing from the current results) or Added
    (only in the current results). Exit code 1 if anything is Changed, Deleted or
    NotChecked, 2 if the input is invalid, otherwise 0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)][string[]]$Baseline,
    [Parameter(Mandatory = $true, Position = 1)][string[]]$Current
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-Property {
    param($Node, [string]$Name)
    if ($null -eq $Node) { return $null }
    $property = $Node.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Read-State {
    param([string[]]$Files)
    $map = [ordered]@{}
    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            Write-Error -ErrorAction Continue "File not found: $file"
            exit 2
        }
        $json = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
        $components = Get-Property $json 'components'
        if ($null -eq $components) { $components = $json }
        foreach ($component in @($components)) {
            $type = Get-Property $component 'type'
            $id = Get-Property $component 'id'
            if ($null -eq $type -or -not $id) {
                Write-Error -ErrorAction Continue "$file holds an entry without type and id; is it a get_component_state result?"
                exit 2
            }
            $key = '{0}|{1}' -f $type, ([string]$id).Trim('{', '}').ToLowerInvariant()
            $map[$key] = $component
        }
    }
    return $map
}

$before = Read-State -Files $Baseline
$after = Read-State -Files $Current

$results = New-Object System.Collections.ArrayList
foreach ($key in (@($before.Keys) + @($after.Keys) | Select-Object -Unique)) {
    $old = if ($before.Contains($key)) { $before[$key] } else { $null }
    $new = if ($after.Contains($key)) { $after[$key] } else { $null }

    $status = $null
    if ($null -eq $new) { $status = 'NotChecked' }
    elseif ($null -eq $old) { $status = 'Added' }
    elseif ((Get-Property $old 'found') -and -not (Get-Property $new 'found')) { $status = 'Deleted' }
    elseif ([string](Get-Property $old 'versionNumber') -ne [string](Get-Property $new 'versionNumber')) { $status = 'Changed' }
    if (-not $status) { continue }

    $source = if ($null -ne $new) { $new } else { $old }
    $modifiedBy = Get-Property $new 'modifiedBy'
    $null = $results.Add([pscustomobject]@{
            Status          = $status
            Type            = Get-Property $source 'type'
            Id              = Get-Property $source 'id'
            Name            = Get-Property $source 'name'
            BaselineVersion = Get-Property $old 'versionNumber'
            CurrentVersion  = Get-Property $new 'versionNumber'
            ModifiedOn      = Get-Property $new 'modifiedOn'
            ModifiedBy      = Get-Property $modifiedBy 'name'
        })
}

if ($results.Count -gt 0) { $results }

$blocking = @($results | Where-Object { $_.Status -ne 'Added' }).Count
Write-Host ("Compared {0} component(s): {1} changed, deleted or not checked." -f $before.Count, $blocking)
if ($blocking -gt 0) { exit 1 }
exit 0
