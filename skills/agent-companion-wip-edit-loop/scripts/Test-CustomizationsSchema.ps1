<#
.SYNOPSIS
    Validates a packed WIP solution's customizations.xml against the customization XSD.

.DESCRIPTION
    `pac solution unpack` splits customizations.xml into many files, so the schema is
    checked on the packed solution: run `pac solution pack` first, then this script on
    the zip. Import does the same check and fails with "contains invalid XML" when it
    does not pass.

    The published schema bundle is older than the platform, so an untouched export
    already reports findings for elements the bundle does not know - for example
    AutoNumberFormat on every column. Pass baselines: the zip as exported before any
    edit, and, when the edit adds kinds of element that export does not hold (a new
    column, a relationship), an unmanaged export that does - such as the destination
    transport solution's. A finding whose kind appears in any baseline is marked
    New = $false and does not fail the check. The kind is the message up to the list
    of expected elements, so "attribute has invalid child element AutoNumberFormat" in
    a baseline covers the same finding on every column. Only kinds the edit introduced
    count.

    The schemas are not shipped with this script. Download Microsoft's bundle (see the
    skill) and pass the folder that holds CustomizationsSolution.xsd and the files it
    includes; a parent folder is searched.

.PARAMETER PackagePath
    The packed solution zip, or a customizations.xml file.

.PARAMETER SchemaPath
    The folder holding CustomizationsSolution.xsd, or a parent of it.

.PARAMETER BaselinePackagePath
    One or more untouched exports (zip, or customizations.xml): the WIP as exported
    before editing, and optionally a reference export holding the kinds of element
    the edit adds.

.EXAMPLE
    .\Test-CustomizationsSchema.ps1 -PackagePath .\work\wip-updated.zip -SchemaPath C:\Schemas `
        -BaselinePackagePath .\work\wip.zip, .\work\transport-reference.zip

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

    [string[]]$BaselinePackagePath = @()
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
            param($origin, $e)
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

function Get-FindingKind {
    <# Severity and message up to the list of expected elements; line numbers are left out. #>
    param($Issue)
    $message = ($Issue.Message -split ' List of possible elements expected')[0]
    return "$($Issue.Severity)|$message"
}

$known = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($baselinePath in $BaselinePackagePath) {
    foreach ($issue in (Get-SchemaFindings -Text (Get-CustomizationsText -Source $baselinePath) -Schemas $schemas)) {
        $null = $known.Add((Get-FindingKind $issue))
    }
}
foreach ($issue in $findings) {
    if ($known.Contains((Get-FindingKind $issue))) { $issue.New = $false }
}

if ($findings.Count -gt 0) { $findings }

$newErrors = @($findings | Where-Object { $_.Severity -eq 'Error' -and $_.New }).Count
$inBaseline = @($findings | Where-Object { -not $_.New }).Count
Write-Host ("Schema check: {0} finding(s), {1} new error(s), {2} of a kind already in a baseline." -f $findings.Count, $newErrors, $inBaseline)

if ($newErrors -gt 0) { exit 1 }
exit 0
