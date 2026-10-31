<#
.SYNOPSIS
    Validates a packed WIP solution's customizations.xml against the customization XSD.

.DESCRIPTION
    `pac solution unpack` splits customizations.xml into many files, so the schema is
    checked on the packed solution: run `pac solution pack` first, then this script on
    the zip. Import does the same check and fails with "contains invalid XML" when it
    does not pass.

    The published schema bundle is older than the platform, so an untouched export can
    already report findings for elements the bundle does not know. Pass the zip as it
    was exported, before any edit, as -BaselinePackagePath: findings present in both
    are marked New = $false and do not fail the check. Only what the edit introduced
    counts.

    The schemas are not shipped with this script. Download Microsoft's bundle (see the
    skill) and pass the folder that holds CustomizationsSolution.xsd and the files it
    includes; a parent folder is searched.

.PARAMETER PackagePath
    The packed solution zip, or a customizations.xml file.

.PARAMETER SchemaPath
    The folder holding CustomizationsSolution.xsd, or a parent of it.

.PARAMETER BaselinePackagePath
    The solution zip as exported before editing, or its customizations.xml.

.EXAMPLE
    .\Test-CustomizationsSchema.ps1 -PackagePath .\work\wip-updated.zip -SchemaPath C:\Schemas `
        -BaselinePackagePath .\work\wip.zip

.OUTPUTS
    Finding objects (Severity, Line, Position, Message, New). Exit code 1 if a new
    Error was found, 2 if the input is invalid, otherwise 0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$SchemaPath,

    [string]$BaselinePackagePath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-CustomizationsText {
    param([string]$Source)
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        Write-Error -ErrorAction Continue "File not found: $Source"
        exit 2
    }
    $full = (Resolve-Path -LiteralPath $Source).Path
    if ([System.IO.Path]::GetExtension($full) -ne '.zip') {
        return [System.IO.File]::ReadAllText($full)
    }
    $zip = [System.IO.Compression.ZipFile]::OpenRead($full)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -ieq 'customizations.xml' } | Select-Object -First 1
        if ($null -eq $entry) {
            Write-Error -ErrorAction Continue "No customizations.xml at the root of $Source"
            exit 2
        }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

function Get-SchemaSet {
    param([string]$Folder)
    if (-not (Test-Path -LiteralPath $Folder)) {
        Write-Error -ErrorAction Continue "Schema folder not found: $Folder"
        exit 2
    }
    $xsd = Get-ChildItem -LiteralPath $Folder -Recurse -File -Filter 'CustomizationsSolution.xsd' |
        Sort-Object { $_.Directory.FullName } -Descending | Select-Object -First 1
    if ($null -eq $xsd) {
        Write-Error -ErrorAction Continue "CustomizationsSolution.xsd not found under $Folder"
        exit 2
    }
    $set = New-Object System.Xml.Schema.XmlSchemaSet
    $set.XmlResolver = New-Object System.Xml.XmlUrlResolver
    $null = $set.Add($null, $xsd.FullName)
    $set.Compile()
    Write-Verbose "Schema: $($xsd.FullName)"
    return , $set
}

function Get-SchemaFindings {
    param([string]$Text, [System.Xml.Schema.XmlSchemaSet]$Schemas)
    $script:Issues = New-Object System.Collections.ArrayList
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.ValidationType = [System.Xml.ValidationType]::Schema
    $settings.Schemas = $Schemas
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.ValidationFlags = $settings.ValidationFlags -bor [System.Xml.Schema.XmlSchemaValidationFlags]::ReportValidationWarnings
    $settings.add_ValidationEventHandler([System.Xml.Schema.ValidationEventHandler] {
            param($sender, $e)
            $null = $script:Issues.Add([pscustomobject]@{
                    Severity = if ($e.Severity -eq [System.Xml.Schema.XmlSeverityType]::Error) { 'Error' } else { 'Warning' }
                    Line     = $e.Exception.LineNumber
                    Position = $e.Exception.LinePosition
                    Message  = $e.Message
                    New      = $true
                })
        })
    $reader = [System.Xml.XmlReader]::Create((New-Object System.IO.StringReader($Text)), $settings)
    try {
        while ($reader.Read()) { }
    }
    catch [System.Xml.XmlException] {
        $null = $script:Issues.Add([pscustomobject]@{
                Severity = 'Error'; Line = $_.Exception.LineNumber; Position = $_.Exception.LinePosition
                Message = "Not well-formed: $($_.Exception.Message)"; New = $true
            })
    }
    finally { $reader.Dispose() }
    return , $script:Issues
}

$schemas = Get-SchemaSet -Folder $SchemaPath
$findings = Get-SchemaFindings -Text (Get-CustomizationsText -Source $PackagePath) -Schemas $schemas

if ($BaselinePackagePath) {
    # Findings are matched on severity and message, by count: line numbers move with edits.
    $baseline = @{}
    foreach ($issue in (Get-SchemaFindings -Text (Get-CustomizationsText -Source $BaselinePackagePath) -Schemas $schemas)) {
        $key = "$($issue.Severity)|$($issue.Message)"
        $baseline[$key] = 1 + $(if ($baseline.ContainsKey($key)) { $baseline[$key] } else { 0 })
    }
    foreach ($issue in $findings) {
        $key = "$($issue.Severity)|$($issue.Message)"
        if ($baseline.ContainsKey($key) -and $baseline[$key] -gt 0) {
            $issue.New = $false
            $baseline[$key]--
        }
    }
}

if ($findings.Count -gt 0) { $findings }

$newErrors = @($findings | Where-Object { $_.Severity -eq 'Error' -and $_.New }).Count
$known = @($findings | Where-Object { -not $_.New }).Count
Write-Host ("Schema check: {0} finding(s), {1} new error(s), {2} also in the baseline." -f $findings.Count, $newErrors, $known)

if ($newErrors -gt 0) { exit 1 }
exit 0
