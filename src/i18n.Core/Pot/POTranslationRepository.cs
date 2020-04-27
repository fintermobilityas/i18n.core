using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using i18n.Core.Abstractions.Domain;
using i18n.Core.Helpers;
using i18n.Core.Pot.Entities;
using i18n.Core.Pot.Helpers;

namespace i18n.Core.Pot
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal class PoTranslationRepository : ITranslationRepository
    {
        readonly I18NLocalizationOptions _localizationOptions;

        public PoTranslationRepository(I18NLocalizationOptions localizationOptions)
        {
            _localizationOptions = localizationOptions;
        }

        public Translation GetTranslation(string langtag, List<string> fileNames = null, bool loadingCache = true)
        {
            return ParseTranslationFile(langtag, fileNames, loadingCache);
        }

        /// <summary>
        /// Checks in first hand settings file, if not found there it checks file structure
        /// </summary>
        /// <returns>List of available languages</returns>
        public IEnumerable<Language> GetAvailableLanguages()
        {
            //todo: ideally we want to fill the other data in the Language object so this is usable by project incorporating i18n that they can simply
            // lookup available languages. Maybe we even add a country property so that it's easier for projects to add corresponding flags.

            var languages = _localizationOptions.AvailableLanguages.ToList();
            Language item;
            var availableLanguages = new List<Language>();

            //This means there was no languages from settings
            if (languages.Count == 0
                || languages.Count == 1 && languages[0] == "")
            {
                //We instead check for file structure
                var directoryInfo = new DirectoryInfo(GetAbsoluteLocaleDir());

                foreach (var languageShortTag in directoryInfo.EnumerateDirectories().Select(x => x.Name))
                {
                    try
                    {
                        var languageTag = new LanguageTag(languageShortTag);
                        if (languageTag.CultureInfo == null)
                        {
                            throw new CultureNotFoundException(languageShortTag);
                        }

                        item = new Language
                        {
                            LanguageShortTag = languageShortTag
                        };
                        availableLanguages.Add(item);
                    }
                    catch (CultureNotFoundException)
                    {
                        //There is a directory in the locale directory that is not a valid culture so ignore it
                    }
                }
            }
            else
            {
                //see if the desired language was one of the returned from settings
                foreach (var language in languages)
                {
                    item = new Language { LanguageShortTag = language };
                    availableLanguages.Add(item);
                }
            }

            return availableLanguages;

        }

        /// <summary>
        /// Checks if the language is set as supported in config file
        /// If not it checks if the PO file is available
        /// </summary>
        /// <param name="langtag">The tag for which you want to check if support exists. For instance "sv-SE"</param>
        /// <returns>True if language exists, otherwise false</returns>
        public bool TranslationExists(string langtag)
        {
            var languages = _localizationOptions.AvailableLanguages.ToList();

            if (languages.Count == 0
                || languages.Count == 1 && languages[0] == string.Empty)
            {
                return File.Exists(GetPathForLanguage(langtag));
            }

            return languages.Any(language => language == langtag);
        }

        /// <summary>
        /// Saves a translation into file with standard pattern locale/langtag/message.po
        /// Also saves a backup of previous version
        /// </summary>
        /// <param name="translation">The translation you wish to save. Must have Language shortag filled out.</param>
        public void SaveTranslation(Translation translation)
        {
            var templateFilePath = Path.Combine(GetAbsoluteLocaleDir(), _localizationOptions.LocaleFilename + ".pot");
            var potDate = DateTime.Now;

            if (File.Exists(templateFilePath))
            {
                potDate = File.GetLastWriteTime(templateFilePath);
            }

            var filePath = GetPathForLanguage(translation.LanguageInformation.LanguageShortTag);
            using var fs = I18NUtility.Retry(() => File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Write), 3);
            using var stream = new StreamWriter(fs);
            DebugHelpers.WriteLine("Writing file: {0}", filePath);

            // Establish ordering of items in PO file.
            var orderedItems = translation.Items.Values
                .OrderBy(x => x.References == null || !x.References.Any())
                // Non-orphan items before orphan items.
                .ThenBy(x => x.MsgKey);
            // Then order alphanumerically.

            //This is required for poedit to read the files correctly if they contains for instance swedish characters
            stream.WriteLine("msgid \"\"");
            stream.WriteLine("msgstr \"\"");
            stream.WriteLine("\"Project-Id-Version: \\n\"");
            stream.WriteLine("\"POT-Creation-Date: " + potDate.ToString("yyyy-MM-dd HH:mmzzz") + "\\n\"");
            stream.WriteLine("\"PO-Revision-Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mmzzz") + "\\n\"");
            stream.WriteLine("\"MIME-Version: 1.0\\n\"");
            stream.WriteLine("\"Content-Type: text/plain; charset=utf-8\\n\"");
            stream.WriteLine("\"Content-Transfer-Encoding: 8bit\\n\"");
            stream.WriteLine("\"X-Generator: i18n.POTGenerator\\n\"");
            stream.WriteLine();

            foreach (var item in orderedItems)
            {
                var hasReferences = false;

                if (item.TranslatorComments != null)
                {
                    foreach (var translatorComment in item.TranslatorComments.Distinct())
                    {
                        stream.WriteLine("# " + translatorComment);
                    }
                }

                if (item.ExtractedComments != null)
                {
                    foreach (var extractedComment in item.ExtractedComments.Distinct())
                    {
                        stream.WriteLine("#. " + extractedComment);
                    }
                }

                if (item.References != null)
                {
                    foreach (var reference in item.References.Distinct())
                    {
                        hasReferences = true;
                        stream.WriteLine("#: " + reference.ToComment());
                    }
                }

                if (item.Flags != null)
                {
                    foreach (var flag in item.Flags.Distinct())
                    {
                        stream.WriteLine("#, " + flag);
                    }
                }

                if (_localizationOptions.MessageContextEnabledFromComment
                    && item.ExtractedComments != null
                    && item.ExtractedComments.Count() != 0)
                {
                    WriteString(stream, hasReferences, "msgctxt", item.ExtractedComments.First());
                }

                WriteString(stream, hasReferences, "msgid", Escape(item.MsgId));
                WriteString(stream, hasReferences, "msgstr", Escape(item.Message));

                stream.WriteLine("");
            }
        }

        /// <summary>
        /// Saves a template file which is a all strings (needing translation) used in the entire project. Not language dependent
        /// </summary>
        /// <param name="items">A list of template items to save. The list should be all template items for the entire project.</param>
        public bool SaveTemplate(IDictionary<string, TemplateItem> items)
        {
            if (!_localizationOptions.GenerateTemplatePerFile)
            {
                return SaveTemplate(items, string.Empty);
            }

            var result = false;
            foreach (var item in items.GroupBy(x => x.Value.FileName))
            {
                result |= SaveTemplate(item.ToDictionary(x => x.Key, x => x.Value), item.Key);
            }

            return result;
        }

        bool SaveTemplate(IDictionary<string, TemplateItem> items, string fileName)
        {
            var filePath = Path.Combine(GetAbsoluteLocaleDir(), !string.IsNullOrWhiteSpace(fileName) ? fileName : _localizationOptions.LocaleFilename) + ".pot";

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (!File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                var dirInfo = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException());
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                }
                fileInfo.Create().Close();
            }

            using var stream = new StreamWriter(filePath);

            DebugHelpers.WriteLine("Writing file: {0}", filePath);
            // Establish ordering of items in PO file.
            var orderedItems = items.Values
                // Non-orphan items before orphan items.
                .OrderBy(x => x.References == null || !x.References.Any())
                // Then order alphanumerically.
                .ThenBy(x => x.MsgKey);

            // This is required for poedit to read the files correctly if they contains 
            // for instance swedish characters.
            stream.WriteLine("msgid \"\"");
            stream.WriteLine("msgstr \"\"");
            stream.WriteLine("\"Project-Id-Version: \\n\"");
            stream.WriteLine("\"POT-Creation-Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mmzzz") + "\\n\"");
            stream.WriteLine("\"MIME-Version: 1.0\\n\"");
            stream.WriteLine("\"Content-Type: text/plain; charset=utf-8\\n\"");
            stream.WriteLine("\"Content-Transfer-Encoding: 8bit\\n\"");
            stream.WriteLine("\"X-Generator: pot\\n\"");
            stream.WriteLine();

            foreach (var item in orderedItems)
            {
                if (item.Comments != null)
                {
                    foreach (var comment in item.Comments)
                    {
                        stream.WriteLine("#. " + comment);
                    }
                }

                foreach (var reference in item.References)
                {
                    stream.WriteLine("#: " + reference.ToComment());
                }

                if (_localizationOptions.MessageContextEnabledFromComment
                    && item.Comments != null
                    && item.Comments.Any())
                {
                    WriteString(stream, true, "msgctxt", item.Comments.First());
                }

                WriteString(stream, true, "msgid", Escape(item.MsgId));
                WriteString(stream, true, "msgstr", string.Empty); // Enable loading of POT file into editor e.g. PoEdit.

                stream.WriteLine(string.Empty);
            }

            return true;
        }

        /// <summary>
        /// Gets the locale directory from settings and makes sure it is translated into absolute path
        /// </summary>
        /// <returns>the locale directory in absolute path</returns>
        string GetAbsoluteLocaleDir()
        {
            return _localizationOptions.LocaleDirectory;
        }

        string GetPathForLanguage(string langtag, string filename = null)
        {
            if (!filename.IsSet())
            {
                filename = _localizationOptions.LocaleFilename;
            }

            return Path.Combine(GetAbsoluteLocaleDir(), langtag, filename + ".po");
        }

        /// <summary>
        /// Parses a PO file into a Language object
        /// </summary>
        /// <param name="langTag">The language (tag) you wish to load into Translation object</param>
        /// <param name="fileNames"></param>
        /// <param name="loadingCache"></param>
        /// <returns>A complete translation object with all all translations and language values set.</returns>
        Translation ParseTranslationFile(string langTag, List<string> fileNames, bool loadingCache)
        {
            //todo: consider that lines we don't understand like headers from poedit and #| should be preserved and outputted again.

            var translation = new Translation();
            var language = new Language
            {
                LanguageShortTag = langTag
            };
            translation.LanguageInformation = language;
            var items = new ConcurrentDictionary<string, TranslationItem>();

            var paths = new List<string>();

            if (!_localizationOptions.GenerateTemplatePerFile || loadingCache)
            {
                paths.Add(GetPathForLanguage(langTag));
            }

            paths.AddRange(_localizationOptions.LocaleOtherFiles.Where(file => file.IsSet()).Select(file => GetPathForLanguage(langTag, file)));

            if (_localizationOptions.GenerateTemplatePerFile && !loadingCache)
            {
                if (fileNames != null && fileNames.Count > 0)
                {
                    paths.AddRange(fileNames.Select(fileName => GetPathForLanguage(langTag, fileName)));
                }
            }

            foreach (var path in paths.Where(File.Exists))
            {
                DebugHelpers.WriteLine("Reading file: {0}", path);

                using var fs = I18NUtility.Retry(() => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read), 3);
                using var streamReader = new StreamReader(fs);

                // http://www.gnu.org/s/hello/manual/gettext/PO-Files.html

                string line;
                var itemStarted = false;
                while ((line = streamReader.ReadLine()) != null)
                {
                    var extractedComments = new HashSet<string>();
                    var translatorComments = new HashSet<string>();
                    var flags = new HashSet<string>();
                    var references = new List<ReferenceContext>();

                    //read all comments, flags and other descriptive items for this string
                    //if we have #~ its a historical/log entry but it is the messageID/message so we skip this do/while
                    if (line.StartsWith("#") && !line.StartsWith("#~"))
                    {
                        do
                        {
                            itemStarted = true;
                            switch (line[1])
                            {
                                case '.': //Extracted comments
                                    extractedComments.Add(line.Substring(2).Trim());
                                    break;
                                case ':': //references
                                    references.Add(ReferenceContext.Parse(line.Substring(2).Trim()));
                                    break;
                                case ',': //flags
                                    flags.Add(line.Substring(2).Trim());
                                    break;
                                case '|': //msgid previous-untranslated-string - NOT used by us
                                    break;
                                default: //translator comments
                                    translatorComments.Add(line.Substring(1).Trim());
                                    break;
                            }

                        } while ((line = streamReader.ReadLine()) != null && line.StartsWith("#"));
                    }

                    if (line != null && (itemStarted || line.StartsWith("#~")))
                    {
                        var item = ParseBody(streamReader, line);
                        if (item != null)
                        {
                            //
                            item.TranslatorComments = translatorComments;
                            item.ExtractedComments = extractedComments;
                            item.Flags = flags;
                            item.References = references;
                            //
                            items.AddOrUpdate(
                                item.MsgKey,
                                // Add routine.
                                k => item,
                                // Update routine.
                                (k, v) =>
                                {
                                    v.References = v.References.Append(item.References);
                                    var referencesAsComments =
                                        item.References.Select(r => r.ToComment()).ToList();
                                    v.ExtractedComments = v.ExtractedComments.Append(referencesAsComments);
                                    v.TranslatorComments = v.TranslatorComments.Append(referencesAsComments);
                                    v.Flags = v.Flags.Append(referencesAsComments);
                                    return v;
                                });
                        }
                    }

                    itemStarted = false;
                }
            }
            translation.Items = items;
            return translation;
        }

        /// <summary>
        /// Removes the preceding characters in a file showing that an item is historical/log. That is to say it has been removed from the project.
        /// We don't need care about the character as the fact that it lacks references is what tells us it's a log item
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        static string RemoveCommentIfHistorical(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            return line.StartsWith("#~") ? line.Replace("#~", "").Trim() : line;
        }

        /// <summary>
        /// Parses the body of a PO file item. That is to say the message id and the message itself.
        /// Reason for why it must be on second line (textreader) is so that you can read until you have read to far without peek previously for meta data.
        /// </summary>
        /// <param name="fs">A textreader that must be on the second line of a message body</param>
        /// <param name="line">The first line of the message body.</param>
        /// <returns>Returns a TranslationItem with only key, id and message set</returns>
        TranslationItem ParseBody(TextReader fs, string line)
        {
            var originalLine = line;

            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            var message = new TranslationItem { MsgKey = "" };
            var sb = new StringBuilder();

            string msgctxt = null;
            line = RemoveCommentIfHistorical(line); //so that we read in removed historical records too
            if (line.StartsWith("msgctxt"))
            {
                msgctxt = Unquote(line);
                line = fs.ReadLine();
            }

            line = RemoveCommentIfHistorical(line); //so that we read in removed historical records too
            if (line.StartsWith("msgid"))
            {
                var msgid = Unquote(line);
                sb.Append(msgid);

                while ((line = fs.ReadLine()) != null)
                {
                    line = RemoveCommentIfHistorical(line);
                    if (string.IsNullOrEmpty(line))
                    {
                        DebugHelpers.WriteLine("ERROR - line is empty. Original line: " + originalLine);
                        continue;
                    }
                    if (!line.StartsWith("msgstr") && (msgid = Unquote(line)) != null)
                    {
                        sb.Append(msgid);
                    }
                    else
                    {
                        break;
                    }
                }

                message.MsgId = Unescape(sb.ToString());

                // If no msgctxt is set then msgkey is the msgid; otherwise it is msgid+msgctxt.
                message.MsgKey = string.IsNullOrEmpty(msgctxt) ?
                    message.MsgId :
                    TemplateItem.KeyFromMsgidAndComment(message.MsgId, msgctxt, true);
            }

            sb.Clear();
            line = RemoveCommentIfHistorical(line);
            if (!string.IsNullOrEmpty(line) && line.StartsWith("msgstr"))
            {
                var msgstr = Unquote(line);
                sb.Append(msgstr);

                while ((line = fs.ReadLine()) != null && (msgstr = Unquote(line)) != null)
                {
                    RemoveCommentIfHistorical(line);
                    sb.Append(msgstr);
                }

                message.Message = Unescape(sb.ToString());
            }
            return message;
        }

        /// <summary>
        /// Helper for writing either a msgid or msgstr to the po file.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="hasReferences"></param>
        /// <param name="type">"msgid" or "msgstr"</param>
        /// <param name="value"></param>
        static void WriteString(TextWriter stream, bool hasReferences, string type, string value)
        {
            // Logic for outputting multi-line msgid.
            //
            // IN : a<LF>b
            // OUT: msgid ""
            //      "a\n"
            //      "b"
            //
            // IN : a<LF>b<LF>
            // OUT: msgid ""
            // OUT: "a\n"
            //      "b\n"
            //
            value ??= "";
            value = value.Replace("\r\n", "\n");
            var sb = new StringBuilder(100);
            // If multi-line
            if (value.Contains('\n'))
            {
                // · msgid ""
                sb.AppendFormat("{0} \"\"\r\n", type);
                // · following lines
                sb.Append("\"");
                var s1 = value.Replace("\n", "\\n\"\r\n\"");
                sb.Append(s1);
                sb.Append("\"");
            }
            // If single-line
            else
            {
                sb.AppendFormat("{0} \"{1}\"", type, value);
            }
            // If noref...prefix each line with "#~ ".
            if (!hasReferences)
            {
                sb.Insert(0, "#~ ");
                sb.Replace("\r\n", "\r\n#~ ");
            }
            //
            var s = sb.ToString();
            stream.WriteLine(s);
        }

        //this method removes anything before the first quote and also removes first and last quote
        static string Unquote(string lhs, string quotechar = "\"")
        {
            var begin = lhs.IndexOf(quotechar, StringComparison.Ordinal);
            if (begin == -1)
            {
                return null;
            }
            var end = lhs.LastIndexOf(quotechar, StringComparison.Ordinal);
            return end <= begin ? null : lhs.Substring(begin + 1, end - begin - 1);
        }

        static string Escape(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Looks up in the subject string standard C escape sequences and converts them
        /// to their actual character counterparts.
        /// </summary>
        /// <seealso href="http://stackoverflow.com/questions/6629020/evaluate-escaped-string/8854626#8854626"/>
        static string Unescape(string s)
        {
            var regexUnescape = new Regex("\\\\[abfnrtv?\"'\\\\]|\\\\[0-3]?[0-7]{1,2}|\\\\u[0-9a-fA-F]{4}|.", RegexOptions.Singleline);

            var sb = new StringBuilder();
            var mc = regexUnescape.Matches(s, 0);

            foreach (Match m in mc)
            {
                if (m.Length == 1)
                {
                    sb.Append(m.Value);
                }
                else
                {
                    if (m.Value[1] >= '0' && m.Value[1] <= '7')
                    {
                        var i = 0;

                        for (var j = 1; j < m.Length; j++)
                        {
                            i *= 8;
                            i += m.Value[j] - '0';
                        }

                        sb.Append((char)i);
                    }
                    else if (m.Value[1] == 'u')
                    {
                        var i = 0;

                        for (var j = 2; j < m.Length; j++)
                        {
                            i *= 16;

                            if (m.Value[j] >= '0' && m.Value[j] <= '9')
                            {
                                i += m.Value[j] - '0';
                            }
                            else if (m.Value[j] >= 'A' && m.Value[j] <= 'F')
                            {
                                i += m.Value[j] - 'A' + 10;
                            }
                            else if (m.Value[j] >= 'a' && m.Value[j] <= 'a')
                            {
                                i += m.Value[j] - 'a' + 10;
                            }
                        }

                        sb.Append((char)i);
                    }
                    else
                    {
                        switch (m.Value[1])
                        {
                            case 'a':
                                sb.Append('\a');
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'v':
                                sb.Append('\v');
                                break;
                            default:
                                sb.Append(m.Value[1]);
                                break;
                        }
                    }
                }
            }

            return sb.ToString();
        }
    }
}
