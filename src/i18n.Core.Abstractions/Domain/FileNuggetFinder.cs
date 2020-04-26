using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using i18n.Core.Abstractions.Domain.Abstract;
using i18n.Core.Abstractions.Domain.Entities;
using i18n.Core.Abstractions.Domain.Helpers;

namespace i18n.Core.Abstractions.Domain
{
    public class FileNuggetFinder : INuggetFinder
    {
        readonly I18NSettings _settings;
        readonly NuggetParser _nuggetParser;

        public FileNuggetFinder(I18NSettings settings)
        {
            _settings = settings;
            _nuggetParser = new NuggetParser(new NuggetTokens(
                _settings.NuggetBeginToken,
                _settings.NuggetEndToken,
                _settings.NuggetDelimiterToken,
                _settings.NuggetCommentToken),
                NuggetParser.Context.SourceProcessing);
        }

        /// <summary>
        /// Goes through the Directories to scan recursively and starts a scan of each while that matches the whitelist. (both from settings)
        /// </summary>
        /// <returns>All found nuggets.</returns>
        public IDictionary<string, TemplateItem> ParseAll()
        {
            var fileWhiteList = _settings.WhiteList.ToList();
            var directoriesToSearchRecursively = _settings.DirectoriesToScan;
            var fileEnumerator = new FileEnumerator(_settings.BlackList.ToList());
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
                    var blacklistFound = _settings.BlackList.Any(blackItem => currentFullPath == null || currentFullPath.StartsWith(blackItem, StringComparison.OrdinalIgnoreCase));
                    if (blacklistFound)
                    {
                        continue;
                    }

                    //we check every filePath against our white list. if it's on there in at least one form we check it.
                    foreach (var whiteListItem in fileWhiteList)
                    {
                        //We have a catch all for a filetype
                        if (whiteListItem.StartsWith("*."))
                        {
                            if (Path.GetExtension(filePath) != whiteListItem.Substring(1))
                            {
                                continue;
                            }

                            //we got a match
                            ParseFile(_settings.ProjectDirectory, filePath, templateItems);
                            break;
                        }

                        if (Path.GetFileName(filePath) != whiteListItem)
                        {
                            continue;
                        }

                        //we got a match
                        ParseFile(_settings.ProjectDirectory, filePath, templateItems);
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
                var referenceContext = _settings.DisableReferences
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
            var key = TemplateItem.KeyFromMsgidAndComment(msgid, nugget.Comment, _settings.MessageContextEnabledFromComment);
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
                    if (!_settings.DisableReferences)
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
                    if (!_settings.DisableReferences || !tmpList.Contains(nugget.Comment))
                    {
                        tmpList.Add(nugget.Comment);
                    }

                    v.Comments = tmpList;

                    return v;
                });
        }
    }
}
