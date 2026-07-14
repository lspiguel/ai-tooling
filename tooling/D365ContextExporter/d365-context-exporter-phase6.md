# Phase 6 — Scriban Source Embedding + First-Run Welcome Popup

## Context

Phase 5 completed the migration from Python/Jinja2 to .NET-native Scriban rendering. Scriban currently ships as a separate `Scriban.dll` in the plugin's private-lib subfolder and is loaded at runtime via the `AppDomain.AssemblyResolve` handler in `D365ContextExporterPlugin.cs`. Phase 6 eliminates that DLL by compiling Scriban source directly into the plugin assembly (source embedding), and adds a one-time welcome dialog that shows the Quick Start guide to new users.

---

## Task 1 — Embed Scriban via Source Embedding

### Why

Source embedding compiles Scriban's `.cs` files into the plugin DLL at build time. No `Scriban.dll` is emitted, no separate file needs to be shipped or resolved at runtime. All Scriban types become `internal` — since `TemplateRenderer.cs` and `TemplateFilters.cs` are both `internal sealed` and never expose Scriban types in their public signatures, this is transparent.

The `AssemblyResolve` handler is kept intact. After the change, a probe for `Scriban.dll` will find no file and return `null` (the "not found" signal) — harmless. The handler continues to serve `System.Text.Json.dll` and `System.Text.Encodings.Web.dll`.

### `D365ContextExporter.csproj`

1. **PropertyGroup** — add inside the first `<PropertyGroup>` block:
   ```xml
   <PackageScribanIncludeSource>true</PackageScribanIncludeSource>
   ```

2. **Scriban reference** — change `IncludeAssets` so the DLL is not emitted:
   ```xml
   <!-- before -->
   <PackageReference Include="Scriban" Version="7.2.0" />
   <!-- after -->
   <PackageReference Include="Scriban" Version="7.2.0" IncludeAssets="Build" />
   ```

3. **Polyfill packages** — required for .NET Framework 4.8 source embedding (add alongside existing PackageReferences):
   ```xml
   <PackageReference Include="Microsoft.CSharp" Version="4.7.0" />
   <PackageReference Include="System.Threading.Tasks.Extensions" Version="4.6.3" />
   <PackageReference Include="PolySharp" Version="1.15.0">
     <PrivateAssets>all</PrivateAssets>
     <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
   </PackageReference>
   ```
   `PolySharp` is source-only (same asset pattern as StyleCop already in the file). `Microsoft.CSharp` and `System.Threading.Tasks.Extensions` remain as small runtime DLLs; the AssemblyResolve handler will pick them up if needed.

4. **New form and resource entries** (see Task 2 below for the actual files):
   ```xml
   <!-- in the Compile Update ItemGroup -->
   <Compile Update="UI\WelcomeForm.Designer.cs">
     <DependentUpon>WelcomeForm.cs</DependentUpon>
   </Compile>
   <!-- in the EmbeddedResource Update ItemGroup -->
   <EmbeddedResource Update="UI\WelcomeForm.resx">
     <DependentUpon>WelcomeForm.cs</DependentUpon>
   </EmbeddedResource>
   <!-- new ItemGroup for the HTML resource -->
   <ItemGroup>
     <EmbeddedResource Include="Resources\WelcomeQuickStart.html" />
   </ItemGroup>
   ```

### `D365ContextExporter.nuspec`

Remove the `Scriban.dll` file entry (it no longer exists in the build output). Update the comment. The other two DLL entries stay:

```xml
<!-- before -->
<!-- Scriban template engine dependencies (NuGet dependency, must ship with plugin) -->
<file src="bin\Release\net48\Scriban.dll" target="lib\net472\Plugins\D365ContextExporter" />
<file src="bin\Release\net48\System.Text.Encodings.Web.dll" target="lib\net472\Plugins\D365ContextExporter" />
<file src="bin\Release\net48\System.Text.Json.dll" target="lib\net472\Plugins\D365ContextExporter" />

<!-- after -->
<!-- Runtime dependencies (Scriban is now source-embedded into the plugin DLL) -->
<file src="bin\Release\net48\System.Text.Encodings.Web.dll" target="lib\net472\Plugins\D365ContextExporter" />
<file src="bin\Release\net48\System.Text.Json.dll" target="lib\net472\Plugins\D365ContextExporter" />
```

### `Directory.Build.targets`

Remove `$(TargetDir)Scriban.dll;` from the `PrivateDeps` ItemGroup and update the comment:

```xml
<!-- before -->
<PrivateDeps Include="$(TargetDir)Scriban.dll;
                      $(TargetDir)System.Text.Json.dll;
                      $(TargetDir)System.Text.Encodings.Web.dll" />

<!-- after -->
<PrivateDeps Include="$(TargetDir)System.Text.Json.dll;
                      $(TargetDir)System.Text.Encodings.Web.dll" />
```

Also remove `Scriban.dll` from the deployment layout comment block at the top of the file.

---

## Task 2 — First-Run Welcome Popup

### Detection strategy

Add `WelcomeShown` (bool, User scope, default `False`) to `Properties/Settings.settings`. The flag flips to `true` on first load and is never reset — no version comparison, no re-trigger on upgrade.

### New files

#### `Resources/WelcomeQuickStart.html` (EmbeddedResource)

HTML file containing the Quick Start guide from the README, with minimal CSS styling. Key requirement: include `<meta http-equiv="X-UA-Compatible" content="IE=edge" />` so the `WebBrowser` control renders in IE11 mode rather than IE7 quirks.

Content structure:
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta http-equiv="X-UA-Compatible" content="IE=edge" />
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; font-size: 13px; margin: 16px 20px; }
    h2  { font-size: 15px; margin-top: 0; color: #0078d4; }
    ol  { padding-left: 20px; line-height: 1.7; }
    code { background: #f3f3f3; border: 1px solid #ddd; border-radius: 2px; padding: 0 3px;
           font-family: Consolas, monospace; font-size: 12px; }
    .footer { margin-top: 14px; font-size: 11px; color: #666; }
  </style>
</head>
<body>
  <h2>Quick Start — D365 CE Context Exporter</h2>
  <ol>
    <li><strong>Install the plugin</strong> — open XrmToolBox, go to <em>Tool Library</em>, search for <em>D365 CE Context Exporter</em>, and install.</li>
    <li><strong>Connect</strong> — connect XrmToolBox to your Dataverse environment.</li>
    <li><strong>Pick a base directory</strong> — click the folder picker and choose (or create) an empty folder. On first use the plugin will offer to deploy the reference configuration there.</li>
    <li><strong>Run first-time setup</strong> — accept the prompt to deploy sample specs, FetchXML queries, and Scriban templates to the folder.</li>
    <li><strong>Select a spec</strong> — choose one of the available specs from the dropdown (six sample specs are provided out of the box).</li>
    <li><strong>Click Run</strong> — the plugin executes all queries, renders the template, and places the output in <code>output\&lt;SpecName&gt;.context.md</code>.</li>
    <li><strong>Upload</strong> — attach or paste the <code>.context.md</code> file into your AI assistant conversation as grounding context.</li>
  </ol>
  <p class="footer">This message is shown once. Find the full documentation in the plugin README.</p>
</body>
</html>
```

Resource name (auto-derived from project namespace + folder + filename):
`Lspiguel.Xrm.D365ContextExporter.Resources.WelcomeQuickStart.html`

#### `UI/WelcomeForm.cs`

`internal sealed class WelcomeForm : Form` with:
- Constructor calls `InitializeComponent()` then `LoadHtml()`
- `LoadHtml()` reads the embedded HTML via `Assembly.GetManifestResourceStream(ResourceName)` and sets `webBrowser.DocumentText`
- `btnClose_Click` calls `this.Close()`

```csharp
// <copyright file="WelcomeForm.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Windows.Forms;

    /// <summary>Modal welcome dialog that displays the Quick Start guide on first plugin load.</summary>
    internal sealed class WelcomeForm : Form
    {
        private const string ResourceName =
            "Lspiguel.Xrm.D365ContextExporter.Resources.WelcomeQuickStart.html";

        /// <summary>
        /// Initializes a new instance of the <see cref="WelcomeForm"/> class.
        /// </summary>
        public WelcomeForm()
        {
            this.InitializeComponent();
            this.LoadHtml();
        }

        private void LoadHtml()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                this.webBrowser.DocumentText = "<p>Welcome to D365 CE Context Exporter!</p>";
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            this.webBrowser.DocumentText = reader.ReadToEnd();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
```

#### `UI/WelcomeForm.Designer.cs`

WinForms designer file defining:
- `WebBrowser webBrowser` — anchored Top/Bottom/Left/Right, `ScrollBarsEnabled = false`, fills top portion (540×310)
- `Button btnClose` — anchored Bottom/Right, labelled "Close"
- Form: `FixedDialog`, `ClientSize(540, 359)`, `CenterParent`, no maximize/minimize box

#### `UI/WelcomeForm.resx`

Standard empty ResX 2.0 shell (identical structure to existing `.resx` files in `UI/`). No data entries.

### `Properties/Settings.settings`

Add after the existing `BaseDirectory` setting:
```xml
<Setting Name="WelcomeShown" Type="System.Boolean" Scope="User">
  <Value Profile="(Default)">False</Value>
</Setting>
```

Also update the generated `Properties/Settings.Designer.cs`:
```csharp
[global::System.Configuration.UserScopedSettingAttribute()]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Configuration.DefaultSettingValueAttribute("False")]
public bool WelcomeShown
{
    get { return ((bool)(this["WelcomeShown"])); }
    set { this["WelcomeShown"] = value; }
}
```

### `ContextExporterPluginControl.cs`

Add two `using` statements:
```csharp
using Lspiguel.Xrm.D365ContextExporter.Properties;
using Lspiguel.Xrm.D365ContextExporter.UI;
```

Modify `ContextExporterPluginControl_Load` — call `ShowWelcomeIfFirstLoad()` as the **first statement** (before `dirPicker.LoadSettings()`, so the popup appears before any directory-based prompts):
```csharp
private void ContextExporterPluginControl_Load(object sender, EventArgs e)
{
    this.ShowWelcomeIfFirstLoad();   // new
    this.dirPicker.LoadSettings();
    // ... rest unchanged
}
```

Add private helper:
```csharp
private void ShowWelcomeIfFirstLoad()
{
    if (Settings.Default.WelcomeShown)
    {
        return;
    }

    // Flip before showing — prevents re-trigger if ShowDialog throws.
    Settings.Default.WelcomeShown = true;
    Settings.Default.Save();

    using var form = new WelcomeForm();
    form.ShowDialog(this);
}
```

---

## Files to Create or Modify

| Action     | File |
|------------|------|
| Modify     | `D365ContextExporter/D365ContextExporter.csproj` |
| Modify     | `D365ContextExporter/D365ContextExporter.nuspec` |
| Modify     | `D365ContextExporter/Directory.Build.targets` |
| Modify     | `D365ContextExporter/Properties/Settings.settings` |
| Modify     | `D365ContextExporter/Properties/Settings.Designer.cs` |
| Modify     | `D365ContextExporter/ContextExporterPluginControl.cs` |
| **Create** | `D365ContextExporter/Resources/WelcomeQuickStart.html` |
| **Create** | `D365ContextExporter/UI/WelcomeForm.cs` |
| **Create** | `D365ContextExporter/UI/WelcomeForm.Designer.cs` |
| **Create** | `D365ContextExporter/UI/WelcomeForm.resx` |

---

## Verification

1. **Build (Debug)** — confirm `bin\Debug\net48\Scriban.dll` is absent; `System.Text.Json.dll` and `System.Text.Encodings.Web.dll` are present.

2. **Unit tests** — run `D365ContextExporter.Tests`. All tests should pass; Scriban types are still available at the same names, only accessibility changed (already internal consumers, no impact).

3. **Local deploy** — `CopyToXrmToolBox` target deploys to XrmToolBox Plugins dir. Confirm `Plugins\D365ContextExporter\` contains only the two remaining DLLs (no `Scriban.dll`).

4. **First-run popup** — delete or rename `%LOCALAPPDATA%\Lspiguel\...\user.config` (or reset `WelcomeShown` to `False`). Open XrmToolBox → plugin. Welcome dialog appears, Quick Start HTML renders correctly, clicking Close dismisses it. Re-opening the plugin does not show the dialog again.

5. **nuget pack smoke test** — inspect the `.nupkg`; `lib\net472\Plugins\D365ContextExporter\` must contain `System.Text.Json.dll` + `System.Text.Encodings.Web.dll` only.

6. **Template rendering regression** — run a full export (e.g. EntityDictionary spec). Confirm `output\EntityDictionary.context.md` is produced correctly.
