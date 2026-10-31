<#
.SYNOPSIS
    Calls one Agent Companion MCP tool over HTTP, for when the agent client has not loaded the plugin's tools.

.DESCRIPTION
    The agent client normally calls the plugin's tools itself. When it cannot - the
    server was registered after the session started, or the client's tool list is
    stale - this script calls the same endpoint the same way: one JSON-RPC
    tools/call request under MCP revision 2026-07-28, with the headers that revision
    requires. It is not another route to Dataverse: the plugin still asks the person
    to approve every call in its tab and logs it (R2, R13).

    -All follows nextCursor until it is null and merges the pages: list fields are
    concatenated, and ribbonXml chunks are joined in order.

    The bearer token is never stored by this script. Pass it with -Token, or set the
    AGENT_COMPANION_TOKEN environment variable for the session. It is the token the
    plugin panel shows; the client configuration the agent already uses holds it too.

.PARAMETER Tool
    The tool name, for example list_solutions.

.PARAMETER Arguments
    The tool's arguments: a hashtable, or a JSON object string. Write and Publisher
    tools need a non-blank reason (R12).

.PARAMETER All
    Follow nextCursor and return every page as one result.

.PARAMETER OutFile
    Also write the result as JSON to this file. Keep it outside every Git working
    tree (R7).

.PARAMETER Url
    The endpoint. Default http://127.0.0.1:47321/mcp.

.PARAMETER Token
    The bearer token, with or without the "Bearer " prefix. Default: the
    AGENT_COMPANION_TOKEN environment variable.

.PARAMETER TimeoutSec
    How long to wait for one call, approval included. Default 600.

.EXAMPLE
    .\Invoke-CompanionTool.ps1 -Tool list_connections

.EXAMPLE
    .\Invoke-CompanionTool.ps1 -Tool list_solution_components -All -OutFile .\work\wip.json `
        -Arguments @{ connection = '<connectionId>'; solutionUniqueName = 'XY_Agent_WIP_1234_Base' }

.OUTPUTS
    The tool's structured result. On a tool error, the ToolError object and exit code
    1; on an HTTP or JSON-RPC error, the error and exit code 2.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Tool,
    [Parameter(Position = 1)]$Arguments = @{},
    [switch]$All,
    [string]$OutFile,
    [string]$Url = 'http://127.0.0.1:47321/mcp',
    [string]$Token = $env:AGENT_COMPANION_TOKEN,
    [int]$TimeoutSec = 600
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Version = '2026-07-28'

if (-not $Token) {
    Write-Error -ErrorAction Continue 'No bearer token: pass -Token or set AGENT_COMPANION_TOKEN. Copy it from the plugin panel.'
    exit 2
}
if ($Token -notmatch '^Bearer ') { $Token = "Bearer $Token" }

if ($Arguments -is [string]) {
    $Arguments = if ($Arguments.Trim()) { $Arguments | ConvertFrom-Json } else { @{} }
}

$script:RequestId = 0

function Invoke-Call {
    param($CallArguments)
    $script:RequestId++
    $body = @{
        jsonrpc = '2.0'
        id      = $script:RequestId
        method  = 'tools/call'
        params  = @{
            name      = $Tool
            arguments = $CallArguments
            _meta     = @{
                'io.modelcontextprotocol/protocolVersion' = $Version
                'io.modelcontextprotocol/clientInfo'      = @{ name = 'Invoke-CompanionTool'; version = '1' }
            }
        }
    } | ConvertTo-Json -Depth 30 -Compress
    $headers = @{
        'Authorization'        = $Token
        'Accept'               = 'application/json, text/event-stream'
        'MCP-Protocol-Version' = $Version
        'Mcp-Method'           = 'tools/call'
        'Mcp-Name'             = $Tool
    }
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Post -UseBasicParsing -Headers $headers -TimeoutSec $TimeoutSec `
            -ContentType 'application/json' -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
        $text = [System.Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray())
    }
    catch [System.Net.WebException] {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        $detail = ''
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $detail = $reader.ReadToEnd()
        }
        return [pscustomobject]@{ Kind = 'http'; Value = [pscustomobject]@{ status = $status; message = $_.Exception.Message; body = $detail } }
    }
    $json = $text | ConvertFrom-Json
    if ($json.PSObject.Properties['error']) {
        return [pscustomobject]@{ Kind = 'rpc'; Value = $json.error }
    }
    $result = $json.result
    if ($result.PSObject.Properties['isError'] -and $result.isError) {
        return [pscustomobject]@{ Kind = 'tool'; Value = ($result.content[0].text | ConvertFrom-Json) }
    }
    return [pscustomobject]@{ Kind = 'ok'; Value = $result.structuredContent }
}

$call = Invoke-Call -CallArguments $Arguments
$result = $call.Value

if ($call.Kind -eq 'ok' -and $All) {
    $connection = if ($Arguments -is [hashtable]) { $Arguments['connection'] } else { $Arguments.connection }
    while ($result.PSObject.Properties['nextCursor'] -and $result.nextCursor) {
        $next = Invoke-Call -CallArguments @{ connection = $connection; cursor = $result.nextCursor }
        if ($next.Kind -ne 'ok') {
            $result | Add-Member -NotePropertyName pagingError -NotePropertyValue $next.Value -Force
            break
        }
        foreach ($property in $next.Value.PSObject.Properties) {
            if ($property.Name -eq 'nextCursor') { continue }
            $current = $result.PSObject.Properties[$property.Name]
            if ($property.Name -eq 'ribbonXml') { $result.ribbonXml = $result.ribbonXml + $property.Value }
            elseif ($null -ne $current -and $current.Value -is [array]) { $result.($property.Name) = @($current.Value) + @($property.Value) }
        }
        $result.nextCursor = $next.Value.nextCursor
    }
}

if ($OutFile) {
    $result | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutFile -Encoding UTF8
}
$result

switch ($call.Kind) {
    'ok' { exit 0 }
    'tool' { exit 1 }
    default { exit 2 }
}
