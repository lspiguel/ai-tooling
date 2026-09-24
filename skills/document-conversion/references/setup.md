# Setup — tools the conversion scripts need

Run the check first; it tells you what is missing for the conversions you need and prints the install command:

```powershell
.\scripts\Test-ConversionPrerequisites.ps1                       # everything
.\scripts\Test-ConversionPrerequisites.ps1 -Conversion md-to-pdf  # one conversion
.\scripts\Test-ConversionPrerequisites.ps1 -Install              # install what is missing (ask the user first)
```

Ask the user before installing anything. Installs change their machine, may need administrator rights, and may be governed by company policy.

## What each tool is for

| Tool | Used by | Chocolatey (admin shell) | winget (normal shell) | Check |
|---|---|---|---|---|
| **Python** 3.10+ | Every `.py` script | `choco install python -y` | `winget install --id Python.Python.3.12 -e` | `python --version` |
| **pandoc** 3.1+ | Imports, DOCX/HTML/PDF/PPTX exports | `choco install pandoc -y` | `winget install --id JohnMacFarlane.Pandoc -e` | `pandoc --version` |
| **Node.js** | Needed by mmdc | `choco install nodejs-lts -y` | `winget install --id OpenJS.NodeJS.LTS -e` | `node --version` |
| **mmdc** (mermaid-cli) | Any mermaid diagram | `npm install -g @mermaid-js/mermaid-cli` | same | `mmdc --version` |
| **Chrome or Edge** | SVG → PNG, HTML/Markdown → PDF, HTML → PNG | `choco install googlechrome -y` | `winget install --id Google.Chrome -e` | Edge ships with Windows |
| **ImageMagick** | SVG icons (64 px or smaller) → PNG | `choco install imagemagick -y` | `winget install --id ImageMagick.ImageMagick -e` | `magick -version` |
| **poppler** (`pdftotext`) | PDF import | `choco install poppler -y` | `winget install --id oschwartz10612.Poppler -e` | `pdftotext -v` |
| **git** | DOCX export reuses the committed DOCX's styles | `choco install git -y` | `winget install --id Git.Git -e` | `git --version` |
| **openpyxl** | XLSX ↔ CSV | `python -m pip install openpyxl` | same | `python -c "import openpyxl"` |

Chocolatey is preferred because it installs system-wide, updates with `choco upgrade all`, and scripts cleanly. It needs an administrator shell. Where that is not available, winget installs per user from a normal shell. Neither needs to be used exclusively.

Only install what the work needs. A team that only publishes markdown to DOCX needs Python, pandoc, and mmdc if it uses mermaid. The check script's `-Conversion` switch narrows the list.

## After installing

**Open a new shell.** Installers change PATH for new processes only. In a Chocolatey admin shell, `refreshenv` updates the current one.

Then run the check again.

## Common problems

| Symptom | Cause and fix |
|---|---|
| A tool is found in Git Bash but reported missing in PowerShell | Git for Windows bundles some tools (`pdftotext` among them) on its own PATH. Install the tool properly so every shell, and the agent, finds it |
| `python` opens the Microsoft Store | The Store alias is shadowing a real install. Install Python, then turn off the `python.exe` alias under *Settings → Apps → Advanced app settings → App execution aliases* |
| `npm install -g @mermaid-js/mermaid-cli` fails downloading Chromium | Proxy or blocked download. Install with `PUPPETEER_SKIP_DOWNLOAD=1` set, then set `PUPPETEER_EXECUTABLE_PATH` to an installed `chrome.exe` or `msedge.exe` (user environment variable, so every shell sees it) |
| `mmdc` works in one shell and not another | npm's global folder (`%APPDATA%\npm`) is not on PATH in that shell. Open a new shell after installing Node.js |
| PDF or PNG export: "the browser produced no output" | The browser is blocked by policy, or is somewhere the scripts do not look. Set `DOC_CONVERSION_BROWSER` to the full path of `chrome.exe` or `msedge.exe` |
| `pip install` refuses: "externally managed environment" | Linux/macOS system Python. Use a virtual environment: `python -m venv .venv`, activate it, then `pip install openpyxl` |
| pandoc cannot read `.pptx` or `.xlsx` | pandoc is older than 3.1. `choco upgrade pandoc -y` |

## macOS and Linux

The Python scripts run as-is. `export-html-to-png.ps1` and `Test-ConversionPrerequisites.ps1` are Windows scripts. Use `export_html_to_pdf.py` or a browser screenshot instead of the former, and the table above for the latter.

| Tool | macOS (Homebrew) | Debian / Ubuntu |
|---|---|---|
| Python, pandoc, poppler, ImageMagick, git | `brew install python pandoc poppler imagemagick git` | `sudo apt install python3 python3-venv pandoc poppler-utils imagemagick git` (check that `pandoc --version` is 3.1+; if it isn't, install from pandoc.org) |
| Node.js, mmdc | `brew install node && npm install -g @mermaid-js/mermaid-cli` | `sudo apt install nodejs npm && sudo npm install -g @mermaid-js/mermaid-cli` |
| Chrome | Install from google.com/chrome | `sudo apt install chromium` |
