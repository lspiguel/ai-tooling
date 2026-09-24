<#
.SYNOPSIS
    Renders a local HTML file to a PNG using headless Chrome or Edge.

.DESCRIPTION
    Screenshots the page at a fixed viewport, then crops the result to the
    bounding box of everything that is not page background — so the output is
    the card itself, with no surrounding margin. Used for the social-card
    versions of the repository's infographics.

    Requires Google Chrome or Microsoft Edge; no other dependency.

.PARAMETER Path
    One or more HTML files to render. Accepts pipeline input and wildcards.

.PARAMETER OutputPath
    Destination PNG. Defaults to the input file's name with a .png extension.
    Only valid when a single input file is given.

.PARAMETER Scale
    Device scale factor. 2 (the default) renders at twice CSS pixel size, which
    is what social platforms want.

.PARAMETER Width
    Viewport width in CSS pixels. Must exceed the card width plus its margins.

.PARAMETER MaxHeight
    Viewport height in CSS pixels. Content taller than this is cut off; the
    script warns when the crop reaches the bottom edge.

.PARAMETER NoCrop
    Keep the full viewport instead of cropping to the content.

.EXAMPLE
    .\export-html-to-png.ps1 -Path ..\workflows\matrix-simple.html

.EXAMPLE
    Get-ChildItem ..\workflows\*-simple.html | .\export-html-to-png.ps1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
    [Alias('FullName')]
    [string[]]$Path,

    [string]$OutputPath,

    [ValidateRange(1, 4)]
    [int]$Scale = 2,

    [ValidateRange(320, 4000)]
    [int]$Width = 760,

    [ValidateRange(320, 16000)]
    [int]$MaxHeight = 4000,

    [switch]$NoCrop
)

begin {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    Add-Type -AssemblyName System.Drawing

    function Get-Browser {
        $candidates = @(
            "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
            "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
            "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe",
            "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
            "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
        )
        foreach ($c in $candidates) {
            if ($c -and (Test-Path -LiteralPath $c)) { return $c }
        }
        throw 'Neither Chrome nor Edge was found. Install one, or render the HTML manually.'
    }

    # Returns the bounding box of every pixel that differs from the corner
    # pixel, which is taken to be the page background.
    function Get-ContentBounds {
        param([System.Drawing.Bitmap]$Bitmap)

        $rect = New-Object System.Drawing.Rectangle 0, 0, $Bitmap.Width, $Bitmap.Height
        $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $bytes = New-Object byte[] ($data.Stride * $Bitmap.Height)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

            $b0 = $bytes[0]; $g0 = $bytes[1]; $r0 = $bytes[2]
            $minX = $Bitmap.Width; $minY = $Bitmap.Height; $maxX = -1; $maxY = -1

            for ($y = 0; $y -lt $Bitmap.Height; $y++) {
                $row = $y * $data.Stride
                for ($x = 0; $x -lt $Bitmap.Width; $x++) {
                    $i = $row + ($x * 4)
                    if ($bytes[$i] -ne $b0 -or $bytes[$i + 1] -ne $g0 -or $bytes[$i + 2] -ne $r0) {
                        if ($x -lt $minX) { $minX = $x }
                        if ($x -gt $maxX) { $maxX = $x }
                        if ($y -lt $minY) { $minY = $y }
                        if ($y -gt $maxY) { $maxY = $y }
                    }
                }
            }
        } finally {
            $Bitmap.UnlockBits($data)
        }

        if ($maxX -lt 0) { return $null }
        New-Object System.Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
    }

    $browser = Get-Browser
    Write-Verbose "Rendering with $browser"
    $inputs = @()
}

process {
    foreach ($p in $Path) {
        $inputs += (Resolve-Path -Path $p).Path
    }
}

end {
    if ($OutputPath -and $inputs.Count -gt 1) {
        throw '-OutputPath can only be used with a single input file.'
    }

    foreach ($html in $inputs) {
        $target = if ($OutputPath) { $OutputPath } else { [System.IO.Path]::ChangeExtension($html, '.png') }
        $shot    = Join-Path ([System.IO.Path]::GetTempPath()) ("html2png-{0}.png" -f [guid]::NewGuid())
        $profile = Join-Path ([System.IO.Path]::GetTempPath()) ("html2png-profile-{0}" -f [guid]::NewGuid())

        $url = ([uri]$html).AbsoluteUri
        $args = @(
            '--headless=new'
            '--disable-gpu'
            '--hide-scrollbars'
            '--no-first-run'
            '--allow-file-access-from-files'
            '--virtual-time-budget=5000'
            "--force-device-scale-factor=$Scale"
            "--window-size=$Width,$MaxHeight"
            "--user-data-dir=$profile"
            "--screenshot=$shot"
            $url
        )

        try {
            # Chrome reports progress on stderr; redirecting it here would make
            # Windows PowerShell treat a successful run as a failure.
            & $browser @args | Out-Null
            if (-not (Test-Path -LiteralPath $shot)) {
                throw "The browser produced no screenshot for $html."
            }

            $src = [System.Drawing.Image]::FromFile($shot)
            try {
                if ($NoCrop) {
                    $src.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
                } else {
                    $bounds = Get-ContentBounds -Bitmap $src
                    if (-not $bounds) { throw "The page rendered blank: $html" }
                    if ($bounds.Bottom -ge $src.Height) {
                        Write-Warning "Content reached the bottom of the viewport for $html - raise -MaxHeight (currently $MaxHeight)."
                    }
                    $cropped = $src.Clone($bounds, $src.PixelFormat)
                    try {
                        $cropped.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
                    } finally {
                        $cropped.Dispose()
                    }
                }
                $written = [System.Drawing.Image]::FromFile($target)
                $size = "$($written.Width) x $($written.Height)"
                $written.Dispose()
                Write-Host "$target  ($size)"
            } finally {
                $src.Dispose()
            }
        } finally {
            Remove-Item -LiteralPath $shot -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
