<#
.SYNOPSIS
    Validates hand-edited Power Automate cloud flow definitions before packing.

.DESCRIPTION
    Checks the structural and reference errors that a text edit introduces and that
    are otherwise only caught by a failed solution import, or not at all until the
    flow runs:

      - JSON parses, and the definition node is where it should be
      - Action names are globally unique and contain no whitespace
      - Every action declares a type
      - runAfter keys resolve to a SIBLING action, with valid status values
      - body()/outputs()/actions()/items()/result() references resolve to real actions
      - @ and @{} delimiters are used correctly and balance
      - Parentheses and single-quoted literals balance inside expressions
      - Every connectionName used by an action is declared in connectionReferences

    Requires no authentication and makes no network calls.

    It cannot evaluate an expression, confirm that a column exists, or judge whether
    the logic is correct. It reduces failed imports; it does not replace one.

.PARAMETER Path
    An unpacked solution folder, a Workflows folder, or a single flow definition .json.

.PARAMETER FailOnWarning
    Treat warnings as failures for the purposes of the exit code.

.EXAMPLE
    .\Test-FlowDefinition.ps1 -Path .\wip\src

.EXAMPLE
    .\Test-FlowDefinition.ps1 -Path .\wip\src\Workflows\My_Flow-{GUID}.json -FailOnWarning

.OUTPUTS
    Finding objects (File, Severity, Rule, Location, Message). Exit code 1 if any
    Error was found, otherwise 0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Path,

    [switch]$FailOnWarning
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:Findings = New-Object System.Collections.ArrayList
$script:CurrentFile = ''

$ValidRunAfterStatuses = @('Succeeded', 'Failed', 'Skipped', 'TimedOut')

# Functions that take an action name as their single quoted argument. Action names
# may contain an apostrophe (only spaces become underscores), which WDL escapes by
# doubling it - so the literal body is "not a quote, or a doubled quote".
$ReferenceFunctionPattern = "(?<fn>body|outputs|actions|items|result)\(\s*'(?<name>(?:[^']|'')*)'\s*\)"

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

function Get-Property {
    <# Safe property access on a PSCustomObject; $null when absent. #>
    param($Node, [string]$Name)
    if ($null -eq $Node) { return $null }
    if ($Node -isnot [System.Management.Automation.PSCustomObject]) { return $null }
    $prop = $Node.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-PropertyCount {
    <# PSMemberInfoCollection does not expose Count under Set-StrictMode. #>
    param($Node)
    if ($null -eq $Node -or $Node -isnot [System.Management.Automation.PSCustomObject]) { return 0 }
    return @($Node.PSObject.Properties).Count
}

function Get-ActionInventory {
    <#
        Walks the action graph and returns one entry per action:
        Name, Node, and Scope (the container path, used for sibling checks).
    #>
    param($ActionsNode, [string]$Scope, [System.Collections.ArrayList]$Accumulator)

    if ($null -eq $ActionsNode -or $ActionsNode -isnot [System.Management.Automation.PSCustomObject]) { return }

    foreach ($prop in $ActionsNode.PSObject.Properties) {
        $name = $prop.Name
        $node = $prop.Value

        $null = $Accumulator.Add([pscustomobject]@{
                Name  = $name
                Node  = $node
                Scope = $Scope
            })

        if ($null -eq $node -or $node -isnot [System.Management.Automation.PSCustomObject]) { continue }

        $childScope = if ([string]::IsNullOrEmpty($Scope)) { $name } else { "$Scope/$name" }

        # Scope, Foreach, Until, If (true branch)
        Get-ActionInventory (Get-Property $node 'actions') $childScope $Accumulator

        # If (false branch)
        $elseNode = Get-Property $node 'else'
        Get-ActionInventory (Get-Property $elseNode 'actions') "$childScope/else" $Accumulator

        # Switch default
        $defaultNode = Get-Property $node 'default'
        Get-ActionInventory (Get-Property $defaultNode 'actions') "$childScope/default" $Accumulator

        # Switch cases
        $casesNode = Get-Property $node 'cases'
        if ($null -ne $casesNode -and $casesNode -is [System.Management.Automation.PSCustomObject]) {
            foreach ($case in $casesNode.PSObject.Properties) {
                Get-ActionInventory (Get-Property $case.Value 'actions') "$childScope/cases/$($case.Name)" $Accumulator
            }
        }
    }
}

function Get-StringValue {
    <# Every string leaf in the tree, with a dotted path for reporting. #>
    param($Node, [string]$NodePath, [System.Collections.ArrayList]$Accumulator)

    if ($null -eq $Node) { return }

    if ($Node -is [string]) {
        $null = $Accumulator.Add([pscustomobject]@{ Path = $NodePath; Value = $Node })
        return
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($prop in $Node.PSObject.Properties) {
            Get-StringValue $prop.Value "$NodePath.$($prop.Name)" $Accumulator
        }
        return
    }

    if ($Node -is [System.Collections.IList]) {
        for ($i = 0; $i -lt $Node.Count; $i++) {
            Get-StringValue $Node[$i] "$NodePath[$i]" $Accumulator
        }
        return
    }
    # Numbers and booleans carry no expressions.
}

function Measure-ExpressionBalance {
    <#
        Paren and quote balance over an expression body. Single-quoted literals are
        skipped, and a doubled '' inside a literal is the WDL escape for one quote.
    #>
    param([string]$Text)

    $depth = 0
    $inQuote = $false
    $negative = $false

    for ($i = 0; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]

        if ($c -eq "'") {
            if ($inQuote -and ($i + 1) -lt $Text.Length -and $Text[$i + 1] -eq "'") { $i++; continue }
            $inQuote = -not $inQuote
            continue
        }
        if ($inQuote) { continue }

        if ($c -eq '(') { $depth++ }
        elseif ($c -eq ')') {
            $depth--
            if ($depth -lt 0) { $negative = $true; $depth = 0 }
        }
    }

    return [pscustomobject]@{
        ParenDepth      = $depth
        UnmatchedClose  = $negative
        UnterminatedStr = $inQuote
    }
}

function Get-InterpolatedSegment {
    <#
        Returns the body of each @{ ... } segment, and reports segments that never
        close. Brace depth is tracked so nested objects inside an expression survive.
    #>
    param([string]$Text, [string]$Location)

    $segments = New-Object System.Collections.ArrayList
    $i = 0

    while ($i -lt $Text.Length - 1) {
        if ($Text[$i] -eq '@' -and $Text[$i + 1] -eq '{') {
            # @@{ is an escaped literal, not an interpolation.
            if ($i -gt 0 -and $Text[$i - 1] -eq '@') { $i += 2; continue }

            $depth = 0
            $start = $i + 2
            $j = $start
            $closed = $false

            for (; $j -lt $Text.Length; $j++) {
                if ($Text[$j] -eq '{') { $depth++ }
                elseif ($Text[$j] -eq '}') {
                    if ($depth -eq 0) { $closed = $true; break }
                    $depth--
                }
            }

            if (-not $closed) {
                Add-Finding -Severity Error -Rule 'expression-delimiter' -Location $Location `
                    -Message "Unclosed '@{' interpolation at offset $i."
                break
            }

            $null = $segments.Add([pscustomobject]@{
                    Body  = $Text.Substring($start, $j - $start)
                    Start = $i
                    End   = $j
                })
            $i = $j + 1
            continue
        }
        $i++
    }

    return $segments
}

function Test-FlowFile {
    param([string]$FilePath)

    $script:CurrentFile = Split-Path $FilePath -Leaf

    $raw = Get-Content -LiteralPath $FilePath -Raw -Encoding UTF8
    try {
        $root = $raw | ConvertFrom-Json
    }
    catch {
        Add-Finding -Severity Error -Rule 'json-parse' -Location '(file)' `
            -Message "File is not valid JSON: $($_.Exception.Message)"
        return
    }

    # The unpacked form nests under properties; accept a bare definition too.
    $properties = Get-Property $root 'properties'
    $definition = Get-Property $properties 'definition'
    if ($null -eq $definition) { $definition = Get-Property $root 'definition' }

    if ($null -eq $definition) {
        Add-Finding -Severity Error -Rule 'shape' -Location '(root)' `
            -Message "No 'properties.definition' node found. This does not look like a cloud flow definition."
        return
    }

    $triggers = Get-Property $definition 'triggers'
    $actions = Get-Property $definition 'actions'

    $triggerCount = Get-PropertyCount $triggers
    if ($triggerCount -eq 0) {
        Add-Finding -Severity Error -Rule 'shape' -Location 'definition.triggers' -Message 'No trigger defined.'
    }
    elseif ($triggerCount -gt 1) {
        Add-Finding -Severity Warning -Rule 'shape' -Location 'definition.triggers' `
            -Message "A cloud flow has exactly one trigger; found $triggerCount."
    }

    $inventory = New-Object System.Collections.ArrayList
    Get-ActionInventory $actions '' $inventory

    if ($inventory.Count -eq 0) {
        Add-Finding -Severity Warning -Rule 'shape' -Location 'definition.actions' -Message 'The flow has no actions.'
    }

    $triggerNames = @()
    if ($null -ne $triggers) { $triggerNames = @($triggers.PSObject.Properties | ForEach-Object { $_.Name }) }

    $actionNames = @($inventory | ForEach-Object { $_.Name })
    $knownNames = @($actionNames + $triggerNames)

    # --- Action names --------------------------------------------------------
    $duplicates = $actionNames | Group-Object | Where-Object { $_.Count -gt 1 }
    foreach ($dup in $duplicates) {
        Add-Finding -Severity Error -Rule 'duplicate-action-name' -Location $dup.Name `
            -Message "Action name is used $($dup.Count) times. Names must be unique across the whole definition."
    }

    foreach ($entry in $inventory) {
        $where = if ([string]::IsNullOrEmpty($entry.Scope)) { $entry.Name } else { "$($entry.Scope)/$($entry.Name)" }

        if ($entry.Name -match '\s') {
            Add-Finding -Severity Error -Rule 'action-name-encoding' -Location $where `
                -Message "Action name contains whitespace. The key must be the underscore-encoded form, e.g. 'Get_a_row_by_ID'."
        }

        if ($null -eq (Get-Property $entry.Node 'type')) {
            Add-Finding -Severity Error -Rule 'missing-type' -Location $where -Message "Action has no 'type'."
        }
    }

    # --- runAfter graph ------------------------------------------------------
    $scopeGroups = @{}
    foreach ($entry in $inventory) {
        $key = [string]$entry.Scope
        if (-not $scopeGroups.ContainsKey($key)) { $scopeGroups[$key] = New-Object System.Collections.ArrayList }
        $null = $scopeGroups[$key].Add($entry.Name)
    }

    foreach ($entry in $inventory) {
        $where = if ([string]::IsNullOrEmpty($entry.Scope)) { $entry.Name } else { "$($entry.Scope)/$($entry.Name)" }
        $runAfter = Get-Property $entry.Node 'runAfter'
        if ($null -eq $runAfter -or $runAfter -isnot [System.Management.Automation.PSCustomObject]) { continue }

        $siblings = @($scopeGroups[[string]$entry.Scope])

        foreach ($dep in $runAfter.PSObject.Properties) {
            if ($siblings -notcontains $dep.Name) {
                if ($actionNames -contains $dep.Name) {
                    Add-Finding -Severity Error -Rule 'runafter-scope' -Location "$where.runAfter" `
                        -Message "'$($dep.Name)' exists but is not a sibling. runAfter may only reference actions at the same nesting level."
                }
                else {
                    Add-Finding -Severity Error -Rule 'runafter-unknown' -Location "$where.runAfter" `
                        -Message "'$($dep.Name)' is not an action in this definition."
                }
            }

            foreach ($status in @($dep.Value)) {
                if ($ValidRunAfterStatuses -notcontains $status) {
                    Add-Finding -Severity Error -Rule 'runafter-status' -Location "$where.runAfter.$($dep.Name)" `
                        -Message "'$status' is not a valid status. Expected one of: $($ValidRunAfterStatuses -join ', ')."
                }
            }
        }
    }

    foreach ($scopeKey in $scopeGroups.Keys) {
        $entryPoints = @($inventory | Where-Object {
                [string]$_.Scope -eq $scopeKey -and
                ((Get-PropertyCount (Get-Property $_.Node 'runAfter')) -eq 0)
            })
        $label = if ([string]::IsNullOrEmpty($scopeKey)) { '(top level)' } else { $scopeKey }
        if ($entryPoints.Count -eq 0 -and $scopeGroups[$scopeKey].Count -gt 0) {
            Add-Finding -Severity Error -Rule 'no-entry-point' -Location $label `
                -Message 'No action has an empty runAfter, so nothing at this level can start.'
        }
        elseif ($entryPoints.Count -gt 1) {
            Add-Finding -Severity Warning -Rule 'parallel-branches' -Location $label `
                -Message "$($entryPoints.Count) actions have an empty runAfter ($(($entryPoints | ForEach-Object { $_.Name }) -join ', ')). They run in parallel - confirm that is intended."
        }
    }

    # --- Expressions ---------------------------------------------------------
    $strings = New-Object System.Collections.ArrayList
    Get-StringValue $definition 'definition' $strings

    foreach ($item in $strings) {
        $value = $item.Value
        if ([string]::IsNullOrEmpty($value)) { continue }
        if ($value.IndexOf('@') -lt 0) { continue }

        $expressionBodies = New-Object System.Collections.ArrayList

        if ($value.StartsWith('@@')) {
            # Escaped literal @; nothing to evaluate at the start, but @{} may still follow.
        }
        elseif ($value.StartsWith('@{')) {
            # Interpolation. Whether it spans the whole value is decided below, once
            # the segments are known - "@{a}-@{b}" starts with '@{' but is not one.
        }
        elseif ($value.StartsWith('@')) {
            $body = $value.Substring(1)
            $null = $expressionBodies.Add($body)

            if ($body -match '\}\s*\S' -or $body.Contains('@{')) {
                Add-Finding -Severity Warning -Rule 'expression-delimiter' -Location $item.Path `
                    -Message "Value starts with '@' (whole-value expression) but also contains braces. A whole-value expression cannot have surrounding text - use '@{...}' interpolation instead."
            }
        }
        elseif ($value.Contains('@{') -eq $false -and $value -match '(?<!@)@[a-zA-Z_]+\s*\(') {
            Add-Finding -Severity Warning -Rule 'expression-delimiter' -Location $item.Path `
                -Message "Looks like an expression but '@' is not at position 0 and is not wrapped in '@{...}', so it is treated as a literal."
        }

        $segments = @(Get-InterpolatedSegment -Text $value -Location $item.Path)
        foreach ($segment in $segments) {
            $null = $expressionBodies.Add($segment.Body)
        }

        # A value that is exactly one '@{...}' and nothing else evaluates to a string.
        # That is correct wherever a string is wanted - the designer emits it constantly -
        # and only a defect where the target expects an object, array, number or boolean.
        # Advisory, because the expected type is not knowable from the definition alone.
        if ($value.StartsWith('@{') -and $segments.Count -eq 1 -and $segments[0].End -eq ($value.Length - 1)) {
            Add-Finding -Severity Info -Rule 'expression-coercion' -Location $item.Path `
                -Message "Value is exactly one '@{...}', so the result is coerced to a string. Correct for a string target; use '@...' without braces if an object, array, number or boolean is expected."
        }

        foreach ($body in $expressionBodies) {
            $balance = Measure-ExpressionBalance -Text $body
            if ($balance.ParenDepth -gt 0) {
                Add-Finding -Severity Error -Rule 'expression-parens' -Location $item.Path `
                    -Message "$($balance.ParenDepth) unclosed '(' in expression: $body"
            }
            if ($balance.UnmatchedClose) {
                Add-Finding -Severity Error -Rule 'expression-parens' -Location $item.Path `
                    -Message "Unmatched ')' in expression: $body"
            }
            if ($balance.UnterminatedStr) {
                Add-Finding -Severity Error -Rule 'expression-quotes' -Location $item.Path `
                    -Message "Unterminated single-quoted literal in expression (a literal quote is escaped by doubling it): $body"
            }

            foreach ($match in [regex]::Matches($body, $ReferenceFunctionPattern)) {
                # Undo the WDL escape so the name compares against the real action key.
                $referenced = $match.Groups['name'].Value.Replace("''", "'")
                $fn = $match.Groups['fn'].Value
                if ($knownNames -notcontains $referenced) {
                    $hint = ''
                    if ($referenced -match '\s') {
                        $hint = " Action references use the underscore-encoded name, e.g. '$($referenced -replace '\s', '_')'."
                    }
                    Add-Finding -Severity Error -Rule 'unknown-reference' -Location $item.Path `
                        -Message "$fn('$referenced') does not name any action or trigger in this definition.$hint"
                }
            }
        }
    }

    # --- Connection references ----------------------------------------------
    $connectionReferences = Get-Property $properties 'connectionReferences'
    $declared = @()
    if ($null -ne $connectionReferences) {
        $declared = @($connectionReferences.PSObject.Properties | ForEach-Object { $_.Name })
    }

    $allNodes = New-Object System.Collections.ArrayList
    foreach ($entry in $inventory) { $null = $allNodes.Add($entry) }
    if ($null -ne $triggers) {
        foreach ($prop in $triggers.PSObject.Properties) {
            $null = $allNodes.Add([pscustomobject]@{ Name = $prop.Name; Node = $prop.Value; Scope = 'triggers' })
        }
    }

    foreach ($entry in $allNodes) {
        $hostNode = Get-Property (Get-Property $entry.Node 'inputs') 'host'
        $connectionName = Get-Property $hostNode 'connectionName'
        if ([string]::IsNullOrEmpty($connectionName)) { continue }

        if ($declared -notcontains $connectionName) {
            Add-Finding -Severity Error -Rule 'undeclared-connection' -Location $entry.Name `
                -Message "connectionName '$connectionName' is not declared in properties.connectionReferences. Add the connector in the designer once and re-export rather than hand-writing it."
        }
    }

    foreach ($name in $declared) {
        $used = @($allNodes | Where-Object {
                (Get-Property (Get-Property (Get-Property $_.Node 'inputs') 'host') 'connectionName') -eq $name
            })
        if ($used.Count -eq 0) {
            Add-Finding -Severity Info -Rule 'unused-connection' -Location "connectionReferences.$name" `
                -Message 'Declared but not used by any action or trigger.'
        }
    }
}

# --- Entry point -------------------------------------------------------------

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Path not found: $Path"
    exit 2
}

$item = Get-Item -LiteralPath $Path
if ($item.PSIsContainer) {
    $workflowsFolder = Join-Path $item.FullName 'Workflows'
    $searchRoot = if (Test-Path -LiteralPath $workflowsFolder) { $workflowsFolder } else { $item.FullName }
    $files = @(Get-ChildItem -LiteralPath $searchRoot -Recurse -Filter '*.json' -File)
}
else {
    $files = @($item)
}

if ($files.Count -eq 0) {
    Write-Warning "No flow definition (.json) files found under: $Path"
    exit 0
}

foreach ($file in $files) {
    Test-FlowFile -FilePath $file.FullName
}

$errors = @($script:Findings | Where-Object { $_.Severity -eq 'Error' })
$warnings = @($script:Findings | Where-Object { $_.Severity -eq 'Warning' })

# Findings go to the pipeline so the script composes; the summary goes to the host
# so it survives being piped, filtered or captured.
if ($script:Findings.Count -gt 0) {
    $script:Findings | Sort-Object File, Severity, Location
}

Write-Host ("Checked {0} file(s): {1} error(s), {2} warning(s), {3} info." -f `
        $files.Count, $errors.Count, $warnings.Count, @($script:Findings | Where-Object { $_.Severity -eq 'Info' }).Count)

if ($errors.Count -gt 0) { exit 1 }
if ($FailOnWarning -and $warnings.Count -gt 0) { exit 1 }
exit 0
