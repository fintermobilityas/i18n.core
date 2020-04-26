using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using i18n.Core.Abstractions.Domain;
using i18n.Core.Pot.Entities;
using i18n.Core.Pot.Helpers;

namespace i18n.Core.Pot
{
    internal class FileNuggetFinder : INuggetFinder
    {
        readonly I18NLocalizationOptions _localizationOptions;
        readonly NuggetParser _nuggetParser;

        public FileNuggetFinder(I18NLocalizationOptions localizationOptions)
        {
            _localizationOptions = localizationOptions;
            _nuggetParser = new NuggetParser(new NuggetTokens(
                _localizationOptions.NuggetBeginToken,
                _localizationOptions.NuggetEndToken,
                _localizationOptions.NuggetDelimiterToken,
                _localizationOptions.NuggetCommentToken),
                NuggetParser.Context.SourceProcessing);
        }

        /// <summary>
        /// Goes through the Directories to scan recursively and starts a scan of each while that matches the whitelist. (both from settings)
        /// </summary>
        /// <returns>All found nuggets.</returns>
        public IDictionary<string, TemplateItem> ParseAll()
        {
            var whiteListItems = _localizationOptions.WhiteList.ToList();
            var directoriesToSearchRecursively = _localizationOptions.DirectoriesToScan;
            var fileEnumerator = new FileEnumerator(_localizationOptions.BlackList.ToList());
            var templateItems = new ConcurrentDictionary<string, TemplateItem>();

            foreach (var directoryPath in directoriesToSearchRecursively)
            {
                foreach (var filePath in fileEnumerator.GetFiles(directoryPath))
                {
                    if (filePath.Length >= 260)
                    {
                        DebugHelpers.WriteLine("Path too long to process. Path: " + filePath);
                        continue;
                    }

                    var currentFullPath = Path.GetDirectoryName(Path.GetFullPath(filePath));
                    var blacklistFound = _localizationOptions.BlackList.Any(blackItem => currentFullPath == null || currentFullPath.StartsWith(blackItem, StringComparison.OrdinalIgnoreCase));
                    if (blacklistFound)
                    {
                        continue;
                    }

                    foreach (var whiteListItem in whiteListItems)
                    {
                        if (whiteListItem.StartsWith("*."))
                        {
                            var fileName = Path.GetFileName(filePath);
                            var dotStartindex = fileName.IndexOf(".", StringComparison.Ordinal);
                            if (dotStartindex == -1)
                            {
                                continue;
                            }

                            var extension = fileName.Substring(dotStartindex);
                            if (!extension.Equals(whiteListItem.Substring(1), StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            ParseFile(_localizationOptions.ProjectDirectory, filePath, templateItems);
                            break;
                        }

                        if (Path.GetFileName(filePath).Equals(whiteListItem, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        ParseFile(_localizationOptions.ProjectDirectory, filePath, templateItems);
                        break;
                    }
                }
            }

            return templateItems;
        }

        void ParseFile(string projectDirectory, string filePath, ConcurrentDictionary<string, TemplateItem> templateItems)
        {
            var referencePath = PathNormalizer.MakeRelativePath(projectDirectory, filePath);

            DebugHelpers.WriteLine("FileNuggetFinder.ParseFile -- {0}", filePath);
            // Lookup any/all nuggets in the file and for each add a new template item.
            using var fs = File.OpenText(filePath);

            _nuggetParser.ParseString(fs.ReadToEnd(), delegate (string nuggetString, int pos, Nugget nugget, string iEntity)
            {
                var referenceContext = _localizationOptions.DisableReferences
                    ? ReferenceContext.Create("Disabled references", iEntity, 0)
                    : ReferenceContext.Create(referencePath, iEntity, pos);
                var fileName = Path.GetFileNameWithoutExtension(filePath);
               // If we have a file like "myfile.aspx.vb" then the fileName will be "myfile.aspx" resulting in split
               // .pot files. So remove all extensions, so that we just have the actual name to deal with.
               fileName = fileName.IndexOf('.') > -1 ? fileName.Split('.')[0] : fileName;

                AddNewTemplateItem(
                    fileName,
                    referenceContext,
                    nugget,
                    templateItems);
               // Done.
               return null; // null means we are not modifying the entity.
           });
        }

        void AddNewTemplateItem(
            string fileName,
            ReferenceContext referenceContext,
            Nugget nugget,
            ConcurrentDictionary<string, TemplateItem> templateItems)
        {
            var msgid = nugget.MsgId.Replace("\r\n", "\n").Replace("\r", "\\n");
            // NB: In memory msgids are normalized so that LFs are converted to "\n" char sequence.
            var key = TemplateItem.KeyFromMsgidAndComment(msgid, nugget.Comment, _localizationOptions.MessageContextEnabledFromComment);
            List<string> tmpList;
            //
            templateItems.AddOrUpdate(
                key,
                // Add routine.
                k =>
                {
                    var item = new TemplateItem
                    {
                        MsgKey = key,
                        MsgId = msgid,
                        FileName = fileName,
                        References = new List<ReferenceContext>
                        {
                            referenceContext
                        }
                    };


                    if (!nugget.Comment.IsSet())
                    {
                        return item;
                    }

                    tmpList = new List<string> { nugget.Comment };
                    item.Comments = tmpList;

                    return item;
                },
                // Update routine.
                (k, v) =>
                {
                    if (!_localizationOptions.DisableReferences)
                    {
                        var newReferences = new List<ReferenceContext>(v.References.ToList())
                        {
                            referenceContext
                        };
                        v.References = newReferences;
                    }

                    if (!nugget.Comment.IsSet())
                    {
                        return v;
                    }

                    tmpList = v.Comments != null ? v.Comments.ToList() : new List<string>();
                    if (!_localizationOptions.DisableReferences || !tmpList.Contains(nugget.Comment))
                    {
                        tmpList.Add(nugget.Comment);
                    }

                    v.Comments = tmpList;

                    return v;
                });
        }
    }
}
