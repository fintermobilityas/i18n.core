using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using i18n.Core.Abstractions;
using Microsoft.Extensions.FileProviders;

namespace i18n.Core.PortableObject
{
    /// <summary>
    /// Represents a provider that provides a translations for .po files.
    /// </summary>
    public class PoFilesTranslationsProvider : ITranslationProvider
    {
        readonly ILocalizationFileLocationProvider _poFilesLocationProvider;
        readonly PoParser _parser;

        /// <summary>
        /// Creates a new instance of <see cref="PoFilesTranslationsProvider"/>.
        /// </summary>
        /// <param name="poFileLocationProvider">The <see cref="ILocalizationFileLocationProvider"/>.</param>
        public PoFilesTranslationsProvider(ILocalizationFileLocationProvider poFileLocationProvider)
        {
            _poFilesLocationProvider = poFileLocationProvider;
            _parser = new PoParser();
        }

        /// <inheritdocs />
        public void LoadTranslations(string cultureName, CultureDictionary dictionary)
        {
            var fileInfos = new List<IFileInfo>();
            fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureName));
            fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureName.Replace("-", "_")));

            var cultureNames = cultureName.Length != 2 ? cultureName.Split("-", StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
            if (cultureNames.Count == 2)
            {
                fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureNames[1]));
            }

            foreach (var fileInfo in fileInfos.Where(x => x.Exists && !x.IsDirectory))
            {
                using var stream = fileInfo.CreateReadStream();
                using var reader = new StreamReader(stream);
                dictionary.MergeTranslations(_parser.Parse(reader));
                break;
            }
        }

    }
}
