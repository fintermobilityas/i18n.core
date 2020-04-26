using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using i18n.Core.Abstractions.Domain.Helpers;

namespace i18n.Core.Abstractions.Domain
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class I18NSettings
    {
        const string Prefix = "i18n.";
        const string AllToken = "*";
        const string OneToken = "?";
        readonly ISettingsProvider _settingsProvider;

        public string ProjectDirectory => _settingsProvider.ProjectDirectory;

        public I18NSettings(ISettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        static string GetPrefixedString(string key)
        {
            return Prefix + key;
        }

        string BuildAbsolutePathFromProjectDirectory(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_settingsProvider.ProjectDirectory ?? throw new InvalidOperationException(), path));
        }

        /// <summary>
        ///     Determines whether the specified path has a windows wildcard character (* or ?)
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        ///     <c>true</c> if the specified path has a wildcard otherwise, <c>false</c>.
        /// </returns>
        static bool HasSearchCharacter(string path)
        {
            return path.Contains(AllToken) || path.Contains(OneToken);
        }

        /// <summary>
        ///     Find all the existing physical paths that corresponds to the specified path.
        ///     Returns a single value if there are no wildcards in the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>An enumeration of corresponding paths</returns>
        IEnumerable<string> FindPaths(string path)
        {
            var paths = new List<string>();
            if (HasSearchCharacter(path))
            {
                var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                paths = GetPaths(parts).ToList();
            }
            else
            {
                paths.Add(path);
            }

            return paths;
        }

        /// <summary>
        ///     Recursively gets the path by moving through a directory tree (parts).
        /// </summary>
        /// <param name="parts">The path parts to process.</param>
        /// <param name="root">The root path from where to start.</param>
        /// <returns>A list of existing paths</returns>
        IEnumerable<string> GetPaths(IReadOnlyList<string> parts, string root = "")
        {
            if (parts == null || parts.Count == 0)
            {
                return Directory.Exists(root) ? new[] {Path.GetFullPath(root)} : Enumerable.Empty<string>();
            }

            var paths = new List<string>();
            if (HasSearchCharacter(parts[0]))
            {
                var rooted = BuildAbsolutePathFromProjectDirectory(root);
                var list = Directory.GetDirectories(rooted, parts[0]);
                foreach (var path in list)
                {
                    paths.AddRange(GetPaths(parts.Skip(1).ToArray(), path));
                }
            }
            else
            {
                return GetPaths(parts.Skip(1).ToArray(), Path.Combine(root, parts[0]));
            }

            return paths;
        }

        const string _localeDirectoryDefault = "locale";

        public string LocaleDirectory
        {
            get
            {
                var prefixedString = GetPrefixedString("LocaleDirectory");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var path = setting ?? _localeDirectoryDefault;

                return BuildAbsolutePathFromProjectDirectory(path);
            }
            set
            {
                var prefixedString = GetPrefixedString("LocaleDirectory");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string _localeFilenameDefault = "messages";

        public string LocaleFilename
        {
            get
            {
                var prefixedString = GetPrefixedString("LocaleFilename");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting.IsSet() ? setting : _localeFilenameDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("LocaleFilename");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string _localeOtherFilesDefault = "";

        public IEnumerable<string> LocaleOtherFiles
        {
            get
            {
                var prefixedString = GetPrefixedString("LocaleOtherFiles");
                var setting = _settingsProvider.GetSetting(prefixedString);
                if (!setting.IsSet())
                {
                    setting = _localeOtherFilesDefault;
                }

                return setting.Split(';');
            }
            set
            {
                var prefixedString = GetPrefixedString("LocaleOtherFiles");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        const string _whiteListDefault = "*.cs;*.cshtml";

        /// <summary>
        ///     Describes zero or more file specifications which in turn specify
        ///     the source files to be targeted by FileNuggetParser.
        /// </summary>
        /// <remarks>
        ///     Each element in the list may be a full file name e.g. "myfile.js",
        ///     or a file extension e.g. "*.js".<br />
        ///     When the list is stored in the config file as a string, each element is delimited by
        ///     a semi colon character.<br />
        ///     Defaults to "*.cs;*.cshtml".
        /// </remarks>
        public IEnumerable<string> WhiteList
        {
            get
            {
                var prefixedString = GetPrefixedString("WhiteList");
                var setting = _settingsProvider.GetSetting(prefixedString);
                if (setting != null)
                {
                    return setting.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                }

                return _whiteListDefault.IsSet()
                    ? _whiteListDefault.Split(';')
                        .Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : new List<string>();
            }
            set
            {
                var prefixedString = GetPrefixedString("WhiteList");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        const string _blackListDefault = "";

        /// <summary>
        ///     Describes zero or more source directory/folder paths to be ignored during nugget parsing
        ///     e.g. by FileNuggetParser.
        /// </summary>
        /// <remarks>
        ///     Each element in the list may be either an absolute (rooted) or relative path.
        ///     When the list is stored in the config file as a string, each element is delimited by
        ///     a semi colon character.<br />
        ///     Default value is empty list.<br />
        /// </remarks>
        public IEnumerable<string> BlackList
        {
            get
            {
                var blackList = new List<string>();
                var prefixedString = GetPrefixedString("BlackList");
                var setting = _settingsProvider.GetSetting(prefixedString);
                //If we find any wildcard in the setting, we replace it by the exitsing physical paths
                if (setting != null && HasSearchCharacter(setting))
                {
                    IEnumerable<string> preblacklist = setting.Split(';');
                    setting = string.Join(";", preblacklist.SelectMany(FindPaths));
                }

                List<string> list;
                if (setting != null)
                {
                    list = setting.Split(';').ToList();
                }
                else if (_blackListDefault.IsSet())
                {
                    list = _blackListDefault.Split(';').ToList();
                }
                else
                {
                    return blackList;
                }

                blackList.AddRange(list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(BuildAbsolutePathFromProjectDirectory));

                return blackList;
            }
            set
            {
                var prefixedString = GetPrefixedString("BlackList");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        const string _nuggetBeginTokenDefault = "[[[";

        public string NuggetBeginToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetBeginToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? _nuggetBeginTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetBeginToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string _nuggetEndTokenDefault = "]]]";

        public string NuggetEndToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetEndToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? _nuggetEndTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetEndToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string _nuggetDelimiterTokenDefault = "|||";

        public string NuggetDelimiterToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetDelimiterToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? _nuggetDelimiterTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetDelimiterToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string _nuggetCommentTokenDefault = "///";

        public string NuggetCommentToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetCommentToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? _nuggetCommentTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetCommentToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string NuggetParameterBeginTokenDefault = "(((";

        public string NuggetParameterBeginToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetParameterBeginToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? NuggetParameterBeginTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetParameterBeginToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string NuggetParameterEndTokenDefault = ")))";

        public string NuggetParameterEndToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetParameterEndToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? NuggetParameterEndTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetParameterEndToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string NuggetVisualizeTokenDefault = "!";

        public string NuggetVisualizeToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetVisualizeToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? NuggetVisualizeTokenDefault;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetVisualizeToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        public string NuggetVisualizeEndToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetVisualizeEndToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? string.Empty;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetVisualizeEndToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        const string _directoriesToScan = ".";

        /// <summary>
        ///     A semi-colon-delimited string that specifies one or more paths to the
        ///     root directory/folder of the branches which FileNuggetParser is to scan for source files.
        /// </summary>
        public IEnumerable<string> DirectoriesToScan
        {
            get
            {
                var prefixedString = GetPrefixedString("DirectoriesToScan");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var list = setting != null ? setting.Split(';').ToList() : _directoriesToScan.Split(';').ToList();
                return list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(BuildAbsolutePathFromProjectDirectory).ToList();
            }
            set
            {
                var prefixedString = GetPrefixedString("DirectoriesToScan");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        //If empty string is returned the repository can if it choses enumerate languages in a different way (like enumerating directories in the case of PO files)
        //empty string is returned as an IEnumerable with one empty element
        const string _availableLanguages = "";

        public IEnumerable<string> AvailableLanguages
        {
            get
            {
                var prefixedString = GetPrefixedString("AvailableLanguages");
                var setting = _settingsProvider.GetSetting(prefixedString);
                if (setting != null)
                {
                    return setting.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                }

                return _availableLanguages.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            }
            set
            {
                var prefixedString = GetPrefixedString("AvailableLanguages");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }


        public bool MessageContextEnabledFromComment
        {
            get
            {
                var prefixedString = GetPrefixedString("MessageContextEnabledFromComment");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                return result;
            }
            set
            {
                var prefixedString = GetPrefixedString("MessageContextEnabledFromComment");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
            }
        }

        public bool VisualizeMessages
        {
            get
            {
                var prefixedString = GetPrefixedString("VisualizeMessages");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                return result;
            }
            set
            {
                var prefixedString = GetPrefixedString("VisualizeMessages");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
            }
        }

        public string VisualizeLanguageSeparator
        {
            get
            {
                var prefixedString = GetPrefixedString("VisualizeLanguageSeparator");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return setting ?? string.Empty;
            }
            set
            {
                var prefixedString = GetPrefixedString("VisualizeLanguageSeparator");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        public bool DisableReferences
        {
            get
            {
                var prefixedString = GetPrefixedString("DisableReferences");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                return result;
            }
            set
            {
                var prefixedString = GetPrefixedString("DisableReferences");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
            }
        }

        public bool GenerateTemplatePerFile
        {
            get
            { 
                var prefixedString = GetPrefixedString("GenerateTemplatePerFile");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                return result;
            }
            set
            {
                var prefixedString = GetPrefixedString("GenerateTemplatePerFile");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
            }
        }
    }
}