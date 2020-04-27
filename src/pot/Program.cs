using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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

        [Option("web-config-path", Required = false, HelpText = "Path to web.config that contain i18n.* settings.")]
        public string WebConfigPath { get; [UsedImplicitly] set; }

        [Option("verbose", Required = false, HelpText = "Set output to verbose.")]
        public bool Verbose { get; [UsedImplicitly] set; }

        [Option("watch", Required = false, HelpText = "Automatically rebuild pot if any translatable files changes.")]
        public bool Watch { get; [UsedImplicitly] set; }

        [Option("watch-delay", Required = false, HelpText = "Delay between each build (throttling). Default value is 500 ms. ")]
        public int WatchDelay { get; [UsedImplicitly] set; } = 500;
    }

    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    internal static class Program
    {
        static readonly object BuildLock = new object();
        static bool _isBuilding;
        static DateTime? _lastBuildDate;

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

            if (options.Watch)
            {
                return Watch(options, projectDirectory, webConfigFilename, () => Build(projectDirectory, webConfigFilename, options.WatchDelay));
            }

            Build(projectDirectory, webConfigFilename);

            return 0;
        }

        static int Watch(Options options, string projectDirectory, string webConfigFilename, Action onChangeAction)
        {
            var settingsProvider = (ISettingsProvider)new SettingsProvider(projectDirectory);
            settingsProvider.PopulateFromWebConfig(webConfigFilename);
            var settings = new I18NLocalizationOptions(settingsProvider);
            var watchers = new List<FileSystemWatcher>();

            var filters = settings.WhiteList.Where(x => x.StartsWith("*.")).ToList();

            Console.WriteLine($"Watching directories: {string.Join(", ", settings.DirectoriesToScan)}. Filters: {string.Join(", ", filters)}");

            try
            {
                foreach (var directory in settings.DirectoriesToScan)
                {
                    if (!Directory.Exists(directory))
                    {
                        Console.Error.WriteLine($"Watch directory does not exist: {directory}");
                        return 1;
                    }
                    
                    var watcher = new FileSystemWatcher
                    {
                        Path = directory,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        IncludeSubdirectories = true
                    };

                    watcher.NotifyFilter = NotifyFilters.LastAccess
                                           | NotifyFilters.LastWrite
                                           | NotifyFilters.FileName
                                           | NotifyFilters.DirectoryName;

                    foreach (var filter in filters)
                    {
                        watcher.Filters.Add(filter);
                    }

                    watcher.Changed += (sender, args) =>
                    {
                        onChangeAction.Invoke();
                    };

                    watcher.Created += (sender, args) =>
                    {
                        onChangeAction.Invoke();
                    };

                    watcher.Renamed += (sender, args) =>
                    {
                        onChangeAction.Invoke();
                    };

                    watcher.Deleted += (sender, args) =>
                    {
                        onChangeAction.Invoke();
                    };

                    watcher.EnableRaisingEvents = true;

                    watchers.Add(watcher);
                }
            }
            finally
            {
                Console.ReadLine();

                foreach (var watcher in watchers)
                {
                    watcher.Dispose();
                }
            }
            
            return 0;
        }

        static void Build(string projectDirectory, string webConfigFilename, int buildDelayMilliseconds = -1)
        {
            lock (BuildLock)
            {
                if (_isBuilding)
                {
                    return;
                }

                if (buildDelayMilliseconds > 0
                    && _lastBuildDate.HasValue 
                    && DateTime.Now - _lastBuildDate.Value < TimeSpan.FromMilliseconds(buildDelayMilliseconds))
                {
                    return;
                }

                _isBuilding = true;
            }

            try
            {
                var sw = new Stopwatch();
                sw.Restart();

                var settingsProvider = (ISettingsProvider)new SettingsProvider(projectDirectory);
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

                Console.WriteLine($"Build operation completed in {sw.Elapsed.TotalSeconds:F} seconds.");
            }
            finally
            {
                lock (BuildLock)
                {
                    _isBuilding = false;
                    _lastBuildDate = DateTime.Now;
                }
            }
        }

    }
}
