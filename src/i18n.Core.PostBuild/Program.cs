using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using CommandLine;
using i18n.Core.Abstractions.Domain;
using i18n.Core.Abstractions.Domain.Entities;
using JetBrains.Annotations;

namespace i18n.Core.PostBuild
{
    [UsedImplicitly]
    internal class Options
    {
        [Option("show-source-context", Required = false, HelpText = "Append source context to references")]
        public bool ShowSourceContext { get; set; }

        [Option('w', "web-config-path", Required = false, HelpText = "Path to web.config that contain i18n.* settings.")]
        public string WebConfigPath { get; set; }
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Environment.ExitCode = 1;

            Parser.Default
                .ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    try
                    {
                        Run(options);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("ERROR: {0}", exception.Message);
                        if (exception.InnerException == null)
                        {
                            return;
                        }
                        while (exception.InnerException != null)
                        {
                            exception = exception.InnerException;
                        }
                        Console.WriteLine("Error (InnerException): {0}", exception.Message);
                    }
                });
        }

        static void Run(Options options)
        {
            ReferenceContext.ShowSourceContext = options.ShowSourceContext;

            var webConfigFilename = options.WebConfigPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Web.config");
            if (!File.Exists(webConfigFilename))
            {
                Console.Error.WriteLine($"Unable to find web.config: {webConfigFilename}");
                return;
            }

            var sw = new Stopwatch();
            sw.Restart();

            var settingsProvider = new SettingsProvider(webConfigFilename);
            var settings = new I18NSettings(settingsProvider);
            var repository = new PoTranslationRepository(settings);
            var nuggetFinder = new FileNuggetFinder(settings);

            var items = nuggetFinder.ParseAll();
            if (repository.SaveTemplate(items))
            {
                var merger = new TranslationMerger(repository);
                merger.MergeAllTranslation(items);
            }

            sw.Stop();

            Console.WriteLine($"i18n.PostBuild completed successfully. Operation completed in {sw.Elapsed.TotalSeconds:F} seconds.");
        }
    }
}
