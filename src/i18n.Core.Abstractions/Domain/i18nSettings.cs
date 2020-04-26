using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace i18n.Core.Abstractions.Domain
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class I18NSettings
    {
        readonly ISettingsProvider _settingsProvider;

        const string Prefix = "i18n.";
        const string AllToken = "*";
        const string OneToken = "?";

        public I18NSettings(ISettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        public string ProjectDirectory => _settingsProvider.ProjectDirectory;

        static string GetPrefixedString(string key)
        {
            return Prefix + key;
        }

        string MakePathAbsoluteAndFromConfigFile(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            var startPath = Path.GetDirectoryName(_settingsProvider.ProjectDirectory);
            return Path.GetFullPath(Path.Combine(startPath ?? throw new InvalidOperationException(), path));
        }


        /// <summary>
        /// Determines whether the specified path has a windows wildcard character (* or ?)
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        ///   <c>true</c> if the specified path has a wildcard otherwise, <c>false</c>.
        /// </returns>
        static bool HasSearchCharacter(string path)
        {
            return path.Contains(AllToken) || path.Contains(OneToken);
        }

        /// <summary>
        /// Find all the existing physical paths that corresponds to the specified path.
        /// Returns a single value if there are no wildcards in the specified path.
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
        /// Recursively gets the path by moving through a directory tree (parts).
        /// </summary>
        /// <param name="parts">The path parts to process.</param>
        /// <param name="root">The root path from where to start.</param>
        /// <returns>A list of existing paths</returns>
        IEnumerable<string> GetPaths(string[] parts, string root = "")
        {
            if (parts == null || parts.Length == 0)
            {
                if (Directory.Exists(root))
                    return new[] { Path.GetFullPath(root) };
                return Enumerable.Empty<string>();
            }

            var paths = new List<string>();
            if (HasSearchCharacter(parts[0]))
            {
                var rooted = MakePathAbsoluteAndFromConfigFile(root);
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

        #region Locale directory

        const string _localeDirectoryDefault = "locale";
        public string LocaleDirectory
        {
            get
            {
                var prefixedString = GetPrefixedString("LocaleDirectory");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var path = setting ?? _localeDirectoryDefault;

                return MakePathAbsoluteAndFromConfigFile(path);
            }
            set
            {
                var prefixedString = GetPrefixedString("LocaleDirectory");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        #endregion

        #region Locale filename

        const string _localeFilenameDefault = "messages";
        public string LocaleFilename
        {
            get
            {
                var prefixedString = GetPrefixedString("LocaleFilename");
                var setting = _settingsProvider.GetSetting(prefixedString);
                return Helpers.Extensions.IsSet(setting) ? setting : _localeFilenameDefault;
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
                if (!Helpers.Extensions.IsSet(setting))
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

        #endregion

        #region White list

        const string _whiteListDefault = "*.cs;*.cshtml";
        
        /// <summary>
        /// Describes zero or more file specifications which in turn specify
        /// the source files to be targeted by FileNuggetParser.
        /// </summary>
        /// <remarks>
        /// Each element in the list may be a full file name e.g. "myfile.js",
        /// or a file extension e.g. "*.js".<br/>
        /// When the list is stored in the config file as a string, each element is delimited by
        /// a semi colon character.<br/>
        /// Defaults to "*.cs;*.cshtml".
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

                return Helpers.Extensions.IsSet(_whiteListDefault) ? 
                    _whiteListDefault.Split(';')
                        .Where(x => !string.IsNullOrWhiteSpace(x)).ToList() : new List<string>();
            }
            set
            {
                var prefixedString = GetPrefixedString("WhiteList");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        #endregion

        #region Black list

        const string _blackListDefault = "";
        IList<string> _cached_blackList;

        /// <summary>
        /// Describes zero or more source directory/folder paths to be ignored during nugget parsing
        /// e.g. by FileNuggetParser.
        /// </summary>
        /// <remarks>
        /// Each element in the list may be either an absolute (rooted) or relative path.
        /// When the list is stored in the config file as a string, each element is delimited by
        /// a semi colon character.<br/>
        /// Default value is empty list.<br/>
        /// </remarks>
        public IEnumerable<string> BlackList
        {
            get
            {
                if(_cached_blackList != null)
                {
                    return _cached_blackList;
                }
                _cached_blackList = new List<string>();
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
                else if (Helpers.Extensions.IsSet(_blackListDefault))
                {
                    list = _blackListDefault.Split(';').ToList();
                }
                else
                {
                    return _cached_blackList;
                }

                foreach (var path in list.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    _cached_blackList.Add(MakePathAbsoluteAndFromConfigFile(path));
                }

                return _cached_blackList;
            }
            set
            {
                var prefixedString = GetPrefixedString("BlackList");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        #endregion

        #region Nugget tokens

        const string _nuggetBeginTokenDefault = "[[[";
        public string NuggetBeginToken
        {
            get
            {
                var prefixedString = GetPrefixedString("NuggetBeginToken");
                var setting = _settingsProvider.GetSetting(prefixedString);
                if (setting != null)
                {
                    return setting;
                }

                return _nuggetBeginTokenDefault;
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
                if (setting != null)
                {
                    return setting;
                }

                return _nuggetEndTokenDefault;

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
                if (setting != null)
                {
                    return setting;
                }

                return _nuggetDelimiterTokenDefault;

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
                if (setting != null)
                {
                    return setting;
                }

                return _nuggetCommentTokenDefault;

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
                if (setting != null)
                {
                    return setting;
                }
                return NuggetParameterBeginTokenDefault;
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
                if (setting != null)
                {
                    return setting;
                }
                return NuggetParameterEndTokenDefault;
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
                if (setting != null)
                {
                    return setting;
                }
                return NuggetVisualizeTokenDefault;
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
                if (setting != null)
                {
                    return setting;
                }
                return string.Empty;
            }
            set
            {
                var prefixedString = GetPrefixedString("NuggetVisualizeEndToken");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        #endregion
        
        #region DirectoriesToScan

        const string _directoriesToScan = ".";

        /// <summary>
        /// A semi-colon-delimited string that specifies one or more paths to the 
        /// root directory/folder of the branches which FileNuggetParser is to scan for source files.
        /// </summary>
        /// <remarks>
        /// Each string may be an absolute (rooted) path, or a path
        /// relative to the folder containing the current config file
        /// (<see cref="AbstractSettingService.GetConfigFileLocation"/>).<br/>
        /// Default value is "." which equates to the the single folder containing the 
        /// current config file (<see cref="AbstractSettingService.GetConfigFileLocation"/>).<br/>
        /// Typically, you may set to ".." equating to the solution folder for the
        /// project containing the current config file.<br/>
        /// An example of a multi-path string is "c:\mywebsite;c:\mylibs\asp.net".
        /// </remarks>
        public IEnumerable<string> DirectoriesToScan
        {
            get
            {
                var prefixedString = GetPrefixedString("DirectoriesToScan");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var list = setting != null ? setting.Split(';').ToList() : _directoriesToScan.Split(';').ToList();
                return list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(MakePathAbsoluteAndFromConfigFile).ToList();
            }
            set
            {
                var prefixedString = GetPrefixedString("DirectoriesToScan");
                _settingsProvider.SetSetting(prefixedString, string.Join(";", value));
            }
        }

        #endregion
        
        #region Available Languages

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

        #endregion

        #region MessageContextEnabledFromComment

        bool? _cached_MessageContextEnabledFromComment;
        public bool MessageContextEnabledFromComment
        {
            get
            {
                // NB: this is not particularly thread-safe, but not seen as dangerous
                // if done concurrently as modification is one-way.
                if (_cached_MessageContextEnabledFromComment != null) {
                    return _cached_MessageContextEnabledFromComment.Value; }

                var prefixedString = GetPrefixedString("MessageContextEnabledFromComment");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) &&  setting == "true";
                _cached_MessageContextEnabledFromComment = result;
                return result;
            }
            set
            {
                var prefixedString = GetPrefixedString("MessageContextEnabledFromComment");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
                _cached_MessageContextEnabledFromComment = value;
            }
        }

        #endregion

        #region VisualizeMessages

        bool? _cached_visualizeMessages;
        public bool VisualizeMessages
        {
            get
            {
                // NB: this is not particularly thread-safe, but not seen as dangerous
                // if done concurrently as modification is one-way.
                if (_cached_visualizeMessages != null)
                {
                    return _cached_visualizeMessages.Value;
                }

                var prefixedString = GetPrefixedString("VisualizeMessages");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                _cached_visualizeMessages = result;
                return _cached_visualizeMessages.Value;
            }
            set
            {
                var prefixedString = GetPrefixedString("VisualizeMessages");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
                _cached_visualizeMessages = value;
            }
        }

        public string VisualizeLanguageSeparator
        {
            get
            {
                var prefixedString = GetPrefixedString("VisualizeLanguageSeparator");
                var setting = _settingsProvider.GetSetting(prefixedString);
                if (setting != null)
                {
                    return setting;
                }
                return string.Empty;
            }
            set
            {
                var prefixedString = GetPrefixedString("VisualizeLanguageSeparator");
                _settingsProvider.SetSetting(prefixedString, value);
            }
        }

        #endregion

        #region DisableReferences

        bool? _cached_disableReferences;

        public bool DisableReferences
        {
            get
            {
                if (_cached_disableReferences != null)
                {
                    return _cached_disableReferences.Value;
                }

                var prefixedString = GetPrefixedString("DisableReferences");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                _cached_disableReferences = result;
                return _cached_disableReferences.Value;
            }
            set
            {
                var prefixedString = GetPrefixedString("DisableReferences");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
                _cached_disableReferences = value;
            }
        }
        #endregion

        #region GenerateTemplatePerFile

        bool? _cached_generateTemplatePerFile;
        public bool GenerateTemplatePerFile
        {
            get
            {
                // NB: this is not particularly thread-safe, but not seen as dangerous
                // if done concurrently as modification is one-way.
                if (_cached_generateTemplatePerFile != null)
                {
                    return _cached_generateTemplatePerFile.Value;
                }

                var prefixedString = GetPrefixedString("GenerateTemplatePerFile");
                var setting = _settingsProvider.GetSetting(prefixedString);
                var result = !string.IsNullOrEmpty(setting) && setting == "true";
                _cached_generateTemplatePerFile = result;
                return _cached_generateTemplatePerFile.Value;
            }
            set
            {
                var prefixedString = GetPrefixedString("GenerateTemplatePerFile");
                _settingsProvider.SetSetting(prefixedString, value ? "true" : "false");
                _cached_generateTemplatePerFile = value;
            }
        }

        #endregion
    }
}
