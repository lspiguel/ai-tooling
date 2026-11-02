<#
.SYNOPSIS
    Builds a WIP solution's unique and friendly names from the naming rule.

.DESCRIPTION
    <initials>_Agent_WIP_<work item id>_<Group>[_<n>]

      - initials: the first letter of the first name and of the last name, uppercase
        ASCII; accented letters lose the accent and any other character is dropped
      - work item id: numeric
      - group: the transport group's name in the segmentation map
      - n: optional sequence number from 2, for a story's second or later WIP for the
        same group

    The friendly name is the unique name with each underscore replaced by a space.
    The script also checks create_solution's rules (letters, digits and underscores,
    starting with a letter, at most 65 characters) and the project's protected
    prefixes.

    Use the names from who_am_i. When it reports an empty first or last name, an
    application user or impersonation, ask the person for their initials and pass
    -Initials instead.

.PARAMETER FirstName
    who_am_i firstName.

.PARAMETER LastName
    who_am_i lastName.

.PARAMETER Initials
    Initials the person gave, instead of first and last name.

.PARAMETER WorkItemId
    The work item's numeric ID.

.PARAMETER Group
    The transport group, as named in the segmentation map.

.PARAMETER Sequence
    2 or more for a second or later WIP for the same group and story.

.PARAMETER ProtectedPrefixes
    Prefixes a WIP name must not start with (W4), compared without case.

.EXAMPLE
    .\Get-WipName.ps1 -FirstName 'Émile' -LastName 'Zola' -WorkItemId 1234 -Group Base

.OUTPUTS
    An object (UniqueName, FriendlyName). Exit code 1 when a rule fails, with the
    reason on the error stream.
#>
[CmdletBinding(DefaultParameterSetName = 'Names')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Names')][AllowEmptyString()][string]$FirstName,
    [Parameter(Mandatory = $true, ParameterSetName = 'Names')][AllowEmptyString()][string]$LastName,
    [Parameter(Mandatory = $true, ParameterSetName = 'Initials')][string]$Initials,
    [Parameter(Mandatory = $true)][string]$WorkItemId,
    [Parameter(Mandatory = $true)][string]$Group,
    [int]$Sequence = 0,
    [string[]]$ProtectedPrefixes = @()
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-AsciiLetters {
    <# Strips accents, then keeps only A-Z, uppercased. #>
    param([string]$Text)
    if (-not $Text) { return '' }
    $builder = New-Object System.Text.StringBuilder
    foreach ($char in $Text.Normalize([System.Text.NormalizationForm]::FormD).ToCharArray()) {
        if ([System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($char) -eq [System.Globalization.UnicodeCategory]::NonSpacingMark) { continue }
        if ($char -match '[A-Za-z]') { $null = $builder.Append([char]::ToUpperInvariant($char)) }
    }
    return $builder.ToString()
}

function Stop-WithReason {
    param([string]$Reason)
    Write-Error $Reason -ErrorAction Continue
    exit 1
}

if ($PSCmdlet.ParameterSetName -eq 'Initials') {
    $prefix = Get-AsciiLetters $Initials
    if (-not $prefix) { Stop-WithReason "Initials '$Initials' contain no letter A-Z after removing accents." }
}
else {
    # The first letter of each name, after accents are removed; a first character that
    # is not a letter is dropped, so it gives no initial.
    $first = (Get-AsciiLetters ($FirstName.Trim() + ' ').Substring(0, 1))
    $last = (Get-AsciiLetters ($LastName.Trim() + ' ').Substring(0, 1))
    if (-not $first -or -not $last) {
        Stop-WithReason "No initial from first name '$FirstName' and last name '$LastName'. Ask the person for their initials and pass -Initials."
    }
    $prefix = $first + $last
}

if ($WorkItemId -notmatch '^\d+$') { Stop-WithReason "Work item ID '$WorkItemId' is not numeric." }
if ($Group -notmatch '^[A-Za-z0-9]+$') { Stop-WithReason "Group '$Group' must be letters and digits, as named in the segmentation map." }
if ($Sequence -eq 1 -or $Sequence -lt 0) { Stop-WithReason 'Sequence starts at 2; the first WIP for a group has no suffix.' }

$unique = '{0}_Agent_WIP_{1}_{2}' -f $prefix, $WorkItemId, $Group
if ($Sequence -ge 2) { $unique = '{0}_{1}' -f $unique, $Sequence }

if ($unique -notmatch '^[A-Za-z][A-Za-z0-9_]*$') { Stop-WithReason "'$unique' must start with a letter and hold only letters, digits and underscores." }
if ($unique.Length -gt 65) { Stop-WithReason "'$unique' is $($unique.Length) characters; the limit is 65." }
foreach ($protected in $ProtectedPrefixes) {
    if ($protected -and $unique.StartsWith($protected, [System.StringComparison]::OrdinalIgnoreCase)) {
        Stop-WithReason "'$unique' starts with the protected prefix '$protected' (W4). Ask the person how to name it."
    }
}

[pscustomobject]@{
    UniqueName   = $unique
    FriendlyName = $unique.Replace('_', ' ')
}
exit 0
