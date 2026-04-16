$inputPath = "c:\Users\lspig\Git-Home\CS-Group-Context\13535 - Add market type mapping and logic for customer creation. Power Platform part\08584257780208540590313714654CU00.json"
$outputPath = "c:\Users\lspig\Git-Home\CS-Group-Context\13535 - Add market type mapping and logic for customer creation. Power Platform part\08584257780208540590313714654CU00.zip"

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
