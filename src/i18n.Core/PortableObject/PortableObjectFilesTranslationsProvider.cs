using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using i18n.Core.Abstractions;
using JetBrains.Annotations;
using Microsoft.Extensions.FileProviders;

namespace i18n.Core.PortableObject
{
    /// <summary>
    /// Represents a provider that provides a translations for .po files.
    /// </summary>
    public class PortableObjectFilesTranslationsProvider : ITranslationProvider
    {
        readonly ILocalizationFileLocationProvider _poFilesLocationProvider;
        readonly PortableObjectParser _parser;

        /// <summary>
        /// Creates a new instance of <see cref="PortableObjectFilesTranslationsProvider"/>.
        /// </summary>
        /// <param name="poFileLocationProvider">The <see cref="ILocalizationFileLocationProvider"/>.</param>
        public PortableObjectFilesTranslationsProvider(ILocalizationFileLocationProvider poFileLocationProvider)
        {
            _poFilesLocationProvider = poFileLocationProvider;
            _parser = new PortableObjectParser();
        }

        /// <inheritdocs />
        public void LoadTranslations([NotNull] CultureInfo cultureInfo, [NotNull] CultureDictionary dictionary)
        {
            if (cultureInfo == null) throw new ArgumentNullException(nameof(cultureInfo));
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            var fileInfos = new List<IFileInfo>();
            var cultureName = cultureInfo.Name;
            fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureName));

            if (cultureName.IndexOf("-", StringComparison.Ordinal) != -1)
            {
                fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureName.Replace("-", "_")));
            }

            if (cultureName != cultureInfo.Name)
            {
                fileInfos.AddRange(_poFilesLocationProvider.GetLocations(cultureInfo.Name));
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
