<#
.SYNOPSIS
    Self-tests for Get-WipName.ps1. Runs on Windows PowerShell 5.1 and later without Pester.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script = Join-Path (Split-Path -Parent $PSScriptRoot) 'Get-WipName.ps1'
$script:Failures = 0
$script:Passes = 0

function Assert {
    param([bool]$Condition, [string]$Name)
    if ($Condition) { $script:Passes++; Write-Host "  pass  $Name" }
    else { $script:Failures++; Write-Host "  FAIL  $Name" -ForegroundColor Red }
}

function Invoke-Name {
    param([hashtable]$Arguments)
    $output = & $script @Arguments 2>$null
    return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
}

Write-Host 'Get-WipName'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = 'Byron'; WorkItemId = '1234'; Group = 'Base' }
Assert ($result.ExitCode -eq 0 -and $result.Output.UniqueName -eq 'AB_Agent_WIP_1234_Base') 'unique name from first and last name'
Assert ($result.Output.FriendlyName -eq 'AB Agent WIP 1234 Base') 'friendly name replaces underscores'

$result = Invoke-Name @{ FirstName = ([string][char]0x00C9 + 'mile'); LastName = ([string][char]0x00F1 + 'u'); WorkItemId = '7'; Group = 'Resources' }
Assert ($result.ExitCode -eq 0 -and $result.Output.UniqueName -eq 'EN_Agent_WIP_7_Resources') 'accents are removed and initials uppercased'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = 'Byron'; WorkItemId = '1234'; Group = 'Base'; Sequence = 2 }
Assert ($result.Output.UniqueName -eq 'AB_Agent_WIP_1234_Base_2') 'sequence suffix from 2'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = 'Byron'; WorkItemId = '1234'; Group = 'Base'; Sequence = 1 }
Assert ($result.ExitCode -eq 1) 'sequence 1 is refused'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = ''; WorkItemId = '1234'; Group = 'Base' }
Assert ($result.ExitCode -eq 1) 'empty last name stops and asks for initials'

$result = Invoke-Name @{ FirstName = ([string][char]0x00D8 + 'ystein'); LastName = 'Byron'; WorkItemId = '1234'; Group = 'Base' }
Assert ($result.ExitCode -eq 1) 'a first letter with no ASCII form stops and asks for initials'

$result = Invoke-Name @{ Initials = 'ob'; WorkItemId = '1234'; Group = 'Base' }
Assert ($result.ExitCode -eq 0 -and $result.Output.UniqueName -eq 'OB_Agent_WIP_1234_Base') 'initials given by the person'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = 'Byron'; WorkItemId = 'ABC-1'; Group = 'Base' }
Assert ($result.ExitCode -eq 1) 'non-numeric work item id is refused'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = 'Byron'; WorkItemId = '1234'; Group = 'Base'; ProtectedPrefixes = @('ab') }
Assert ($result.ExitCode -eq 1) 'protected prefix is refused regardless of case (W4)'

$result = Invoke-Name @{ FirstName = 'Ada'; LastName = 'Byron'; WorkItemId = ('9' * 60); Group = 'Base' }
Assert ($result.ExitCode -eq 1) 'name over 65 characters is refused'

Write-Host ("{0} passed, {1} failed." -f $script:Passes, $script:Failures)
if ($script:Failures -gt 0) { exit 1 }
exit 0
