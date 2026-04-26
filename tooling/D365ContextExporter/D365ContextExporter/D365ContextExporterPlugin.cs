using System.ComponentModel.Composition;

using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace D365ContextExporter
{
    /// <summary>MEF export that registers this assembly as an XrmToolBox plugin.</summary>
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "D365 CE Context Exporter")]
    [ExportMetadata("Description", "Executes FetchXML and Web API queries against Dataverse and produces Markdown grounding files for AI assistants.")]
    [ExportMetadata("SmallImageBase64", "")]
    [ExportMetadata("BigImageBase64", "")]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public sealed class D365ContextExporterPlugin : PluginBase
    {
        /// <inheritdoc/>
        public override IXrmToolBoxPluginControl GetControl() => new ContextExporterPluginControl();
    }
}
