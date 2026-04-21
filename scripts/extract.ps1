$inputPath = "in.json"
$outputPath = "out.zip"

$json = Get-Content -Raw $inputPath | ConvertFrom-Json

# Try to find $content at various levels
if ($json.'$content') {
    $base64 = $json.'$content'
} elseif ($json.body -and $json.body.'$content') {
    $base64 = $json.body.'$content'
}

if ($base64) {
    $bytes = [System.Convert]::FromBase64String($base64)
    [System.IO.File]::WriteAllBytes($outputPath, $bytes)
    Write-Host "Saved package.zip ($($bytes.Length) bytes)"
} else {
    Write-Host "Could not find `$content field"
}
