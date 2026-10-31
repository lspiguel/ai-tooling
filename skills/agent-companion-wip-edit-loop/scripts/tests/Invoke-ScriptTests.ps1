<#
.SYNOPSIS
    Self-tests for the edit-loop scripts, on synthetic fixtures built in a temp folder.

.DESCRIPTION
    No client data: every table, column, web resource and GUID below is invented.
    Runs on Windows PowerShell 5.1 and later without Pester.

.PARAMETER SchemaPath
    Optional folder holding CustomizationsSolution.xsd. Without it the schema tests are
    skipped.

.EXAMPLE
    .\Invoke-ScriptTests.ps1 -SchemaPath C:\Schemas
#>
[CmdletBinding()]
param([string]$SchemaPath)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$scripts = Split-Path -Parent $PSScriptRoot
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('agent-companion-tests-' + [guid]::NewGuid().ToString('N'))
$script:Failures = 0
$script:Passes = 0

function Assert {
    param([bool]$Condition, [string]$Name)
    if ($Condition) { $script:Passes++; Write-Host "  pass  $Name" }
    else { $script:Failures++; Write-Host "  FAIL  $Name" -ForegroundColor Red }
}

function Write-Fixture {
    param([string]$Relative, [string]$Content)
    $file = Join-Path $work $Relative
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $file)
    [System.IO.File]::WriteAllText($file, $Content, (New-Object System.Text.UTF8Encoding($false)))
    return $file
}

function Invoke-Script {
    param([string]$Name, [hashtable]$Arguments)
    # Host and error output are dropped: negative cases write their reason there.
    $output = & (Join-Path $scripts $Name) @Arguments 6>$null 2>$null
    return [pscustomobject]@{ Output = @($output); ExitCode = $LASTEXITCODE }
}

function Get-Rules {
    param($Result, [string]$Severity)
    return , @($Result.Output | Where-Object { $_.PSObject.Properties['Rule'] -and $_.Severity -eq $Severity } | ForEach-Object { $_.Rule })
}

$textControl = '{4273EDBD-AC1D-40d3-9FB2-095C621B552D}'

function Get-FormXml {
    param([string]$ExtraControls = '', [string]$ExtraHandlers = '')
    return @"
<?xml version="1.0" encoding="utf-8"?>
<forms xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <systemform>
    <formid>{11111111-1111-1111-1111-111111111111}</formid>
    <FormPresentation>1</FormPresentation>
    <FormActivationState>1</FormActivationState>
    <form>
      <tabs>
        <tab name="general" id="{22222222-2222-2222-2222-222222222222}">
          <columns><column width="100%"><sections>
            <section name="main" id="{33333333-3333-3333-3333-333333333333}">
              <rows>
                <row><cell id="{44444444-4444-4444-4444-444444444441}"><control id="telephone1" classid="$textControl" datafieldname="telephone1" /></cell></row>
                <row><cell id="{44444444-4444-4444-4444-444444444442}"><control id="name" classid="$textControl" datafieldname="name" /></cell></row>
                $ExtraControls
              </rows>
            </section>
          </sections></column></columns>
        </tab>
      </tabs>
      <formLibraries><Library name="new_/sample/form.js" libraryUniqueId="{66666666-6666-6666-6666-666666666666}" /></formLibraries>
      <events><event name="onload" application="false" active="false"><Handlers>
        <Handler functionName="Sample.onLoad" libraryName="new_/sample/form.js" handlerUniqueId="{77777777-7777-7777-7777-777777777777}" enabled="true" parameters="" passExecutionContext="true" />
        $ExtraHandlers
      </Handlers></event></events>
    </form>
    <LocalizedNames><LocalizedName description="Sample main" languagecode="1033" /></LocalizedNames>
  </systemform>
</forms>
"@
}

function Get-RibbonXml {
    param([string]$ExtraButtons = '', [string]$ExtraRules = '')
    return @"
<RibbonDiffXml>
  <CustomActions>
    <CustomAction Id="new.sample.Run.CustomAction" Location="Mscrm.Form.account.MainTab.Save.Controls._children" Sequence="100">
      <CommandUIDefinition>
        <Button Id="new.sample.Run" Command="new.sample.Run.Command" LabelText="`$LocLabels:new.sample.Run.Label" Image16by16="`$webresource:new_/sample/icon.svg" TemplateAlias="o1" />
        $ExtraButtons
      </CommandUIDefinition>
    </CustomAction>
  </CustomActions>
  <Templates><RibbonTemplates Id="Mscrm.Templates" /></Templates>
  <CommandDefinitions>
    <CommandDefinition Id="new.sample.Run.Command">
      <EnableRules><EnableRule Id="new.sample.Run.Enable" /><EnableRule Id="Mscrm.AvailableOnForm" />$ExtraRules</EnableRules>
      <DisplayRules />
      <Actions><JavaScriptFunction Library="`$webresource:new_/sample/form.js" FunctionName="Sample.run" /></Actions>
    </CommandDefinition>
  </CommandDefinitions>
  <RuleDefinitions>
    <TabDisplayRules />
    <DisplayRules />
    <EnableRules><EnableRule Id="new.sample.Run.Enable"><FormStateRule State="Create" InvertResult="true" /></EnableRule></EnableRules>
  </RuleDefinitions>
  <LocLabels><LocLabel Id="new.sample.Run.Label"><Titles><Title description="Run" languagecode="1033" /></Titles></LocLabel></LocLabels>
</RibbonDiffXml>
"@
}

function New-SolutionTree {
    param([string]$Name, [string]$FormXml, [string]$RibbonXml)
    $base = "$Name\Entities\Account"
    $null = Write-Fixture "$base\Entity.xml" @"
<Entity><Name LocalizedName="Account" OriginalName="Account">Account</Name>
<EntityInfo><entity Name="Account"><attributes>
<attribute PhysicalName="Telephone1"><Type>nvarchar</Type><Name>telephone1</Name><LogicalName>telephone1</LogicalName></attribute>
</attributes></entity></EntityInfo></Entity>
"@
    $null = Write-Fixture "$base\FormXml\main\{11111111-1111-1111-1111-111111111111}.xml" $FormXml
    $null = Write-Fixture "$base\RibbonDiff.xml" $RibbonXml
    $null = Write-Fixture "$Name\WebResources\new_\sample\form.js.data.xml" '<WebResource><WebResourceId>{88888888-8888-8888-8888-888888888881}</WebResourceId><Name>new_/sample/form.js</Name></WebResource>'
    $null = Write-Fixture "$Name\WebResources\new_\sample\icon.svg.data.xml" '<WebResource><WebResourceId>{88888888-8888-8888-8888-888888888882}</WebResourceId><Name>new_/sample/icon.svg</Name></WebResource>'
    return (Join-Path $work $Name)
}

try {
    $columnsFile = Write-Fixture 'columns.json' '{ "account": ["accountid", "name", "telephone1"] }'
    $baseRibbon = Write-Fixture 'base-ribbon.xml' '<RibbonDefinitions><CommandDefinitions><CommandDefinition Id="Mscrm.SavePrimary" /></CommandDefinitions><RuleDefinitions><EnableRules><EnableRule Id="Mscrm.AvailableOnForm" /></EnableRules></RuleDefinitions></RibbonDefinitions>'

    Write-Host 'Test-UnpackedSolution'

    $clean = New-SolutionTree -Name 'clean' -FormXml (Get-FormXml) -RibbonXml (Get-RibbonXml)
    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = $clean; KnownColumnsPath = $columnsFile; BaseRibbonPath = @($baseRibbon) }
    Assert ($result.ExitCode -eq 0) 'clean tree with evidence exits 0'
    Assert ((Get-Rules $result 'Error').Count -eq 0 -and (Get-Rules $result 'Warning').Count -eq 0) 'clean tree with evidence has no findings'

    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = $clean }
    Assert ($result.ExitCode -eq 0) 'clean tree without evidence exits 0'
    Assert ((Get-Rules $result 'Warning') -contains 'FormColumnUnverified') 'column outside the tree is unverified without -KnownColumnsPath'
    Assert ((Get-Rules $result 'Warning') -contains 'RibbonRuleUnverified') 'Mscrm rule is unverified without -BaseRibbonPath'

    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = $clean; FailOnWarning = $true }
    Assert ($result.ExitCode -eq 1) '-FailOnWarning turns warnings into exit 1'

    $brokenForm = Get-FormXml -ExtraControls @"
<row><cell id="{44444444-4444-4444-4444-444444444441}"><control id="name" classid="not-a-guid" datafieldname="missingcolumn" /></cell></row>
"@ -ExtraHandlers '<Handler functionName="Sample.other" libraryName="new_/sample/other.js" handlerUniqueId="{77777777-7777-7777-7777-777777777778}" enabled="true" />'
    $brokenRibbon = Get-RibbonXml -ExtraButtons @"
<Button Id="new.sample.Undefined" Command="new.sample.Undefined.Command" LabelText="`$LocLabels:new.sample.NoLabel" Image16by16="`$webresource:new_/sample/missing.svg" />
<Button Id="new.sample.Save" Command="Mscrm.SavePrimary" />
"@ -ExtraRules '<EnableRule Id="new.sample.NoRule" />'
    $broken = New-SolutionTree -Name 'broken' -FormXml $brokenForm -RibbonXml $brokenRibbon
    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = $broken; KnownColumnsPath = $columnsFile; KnownWebResources = @('new_/sample/form.js') }
    $errors = Get-Rules $result 'Error'
    Assert ($result.ExitCode -eq 1) 'broken tree exits 1'
    Assert ($errors -contains 'DuplicateId') 'duplicate control and cell ids are errors'
    Assert ($errors -contains 'GuidFormat') 'non-GUID classid is an error'
    Assert ($errors -contains 'FormColumnMissing') 'column not in the known columns is an error'
    Assert ($errors -contains 'FormHandlerLibrary') 'handler library outside formLibraries is an error'
    Assert ($errors -contains 'RibbonCommandMissing') 'undefined custom command is an error'
    Assert ($errors -contains 'RibbonRuleMissing') 'undefined custom rule is an error'
    Assert ($errors -contains 'RibbonLocLabelMissing') 'undefined LocLabel is an error'
    Assert ($errors -contains 'WebResourceMissing') 'unknown web resource with evidence is an error'
    Assert ((Get-Rules $result 'Warning') -contains 'RibbonCommandUnverified') 'Mscrm command is unverified without base ribbon'

    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = $broken; BaseRibbonPath = @($baseRibbon) }
    Assert (-not ((Get-Rules $result 'Warning') -contains 'RibbonCommandUnverified')) 'base ribbon resolves Mscrm.SavePrimary'
    Assert ((Get-Rules $result 'Warning') -contains 'WebResourceUnverified') 'unknown web resource without evidence is a warning'

    $null = Write-Fixture 'malformed\Other\Solution.xml' '<ImportExportXml><SolutionManifest></ImportExportXml>'
    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = (Join-Path $work 'malformed') }
    Assert ($result.ExitCode -eq 1 -and (Get-Rules $result 'Error') -contains 'XmlParse') 'malformed XML is an error'

    $result = Invoke-Script 'Test-UnpackedSolution.ps1' @{ Path = (Join-Path $work 'no-such-folder') }
    Assert ($result.ExitCode -eq 2) 'missing folder exits 2'

    Write-Host 'Compare-ComponentState'

    $baselineState = Write-Fixture 'state-baseline.json' @'
{ "components": [
  { "type": 60, "id": "11111111-1111-1111-1111-111111111111", "found": true, "name": "Sample main", "versionNumber": "1001", "modifiedOn": null, "modifiedBy": null },
  { "type": 26, "id": "22222222-2222-2222-2222-222222222222", "found": true, "name": "Sample view", "versionNumber": "2001", "modifiedOn": "2026-10-31T10:00:00Z", "modifiedBy": { "id": "99999999-9999-9999-9999-999999999999", "name": "[UserName]" } },
  { "type": 61, "id": "33333333-3333-3333-3333-333333333333", "found": true, "name": "new_/sample/form.js", "versionNumber": "3001", "modifiedOn": "2026-10-31T10:00:00Z", "modifiedBy": null }
] }
'@
    $sameState = Write-Fixture 'state-same.json' (Get-Content -LiteralPath $baselineState -Raw)
    $result = Invoke-Script 'Compare-ComponentState.ps1' @{ Baseline = @($baselineState); Current = @($sameState) }
    Assert ($result.ExitCode -eq 0 -and $result.Output.Count -eq 0) 'identical state exits 0'

    $changedState = Write-Fixture 'state-changed.json' @'
[
  { "type": 60, "id": "{11111111-1111-1111-1111-111111111111}", "found": true, "name": "Sample main", "versionNumber": "1002", "modifiedOn": null, "modifiedBy": null },
  { "type": 26, "id": "22222222-2222-2222-2222-222222222222", "found": false, "name": null, "versionNumber": null, "modifiedOn": null, "modifiedBy": null },
  { "type": 62, "id": "44444444-4444-4444-4444-444444444444", "found": true, "name": "Sample sitemap", "versionNumber": "4001", "modifiedOn": "2026-10-31T10:00:00Z", "modifiedBy": null }
]
'@
    $result = Invoke-Script 'Compare-ComponentState.ps1' @{ Baseline = @($baselineState); Current = @($changedState) }
    $statuses = @($result.Output | ForEach-Object { $_.Status })
    Assert ($result.ExitCode -eq 1) 'changed state exits 1'
    Assert ($statuses -contains 'Changed') 'version change is Changed (id braces and case ignored)'
    Assert ($statuses -contains 'Deleted') 'found false is Deleted'
    Assert ($statuses -contains 'NotChecked') 'component missing from the current results is NotChecked'
    Assert ($statuses -contains 'Added') 'component only in the current results is Added'

    Write-Host 'Compare-SolutionExport'

    $null = Write-Fixture 'exportA\Entities\Account\Entity.xml' '<Entity>a</Entity>'
    $null = Write-Fixture 'exportA\Other\Solution.xml' '<x>1</x>'
    $null = Write-Fixture 'exportB\Entities\Account\Entity.xml' '<Entity>b</Entity>'
    $null = Write-Fixture 'exportB\Other\Solution.xml' '<x>1</x>'
    $null = Write-Fixture 'exportB\Other\Extra.xml' '<x/>'
    $result = Invoke-Script 'Compare-SolutionExport.ps1' @{ Baseline = (Join-Path $work 'exportA'); Current = (Join-Path $work 'exportB') }
    Assert ($result.ExitCode -eq 1 -and $result.Output.Count -eq 2) 'changed and added files are reported'
    $result = Invoke-Script 'Compare-SolutionExport.ps1' @{ Baseline = (Join-Path $work 'exportA'); Current = (Join-Path $work 'exportB'); Include = @('Other\*') }
    Assert ($result.ExitCode -eq 1 -and $result.Output.Count -eq 1 -and $result.Output[0].Status -eq 'Added') '-Include restricts the comparison'
    $result = Invoke-Script 'Compare-SolutionExport.ps1' @{ Baseline = (Join-Path $work 'exportA'); Current = (Join-Path $work 'exportA') }
    Assert ($result.ExitCode -eq 0) 'identical trees exit 0'

    Write-Host 'Test-CustomizationsSchema'

    if (-not $SchemaPath) {
        Write-Host '  skip  no -SchemaPath'
    }
    else {
        $document = '<?xml version="1.0" encoding="utf-8"?><ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" OrganizationVersion="9.2.0.0" OrganizationSchemaType="Standard"><Entities></Entities><Roles></Roles><Workflows></Workflows><FieldSecurityProfiles></FieldSecurityProfiles><Templates /><EntityMaps /><EntityRelationships /><OrganizationSettings /><optionsets /><Languages><Language>1033</Language></Languages>{0}</ImportExportXml>'
        $baselineXml = Write-Fixture 'schema\baseline\customizations.xml' ($document -f '')
        $editedXml = Write-Fixture 'schema\edited\customizations.xml' ($document -f '<NotAnElement />')
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = Join-Path $work 'schema\baseline.zip'
        [System.IO.Compression.ZipFile]::CreateFromDirectory((Split-Path -Parent $baselineXml), $zip)

        $result = Invoke-Script 'Test-CustomizationsSchema.ps1' @{ PackagePath = $zip; SchemaPath = $SchemaPath; BaselinePackagePath = $baselineXml }
        Assert ($result.ExitCode -eq 0) 'zip identical to its baseline has no new errors'
        $result = Invoke-Script 'Test-CustomizationsSchema.ps1' @{ PackagePath = $editedXml; SchemaPath = $SchemaPath; BaselinePackagePath = $zip }
        Assert ($result.ExitCode -eq 1 -and @($result.Output | Where-Object { $_.New -and $_.Severity -eq 'Error' }).Count -ge 1) 'an invalid element is a new error'
        $result = Invoke-Script 'Test-CustomizationsSchema.ps1' @{ PackagePath = $editedXml; SchemaPath = (Join-Path $work 'no-schemas') }
        Assert ($result.ExitCode -eq 2) 'missing schema folder exits 2'
    }
}
finally {
    if (-not $env:KEEP_FIXTURES) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue } else { Write-Host "Fixtures kept in $work" }
}

Write-Host ("{0} passed, {1} failed." -f $script:Passes, $script:Failures)
if ($script:Failures -gt 0) { exit 1 }
exit 0
