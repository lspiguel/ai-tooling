<#
.SYNOPSIS
    Content-diffs two unpacked exports of the same WIP solution.

.DESCRIPTION
    Compares files by SHA256 and reports what was added, removed or changed. A pack
    followed by an unpack is content-stable, so a difference is a real change.

    Three uses in the edit loop:

      - Overwrite check for table and column metadata, which carry no row version:
        re-export the WIP just before import, unpack it to a new folder, and compare it
        with the baseline unpack, restricted to the entity files.
      - Blast radius before packing: compare the pristine copy of the unpack with the
        edited tree; only the files the change needed should differ.
      - After import: compare the edited tree with a fresh export of the WIP to see
        what landed and what the platform normalized.

.PARAMETER Baseline
    The reference folder - the baseline unpack, or the pristine copy.

.PARAMETER Current
    The folder to compare with it.

.PARAMETER Include
    Optional wildcard patterns matched against paths relative to each folder, for
    example 'Entities\*\Entity.xml'. Only matching files are compared.

.EXAMPLE
    .\Compare-SolutionExport.ps1 -Baseline .\work\wip\baseline -Current .\work\wip\recheck -Include 'Entities\*\Entity.xml'

.OUTPUTS
    One object per differing file (Status, Path). Exit code 1 if anything differs, 2 if
    a folder is missing, otherwise 0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Baseline,
    [Parameter(Mandatory = $true, Position = 1)][string]$Current,
    [string[]]$Include = @()
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

foreach ($folder in @($Baseline, $Current)) {
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) {
        Write-Error -ErrorAction Continue "Folder not found: $folder"
        exit 2
    }
}

function Get-TreeHash {
    param([string]$Root)
    $full = (Resolve-Path -LiteralPath $Root).Path
    $map = @{}
    foreach ($file in Get-ChildItem -LiteralPath $full -Recurse -File) {
        $relative = $file.FullName.Substring($full.Length).TrimStart('\', '/')
        if ($Include.Count -gt 0 -and -not ($Include | Where-Object { $relative -like $_ })) { continue }
        $map[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return $map
}

$left = Get-TreeHash -Root $Baseline
$right = Get-TreeHash -Root $Current

$results = New-Object System.Collections.ArrayList
foreach ($path in (@($left.Keys) + @($right.Keys) | Sort-Object -Unique)) {
    $status = if (-not $right.ContainsKey($path)) { 'Removed' }
    elseif (-not $left.ContainsKey($path)) { 'Added' }
    elseif ($left[$path] -ne $right[$path]) { 'Changed' }
    else { $null }

    if ($status) {
        $null = $results.Add([pscustomobject]@{ Status = $status; Path = $path })
    }
}

if ($results.Count -gt 0) { $results }

Write-Host ("Compared {0} file(s): {1} differ." -f $left.Count, $results.Count)
if ($results.Count -gt 0) { exit 1 }
exit 0
