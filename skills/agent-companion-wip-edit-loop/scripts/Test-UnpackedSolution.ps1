<#
.SYNOPSIS
    Checks the forms and ribbon diffs of an unpacked WIP solution before packing.

.DESCRIPTION
    Catches the reference errors a local edit introduces in a solution unpacked by
    `pac solution unpack` (XML layout), which otherwise surface as a failed import or
    a broken form at run time:

      - Every .xml file parses
      - Forms: formid is a GUID and matches the file name; tab, section, cell and
        control ids are unique within the form; uniqueid and classid are GUIDs; tab
        and section names are unique; every control's datafieldname is a column of
        the form's table; every event handler's library is a form library; every
        form library is a known web resource
      - Ribbon diffs: Ids are unique; every Command resolves to a CommandDefinition;
        every EnableRule and DisplayRule a command uses resolves to a rule definition;
        every $webresource: reference is a known web resource; every $LocLabels:
        reference resolves to a LocLabel in the same diff

    A WIP holds only part of each table, so many references point outside the tree.
    Evidence for them is passed in: the columns from get_table_metadata, the web
    resource names from a query, and the base ribbon from get_entity_ribbon or
    get_application_ribbon. Where evidence is given, a reference it does not contain
    is an Error. Where none is given, a reference outside the tree is a Warning that
    says it was not verified.

    Requires no authentication and makes no network calls. It reduces failed imports;
    it does not replace the import.

.PARAMETER Path
    The unpacked solution folder.

.PARAMETER KnownColumnsPath
    A JSON file mapping table logical names to arrays of column logical names, for
    example { "account": ["name", "telephone1"] }, built from get_table_metadata.
    Columns defined in the tree are always known.

.PARAMETER KnownWebResources
    Web resource names known to exist in the environment, for example from a query
    over webresource. Web resources in the tree are always known.

.PARAMETER BaseRibbonPath
    One or more files holding a reassembled base ribbon XML document, from
    get_entity_ribbon or get_application_ribbon.

.PARAMETER FailOnWarning
    Treat warnings as failures for the purposes of the exit code.

.EXAMPLE
    .\Test-UnpackedSolution.ps1 -Path .\work\XY_Agent_WIP_1234_Base\src

.EXAMPLE
    .\Test-UnpackedSolution.ps1 -Path .\work\wip\src -KnownColumnsPath .\work\columns.json `
        -KnownWebResources 'new_/account/form.js' -BaseRibbonPath .\work\account-ribbon.xml

.OUTPUTS
    Finding objects (File, Severity, Rule, Location, Message). Exit code 1 if any
    Error was found (or any Warning, with -FailOnWarning), 2 if the input is invalid,
    otherwise 0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Path,

    [string]$KnownColumnsPath,

    [string[]]$KnownWebResources = @(),

    [string[]]$BaseRibbonPath = @(),

    [switch]$FailOnWarning
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:Findings = New-Object System.Collections.ArrayList
$script:CurrentFile = ''

$GuidPattern = '^\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}?$'

function Add-Finding {
    param(
        [ValidateSet('Error', 'Warning', 'Info')][string]$Severity,
        [string]$Rule,
        [string]$Location,
        [string]$Message
    )
    $null = $script:Findings.Add([pscustomobject]@{
            File     = $script:CurrentFile
            Severity = $Severity
            Rule     = $Rule
            Location = $Location
            Message  = $Message
        })
}

function New-NameSet {
    return , (New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase))
}

function Get-Attr {
    <# Attribute value, or $null when absent. #>
    param([System.Xml.XmlNode]$Node, [string]$Name)
    if ($null -eq $Node -or $null -eq $Node.Attributes) { return $null }
    $attribute = $Node.Attributes.GetNamedItem($Name)
    if ($null -eq $attribute) { return $null }
    return $attribute.Value
}

function Get-Text {
    param([System.Xml.XmlNode]$Node, [string]$XPath)
    $found = $Node.SelectSingleNode($XPath)
    if ($null -eq $found) { return $null }
    # PowerShell's XML adapter returns $null for an attribute's InnerText; use Value.
    $text = if ($found -is [System.Xml.XmlAttribute]) { $found.Value } else { $found.InnerText }
    if ($null -eq $text) { return $null }
    return $text.Trim()
}

function Read-XmlFile {
    <# Parses a file; records a finding and returns $null when it does not parse. #>
    param([string]$File)
    try {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($File)
        return , $doc
    }
    catch {
        Add-Finding -Severity Error -Rule 'XmlParse' -Location '' -Message $_.Exception.Message
        return $null
    }
}

if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    Write-Error -ErrorAction Continue "Folder not found: $Path"
    exit 2
}
$root = (Resolve-Path -LiteralPath $Path).Path

function Get-RelativePath {
    param([string]$FullName)
    return $FullName.Substring($root.Length).TrimStart('\', '/')
}

# --- Parse every XML file once -------------------------------------------------

$documents = New-Object System.Collections.ArrayList
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xml') {
    $script:CurrentFile = Get-RelativePath $file.FullName
    $doc = Read-XmlFile -File $file.FullName
    if ($null -ne $doc) {
        $null = $documents.Add([pscustomobject]@{ Relative = $script:CurrentFile; File = $file; Doc = $doc })
    }
}

# --- Evidence ------------------------------------------------------------------

$webResources = New-NameSet
foreach ($name in $KnownWebResources) { if ($name) { $null = $webResources.Add($name) } }
$webResourceEvidence = $KnownWebResources.Count -gt 0
foreach ($entry in $documents) {
    $name = Get-Text -Node $entry.Doc -XPath '/WebResource/Name'
    if ($name) { $null = $webResources.Add($name) }
}

# Columns per table: from the tree's Entity.xml files, plus the supplied file.
$columns = @{}
$columnEvidence = New-NameSet
$tableByFolder = @{}
foreach ($entry in $documents) {
    if ($entry.File.Name -ne 'Entity.xml') { continue }
    $schemaName = Get-Text -Node $entry.Doc -XPath '/Entity/Name'
    if (-not $schemaName) { continue }
    $table = $schemaName.ToLowerInvariant()
    $tableByFolder[$entry.File.Directory.FullName.ToLowerInvariant()] = $table
    if (-not $columns.ContainsKey($table)) { $columns[$table] = New-NameSet }
    foreach ($node in $entry.Doc.SelectNodes('/Entity/EntityInfo/entity/attributes/attribute/LogicalName')) {
        $null = $columns[$table].Add($node.InnerText.Trim())
    }
}
if ($KnownColumnsPath) {
    if (-not (Test-Path -LiteralPath $KnownColumnsPath -PathType Leaf)) {
        Write-Error -ErrorAction Continue "Known columns file not found: $KnownColumnsPath"
        exit 2
    }
    $known = Get-Content -LiteralPath $KnownColumnsPath -Raw | ConvertFrom-Json
    foreach ($property in $known.PSObject.Properties) {
        $table = $property.Name.ToLowerInvariant()
        if (-not $columns.ContainsKey($table)) { $columns[$table] = New-NameSet }
        $null = $columnEvidence.Add($table)
        foreach ($column in @($property.Value)) { $null = $columns[$table].Add([string]$column) }
    }
}

$baseCommands = New-NameSet
$baseRules = New-NameSet
foreach ($basePath in $BaseRibbonPath) {
    if (-not (Test-Path -LiteralPath $basePath -PathType Leaf)) {
        Write-Error -ErrorAction Continue "Base ribbon file not found: $basePath"
        exit 2
    }
    $script:CurrentFile = $basePath
    $baseDoc = Read-XmlFile -File (Resolve-Path -LiteralPath $basePath).Path
    if ($null -eq $baseDoc) { continue }
    foreach ($node in $baseDoc.SelectNodes('//CommandDefinition')) { $null = $baseCommands.Add((Get-Attr $node 'Id')) }
    foreach ($node in $baseDoc.SelectNodes('//EnableRule[@Id] | //DisplayRule[@Id]')) { $null = $baseRules.Add((Get-Attr $node 'Id')) }
}
$baseEvidence = $BaseRibbonPath.Count -gt 0

function Test-WebResourceReference {
    param([string]$Name, [string]$Location)
    if ($webResources.Contains($Name)) { return }
    if ($webResourceEvidence) {
        Add-Finding -Severity Error -Rule 'WebResourceMissing' -Location $Location -Message "Web resource '$Name' is neither in the solution nor in the known web resources."
    }
    else {
        Add-Finding -Severity Warning -Rule 'WebResourceUnverified' -Location $Location -Message "Web resource '$Name' is not in the solution; not verified. Pass -KnownWebResources to check it."
    }
}

function Test-UniqueValue {
    param([System.Xml.XmlNodeList]$Nodes, [string]$Attribute, [string]$Kind, [string]$Location, [string]$Severity = 'Error', [switch]$RequireGuid)
    $seen = New-NameSet
    foreach ($node in $Nodes) {
        $value = Get-Attr $node $Attribute
        if ([string]::IsNullOrEmpty($value)) { continue }
        if ($RequireGuid -and $value -notmatch $GuidPattern) {
            Add-Finding -Severity Error -Rule 'GuidFormat' -Location $Location -Message "$Kind $Attribute '$value' is not a GUID."
        }
        if (-not $seen.Add($value)) {
            Add-Finding -Severity $Severity -Rule 'DuplicateId' -Location $Location -Message "$Kind $Attribute '$value' appears more than once."
        }
    }
}

# --- Forms ---------------------------------------------------------------------

$formCount = 0
foreach ($entry in $documents) {
    foreach ($systemForm in $entry.Doc.SelectNodes('/forms/systemform')) {
        $formCount++
        $script:CurrentFile = $entry.Relative
        $formId = Get-Text -Node $systemForm -XPath 'formid'
        $formName = Get-Text -Node $systemForm -XPath "LocalizedNames/LocalizedName[1]/@description"
        $where = if ($formName) { "form '$formName'" } else { "form $formId" }

        if (-not $formId -or $formId -notmatch $GuidPattern) {
            Add-Finding -Severity Error -Rule 'GuidFormat' -Location $where -Message "formid '$formId' is not a GUID."
        }
        elseif ($entry.File.BaseName -match $GuidPattern -and $entry.File.BaseName.Trim('{', '}') -ne $formId.Trim('{', '}')) {
            Add-Finding -Severity Warning -Rule 'FormIdFileName' -Location $where -Message "formid $formId does not match the file name $($entry.File.Name)."
        }

        $form = $systemForm.SelectSingleNode('form')
        if ($null -eq $form) { continue }

        Test-UniqueValue -Nodes $form.SelectNodes('.//tab') -Attribute 'id' -Kind 'Tab' -Location $where -RequireGuid
        Test-UniqueValue -Nodes $form.SelectNodes('.//section') -Attribute 'id' -Kind 'Section' -Location $where -RequireGuid
        Test-UniqueValue -Nodes $form.SelectNodes('.//cell') -Attribute 'id' -Kind 'Cell' -Location $where -RequireGuid
        Test-UniqueValue -Nodes $form.SelectNodes('.//control') -Attribute 'id' -Kind 'Control' -Location $where
        Test-UniqueValue -Nodes $form.SelectNodes('.//control') -Attribute 'uniqueid' -Kind 'Control' -Location $where -RequireGuid
        Test-UniqueValue -Nodes $form.SelectNodes('.//tab') -Attribute 'name' -Kind 'Tab' -Location $where -Severity Warning
        Test-UniqueValue -Nodes $form.SelectNodes('.//section') -Attribute 'name' -Kind 'Section' -Location $where -Severity Warning

        foreach ($control in $form.SelectNodes('.//control[@classid]')) {
            $classId = Get-Attr $control 'classid'
            if ($classId -notmatch $GuidPattern) {
                Add-Finding -Severity Error -Rule 'GuidFormat' -Location "$where control '$(Get-Attr $control 'id')'" -Message "classid '$classId' is not a GUID."
            }
        }

        # Columns: the table is the Entities\<table> folder that holds FormXml.
        $table = $null
        $directory = $entry.File.Directory
        while ($null -ne $directory -and $directory.FullName.Length -ge $root.Length) {
            if ($directory.Name -eq 'FormXml') {
                $entityFolder = $directory.Parent.FullName.ToLowerInvariant()
                $table = if ($tableByFolder.ContainsKey($entityFolder)) { $tableByFolder[$entityFolder] } else { $directory.Parent.Name.ToLowerInvariant() }
                break
            }
            $directory = $directory.Parent
        }
        if ($table) {
            # Assigned in branches: an if-expression would unroll the set.
            $tableColumns = New-NameSet
            if ($columns.ContainsKey($table)) { $tableColumns = $columns[$table] }
            $reported = New-NameSet
            foreach ($control in $form.SelectNodes('.//control[@datafieldname]')) {
                $column = (Get-Attr $control 'datafieldname').ToLowerInvariant()
                if ($tableColumns.Contains($column) -or -not $reported.Add($column)) { continue }
                $location = "$where control '$(Get-Attr $control 'id')'"
                if ($columnEvidence.Contains($table)) {
                    Add-Finding -Severity Error -Rule 'FormColumnMissing' -Location $location -Message "Column '$column' is not a column of '$table'."
                }
                else {
                    Add-Finding -Severity Warning -Rule 'FormColumnUnverified' -Location $location -Message "Column '$column' is not in the solution; not verified. Pass -KnownColumnsPath with the columns of '$table'."
                }
            }
        }

        $libraries = New-NameSet
        foreach ($library in $form.SelectNodes('formLibraries/Library')) {
            $name = Get-Attr $library 'name'
            if (-not $name) { continue }
            $null = $libraries.Add($name)
            Test-WebResourceReference -Name $name -Location "$where form library"
        }
        foreach ($handler in $form.SelectNodes('.//Handler[@libraryName]')) {
            $name = Get-Attr $handler 'libraryName'
            if ($name -like 'Mscrm*' -or $libraries.Contains($name)) { continue }
            Add-Finding -Severity Error -Rule 'FormHandlerLibrary' -Location "$where handler '$(Get-Attr $handler 'functionName')'" -Message "Handler library '$name' is not among the form's libraries."
        }
    }
}

# --- Ribbon diffs ----------------------------------------------------------------

$ribbonCount = 0
foreach ($entry in $documents) {
    foreach ($diff in $entry.Doc.SelectNodes('//RibbonDiffXml')) {
        $ribbonCount++
        $script:CurrentFile = $entry.Relative

        Test-UniqueValue -Nodes $diff.SelectNodes('CustomActions/CustomAction | CustomActions/HideCustomAction') -Attribute 'Id' -Kind 'CustomAction' -Location 'ribbon diff'
        Test-UniqueValue -Nodes $diff.SelectNodes('CommandDefinitions/CommandDefinition') -Attribute 'Id' -Kind 'CommandDefinition' -Location 'ribbon diff'
        Test-UniqueValue -Nodes $diff.SelectNodes('RuleDefinitions/EnableRules/EnableRule | RuleDefinitions/DisplayRules/DisplayRule') -Attribute 'Id' -Kind 'Rule' -Location 'ribbon diff'
        Test-UniqueValue -Nodes $diff.SelectNodes('LocLabels/LocLabel') -Attribute 'Id' -Kind 'LocLabel' -Location 'ribbon diff'

        $commands = New-NameSet
        foreach ($node in $diff.SelectNodes('CommandDefinitions/CommandDefinition')) { $null = $commands.Add((Get-Attr $node 'Id')) }
        $rules = New-NameSet
        foreach ($node in $diff.SelectNodes('RuleDefinitions/EnableRules/EnableRule | RuleDefinitions/DisplayRules/DisplayRule')) { $null = $rules.Add((Get-Attr $node 'Id')) }
        $labels = New-NameSet
        foreach ($node in $diff.SelectNodes('LocLabels/LocLabel')) { $null = $labels.Add((Get-Attr $node 'Id')) }

        foreach ($node in $diff.SelectNodes('CustomActions//CommandUIDefinition//*[@Command]')) {
            $command = Get-Attr $node 'Command'
            $location = "ribbon $($node.LocalName) '$(Get-Attr $node 'Id')'"
            if ($commands.Contains($command) -or $baseCommands.Contains($command)) { continue }
            if ($command -like 'Mscrm.*') {
                $message = if ($baseEvidence) { "Command '$command' is not in the base ribbon provided." } else { "Command '$command' is assumed to be in the base ribbon; not verified. Pass -BaseRibbonPath to check it." }
                Add-Finding -Severity Warning -Rule 'RibbonCommandUnverified' -Location $location -Message $message
            }
            else {
                Add-Finding -Severity Error -Rule 'RibbonCommandMissing' -Location $location -Message "Command '$command' has no CommandDefinition in the diff or the base ribbon."
            }
        }

        foreach ($definition in $diff.SelectNodes('CommandDefinitions/CommandDefinition')) {
            foreach ($node in $definition.SelectNodes('EnableRules/EnableRule | DisplayRules/DisplayRule')) {
                $rule = Get-Attr $node 'Id'
                $location = "ribbon command '$(Get-Attr $definition 'Id')'"
                if ($rules.Contains($rule) -or $baseRules.Contains($rule)) { continue }
                if ($rule -like 'Mscrm.*') {
                    $message = if ($baseEvidence) { "Rule '$rule' is not in the base ribbon provided." } else { "Rule '$rule' is assumed to be in the base ribbon; not verified. Pass -BaseRibbonPath to check it." }
                    Add-Finding -Severity Warning -Rule 'RibbonRuleUnverified' -Location $location -Message $message
                }
                else {
                    Add-Finding -Severity Error -Rule 'RibbonRuleMissing' -Location $location -Message "$($node.LocalName) '$rule' has no definition in the diff or the base ribbon."
                }
            }
        }

        foreach ($attribute in $diff.SelectNodes('.//@*')) {
            $value = $attribute.Value
            $location = "ribbon $($attribute.OwnerElement.LocalName) '$(Get-Attr $attribute.OwnerElement 'Id')'"
            if ($value -like '$webresource:*') {
                Test-WebResourceReference -Name $value.Substring('$webresource:'.Length) -Location $location
            }
            elseif ($value -like '$LocLabels:*') {
                $label = $value.Substring('$LocLabels:'.Length)
                if (-not $labels.Contains($label)) {
                    Add-Finding -Severity Error -Rule 'RibbonLocLabelMissing' -Location $location -Message "LocLabel '$label' is not defined in this diff's LocLabels."
                }
            }
        }
    }
}

# --- Report --------------------------------------------------------------------

if ($script:Findings.Count -gt 0) { $script:Findings }

$errors = @($script:Findings | Where-Object { $_.Severity -eq 'Error' }).Count
$warnings = @($script:Findings | Where-Object { $_.Severity -eq 'Warning' }).Count
Write-Host ("Checked {0} XML file(s), {1} form(s), {2} ribbon diff(s): {3} error(s), {4} warning(s)." -f $documents.Count, $formCount, $ribbonCount, $errors, $warnings)

if ($errors -gt 0 -or ($FailOnWarning -and $warnings -gt 0)) { exit 1 }
exit 0
