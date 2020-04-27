using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using CommandLine;
using i18n.Core;
using i18n.Core.Abstractions.Domain;
using i18n.Core.Pot;
using i18n.Core.Pot.Entities;
using JetBrains.Annotations;

namespace pot
{
    [UsedImplicitly]
    internal class Options
    {
        [Option("show-source-context", Required = false, HelpText = "Append source context to references")]
        public bool ShowSourceContext { get; [UsedImplicitly] set; }

        [Option('w', "web-config-path", Required = false, HelpText = "Path to web.config that contain i18n.* settings.")]
        public string WebConfigPath { get; [UsedImplicitly] set; }

        [Option("verbose", Required = false, HelpText = "Set output to verbose.")]
        public bool Verbose { get; [UsedImplicitly] set; }
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
                        Environment.ExitCode = Run(options);
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

        static int Run(Options options)
        {
            ReferenceContext.ShowSourceContext = options.ShowSourceContext;

            string projectDirectory;
            string webConfigFilename;

            if (options.WebConfigPath != null)
            {
                if (options.WebConfigPath.LastIndexOf("Web.config", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    projectDirectory = Path.GetFullPath(options.WebConfigPath);
                    webConfigFilename = Path.Combine(projectDirectory, "Web.config");
                }
                else
                {
                    projectDirectory = Path.GetFullPath(Path.GetDirectoryName(options.WebConfigPath)!);
                    webConfigFilename = options.WebConfigPath;
                }
            }
            else
            {
                projectDirectory = Directory.GetCurrentDirectory();
                webConfigFilename = Path.Combine(projectDirectory, "Web.config");
            }

            if (options.Verbose)
            {
                Console.WriteLine($"Project directory: {projectDirectory}");
                Console.WriteLine($"Web.config filename: {webConfigFilename}");
            }

            var sw = new Stopwatch();
            sw.Restart();

            var settingsProvider = (ISettingsProvider) new SettingsProvider(projectDirectory);
            settingsProvider.PopulateFromWebConfig(webConfigFilename);

            var settings = new I18NLocalizationOptions(settingsProvider);
            var repository = new PoTranslationRepository(settings);
            var nuggetFinder = new FileNuggetFinder(settings);

            var items = nuggetFinder.ParseAll();
            if (repository.SaveTemplate(items))
            {
                var merger = new TranslationMerger(repository);
                merger.MergeAllTranslation(items);
            }

            sw.Stop();

            Console.WriteLine($"Operation completed in {sw.Elapsed.TotalSeconds:F} seconds.");

            return 0;
        }
    }
}
