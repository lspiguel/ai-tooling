# PowerShell script to convert office documents to .md using pandoc
# (.docx, .xlsx, .pptx) and .mmd files to .svg and .png (scale 3) using mmdc
# Markdown, SVG, and PNG output files are only converted if missing
# Embedded DOCX/PPTX images are extracted to .attachments/{doc-name}-imageN.{ext}
# in the same folder as the source file, with markdown links rewritten accordingly.

param(
    [string]$Path = ".",
    [string[]]$TextExtensions = @(".docx", ".xlsx", ".pptx"),
    [string[]]$DiagramExtensions = @(".mmd")
)

$missingTools = @()

if ($TextExtensions.Count -gt 0 -and -not (Get-Command pandoc -ErrorAction SilentlyContinue)) {
    $missingTools += "pandoc (required for office document conversion: $($TextExtensions -join ', '))"
}

if ($DiagramExtensions.Count -gt 0 -and -not (Get-Command mmdc -ErrorAction SilentlyContinue)) {
    $missingTools += "mmdc (required for diagram conversion: $($DiagramExtensions -join ', '))"
}

if ($missingTools.Count -gt 0) {
    Write-Host "Error: Required tools are not available on PATH:" -ForegroundColor Red
    foreach ($tool in $missingTools) {
        Write-Host "  - $tool" -ForegroundColor Red
    }
    Write-Host "`nInstall pandoc from https://pandoc.org/installing.html" -ForegroundColor Yellow
    Write-Host "Install mmdc with: npm install -g @mermaid-js/mermaid-cli" -ForegroundColor Yellow
    exit 1
}

function Get-SafeDocName {
    param([string]$Name)

    $safeName = $Name -replace '\s+', '-'
    return $safeName -replace '[\\/:*?"<>|]', ''
}

function Get-ImageSortKey {
    param([string]$FileName)

    if ($FileName -match '(?i)image(\d+)') {
        return [int]$matches[1]
    }

    return [int]::MaxValue
}

function Get-RelativePathFromDirectory {
    param(
        [string]$FromDirectory,
        [string]$ToFilePath
    )

    $from = [System.IO.Path]::GetFullPath($FromDirectory.TrimEnd('\', '/'))
    if (-not $from.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $from += [System.IO.Path]::DirectorySeparatorChar
    }

    $to = [System.IO.Path]::GetFullPath($ToFilePath)
    $fromUri = New-Object System.Uri($from)
    $toUri = New-Object System.Uri($to)
    $relative = $fromUri.MakeRelativeUri($toUri).ToString()

    return ($relative -replace '\\', '/')
}

function Move-ExtractedMediaImages {
    param(
        [string]$ExtractMediaRoot,
        [string]$AttachmentsDirectory,
        [string]$DocName,
        [string]$MarkdownDirectory
    )

    $imageExtensions = @('.png', '.jpg', '.jpeg', '.gif', '.bmp', '.tif', '.tiff', '.emf', '.wmf')
    $extractedImages = Get-ChildItem -Path $ExtractMediaRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $imageExtensions -contains $_.Extension.ToLower() } |
        Sort-Object { Get-ImageSortKey $_.Name }, FullName

    if (-not $extractedImages) {
        return @()
    }

    if (-not (Test-Path $AttachmentsDirectory)) {
        New-Item -ItemType Directory -Path $AttachmentsDirectory -Force | Out-Null
    }

    $imageMappings = @()
    $imageIndex = 1

    foreach ($image in $extractedImages) {
        $extension = $image.Extension.ToLower()
        if ($extension -eq '.jpeg') {
            $extension = '.jpg'
        }

        $attachmentName = "$DocName-image$imageIndex$extension"
        $attachmentPath = Join-Path $AttachmentsDirectory $attachmentName
        Copy-Item -Path $image.FullName -Destination $attachmentPath -Force

        $imageMappings += [pscustomobject]@{
            SourceFileName = $image.Name
            AttachmentRelativePath = Get-RelativePathFromDirectory -FromDirectory $MarkdownDirectory -ToFilePath $attachmentPath
        }

        $imageIndex++
    }

    return @($imageMappings)
}

function Get-PandocInputFormat {
    param([string]$Extension)

    switch ($Extension.ToLower()) {
        '.docx' { return 'docx' }
        '.xlsx' { return 'xlsx' }
        '.pptx' { return 'pptx' }
        default {
            throw "Unsupported text extension for pandoc conversion: $Extension"
        }
    }
}

function Convert-OfficeDocumentToMarkdown {
    param(
        [string]$InputPath,
        [string]$OutputPath,
        [string]$Extension,
        [string]$AttachmentsDirectory
    )

    $inputFormat = Get-PandocInputFormat -Extension $Extension
    $extractMedia = $inputFormat -in @('docx', 'pptx')
    $tempMediaRoot = $null

    try {
        if ($extractMedia) {
            $tempMediaRoot = Join-Path $env:TEMP ("pandoc-media-" + [guid]::NewGuid().ToString())
            New-Item -ItemType Directory -Path $tempMediaRoot -Force | Out-Null
            pandoc --from=$inputFormat --to=markdown --extract-media="$tempMediaRoot" "$InputPath" -o "$OutputPath"
        }
        else {
            pandoc --from=$inputFormat --to=markdown "$InputPath" -o "$OutputPath"
        }

        if ($LASTEXITCODE -ne 0) {
            return @{
                Success = $false
                ExitCode = $LASTEXITCODE
                ExtractedImageCount = 0
            }
        }

        $extractedImageCount = 0
        if ($extractMedia) {
            $safeDocName = Get-SafeDocName -Name ([System.IO.Path]::GetFileNameWithoutExtension($InputPath))
            $markdownDirectory = [System.IO.Path]::GetDirectoryName($OutputPath)
            $imageMappings = Move-ExtractedMediaImages `
                -ExtractMediaRoot $tempMediaRoot `
                -AttachmentsDirectory $AttachmentsDirectory `
                -DocName $safeDocName `
                -MarkdownDirectory $markdownDirectory

            Update-MarkdownImageReferences -MarkdownPath $OutputPath -ImageMappings $imageMappings
            $extractedImageCount = @($imageMappings).Count
        }

        return @{
            Success = $true
            ExitCode = 0
            ExtractedImageCount = $extractedImageCount
        }
    }
    finally {
        if ($tempMediaRoot -and (Test-Path $tempMediaRoot)) {
            Remove-Item -Path $tempMediaRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Update-MarkdownImageReferences {
    param(
        [string]$MarkdownPath,
        [array]$ImageMappings
    )

    if (-not $ImageMappings -or $ImageMappings.Count -eq 0) {
        return
    }

    $content = [System.IO.File]::ReadAllText($MarkdownPath)

    # Remove pandoc image dimension attributes that ADO Wiki does not render.
    $content = $content -replace '\)\{[^}]+\}', ')'

    foreach ($mapping in $ImageMappings) {
        $fileName = [regex]::Escape($mapping.SourceFileName)
        # Replace relative or absolute media paths ending with this filename.
        $content = [regex]::Replace(
            $content,
            '[^\s\(!\[]*[/\\]' + $fileName + '(?=\s*(?:\)|"|''|\{))',
            $mapping.AttachmentRelativePath)
    }

    # Remove pandoc image title attributes, e.g. (.attachments/foo.png "Picture 2")
    $content = $content -replace '\((\.attachments/[^)\s]+)\s+"[^"]*"\)', '($1)'

    [System.IO.File]::WriteAllText($MarkdownPath, $content)
}

# Combine all extensions to search for
$allExtensions = $TextExtensions + $DiagramExtensions
$rootPath = [System.IO.Path]::GetFullPath($Path)

# Get all files with specified extensions recursively
$files = Get-ChildItem -Path $rootPath -Recurse -File | Where-Object {
    $allExtensions -contains $_.Extension
}

$convertedCount = 0
$skippedCount = 0
$errorCount = 0
$extractedImageCount = 0

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

        $attachmentsDir = Join-Path $file.DirectoryName ".attachments"

        Write-Host "Converting: $inputPath -> $outputPath" -ForegroundColor Green

        try {
            $result = Convert-OfficeDocumentToMarkdown `
                -InputPath $inputPath `
                -OutputPath $outputPath `
                -Extension $extension `
                -AttachmentsDirectory $attachmentsDir

            if ($result.Success) {
                if ($result.ExtractedImageCount -gt 0) {
                    Write-Host "  Extracted $($result.ExtractedImageCount) image(s) to $attachmentsDir" -ForegroundColor Cyan
                    $extractedImageCount += $result.ExtractedImageCount
                }

                Write-Host "Successfully converted: $outputPath" -ForegroundColor Green
                $convertedCount++
            }
            else {
                Write-Host "Error converting: $inputPath (pandoc exit code: $($result.ExitCode))" -ForegroundColor Red
                $errorCount++
            }
        }
        catch {
            Write-Host "Error converting: $inputPath - $($_.Exception.Message)" -ForegroundColor Red
            $errorCount++
        }
    }
    elseif ($DiagramExtensions -contains $extension) {
        # Diagram files: convert to SVG and PNG using mmdc
        $svgPath = Join-Path $file.DirectoryName ($file.BaseName + ".svg")
        $pngPath = Join-Path $file.DirectoryName ($file.BaseName + ".png")

        foreach ($outputPath in @($svgPath, $pngPath)) {
            if (Test-Path $outputPath) {
                Write-Host "Skipping: $outputPath (file already exists)" -ForegroundColor Yellow
                $skippedCount++
                continue
            }

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
Write-Host "  Images extracted: $extractedImageCount" -ForegroundColor Cyan
Write-Host "  Errors: $errorCount" -ForegroundColor Red
