using System;
using System.IO;
using System.Text;

using Lspiguel.Xrm.D365ContextExporter.Models;
using Lspiguel.Xrm.D365ContextExporter.Orchestration;

namespace Lspiguel.Xrm.D365ContextExporter.Tests.OrchestrationTests
{
    /// <summary>
    /// A temporary base directory with <c>config\transformations\</c> and one run folder, for rendering
    /// templates through <see cref="TemplateRenderer"/> without a connection.
    /// </summary>
    internal sealed class TemplateHarness : IDisposable
    {
        public const string SpecName = "HarnessSpec";
        public const string TemplateName = "main.sbn";

        public TemplateHarness()
        {
            BaseDir = Path.Combine(Path.GetTempPath(), "TemplateTests_" + Guid.NewGuid().ToString("N"));
            TransformationsDir = Path.Combine(BaseDir, "config", "transformations");
            RunDir = Path.Combine(BaseDir, "runs", "test-run");
            Directory.CreateDirectory(TransformationsDir);
            Directory.CreateDirectory(RunDir);
        }

        public string BaseDir { get; }

        public string TransformationsDir { get; }

        public string RunDir { get; }

        public string OutputPath => Path.Combine(RunDir, "output.md");

        /// <summary>Writes <paramref name="template"/> as the main template and <paramref name="intermediateJson"/> as the run's intermediate, renders, and returns output.md.</summary>
        public string Render(string template, string intermediateJson = "{}")
        {
            WriteTemplate(TemplateName, template);
            File.WriteAllText(Path.Combine(RunDir, "intermediate.json"), intermediateJson);
            new TemplateRenderer(_ => { }).Render(Job(), BaseDir, RunDir);
            return File.ReadAllText(OutputPath);
        }

        public void WriteTemplate(string name, string content) =>
            File.WriteAllText(Path.Combine(TransformationsDir, name), content, new UTF8Encoding(false));

        public ExportJob Job(string transformation = TemplateName) =>
            new ExportJob { Spec = SpecName, Transformation = transformation };

        public void Dispose()
        {
            if (Directory.Exists(BaseDir))
                Directory.Delete(BaseDir, recursive: true);
        }
    }
}
