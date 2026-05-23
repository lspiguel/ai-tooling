// <copyright file="TemplateRenderer.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365ContextExporter.Orchestration
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Lspiguel.Xrm.D365ContextExporter.Helpers;
    using Lspiguel.Xrm.D365ContextExporter.Models;

    using Newtonsoft.Json.Linq;

    using Scriban;
    using Scriban.Parsing;
    using Scriban.Runtime;

    /// <summary>In-process Scriban template renderer. Replaces <c>PythonInvoker</c>.</summary>
    internal sealed class TemplateRenderer
    {
        private readonly Action<string> log;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateRenderer"/> class.
        /// </summary>
        /// <param name="log">Log delegate; called for each log line.</param>
        public TemplateRenderer(Action<string> log)
        {
            this.log = log;
        }

        /// <summary>Renders the Scriban template for the given job and writes <c>output.md</c> to <paramref name="runDir"/>.</summary>
        /// <param name="job">The loaded spec configuration.</param>
        /// <param name="baseDir">The base directory containing <c>config\transformations\</c>.</param>
        /// <param name="runDir">The run directory where <c>intermediate.json</c> is located and <c>output.md</c> will be written.</param>
        /// <exception cref="FileNotFoundException">Thrown when the template file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown on template parse or render errors.</exception>
        public void Render(ExportJob job, string baseDir, string runDir)
        {
            var transformationsDir = Path.Combine(baseDir, "config", "transformations");
            var templatePath = Path.Combine(transformationsDir, job.Transformation);

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    $"Template file not found: '{templatePath}'. Ensure the transformation file exists in config\\transformations\\.",
                    templatePath);
            }

            var templateText = File.ReadAllText(templatePath, Encoding.UTF8);
            var template = Template.Parse(templateText, templatePath);

            if (template.HasErrors)
            {
                var errors = string.Join(
                    Environment.NewLine,
                    template.Messages.Select(m =>
                        $"{m.Span.FileName}({m.Span.Start.Line},{m.Span.Start.Column}): {m.Message}"));
                throw new InvalidOperationException(
                    $"Template parse errors in '{job.Transformation}':{Environment.NewLine}{errors}");
            }

            var intermediatePath = Path.Combine(runDir, "intermediate.json");
            var json = JToken.Parse(File.ReadAllText(intermediatePath, Encoding.UTF8));

            var scriptObject = (ScriptObject)ConvertJToken(json)!;
            scriptObject["_spec"] = job.Spec;

            TemplateFilters.RegisterAll(scriptObject);

            var context = new TemplateContext { StrictVariables = true, LoopLimit = int.MaxValue, LimitToString = 0 };
            context.TemplateLoader = new FileSystemTemplateLoader(transformationsDir);
            context.PushGlobal(scriptObject);

            string rendered;
            try
            {
                rendered = template.Render(context);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Template render error in '{job.Transformation}': {ex.Message}", ex);
            }

            var outputPath = Path.Combine(runDir, "output.md");
            File.WriteAllText(outputPath, rendered, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            this.log($"[Render] output.md written ({new FileInfo(outputPath).Length} bytes)");
        }

        private static object? ConvertJToken(JToken token)
        {
            return token switch
            {
                JObject obj => ConvertJObject(obj),
                JArray arr => ConvertJArray(arr),
                JValue val => val.Value,
                _ => null,
            };
        }

        private static ScriptObject ConvertJObject(JObject obj)
        {
            var so = new ScriptObject();
            foreach (var prop in obj.Properties())
            {
                so[prop.Name] = ConvertJToken(prop.Value);
            }

            return so;
        }

        private static ScriptArray ConvertJArray(JArray arr)
        {
            var sa = new ScriptArray();
            foreach (var item in arr)
            {
                sa.Add(ConvertJToken(item));
            }

            return sa;
        }

        private sealed class FileSystemTemplateLoader : ITemplateLoader
        {
            private readonly string baseDir;

            public FileSystemTemplateLoader(string baseDir)
            {
                this.baseDir = baseDir;
            }

            public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
                => Path.Combine(this.baseDir, templateName);

            public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
                => File.ReadAllText(templatePath, Encoding.UTF8);

            public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
                => new ValueTask<string>(this.Load(context, callerSpan, templatePath));
        }
    }
}
