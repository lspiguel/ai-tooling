// <copyright file="D365ContextExporterPlugin.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter
{
    using System;
    using System.ComponentModel.Composition;
    using System.IO;
    using System.Reflection;
    using XrmToolBox.Extensibility;
    using XrmToolBox.Extensibility.Interfaces;

    /// <summary>MEF export that registers this assembly as an XrmToolBox plugin.</summary>
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "D365 CE Context Exporter")]
    [ExportMetadata("Description", "Executes FetchXML and Web API queries against Dataverse and produces Markdown grounding files for AI assistants.")]
    [ExportMetadata("SmallImageBase64", "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAACEElEQVR4AZyPTWgTURCA573d/HW17G5XJSpRQURT1INWxIMoCF5E8ODFk6ceREpz0FQocUVRC4Je1R6iFw+1BwURMQURDKKIJy0EJJFWtGaTbkraxmx2xzfbH9KGQtvHfjtvZ958O4/DOhYCMuzGwFgCI2sWUDOYIFkqhEPh2uE1C0QzKwDIIM9scBm/yGEdKwTAHcaDzGMHlwh+vnfv//riFSe+4fVmb//R4on+YyUkUm/L7sPM5PSTD7OFnFXf7wuoQYBj76q92eQfo/Biqnf8kzspcr4oqEhDl56qcO2NDn2vdbj6SoOeYQ12Xw4H+e9R3Cn+Zn68acGP4Qr8++tA/llZ/ZocV/k0mvbzRl7U/SfzqASZx4LBEoykS9Aek4BTpT7lVSujNZARBZ4AoSFEGGQw891xImFm0Dlta4CCD4q3XXTmBGIP3HNBQs+Hi0hQXmpjCkXi0Jl2ONXdsQgJ/QmYBA0jHhLN6EOTtBkysDpCaIecm62hRYKBs3no68oBXYW+CR7dxwqSwhMHrmy24j1GVROi2AXdjt+L2Z7CTPW8fBLmV/LlLrj7eY8/wXxq7grRTpZmCu/auDcyGDe3Q/Sc/mDbEVnb0sluLBxcKfpXoCJNEjseSIgmJljSGFa4VZlw6VgLi4KWSlPCthoDQ6mqded0GZazKsHtrJFOjeibbmU72HJWJWgapmX7HwAA///TGYi4AAAABklEQVQDAKnZ2CEB0WwXAAAAAElFTkSuQmCC")]
    [ExportMetadata("BigImageBase64", "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")]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public sealed class D365ContextExporterPlugin : PluginBase
    {
        // Subfolder next to the plugin DLL that holds private dependencies (e.g. Scriban, System.Text.Json).
        // This isolates our versions from other plugins that may ship conflicting versions of the same assemblies.
        private static readonly string PrivateLibDir = Path.Combine(
            Paths.PluginsPath,
            "D365ContextExporter");

        /// <summary>
        /// Initializes a new instance of the <see cref="D365ContextExporterPlugin"/> class.
        /// </summary>
        public D365ContextExporterPlugin()
        {
            // Handle the AppDomain's AssemblyResolve event to load dependencies from our private lib folder.
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        /// <inheritdoc/>
        public override IXrmToolBoxPluginControl GetControl() => new ContextExporterPluginControl();

        private static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name.Split(',')[0];
            var path = Path.Combine(PrivateLibDir, $"{name}.dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }
    }
}
