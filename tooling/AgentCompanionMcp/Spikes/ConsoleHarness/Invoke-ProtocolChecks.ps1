<#
.SYNOPSIS
    Spike S1: checks the MCP layer's transport and protocol rules (SPEC §6.1) against a running endpoint.

.DESCRIPTION
    Sends one request per rule and compares the HTTP status and JSON-RPC error code with what the spec
    expects. Works against the console harness or the XrmToolBox spike plugin. Throwaway.

.EXAMPLE
    .\Invoke-ProtocolChecks.ps1 -Token <token>
#>
param(
    [string] $Endpoint = 'http://127.0.0.1:47321/mcp/',
    [Parameter(Mandatory = $true)] [string] $Token
)

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$version = '2026-07-28'

function Send-Request {
    param(
        [string] $HttpMethod = 'POST',
        [string] $Body,
        [hashtable] $Headers = @{},
        [switch] $NoAuth
    )

    $request = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($HttpMethod)), $Endpoint
    if (-not $NoAuth) { $request.Headers.TryAddWithoutValidation('Authorization', "Bearer $Token") | Out-Null }
    $request.Headers.TryAddWithoutValidation('Accept', 'application/json, text/event-stream') | Out-Null
    foreach ($key in $Headers.Keys) { $request.Headers.TryAddWithoutValidation($key, $Headers[$key]) | Out-Null }
    if ($Body) { $request.Content = New-Object System.Net.Http.StringContent $Body, ([Text.Encoding]::UTF8), 'application/json' }

    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $json = $null
    if ($text) { try { $json = $text | ConvertFrom-Json } catch { } }
    [pscustomobject]@{ Status = [int]$response.StatusCode; Json = $json; Length = $text.Length }
}

function New-Body {
    param([string] $Method, [hashtable] $Params = @{}, [string] $Version = $version, [switch] $NoMeta, [switch] $Notification)

    $p = @{} + $Params
    if (-not $NoMeta) { $p['_meta'] = @{ 'io.modelcontextprotocol/protocolVersion' = $Version; 'io.modelcontextprotocol/clientInfo' = @{ name = 'protocol-checks'; version = '0' } } }
    $message = @{ jsonrpc = '2.0'; method = $Method; params = $p }
    if (-not $Notification) { $message['id'] = 1 }
    $message | ConvertTo-Json -Depth 10 -Compress
}

function Get-StandardHeaders {
    param([string] $Method, [string] $Name)
    $h = @{ 'MCP-Protocol-Version' = $version; 'Mcp-Method' = $Method }
    if ($Name) { $h['Mcp-Name'] = $Name }
    $h
}

$cases = @(
    @{ Case = 'server/discover';                    Expect = '200';         Run = { Send-Request -Body (New-Body 'server/discover') -Headers (Get-StandardHeaders 'server/discover') } }
    @{ Case = 'tools/list';                         Expect = '200';         Run = { Send-Request -Body (New-Body 'tools/list') -Headers (Get-StandardHeaders 'tools/list') } }
    @{ Case = 'tools/call hello';                   Expect = '200';         Run = { Send-Request -Body (New-Body 'tools/call' @{ name = 'hello'; arguments = @{} }) -Headers (Get-StandardHeaders 'tools/call' 'hello') } }
    @{ Case = 'Mcp-Name base64 sentinel';           Expect = '200';         Run = { Send-Request -Body (New-Body 'tools/call' @{ name = 'hello'; arguments = @{} }) -Headers (Get-StandardHeaders 'tools/call' ('=?base64?' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('hello')) + '?=')) } }
    @{ Case = 'unknown tool';                       Expect = '200 -32602';  Run = { Send-Request -Body (New-Body 'tools/call' @{ name = 'nope'; arguments = @{} }) -Headers (Get-StandardHeaders 'tools/call' 'nope') } }
    @{ Case = 'unknown method';                     Expect = '404 -32601';  Run = { Send-Request -Body (New-Body 'resources/list') -Headers (Get-StandardHeaders 'resources/list') } }
    @{ Case = 'legacy initialize';                  Expect = '400 -32022';  Run = { Send-Request -Body (New-Body 'initialize' @{ protocolVersion = '2025-11-25' } -NoMeta) } }
    @{ Case = 'unsupported version in _meta';       Expect = '400 -32022';  Run = { Send-Request -Body (New-Body 'tools/list' -Version '2025-11-25') -Headers @{ 'MCP-Protocol-Version' = '2025-11-25'; 'Mcp-Method' = 'tools/list' } } }
    @{ Case = 'missing Mcp-Method header';          Expect = '400 -32020';  Run = { Send-Request -Body (New-Body 'tools/list') -Headers @{ 'MCP-Protocol-Version' = $version } } }
    @{ Case = 'Mcp-Name not matching body';         Expect = '400 -32020';  Run = { Send-Request -Body (New-Body 'tools/call' @{ name = 'hello'; arguments = @{} }) -Headers (Get-StandardHeaders 'tools/call' 'big') } }
    @{ Case = 'notification';                       Expect = '202';         Run = { Send-Request -Body (New-Body 'notifications/cancelled' -Notification) -Headers (Get-StandardHeaders 'notifications/cancelled') } }
    @{ Case = 'GET';                                Expect = '405';         Run = { Send-Request -HttpMethod 'GET' } }
    @{ Case = 'DELETE';                             Expect = '405';         Run = { Send-Request -HttpMethod 'DELETE' } }
    @{ Case = 'no bearer token';                    Expect = '401';         Run = { Send-Request -Body (New-Body 'tools/list') -Headers (Get-StandardHeaders 'tools/list') -NoAuth } }
    @{ Case = 'foreign Origin';                     Expect = '403';         Run = { Send-Request -Body (New-Body 'tools/list') -Headers ((Get-StandardHeaders 'tools/list') + @{ Origin = 'https://example.com' }) } }
    @{ Case = 'Mcp-Session-Id ignored';             Expect = '200';         Run = { Send-Request -Body (New-Body 'tools/list') -Headers ((Get-StandardHeaders 'tools/list') + @{ 'Mcp-Session-Id' = 'abc' }) } }
)

$failures = 0
foreach ($case in $cases) {
    $r = & $case.Run
    $actual = "$($r.Status)"
    if ($r.Json -and $r.Json.error) { $actual = "$actual $($r.Json.error.code)" }
    $ok = $actual -eq $case.Expect
    if (-not $ok) { $failures++ }
    '{0,-4} {1,-34} expected {2,-11} got {3}' -f ($(if ($ok) { 'OK' } else { 'FAIL' })), $case.Case, $case.Expect, $actual
}

"`n$failures failure(s)"
exit $failures
