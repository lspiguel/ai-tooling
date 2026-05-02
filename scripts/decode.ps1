param(
    [string]$InputFile,
    [string]$OutputFile
)

if (-not $InputFile -or -not $OutputFile) {
    Write-Host "Usage: .\decode.ps1 -InputFile <path-to-json> -OutputFile <path-to-output-binary>"
    Write-Host "  Reads a JSON file with a `$`$content base64 field and writes the decoded binary."
    exit 0
}

$json = Get-Content -Raw $InputFile | ConvertFrom-Json

if ($json.'$content') {
    $base64 = $json.'$content'
} elseif ($json.body -and $json.body.'$content') {
    $base64 = $json.body.'$content'
}

if ($base64) {
    $bytes = [System.Convert]::FromBase64String($base64)
    [System.IO.File]::WriteAllBytes((Join-Path (Get-Location) $OutputFile), $bytes)
    Write-Host "Saved $OutputFile ($($bytes.Length) bytes)"
} else {
    Write-Host "Could not find `$content field"
}
