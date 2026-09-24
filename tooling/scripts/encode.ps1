param(
    [string]$InputFile,
    [string]$OutputFile
)

if (-not $InputFile) {
    Write-Host "Usage: .\encode.ps1 -InputFile <path-to-binary> [-OutputFile <path-to-json>]"
    Write-Host "  Encodes a binary file as base64 and outputs a JSON with a `$`$content field."
    Write-Host "  If -OutputFile is omitted, prints JSON to stdout."
    exit 0
}

$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path $InputFile))
$base64 = [System.Convert]::ToBase64String($bytes)

$json = [ordered]@{ '$content' = $base64 } | ConvertTo-Json

if ($OutputFile) {
    $json | Set-Content -Encoding utf8 $OutputFile
    Write-Host "Saved $OutputFile ($($bytes.Length) bytes encoded)"
} else {
    $json
}
