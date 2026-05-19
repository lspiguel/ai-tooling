# PowerShell script to convert .docx files to .md using pandoc
# and .mmd files to .svg and .png (scale 3) using mmdc
# Markdown files are only converted if missing; SVG/PNG files are always overwritten

param(
    [string]$Path = ".",
    [string[]]$TextExtensions = @(".docx"),
    [string[]]$DiagramExtensions = @(".mmd")
)

# Combine all extensions to search for
$allExtensions = $TextExtensions + $DiagramExtensions

# Get all files with specified extensions recursively
$files = Get-ChildItem -Path $Path -Recurse -File | Where-Object {
    $allExtensions -contains $_.Extension
}

$convertedCount = 0
$skippedCount = 0
$errorCount = 0

foreach ($file in $files) {
    $inputPath = $file.FullName
    $extension = $file.Extension
    
    # Determine output path and conversion method based on extension
    if ($TextExtensions -contains $extension) {
        # Text files: convert to markdown
        $outputPath = Join-Path $file.DirectoryName ($file.BaseName + ".md")
        $outputExists = Test-Path $outputPath
        
        if ($outputExists) {
            Write-Host "Skipping: $inputPath (markdown file already exists)" -ForegroundColor Yellow
            $skippedCount++
            continue
        }
        
        # Convert using pandoc
        Write-Host "Converting: $inputPath -> $outputPath" -ForegroundColor Green
        
        try {
            pandoc --from=docx --to=markdown "$inputPath" -o "$outputPath"
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Successfully converted: $outputPath" -ForegroundColor Green
                $convertedCount++
            }
            else {
                Write-Host "Error converting: $inputPath (pandoc exit code: $LASTEXITCODE)" -ForegroundColor Red
                $errorCount++
            }
        }
        catch {
            Write-Host "Error converting: $inputPath - $($_.Exception.Message)" -ForegroundColor Red
            $errorCount++
        }
    }
    elseif ($DiagramExtensions -contains $extension) {
        # Diagram files: convert to SVG and PNG using mmdc (always overwrite)
        $svgPath = Join-Path $file.DirectoryName ($file.BaseName + ".svg")
        $pngPath = Join-Path $file.DirectoryName ($file.BaseName + ".png")

        foreach ($outputPath in @($svgPath, $pngPath)) {
            Write-Host "Converting: $inputPath -> $outputPath" -ForegroundColor Green

            try {
                if ($outputPath -eq $pngPath) {
                    mmdc -i "$inputPath" -o "$outputPath" --scale 3
                } else {
                    mmdc -i "$inputPath" -o "$outputPath"
                }

                if ($LASTEXITCODE -eq 0) {
                    Write-Host "Successfully converted: $outputPath" -ForegroundColor Green
                    $convertedCount++
                }
                else {
                    Write-Host "Error converting: $inputPath (mmdc exit code: $LASTEXITCODE)" -ForegroundColor Red
                    $errorCount++
                }
            }
            catch {
                Write-Host "Error converting: $inputPath - $($_.Exception.Message)" -ForegroundColor Red
                $errorCount++
            }
        }
    }
}

# Summary
Write-Host "`nConversion Summary:" -ForegroundColor Cyan
Write-Host "  Converted: $convertedCount" -ForegroundColor Green
Write-Host "  Skipped: $skippedCount" -ForegroundColor Yellow
Write-Host "  Errors: $errorCount" -ForegroundColor Red

