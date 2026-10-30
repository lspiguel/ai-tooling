<#
.SYNOPSIS
    Checks the MCP layer's transport and protocol rules (SPEC §6.1) against a running console harness.

.DESCRIPTION
    Sends one request per rule and compares the HTTP status and JSON-RPC error code with what the spec
    expects. The thirty-one checks are also unit tests (McpProtocolTests); this script is for manual
    regression over real HTTP. It calls the harness's fake tools (hello, big), so run it against
    D365AgentCompanionMcp.ConsoleHarness, not against the plugin in XrmToolBox.

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
    $session = $null
    $values = $null
    if ($response.Headers.TryGetValues('Mcp-Session-Id', [ref] $values)) { $session = @($values)[0] }
    [pscustomobject]@{ Status = [int]$response.StatusCode; Json = $json; Length = $text.Length; SessionId = $session }
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
    @{ Case = 'request without _meta or session';   Expect = '400 -32022';  Run = { Send-Request -Body (New-Body 'tools/list' -NoMeta) } }
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
function Write-Check {
    param([string] $Case, [string] $Expect, [string] $Actual)
    $ok = $Actual -eq $Expect
    if (-not $ok) { $script:failures++ }
    '{0,-4} {1,-34} expected {2,-11} got {3}' -f ($(if ($ok) { 'OK' } else { 'FAIL' })), $Case, $Expect, $Actual
}

function Get-Outcome { param($r) $a = "$($r.Status)"; if ($r.Json -and $r.Json.error) { $a = "$a $($r.Json.error.code)" }; $a }

foreach ($case in $cases) {
    $r = & $case.Run
    Write-Check $case.Case $case.Expect (Get-Outcome $r)
}

# Handshake-era fallback (D7): a whole session, as a 2025-11-25 client would run it.
"`n-- handshake-era fallback --"
function New-LegacyBody {
    param([string] $Method, [hashtable] $Params = @{}, [switch] $Notification)
    $message = @{ jsonrpc = '2.0'; method = $Method; params = $Params }
    if (-not $Notification) { $message['id'] = 1 }
    $message | ConvertTo-Json -Depth 10 -Compress
}

$init = Send-Request -Body (New-LegacyBody 'initialize' @{ protocolVersion = '2025-11-25'; capabilities = @{}; clientInfo = @{ name = 'protocol-checks'; version = '0' } })
Write-Check 'initialize 2025-11-25' '200' (Get-Outcome $init)
Write-Check '  negotiated version' '2025-11-25' "$($init.Json.result.protocolVersion)"
Write-Check '  session id issued' 'True' "$([bool]$init.SessionId)"
$sh = @{ 'Mcp-Session-Id' = $init.SessionId; 'MCP-Protocol-Version' = '2025-11-25' }

Write-Check 'notifications/initialized' '202' (Get-Outcome (Send-Request -Body (New-LegacyBody 'notifications/initialized' -Notification) -Headers $sh))
Write-Check 'ping' '200' (Get-Outcome (Send-Request -Body (New-LegacyBody 'ping') -Headers $sh))
$list = Send-Request -Body (New-LegacyBody 'tools/list') -Headers $sh
Write-Check 'tools/list' '200' (Get-Outcome $list)
Write-Check '  no modern-only fields' 'True' "$($null -eq $list.Json.result.resultType -and $null -eq $list.Json.result.ttlMs)"
$call = Send-Request -Body (New-LegacyBody 'tools/call' @{ name = 'hello'; arguments = @{} }) -Headers $sh
Write-Check 'tools/call hello' '200' (Get-Outcome $call)
Write-Check '  structuredContent present' 'True' "$($null -ne $call.Json.result.structuredContent)"
Write-Check 'header version mismatch' '400 -32600' (Get-Outcome (Send-Request -Body (New-LegacyBody 'ping') -Headers @{ 'Mcp-Session-Id' = $init.SessionId; 'MCP-Protocol-Version' = '2025-06-18' }))
Write-Check 'unknown session' '404 -32001' (Get-Outcome (Send-Request -Body (New-LegacyBody 'ping') -Headers @{ 'Mcp-Session-Id' = 'nope'; 'MCP-Protocol-Version' = '2025-11-25' }))

$old = Send-Request -Body (New-LegacyBody 'initialize' @{ protocolVersion = '2025-03-26'; capabilities = @{}; clientInfo = @{ name = 'protocol-checks'; version = '0' } })
Write-Check 'initialize 2025-03-26 -> offered' '2025-11-25' "$($old.Json.result.protocolVersion)"
$v618 = Send-Request -Body (New-LegacyBody 'initialize' @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'protocol-checks'; version = '0' } })
Write-Check 'initialize 2025-06-18 -> agreed' '2025-06-18' "$($v618.Json.result.protocolVersion)"

Write-Check 'DELETE session' '200' (Get-Outcome (Send-Request -HttpMethod 'DELETE' -Headers @{ 'Mcp-Session-Id' = $init.SessionId }))
Write-Check 'ping after DELETE' '404 -32001' (Get-Outcome (Send-Request -Body (New-LegacyBody 'ping') -Headers $sh))

"`n$failures failure(s)"
exit $failures
