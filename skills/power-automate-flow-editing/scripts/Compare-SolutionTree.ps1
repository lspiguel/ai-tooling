<#
.SYNOPSIS
    Content-diffs two unpacked solution folders to confirm the blast radius of an edit.

.DESCRIPTION
    Compares every file by SHA256 and reports what was added, removed or changed.

    Use it for two things:

      - Before packing, against a pristine copy of the unpacked tree, to prove that
        only the intended flow file changed.
      - After importing, against a fresh export, to see what the platform normalized.

    Do not use robocopy for this. Robocopy compares timestamps and size, so a
    re-extracted tree reports every file as changed even when the content is identical.

    A pack followed by an unpack is content-stable, so any difference this reports is
    a real edit rather than packager noise.

.PARAMETER Reference
    The baseline folder - the pristine copy, or the pre-change export.

.PARAMETER Difference
    The folder to compare against the baseline.

.EXAMPLE
    .\Compare-SolutionTree.ps1 -Reference .\wip\src.orig -Difference .\wip\src

.OUTPUTS
    One object per differing file (Status, Path). Exit code 1 if anything differs.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Reference,
    [Parameter(Mandatory = $true, Position = 1)][string]$Difference
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

foreach ($folder in @($Reference, $Difference)) {
    if (-not (Test-Path -LiteralPath $folder)) {
        Write-Error "Folder not found: $folder"
        exit 2
    }
}

function Get-TreeHash {
    param([string]$Root)
    $full = (Resolve-Path -LiteralPath $Root).Path
    $map = @{}
    foreach ($file in Get-ChildItem -LiteralPath $full -Recurse -File) {
        $relative = $file.FullName.Substring($full.Length).TrimStart('\', '/')
        $map[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return $map
}

$left = Get-TreeHash -Root $Reference
$right = Get-TreeHash -Root $Difference

$results = New-Object System.Collections.ArrayList

foreach ($path in ($left.Keys + $right.Keys | Sort-Object -Unique)) {
    $inLeft = $left.ContainsKey($path)
    $inRight = $right.ContainsKey($path)

    $status = if (-not $inRight) { 'Removed' }
    elseif (-not $inLeft) { 'Added' }
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
